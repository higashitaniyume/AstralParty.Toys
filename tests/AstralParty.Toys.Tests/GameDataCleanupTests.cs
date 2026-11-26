using System.Runtime.InteropServices;
using AstralParty.Toys.Services.Game;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class GameDataCleanupTests
{
    private sealed class Harness : IDisposable
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        public Harness(string name)
        {
            Sandbox = new TestSandbox(name);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            DataDirectory = Sandbox.EnsureDirectory("game", "AstralParty_CN_Data");
            Sandbox.WriteFile("game/AstralParty_CN_Data/app.info", "feimo\nAstralParty_CN");
            LocalLow = Sandbox.EnsureDirectory("low");
            LocalProduct = Sandbox.EnsureDirectory("low", "feimo", "AstralParty_CN");
            LocalAddressables = Sandbox.EnsureDirectory("low", "feimo", "AstralParty_CN", "com.unity.addressables");
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public string DataDirectory { get; }
        public string LocalLow { get; }
        public string LocalProduct { get; }
        public string LocalAddressables { get; }

        public void WriteSettingsJson(string catalogVersion, bool redirectToGameDir)
        {
            var cacheOverride = redirectToGameDir
                ? "{App.WebServerConfig.CacheDirectoryPath}"
                : "";
            var innerJson = $$"""{"m_CompressionEnabled":true,"m_CacheDirectoryOverride":"{{cacheOverride}}","m_ExpirationDelay":12960000,"m_LimitCacheSize":false,"m_MaximumCacheSize":1073741824}""";
            var escapedInner = innerJson.Replace("\"", "\\\"");
            var json = $$"""
            {
              "m_CatalogLocations": [
                {
                  "m_Keys": ["AddressablesMainContentCatalogCacheHash"],
                  "m_InternalId": "{UnityEngine.Application.persistentDataPath}/com.unity.addressables/catalog_{{catalogVersion}}.hash"
                }
              ],
              "m_ExtraInitializationData": [
                {
                  "m_Id": "CacheInitialization",
                  "m_Data": "{{escapedInner}}"
                }
              ]
            }
            """;
            var aaDir = Path.Combine(DataDirectory, "StreamingAssets", "aa");
            Directory.CreateDirectory(aaDir);
            File.WriteAllText(Path.Combine(aaDir, "settings.json"), json);
        }

        public string WriteBytes(string fullPath, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, bytes);
            return fullPath;
        }

        public bool TryHardLink(string existingFile, string newLink)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newLink)!);
            return CreateHardLinkW(newLink, existingFile, IntPtr.Zero);
        }

        public void Dispose() => Sandbox.Dispose();
    }

    private static string BuildCatalogJsonWithHashes(params string[] hashes)
    {
        var padded = new List<string>(hashes);
        for (var i = padded.Count; i < 12; i++)
        {
            padded.Add(i.ToString("x").PadLeft(32, '0'));
        }
        return "{\"m_InternalIds\":[" + string.Join(",", padded.Select(h => $"\"https://cdn.example/{h}.bundle\"")) + "]}";
    }

    [Fact]
    public void Inspect_WhenCacheRedirected_DetectsLegacyBundlesAndOldCatalogs_PreservesActiveCatalog()
    {
        using var h = new Harness("ap-clean-redirect");
        h.WriteSettingsJson("3.3.0", redirectToGameDir: true);
        File.WriteAllText(Path.Combine(h.LocalAddressables, "catalog_3.3.0.json"), BuildCatalogJsonWithHashes());
        File.WriteAllText(Path.Combine(h.LocalAddressables, "catalog_3.3.0.hash"), "abc");
        File.WriteAllText(Path.Combine(h.LocalAddressables, "catalog_3.2.0.json"), new string('a', 128));
        File.WriteAllText(Path.Combine(h.LocalAddressables, "catalog_3.2.0.hash"), "old");
        h.WriteBytes(Path.Combine(h.LocalAddressables, "AssetBundles", "b1", "v1", "__data"), new byte[2048]);
        h.WriteBytes(Path.Combine(h.DataDirectory, "com.unity.addressables", "AssetBundles", "active1", "00000000000000000000000000000001", "__data"), new byte[512]);

        var status = GameDataCleanupService.Inspect(h.GameDirectory, h.LocalLow, () => false, TimeSpan.Zero);

        Assert.True(status.Available);
        Assert.True(status.CacheRedirectedToGameDir);
        Assert.Equal("3.3.0", status.ActiveCatalogVersion);

        var legacyBundles = Assert.Single(status.Items, i => i.Id == GameDataCleanupService.ItemLegacyBundles);
        Assert.True(legacyBundles.SafeToClean);
        Assert.True(legacyBundles.DefaultSelected);
        Assert.Equal(2048, legacyBundles.Bytes);

        var legacyCatalogs = Assert.Single(status.Items, i => i.Id == GameDataCleanupService.ItemLegacyCatalogs);
        Assert.True(legacyCatalogs.SafeToClean);
        Assert.Equal(2, legacyCatalogs.FileCount);
        Assert.Equal(128 + 3, legacyCatalogs.Bytes);
        Assert.DoesNotContain(legacyCatalogs.Paths, p => p.Contains("catalog_3.3.0.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_WhenCacheNotRedirected_MarksLegacyBundlesNotSafe()
    {
        using var h = new Harness("ap-clean-noredir");
        h.WriteSettingsJson("3.2.0", redirectToGameDir: false);
        h.WriteBytes(Path.Combine(h.LocalAddressables, "AssetBundles", "b1", "v1", "__data"), new byte[1024]);

        var status = GameDataCleanupService.Inspect(h.GameDirectory, h.LocalLow, () => false, TimeSpan.Zero);

        Assert.True(status.Available);
        Assert.False(status.CacheRedirectedToGameDir);
        var legacyBundles = Assert.Single(status.Items, i => i.Id == GameDataCleanupService.ItemLegacyBundles);
        Assert.False(legacyBundles.SafeToClean);
        Assert.False(legacyBundles.DefaultSelected);
        Assert.NotNull(legacyBundles.BlockedReason);
        Assert.Equal(0, status.TotalReclaimableBytes);
    }

    [Fact]
    public void Inspect_GameDirOrphanBundles_DetectsUnreferencedHashesAndPreservesReferenced()
    {
        using var h = new Harness("ap-clean-orphan");
        h.WriteSettingsJson("3.3.0", redirectToGameDir: true);
        const string activeVerHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string orphanVerHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        File.WriteAllText(Path.Combine(h.LocalAddressables, "catalog_3.3.0.json"), BuildCatalogJsonWithHashes(activeVerHash));

        h.WriteBytes(Path.Combine(h.DataDirectory, "com.unity.addressables", "AssetBundles", "bundle1", activeVerHash, "__data"), new byte[300]);
        h.WriteBytes(Path.Combine(h.DataDirectory, "com.unity.addressables", "AssetBundles", "bundle1", orphanVerHash, "__data"), new byte[700]);

        var status = GameDataCleanupService.Inspect(h.GameDirectory, h.LocalLow, () => false, TimeSpan.Zero);

        var orphanItem = Assert.Single(status.Items, i => i.Id == GameDataCleanupService.ItemGameOrphanBundles);
        Assert.True(orphanItem.SafeToClean);
        Assert.Equal(700, orphanItem.Bytes);
        Assert.Single(orphanItem.Paths);
        Assert.EndsWith(orphanVerHash, orphanItem.Paths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Clean_RemovesLegacyBundlesAndOldCatalogs_KeepsActiveCatalogSavesAndReplays()
    {
        using var h = new Harness("ap-clean-exec");
        h.WriteSettingsJson("3.3.0", redirectToGameDir: true);
        var activeCat = Path.Combine(h.LocalAddressables, "catalog_3.3.0.json");
        var activeHash = Path.Combine(h.LocalAddressables, "catalog_3.3.0.hash");
        var oldCat = Path.Combine(h.LocalAddressables, "catalog_3.2.0.json");
        File.WriteAllText(activeCat, BuildCatalogJsonWithHashes());
        File.WriteAllText(activeHash, "h330");
        File.WriteAllText(oldCat, "old-cat-bytes");
        var legacyFile = h.WriteBytes(Path.Combine(h.LocalAddressables, "AssetBundles", "b1", "v1", "__data"), new byte[4096]);
        var saveFile = Path.Combine(h.LocalProduct, "SaveFile.es3");
        var replayFile = Path.Combine(h.LocalProduct, "Temp", "Replay", "1001", "1001");
        Directory.CreateDirectory(Path.GetDirectoryName(replayFile)!);
        File.WriteAllText(saveFile, "save");
        File.WriteAllText(replayFile, "replay");

        var result = await GameDataCleanupService.CleanAsync(
            h.GameDirectory,
            new[] { GameDataCleanupService.ItemLegacyBundles, GameDataCleanupService.ItemLegacyCatalogs },
            toRecycleBin: false,
            localLowRoot: h.LocalLow,
            isGameRunning: () => false,
            orphanMinAge: TimeSpan.Zero);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(4096 + "old-cat-bytes".Length, result.FreedBytes);
        Assert.False(File.Exists(legacyFile));
        Assert.False(Directory.Exists(Path.Combine(h.LocalAddressables, "AssetBundles")));
        Assert.False(File.Exists(oldCat));
        Assert.True(File.Exists(activeCat));
        Assert.True(File.Exists(activeHash));
        Assert.True(File.Exists(saveFile));
        Assert.True(File.Exists(replayFile));
    }

    [Fact]
    public async Task Clean_RemovesGameDirOrphanBundles_AndPrunesEmptyParentDir()
    {
        using var h = new Harness("ap-clean-orphan-exec");
        h.WriteSettingsJson("3.3.0", redirectToGameDir: true);
        const string activeVerHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string orphanVerHash = "cccccccccccccccccccccccccccccccc";
        File.WriteAllText(Path.Combine(h.LocalAddressables, "catalog_3.3.0.json"), BuildCatalogJsonWithHashes(activeVerHash));

        var activeFile = h.WriteBytes(Path.Combine(h.DataDirectory, "com.unity.addressables", "AssetBundles", "bundleKeep", activeVerHash, "__data"), new byte[300]);
        var orphanDir = Path.Combine(h.DataDirectory, "com.unity.addressables", "AssetBundles", "bundleDrop", orphanVerHash);
        h.WriteBytes(Path.Combine(orphanDir, "__data"), new byte[900]);

        var result = await GameDataCleanupService.CleanAsync(
            h.GameDirectory,
            new[] { GameDataCleanupService.ItemGameOrphanBundles },
            toRecycleBin: false,
            localLowRoot: h.LocalLow,
            isGameRunning: () => false,
            orphanMinAge: TimeSpan.Zero);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(900, result.FreedBytes);
        Assert.True(File.Exists(activeFile));
        Assert.False(Directory.Exists(orphanDir));
        Assert.False(Directory.Exists(Path.Combine(h.DataDirectory, "com.unity.addressables", "AssetBundles", "bundleDrop")));
    }

    [Fact]
    public async Task Clean_WhenGameRunning_IsBlockedWithoutDeletingFiles()
    {
        using var h = new Harness("ap-clean-running");
        h.WriteSettingsJson("3.3.0", redirectToGameDir: true);
        var legacyFile = h.WriteBytes(Path.Combine(h.LocalAddressables, "AssetBundles", "b1", "v1", "__data"), new byte[1024]);

        var result = await GameDataCleanupService.CleanAsync(
            h.GameDirectory,
            new[] { GameDataCleanupService.ItemLegacyBundles },
            toRecycleBin: false,
            localLowRoot: h.LocalLow,
            isGameRunning: () => true,
            orphanMinAge: TimeSpan.Zero);

        Assert.False(result.Ok);
        Assert.True(File.Exists(legacyFile));
    }

    [Fact]
    public async Task InspectAndClean_HardLinkedProfileLegacyBundles_DeduplicatesSizeAndCleansAll()
    {
        using var h = new Harness("ap-clean-hardlink");
        h.WriteSettingsJson("3.3.0", redirectToGameDir: true);
        var data = new byte[2048];
        Random.Shared.NextBytes(data);
        var mainBundle = h.WriteBytes(Path.Combine(h.LocalAddressables, "AssetBundles", "b1", "v1", "__data"), data);
        var profileDir = Path.Combine(h.LocalLow, "feimo", "AstralParty_CN_profiles", "P2");
        var profileBundle = Path.Combine(profileDir, "com.unity.addressables", "AssetBundles", "b1", "v1", "__data");
        var profileSave = Path.Combine(profileDir, "SaveFile.es3");
        Assert.True(h.TryHardLink(mainBundle, profileBundle));
        File.WriteAllText(profileSave, "p2-save");

        var status = GameDataCleanupService.Inspect(h.GameDirectory, h.LocalLow, () => false, TimeSpan.Zero);
        var item = Assert.Single(status.Items, i => i.Id == GameDataCleanupService.ItemLegacyBundles);
        Assert.Equal(2048, item.Bytes);
        Assert.Equal(2, item.FileCount);

        var result = await GameDataCleanupService.CleanAsync(
            h.GameDirectory,
            new[] { GameDataCleanupService.ItemLegacyBundles },
            toRecycleBin: false,
            localLowRoot: h.LocalLow,
            isGameRunning: () => false,
            orphanMinAge: TimeSpan.Zero);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(2048, result.FreedBytes);
        Assert.False(File.Exists(mainBundle));
        Assert.False(File.Exists(profileBundle));
        Assert.True(File.Exists(profileSave));
    }
}
