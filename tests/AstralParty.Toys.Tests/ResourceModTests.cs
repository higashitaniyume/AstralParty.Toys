using System.IO.Compression;
using System.Text;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests;

public sealed class ResourceModTests
{
    [Theory]
    [InlineData("bcbe8bf1ff4ea8dab4818e2c91d78145.bundle", "bcbe8bf1ff4ea8dab4818e2c91d78145")]
    [InlineData("CAB-eb66c4c542829c464535951fb95b2a85", "CAB-eb66c4c542829c464535951fb95b2a85")]
    [InlineData("dir\\name.bundle", "name")]
    [InlineData("", "")]
    public void StemStripsDirectoryAndExtension(string input, string expected)
    {
        Assert.Equal(expected, ResourceModCatalog.Stem(input));
    }

    [Fact]
    public void ApplyRedirectsOnlyTouchesMatchingBundleProviders()
    {
        var file = Path.GetTempFileName();
        try
        {
            var ccd = new ContentCatalogData { Resources = new Dictionary<object, List<ResourceLocation>>() };
            var options = new AssetBundleRequestOptions { BundleName = "abc" };
            var bundleLocation = new ResourceLocation
            {
                PrimaryKey = "bundle-key",
                ProviderId = ResourceModCatalog.AssetBundleProviderId,
                InternalId = "old/path",
                Data = new WrappedSerializedObject(new SerializedType(), options)
            };
            var assetLocation = new ResourceLocation
            {
                PrimaryKey = "asset-key",
                ProviderId = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider",
                InternalId = "0"
            };
            ccd.Resources["bundle-key"] = new List<ResourceLocation> { bundleLocation };
            ccd.Resources["asset-key"] = new List<ResourceLocation> { assetLocation };

            var count = ResourceModCatalog.ApplyRedirects(ccd, new Dictionary<string, string> { ["abc"] = file });

            Assert.Equal(1, count);
            Assert.Equal(file, bundleLocation.InternalId);
            Assert.Equal("0", assetLocation.InternalId);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ApplyRedirectsSkipsMissingFiles()
    {
        var ccd = new ContentCatalogData { Resources = new Dictionary<object, List<ResourceLocation>>() };
        var location = new ResourceLocation
        {
            PrimaryKey = "k",
            ProviderId = ResourceModCatalog.AssetBundleProviderId,
            InternalId = "old",
            Data = new WrappedSerializedObject(new SerializedType(), new AssetBundleRequestOptions { BundleName = "abc" })
        };
        ccd.Resources["k"] = new List<ResourceLocation> { location };

        var count = ResourceModCatalog.ApplyRedirects(ccd, new Dictionary<string, string>
        {
            ["abc"] = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".bundle")
        });

        Assert.Equal(0, count);
        Assert.Equal("old", location.InternalId);
    }

    [Fact]
    public void ResolveReadsAppInfoAndCatalogVersion()
    {
        var game = CreateFakeGame(out var company, out var product);
        try
        {
            var layout = ResourceModCatalog.TryResolve(game);

            Assert.NotNull(layout);
            Assert.Equal(company, layout!.Company);
            Assert.Equal(product, layout.Product);
            Assert.Equal("9.9.9", layout.CatalogVersion);
            Assert.EndsWith("catalog_9.9.9.json", layout.CatalogJsonPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("StreamingAssets", "aa", "catalog.bundle"), layout.CatalogBundlePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void ResolveReturnsNullForMissingDirectory()
    {
        Assert.Null(ResourceModCatalog.TryResolve(Path.Combine(Path.GetTempPath(), "toys-missing-" + Guid.NewGuid().ToString("N"))));
    }

    [RealRmlArchiveFact]
    public void RealArchivePreservesEntryNames()
    {
        var zip = Environment.GetEnvironmentVariable("ASTRAL_TEST_RML_ZIP")!;
        var game = CreateFakeGame(out var company, out var product);
        try
        {
            var result = new ResourceModManager().Import(game, zip);
            var package = Path.Combine(game, ResourceModManager.StoreFolderName, "packages", result.Package.Id);

            Assert.True(Directory.Exists(package));
            var entries = Directory.EnumerateFileSystemEntries(package, "*", SearchOption.AllDirectories).ToArray();
            Assert.DoesNotContain(entries, path => path.Contains('\uFFFD'));
            using var archive = CompatZipArchive.OpenRead(zip);
            Assert.Equal(
                archive.Entries.Count(entry => !string.IsNullOrEmpty(entry.Name)),
                entries.Count(path => (File.GetAttributes(path) & FileAttributes.Directory) == 0));
        }
        finally
        {
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void ReimportRemovesPreviousPackageContentOutsideEngineScanTree()
    {
        var game = CreateFakeGame(out var company, out var product);
        var zip = Path.Combine(Path.GetTempPath(), "toys-rml-replace-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            byte[] content = { 1, 2, 3 };
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                using var stream = archive.CreateEntry("预览.png").Open();
                stream.Write(content);
            }

            var manager = new ResourceModManager();
            var first = manager.Import(game, zip);
            var package = Path.Combine(game, ResourceModManager.StoreFolderName, "packages", first.Package.Id);
            File.WriteAllText(Path.Combine(package, "旧版本残留.txt"), "stale");

            manager.Import(game, zip);

            // 旧包不能留在 packages 里（引擎会把它当成第二个模组），也不能以 .replaced- 形式残留。
            Assert.False(File.Exists(Path.Combine(package, "旧版本残留.txt")));
            Assert.True(File.Exists(Path.Combine(package, "预览.png")));
            var store = Path.Combine(game, ResourceModManager.StoreFolderName);
            Assert.DoesNotContain(
                Directory.EnumerateFileSystemEntries(store, "*", SearchOption.AllDirectories),
                path => path.EndsWith(".old", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(path).StartsWith(".replaced-", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void ImportRejectsZipWithoutBundles()
    {
        var game = CreateFakeGame(out var company, out var product);
        var zip = Path.Combine(Path.GetTempPath(), "toys-rml-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("readme.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("no bundle here");
            }

            var manager = new ResourceModManager();
            var error = Assert.Throws<InvalidDataException>(() => manager.Import(game, zip));
            Assert.Contains("可识别", error.Message);
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
            Cleanup(game, company, product);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportKeepsChineseEntryNames(bool gbk)
    {
        var game = CreateFakeGame(out var company, out var product);
        var zip = Path.Combine(Path.GetTempPath(), "toys-rml-gbk-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = gbk
                ? Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                : null;
            byte[] content = { 1, 2, 3 };
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create, encoding))
            {
                using (var stream = archive.CreateEntry("预览/效果图.png").Open()) stream.Write(content);
                using (var stream = archive.CreateEntry("皮肤/替换.bundle").Open()) stream.Write(content);
                using (var stream = archive.CreateEntry("使用说明.txt").Open()) stream.Write(content);
            }

            var manager = new ResourceModManager();
            var result = manager.Import(game, zip);
            var package = Path.Combine(game, ResourceModManager.StoreFolderName, "packages", result.Package.Id);

            Assert.True(File.Exists(Path.Combine(package, "预览", "效果图.png")));
            Assert.True(File.Exists(Path.Combine(package, "皮肤", "替换.bundle")));
            Assert.True(File.Exists(Path.Combine(package, "使用说明.txt")));
            Assert.Equal(3, result.Package.FileCount);
            Assert.DoesNotContain(
                Directory.EnumerateFileSystemEntries(package, "*", SearchOption.AllDirectories),
                path => path.Contains('\uFFFD'));
            Assert.False(manager.GetStatus(game).Mods.Single().HasMojibakeNames);
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void StatusFlagsPackagesWithGarbledFileNames()
    {
        var game = CreateFakeGame(out var company, out var product);
        try
        {
            var store = Path.Combine(game, ResourceModManager.StoreFolderName);
            var broken = Path.Combine(store, "packages", "rm-broken");
            Directory.CreateDirectory(Path.Combine(broken, "\uFFFD\uFFFD\u03F7"));
            File.WriteAllText(Path.Combine(broken, "\uFFFD\uFFFD\u0228\uFFFD.pdf"), "x");
            var clean = Path.Combine(store, "packages", "rm-clean");
            Directory.CreateDirectory(clean);
            File.WriteAllText(Path.Combine(clean, "预览.png"), "x");
            File.WriteAllText(
                Path.Combine(store, "installed.json"),
                "{\"schema\":1,\"mods\":["
                + "{\"id\":\"rm-broken\",\"name\":\"broken\",\"enabled\":true,\"fileCount\":1},"
                + "{\"id\":\"rm-clean\",\"name\":\"clean\",\"enabled\":true,\"fileCount\":1}]}");

            var status = new ResourceModManager().GetStatus(game);

            Assert.True(status.Mods.Single(mod => mod.Id == "rm-broken").HasMojibakeNames);
            Assert.False(status.Mods.Single(mod => mod.Id == "rm-clean").HasMojibakeNames);
        }
        finally
        {
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void RestoreWithoutAppliedStateThrows()
    {
        var game = CreateFakeGame(out var company, out var product);
        try
        {
            var manager = new ResourceModManager();
            Assert.Throws<InvalidOperationException>(() => manager.Restore(game));
        }
        finally
        {
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void ApplyMovesDisabledPackageOutOfPackagesEvenWhenEngineFails()
    {
        var game = CreateFakeGame(out var company, out var product);
        try
        {
            var localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", company, product, "com.unity.addressables");
            Directory.CreateDirectory(localLow);
            File.WriteAllText(Path.Combine(localLow, "catalog_9.9.9.json"), "{}");

            var store = Path.Combine(game, ResourceModManager.StoreFolderName);
            var id = "rm-disabled";
            var package = Path.Combine(store, "packages", id);
            Directory.CreateDirectory(package);
            File.WriteAllBytes(Path.Combine(package, "x.bundle"), new byte[] { 1 });
            var enabledPackage = Path.Combine(store, "packages", "rm-enabled");
            Directory.CreateDirectory(enabledPackage);
            File.WriteAllBytes(Path.Combine(enabledPackage, "y.bundle"), new byte[] { 1 });
            File.WriteAllText(
                Path.Combine(store, "installed.json"),
                "{\"schema\":1,\"mods\":["
                + "{\"id\":\"rm-enabled\",\"name\":\"enabled\",\"enabled\":true,\"fileCount\":1},"
                + "{\"id\":\"" + id + "\",\"name\":\"disabled\",\"enabled\":false,\"fileCount\":1}]}");

            var manager = new ResourceModManager();
            Assert.ThrowsAny<Exception>(() => manager.Apply(game));

            Assert.False(Directory.Exists(package));
            Assert.True(Directory.Exists(Path.Combine(store, "disabled", id)));
        }
        finally
        {
            Cleanup(game, company, product);
        }
    }

    [Fact]
    public void AddAddressableRegistersNewKeyWithSyntheticBundle()
    {
        var file = Path.GetTempFileName();
        try
        {
            var ccd = BuildCatalogWithReference();
            Assert.True(ResourceModCatalog.AddAddressable(ccd, "VUT_Test", file, "3", "RefKey"));
            Assert.True(ccd.Resources.TryGetValue("VUT_Test", out var locations));

            var location = Assert.Single(locations);
            Assert.Equal(ResourceModCatalog.BundledAssetProviderId, location.ProviderId);
            Assert.Equal("3", location.InternalId);
            Assert.NotNull(location.DependencyKey);
            Assert.True(ccd.Resources.TryGetValue(location.DependencyKey!, out var dependencies));
            var bundle = Assert.Single(dependencies);
            Assert.Equal(ResourceModCatalog.AssetBundleProviderId, bundle.ProviderId);
            Assert.Equal(file, bundle.InternalId);
            Assert.NotNull(bundle.Data);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void RedirectAddressableRepointsExistingKey()
    {
        var file = Path.GetTempFileName();
        try
        {
            var ccd = BuildCatalogWithReference();
            Assert.True(ResourceModCatalog.RedirectAddressable(ccd, "RefKey", file, "7"));

            var location = Assert.Single(ccd.Resources["RefKey"]);
            Assert.Equal("7", location.InternalId);
            Assert.True(ccd.Resources.TryGetValue(location.DependencyKey!, out var dependencies));
            Assert.Equal(file, Assert.Single(dependencies).InternalId);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static ContentCatalogData BuildCatalogWithReference()
    {
        var ccd = new ContentCatalogData { Resources = new Dictionary<object, List<ResourceLocation>>() };
        var type = new SerializedType
        {
            AssemblyName = "Unity.Addressables, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            ClassName = "UnityEngine.AddressableAssets.ResourceLocators.ContentCatalogData"
        };
        var bundle = new ResourceLocation
        {
            PrimaryKey = "bundleKey",
            ProviderId = ResourceModCatalog.AssetBundleProviderId,
            InternalId = "old.bundle",
            Type = type,
            Data = new WrappedSerializedObject(type, new AssetBundleRequestOptions { BundleName = "old", Hash = "h", Crc = 1 })
        };
        ccd.Resources["bundleKey"] = new List<ResourceLocation> { bundle };
        ccd.Resources["RefKey"] = new List<ResourceLocation>
        {
            new()
            {
                PrimaryKey = "RefKey",
                ProviderId = ResourceModCatalog.BundledAssetProviderId,
                InternalId = "3",
                DependencyKey = "bundleKey"
            }
        };
        return ccd;
    }

    private static string CreateFakeGame(out string company, out string product)
    {
        company = "toys-rml-tests";
        product = "ResourceModTest-" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), "toys-rml-game-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "AstralParty_Test_Data");
        Directory.CreateDirectory(Path.Combine(data, "StreamingAssets", "aa"));
        File.WriteAllText(Path.Combine(data, "app.info"), company + "\n" + product);
        File.WriteAllText(
            Path.Combine(data, "StreamingAssets", "aa", "settings.json"),
            "{\"m_CatalogLocations\":[{\"m_InternalId\":\"{App.WebServerConfig.Path}/catalog_9.9.9.hash\"}]}");
        return root;
    }

    private static void Cleanup(string game, string company, string product)
    {
        try { if (Directory.Exists(game)) Directory.Delete(game, true); } catch { }
        try
        {
            var localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", company, product);
            if (Directory.Exists(localLow)) Directory.Delete(localLow, true);
        }
        catch { }
    }
}

/// <summary>真实 RML 包取样：环境变量 <c>ASTRAL_TEST_RML_ZIP</c> 指向一个含中文文件名的现成 ZIP。</summary>
public sealed class RealRmlArchiveFactAttribute : FactAttribute
{
    public RealRmlArchiveFactAttribute()
    {
        var archive = Environment.GetEnvironmentVariable("ASTRAL_TEST_RML_ZIP");
        if (string.IsNullOrEmpty(archive) || !File.Exists(archive))
            Skip = "设置 ASTRAL_TEST_RML_ZIP 可验证真实 RML 包的文件名解码。";
    }
}
