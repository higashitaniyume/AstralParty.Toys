using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class LoaderRecoveryTests
{
    private sealed class Harness : IDisposable
    {
        public TestSandbox Sandbox { get; } = new("toys-loader-recovery");
        public string Game { get; }
        public string Profile { get; }
        public ModManager Manager { get; }
        public Harness()
        {
            Game = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(Game, "AstralParty_CN.exe"), "fake game");
            Profile = Sandbox.EnsureDirectory("profile");
            Manager = new ModManager(Sandbox.Root, Profile);
            Manager.SaveStoredGameDirectory(Game);
        }
        public string Dll => Path.Combine(Game, "version.dll");
        public string Files => Path.Combine(Profile, "modloader-history", "files");
        public void Install(string version = "9.1.0") => Manager.InstallPackage(Game, Package(version), false);
        public void Dispose() => Sandbox.Dispose();
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Url { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; Url = request.RequestUri!.AbsoluteUri; return Task.FromResult(respond(request)); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static byte[] Package(string version, bool badHash = false, string? extraPath = null, string build = "")
    {
        var files = new Dictionary<string, byte[]>
        {
            ["version.dll"] = Encoding.UTF8.GetBytes("loader " + version + build),
            ["version.pdb"] = [9, 1, 5],
            ["AstralParty_ModLoader/doorstop_config.json"] = Encoding.UTF8.GetBytes("{\"sdkVersion\":\"9.0.0\"}"),
            ["AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll"] = [1, 2, 3],
            ["AstralParty_ModLoader/mods/ExampleMod/ExampleMod.dll"] = Encoding.UTF8.GetBytes("mod " + version),
            ["AstralParty_ModLoader/mods/ExampleMod/ExampleMod.json"] = Encoding.UTF8.GetBytes("{}")
        };
        var hashes = files.ToDictionary(p => p.Key, p => Hash(p.Value));
        if (badHash) hashes["version.dll"] = new string('0', 64);
        files["cesium-loader.json"] = JsonSerializer.SerializeToUtf8Bytes(new { version, files = hashes });
        if (extraPath is not null) files[extraPath] = [1];
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var pair in files)
            { using var stream = archive.CreateEntry(pair.Key).Open(); stream.Write(pair.Value); }
        return output.ToArray();
    }
    private static Dictionary<string, string> Snapshot(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(directory, p), p => Hash(File.ReadAllBytes(p)));

    [Fact]
    public async Task DisableAndRestore_OnlyChangesVersionDll_AndWorksOffline()
    {
        using var h = new Harness(); h.Install();
        File.WriteAllText(Path.Combine(h.Game, "AstralParty_ModLoader", "logs", "session.log"), "preserve log");
        var before = Snapshot(h.Game);
        var original = File.ReadAllBytes(h.Dll);
        h.Manager.DisableLoader(h.Game);
        Assert.False(File.Exists(h.Dll));
        var remaining = Snapshot(h.Game); before.Remove("version.dll");
        Assert.Equal(before.OrderBy(p => p.Key), remaining.OrderBy(p => p.Key));
        Assert.True(h.Manager.GetStatus().LoaderDisabled);
        Assert.Equal("9.1.0", h.Manager.GetStatus().InstalledVersion);
        Assert.True(h.Manager.GetStatus().CanRestoreLoader);
        using var handler = new Handler(_ => throw new Exception("must not fetch"));
        using var http = new HttpClient(handler);
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(original, File.ReadAllBytes(h.Dll));
        Assert.Equal(0, handler.Calls);
        Assert.True(h.Manager.GetStatus().LoaderManaged);
        Assert.False(h.Manager.GetStatus().LoaderDisabled);
    }

    [Fact]
    public async Task MissingAllLocalContents_DownloadsPinnedVersionAndCachesIt()
    {
        using var h = new Harness(); h.Install();
        h.Manager.DisableLoader(h.Game);
        foreach (var file in Directory.GetFiles(h.Files)) File.Delete(file);
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Package("9.1.0")) });
        using var http = new HttpClient(handler);
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(ModManager.VersionPackageUrl("9.1.0"), handler.Url);
        Assert.DoesNotContain("latest", handler.Url!);
        Assert.Equal(Encoding.UTF8.GetBytes("loader 9.1.0"), File.ReadAllBytes(h.Dll));
        Assert.True(h.Manager.GetStatus().LoaderManaged);
        h.Manager.DisableLoader(h.Game);
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task MissingDllBlob_UsesRetainedPackageWithoutNetwork()
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        File.Delete(Path.Combine(h.Files, Hash(Encoding.UTF8.GetBytes("loader 9.1.0"))));
        using var handler = new Handler(_ => throw new Exception("must use cached zip"));
        using var http = new HttpClient(handler);
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task LegacyManualDllDeletion_DownloadsAndPreservesOtherFiles()
    {
        using var h = new Harness(); h.Install();
        // No history at all, as with an installation from an older Toys.
        Directory.Delete(Path.Combine(h.Profile, "modloader-history"), true);
        File.Delete(h.Dll);
        var before = Snapshot(h.Game);
        Assert.True(h.Manager.GetStatus().CanRestoreLoader);
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Package("9.1.0")) }));
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        var after = Snapshot(h.Game); after.Remove("version.dll");
        Assert.Equal(before.OrderBy(p => p.Key), after.OrderBy(p => p.Key));
    }

    [Theory]
    [InlineData("9.2.0", false)]
    [InlineData("9.1.0", true)]
    public async Task OnlineWrongVersionOrHash_DoesNotRestore(string version, bool badHash)
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        foreach (var file in Directory.GetFiles(h.Files)) File.Delete(file);
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Package(version, badHash)) }));
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Manager.RestoreLoaderAsync(h.Game, http));
        Assert.False(File.Exists(h.Dll));
        Assert.True(h.Manager.GetStatus().LoaderDisabled);
    }

    [Fact]
    public async Task ReleaseNotFound_DoesNotInstallLatest()
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        foreach (var file in Directory.GetFiles(h.Files)) File.Delete(file);
        using var handler = new Handler(_ => new(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => h.Manager.RestoreLoaderAsync(h.Game, http));
        Assert.Equal(1, handler.Calls);
        Assert.False(File.Exists(h.Dll));
    }

    [Fact]
    public async Task UpdatingWhileDisabled_StaysDisabled_ThenRestoresNewVersion()
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        h.Manager.InstallPackage(h.Game, Package("9.2.0"), false);
        Assert.False(File.Exists(h.Dll));
        Assert.Equal("9.2.0", h.Manager.GetStatus().InstalledVersion);
        using var http = new HttpClient(new Handler(_ => throw new Exception("use retained new DLL")));
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(Encoding.UTF8.GetBytes("loader 9.2.0"), File.ReadAllBytes(h.Dll));
    }

    [Fact]
    public void History_RetainsPreviousModContentsAndOperationResults()
    {
        using var h = new Harness(); h.Install();
        var source = h.Sandbox.WriteFile("Custom.dll", new byte[] { 1, 4 });
        h.Manager.ImportMod(h.Game, source);
        File.WriteAllBytes(source, [5, 8]);
        h.Manager.ImportMod(h.Game, source);
        Assert.Equal(new byte[] { 1, 4 }, File.ReadAllBytes(Path.Combine(h.Files, Hash([1, 4]))));
        Assert.Equal(new byte[] { 5, 8 }, File.ReadAllBytes(Path.Combine(h.Files, Hash([5, 8]))));
        var operations = Directory.GetFiles(Path.Combine(h.Profile, "modloader-history", "games"), "*.json", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p) != "current.json").Select(File.ReadAllText).ToArray();
        Assert.Equal(3, operations.Length);
        Assert.All(operations, text => Assert.Contains("\"done\"", text));
    }

    [Fact]
    public void BackupFailure_PreventsDeletion()
    {
        using var h = new Harness(); h.Install();
        var blob = Path.Combine(h.Files, Hash(File.ReadAllBytes(h.Dll)));
        File.Delete(blob); Directory.CreateDirectory(blob);
        var failure = Record.Exception(() => h.Manager.DisableLoader(h.Game));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.True(File.Exists(h.Dll));
    }

    [Fact]
    public async Task MultipleGames_RestoreTheirOwnVersions()
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        var other = h.Sandbox.EnsureDirectory("other"); File.WriteAllText(Path.Combine(other, "AstralParty_CN.exe"), "fake");
        h.Manager.InstallPackage(other, Package("9.3.0"), false); h.Manager.DisableLoader(other);
        using var http = new HttpClient(new Handler(_ => throw new Exception("offline")));
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        await h.Manager.RestoreLoaderAsync(other, http);
        Assert.Equal(Encoding.UTF8.GetBytes("loader 9.1.0"), File.ReadAllBytes(h.Dll));
        Assert.Equal(Encoding.UTF8.GetBytes("loader 9.3.0"), File.ReadAllBytes(Path.Combine(other, "version.dll")));
    }

    [Fact]
    public async Task ExistingForeignDll_IsNeverOverwrittenByRecovery()
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        File.WriteAllText(h.Dll, "foreign");
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Manager.RestoreLoaderAsync(h.Game));
        Assert.Equal("foreign", File.ReadAllText(h.Dll));
    }

    [Fact]
    public void UnsafePackage_IsRejectedBeforeMutatingGame()
    {
        using var h = new Harness();
        Assert.Throws<InvalidDataException>(() => h.Manager.InstallPackage(h.Game, Package("9.1.0", extraPath: "../escape"), false));
        Assert.False(File.Exists(h.Dll));
    }

    [Fact]
    public async Task SameVersionDifferentReleaseBuild_UsesReleaseHashAndRemainsManaged()
    {
        using var h = new Harness(); h.Install(); h.Manager.DisableLoader(h.Game);
        foreach (var file in Directory.GetFiles(h.Files)) File.Delete(file);
        var manifestPath = Path.Combine(h.Game, ModManager.LoaderFolderName, ModManager.InstalledManifestFileName);
        var originalManifest = File.ReadAllBytes(manifestPath);
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Package("9.1.0", build: " release-build")) });
        using var http = new HttpClient(handler);
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifestPath));
        Assert.True(h.Manager.GetStatus().LoaderManaged);
        h.Manager.DisableLoader(h.Game);
        await h.Manager.RestoreLoaderAsync(h.Game, http);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(Encoding.UTF8.GetBytes("loader 9.1.0 release-build"), File.ReadAllBytes(h.Dll));
    }

    [Fact]
    public void MissingManifest_DoesNotBypassDowngradeProtection()
    {
        using var h = new Harness(); h.Install("9.2.0"); h.Manager.DisableLoader(h.Game);
        File.Delete(Path.Combine(h.Game, ModManager.LoaderFolderName, ModManager.InstalledManifestFileName));
        Assert.Throws<InvalidOperationException>(() => h.Manager.InstallPackage(h.Game, Package("9.1.0"), false));
        Assert.False(File.Exists(h.Dll));
    }

    [Fact]
    public void EmbeddedManifestWriteFailure_IsReportedAsFailedInstallation()
    {
        using var h = new Harness(); h.Install();
        var manifest = Path.Combine(h.Game, ModManager.LoaderFolderName, ModManager.InstalledManifestFileName);
        File.Delete(manifest); Directory.CreateDirectory(manifest);
        Assert.Throws<IOException>(() => h.Manager.Install(h.Game, false, false, allowDowngrade: true));
        var failed = Directory.GetFiles(Path.Combine(h.Profile, "modloader-history", "games"), "*.json", SearchOption.AllDirectories)
            .Select(File.ReadAllText).Where(s => s.Contains("\"failed\""));
        Assert.Single(failed);
    }

    [Fact]
    public void PartialInstallFailure_RecordsFailedOperationAndKeepsInputFiles()
    {
        using var h = new Harness(); h.Install();
        var sdkDll = Path.Combine(h.Game, ModManager.LoaderFolderName, "sdk", ModManager.SdkDllName);
        File.Delete(sdkDll); Directory.CreateDirectory(sdkDll);
        Assert.Throws<IOException>(() => h.Manager.InstallPackage(h.Game, Package("9.2.0"), false));
        Assert.True(File.Exists(Path.Combine(h.Files, Hash(Encoding.UTF8.GetBytes("loader 9.2.0")))));
        var failed = Directory.GetFiles(Path.Combine(h.Profile, "modloader-history", "games"), "*.json", SearchOption.AllDirectories)
            .Select(File.ReadAllText).Where(s => s.Contains("\"failed\""));
        Assert.Single(failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("../../file")]
    public void UnknownOrInvalidVersion_CannotGenerateDownloadUrl(string version) => Assert.Throws<InvalidOperationException>(() => ModManager.VersionPackageUrl(version));
}
