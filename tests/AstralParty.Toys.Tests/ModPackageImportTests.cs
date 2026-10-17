using System.IO.Compression;
using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// mod 包（ZIP）导入：包结构与 <c>cesium package</c> / 加载器扫描规则一致
/// （<c>&lt;ModId&gt;\&lt;ModId&gt;.dll</c>，解压到 mods 目录即安装）。
/// 除了正常安装，这里守住的是「不该悄悄改变玩家现状」那几条：不覆盖改过的配置、
/// 不把停用的模组重新打开、包不合法时一个文件都不写。
/// </summary>
public sealed class ModPackageImportTests
{
    private static readonly byte[] FakeDll = { 0x4D, 0x5A, 0x90, 0x00, 0x01 };

    [Fact]
    public void ImportModPackage_InstallsFolderLayoutWithSidecarAndConfig()
    {
        using var harness = new ImportHarness("ap-pkg-import");
        var zip = CreatePackage(harness, "HandViewerMod.zip",
            ("HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.2.3\"}")),
            ("HandViewerMod/config.json", Text("{\"showOpponentHand\":true}")),
            ("HandViewerMod/HandViewerMod.pdb", new byte[] { 0x50, 0x44, 0x42 }));

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        var entry = Assert.Single(result.Mods);
        Assert.Equal("HandViewerMod", entry.Name);
        Assert.Equal("HandViewerMod", entry.DirectoryName);
        Assert.Equal("手牌查看器", entry.DisplayName);
        Assert.Equal("1.2.3", entry.Version);
        Assert.Empty(result.Skipped);
        Assert.Empty(result.PreservedConfigs);
        Assert.Empty(result.PreservedDisabled);

        // 解压位置就是手动解压到 mods 目录的结果：模组文件夹里是包内的相对结构
        var modFolder = Path.Combine(harness.ModsDirectory, "HandViewerMod");
        Assert.Equal(FakeDll, File.ReadAllBytes(Path.Combine(modFolder, "HandViewerMod.dll")));
        Assert.True(File.Exists(Path.Combine(modFolder, "HandViewerMod.json")));
        Assert.True(File.Exists(Path.Combine(modFolder, "HandViewerMod.pdb")));
        Assert.True(File.Exists(Path.Combine(modFolder, "config.json")));

        // 状态页读到的就是这个模组
        var scanned = Assert.Single(harness.Manager.GetStatus().Mods);
        Assert.Equal("手牌查看器", scanned.DisplayName);
        Assert.Equal("1.2.3", scanned.Version);
        Assert.True(scanned.Enabled);
    }

    [Fact]
    public void ImportModPackage_UpgradeKeepsUserConfigAndDisabledState()
    {
        using var harness = new ImportHarness("ap-pkg-upgrade");
        var first = CreatePackage(harness, "v1.zip",
            ("HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.0.0\"}")),
            ("HandViewerMod/config.json", Text("{\"showOpponentHand\":true}")));
        harness.Manager.ImportModPackage(harness.GameDirectory, first);

        // 玩家改过配置、并且在界面里停用了这个模组
        var modFolder = Path.Combine(harness.ModsDirectory, "HandViewerMod");
        var configPath = Path.Combine(modFolder, "config.json");
        File.WriteAllText(configPath, "{\"showOpponentHand\":false,\"myOwn\":1}");
        harness.Manager.ToggleMod(harness.GameDirectory, "HandViewerMod.dll", false);

        var update = CreatePackage(harness, "v2.zip",
            ("HandViewerMod/HandViewerMod.dll", new byte[] { 0x4D, 0x5A, 0x99, 0x02 }),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"2.0.0\"}")),
            ("HandViewerMod/config.json", Text("{\"showOpponentHand\":true}")));
        var result = harness.Manager.ImportModPackage(harness.GameDirectory, update);

        // DLL 换成新版
        Assert.Equal(new byte[] { 0x4D, 0x5A, 0x99, 0x02 }, File.ReadAllBytes(Path.Combine(modFolder, "HandViewerMod.dll")));
        // 配置还是玩家那份（包里的默认配置不能盖掉它）
        Assert.Equal("{\"showOpponentHand\":false,\"myOwn\":1}", File.ReadAllText(configPath));
        Assert.Contains("HandViewerMod", result.PreservedConfigs);
        // 停用状态沿用，版本信息更新
        Assert.Contains("HandViewerMod", result.PreservedDisabled);
        using (var sidecar = JsonDocument.Parse(File.ReadAllText(Path.Combine(modFolder, "HandViewerMod.json"))))
        {
            Assert.False(sidecar.RootElement.GetProperty("enabled").GetBoolean());
            Assert.Equal("2.0.0", sidecar.RootElement.GetProperty("version").GetString());
        }
        var scanned = Assert.Single(harness.Manager.GetStatus().Mods);
        Assert.False(scanned.Enabled);
        Assert.Equal("2.0.0", scanned.Version);
    }

    [Fact]
    public void ImportModPackage_ReportsUpgradeVersionChange()
    {
        using var harness = new ImportHarness("ap-pkg-upgrade-change");
        harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v1.zip",
            ("HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.0.8\"}"))));

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v2.zip",
            ("HandViewerMod/HandViewerMod.dll", new byte[] { 0x4D, 0x5A, 0x99 }),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.1.0\"}"))));

        var change = Assert.Single(result.Changes);
        Assert.Equal("手牌查看器", change.Name);
        Assert.True(change.WasInstalled);
        Assert.True(change.IsUpgrade);
        Assert.False(change.IsDowngrade);
        Assert.Equal("1.0.8", change.FromVersion);
        Assert.Equal("1.1.0", change.ToVersion);
    }

    [Fact]
    public void ImportModPackage_RejectsDowngradeUnlessAllowed()
    {
        using var harness = new ImportHarness("ap-pkg-downgrade");
        var modFolder = Path.Combine(harness.ModsDirectory, "HandViewerMod");
        harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v2.zip",
            ("HandViewerMod/HandViewerMod.dll", new byte[] { 0x4D, 0x5A, 0x02 }),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"2.0.0\"}"))));

        var older = CreatePackage(harness, "v1.zip",
            ("HandViewerMod/HandViewerMod.dll", new byte[] { 0x4D, 0x5A, 0x01 }),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.0.0\"}")));

        // 版本更旧：默认不覆盖（与加载器安装的降级闸门同一套口径）
        var error = Assert.Throws<InvalidOperationException>(
            () => harness.Manager.ImportModPackage(harness.GameDirectory, older));
        Assert.Contains("降级", error.Message);
        Assert.Contains("2.0.0", error.Message);
        Assert.Equal("2.0.0", ReadSidecarVersion(modFolder));
        Assert.Equal(new byte[] { 0x4D, 0x5A, 0x02 }, File.ReadAllBytes(Path.Combine(modFolder, "HandViewerMod.dll")));

        // 明确允许降级后才落盘
        var result = harness.Manager.ImportModPackage(harness.GameDirectory, older, allowDowngrade: true);
        Assert.Equal("1.0.0", ReadSidecarVersion(modFolder));
        Assert.True(Assert.Single(result.Changes).IsDowngrade);
    }

    [Fact]
    public void InspectModPackage_ReadsVersionsWithoutWriting()
    {
        using var harness = new ImportHarness("ap-pkg-inspect");
        var modFolder = Path.Combine(harness.ModsDirectory, "HandViewerMod");
        harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v1.zip",
            ("HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.0.0\"}"))));

        var newer = harness.Manager.InspectModPackage(harness.GameDirectory, CreatePackage(harness, "new.zip",
            ("HandViewerMod/HandViewerMod.dll", new byte[] { 0x4D, 0x5A, 0x03 }),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"name\":\"手牌查看器\",\"version\":\"1.2.0\"}"))));
        var upgrade = Assert.Single(newer.Mods);
        Assert.True(upgrade.Installed);
        Assert.True(upgrade.IsUpgrade);
        Assert.False(newer.HasDowngrade);
        Assert.Equal("1.0.0", upgrade.InstalledVersion);
        Assert.Equal("1.2.0", upgrade.PackageVersion);
        Assert.Equal("手牌查看器", upgrade.Name);

        var older = harness.Manager.InspectModPackage(harness.GameDirectory, CreatePackage(harness, "old.zip",
            ("HandViewerMod/HandViewerMod.dll", new byte[] { 0x4D, 0x5A, 0x04 }),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"version\":\"0.9.0\"}"))));
        Assert.True(older.HasDowngrade);
        Assert.True(Assert.Single(older.Mods).IsDowngrade);

        // 预检只读：磁盘上还是 1.0.0，DLL 也没被换掉
        Assert.Equal("1.0.0", ReadSidecarVersion(modFolder));
        Assert.Equal(FakeDll, File.ReadAllBytes(Path.Combine(modFolder, "HandViewerMod.dll")));
    }

    [Fact]
    public void ImportModPackage_CleansFilesDroppedFromNewVersion()
    {
        using var harness = new ImportHarness("ap-pkg-clean");
        harness.SimulateRecycleBin(available: true);
        var modFolder = Path.Combine(harness.ModsDirectory, "Mod");
        harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v1.zip",
            ("Mod/Mod.dll", FakeDll),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.0.0\"}")),
            ("Mod/Mod.pdb", new byte[] { 0x50, 0x44, 0x42 }),
            ("Mod/data/old-resource.txt", Text("旧资源")),
            ("Mod/config.json", Text("{\"mine\":true}"))));

        // 新版本不再带 pdb 与 data\ 目录
        var result = harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v2.zip",
            ("Mod/Mod.dll", new byte[] { 0x4D, 0x5A, 0x11 }),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.1.0\"}")),
            ("Mod/config.json", Text("{\"mine\":false}"))));

        Assert.False(File.Exists(Path.Combine(modFolder, "Mod.pdb")));
        Assert.False(File.Exists(Path.Combine(modFolder, "data", "old-resource.txt")));
        Assert.False(Directory.Exists(Path.Combine(modFolder, "data")), "空目录应被清理");
        Assert.Equal(2, result.RemovedFiles.Count);
        Assert.Contains("Mod/Mod.pdb", result.RemovedFiles);
        Assert.Contains("Mod/data/old-resource.txt", result.RemovedFiles);
        Assert.Empty(result.KeptFiles);
        // 走的是「回收」而不是硬删：两个文件都交给了回收动作
        Assert.Equal(2, harness.Recycled.Count);
        // 玩家的配置不受清理影响
        Assert.Equal("{\"mine\":true}", File.ReadAllText(Path.Combine(modFolder, "config.json")));
        Assert.Equal(new byte[] { 0x4D, 0x5A, 0x11 }, File.ReadAllBytes(Path.Combine(modFolder, "Mod.dll")));
    }

    [Fact]
    public void ImportModPackage_WithoutManifestCleansOnlyBuildArtifacts()
    {
        using var harness = new ImportHarness("ap-pkg-clean-legacy");
        harness.SimulateRecycleBin(available: true);
        // 手动解压装的旧模组：没有安装清单，只有构建产物与玩家自己的文件
        var modFolder = Path.Combine(harness.ModsDirectory, "Mod");
        Directory.CreateDirectory(Path.Combine(modFolder, "res"));
        File.WriteAllBytes(Path.Combine(modFolder, "Mod.dll"), new byte[] { 0x4D, 0x5A, 0x00 });
        File.WriteAllBytes(Path.Combine(modFolder, "OldDependency.dll"), new byte[] { 0x4D, 0x5A, 0x01 });
        File.WriteAllText(Path.Combine(modFolder, "Mod.json"), "{\"id\":\"Mod\",\"version\":\"1.0.0\"}");
        File.WriteAllText(Path.Combine(modFolder, "config.json"), "{\"mine\":true}");
        File.WriteAllText(Path.Combine(modFolder, "res", "my-avatar.png"), "用户自己的文件");

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "Mod.zip",
            ("Mod/Mod.dll", FakeDll),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.1.0\"}")),
            ("Mod/config.json", Text("{}"))));

        // 构建产物（旧 dll/pdb/json）清掉，玩家自己的文件留着
        Assert.False(File.Exists(Path.Combine(modFolder, "OldDependency.dll")));
        Assert.True(File.Exists(Path.Combine(modFolder, "res", "my-avatar.png")));
        Assert.Equal("{\"mine\":true}", File.ReadAllText(Path.Combine(modFolder, "config.json")));
        Assert.Contains("Mod/OldDependency.dll", result.RemovedFiles);
        Assert.Single(harness.Recycled);
    }

    [Fact]
    public void ImportModPackage_KeepsStaleFilesWhenRecycleBinUnavailable()
    {
        using var harness = new ImportHarness("ap-pkg-clean-norecycle");
        harness.SimulateRecycleBin(available: false);
        var modFolder = Path.Combine(harness.ModsDirectory, "Mod");
        harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v1.zip",
            ("Mod/Mod.dll", FakeDll),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.0.0\"}")),
            ("Mod/OldDependency.dll", new byte[] { 0x4D, 0x5A, 0x07 })));

        // 回收站不可用（例如网络盘）：宁可留着旧文件，也绝不永久删除
        var result = harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "v2.zip",
            ("Mod/Mod.dll", new byte[] { 0x4D, 0x5A, 0x12 }),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.1.0\"}"))));

        Assert.True(File.Exists(Path.Combine(modFolder, "OldDependency.dll")));
        Assert.Contains("Mod/OldDependency.dll", result.KeptFiles);
        Assert.Empty(result.RemovedFiles);
        // 升级本身仍然成功
        Assert.Equal(new byte[] { 0x4D, 0x5A, 0x12 }, File.ReadAllBytes(Path.Combine(modFolder, "Mod.dll")));
        Assert.Equal("1.1.0", ReadSidecarVersion(modFolder));
    }

    [RecycleBinFact]
    public void RecycleBinHelper_SendsStaleFileToRecycleBin()
    {
        // 真正走 Windows 回收站：文件从 mods 目录消失，但能在回收站里还原
        using var sandbox = new TestSandbox("ap-recycle-file");
        var file = sandbox.WriteFile("stale.dll", new byte[] { 0x4D, 0x5A, 0x01 });

        var (recycled, error) = RecycleBinHelper.TryRecycleFile(file);

        Assert.True(recycled, error);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void ImportModPackage_LeavesNoBookkeepingFilesInModsFolder()
    {
        using var harness = new ImportHarness("ap-pkg-bookkeeping");
        harness.Manager.ImportModPackage(harness.GameDirectory, CreatePackage(harness, "Mod.zip",
            ("Mod/Mod.dll", FakeDll),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.0.0\"}")),
            ("Mod/config.json", Text("{}"))));

        var modFolder = Path.Combine(harness.ModsDirectory, "Mod");
        var installed = Directory.GetFiles(modFolder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(modFolder, file).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        // 升级清单存在配置根的安装历史里，mods 目录只应有包内文件
        Assert.Equal(new[] { "Mod.dll", "Mod.json", "config.json" }, installed);
    }

    [Fact]
    public void ImportModPackage_WrapsRootLevelDllIntoItsOwnFolder()
    {
        using var harness = new ImportHarness("ap-pkg-root-flat");
        var zip = CreatePackage(harness, "RootOnly.zip",
            ("RootOnly.dll", FakeDll),
            ("RootOnly.json", Text("{\"id\":\"RootOnly\",\"name\":\"根目录模组\",\"version\":\"0.9.0\"}")),
            ("config.json", Text("{}")),
            ("README.md", Text("说明")));

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        var entry = Assert.Single(result.Mods);
        Assert.Equal("RootOnly", entry.DirectoryName);
        var modFolder = Path.Combine(harness.ModsDirectory, "RootOnly");
        Assert.True(File.Exists(Path.Combine(modFolder, "RootOnly.dll")));
        Assert.True(File.Exists(Path.Combine(modFolder, "RootOnly.json")));
        Assert.True(File.Exists(Path.Combine(modFolder, "config.json")));
        Assert.True(File.Exists(Path.Combine(modFolder, "README.md")));
        // 平铺位置不该再留一份（否则加载器看到的是两个来源不明的 DLL）
        Assert.False(File.Exists(Path.Combine(harness.ModsDirectory, "RootOnly.dll")));
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void ImportModPackage_InstallsEveryModWithTheRightDll()
    {
        using var harness = new ImportHarness("ap-pkg-multi");
        var zip = CreatePackage(harness, "bundle.zip",
            ("ModA/ModA.dll", FakeDll),
            ("ModA/ModA.json", Text("{\"id\":\"ModA\",\"name\":\"模组 A\",\"version\":\"1.0.0\"}")),
            ("ModB/ModB.dll", new byte[] { 0x4D, 0x5A, 0x02 }),
            ("ModB/lib/Helper.dll", new byte[] { 0x4D, 0x5A, 0x03 }),
            ("README.md", Text("包说明")),
            ("docs/usage.txt", Text("文档")));

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        Assert.Equal(2, result.Mods.Count);
        Assert.Equal(new[] { "ModA", "ModB" }, result.Mods.Select(mod => mod.Name).ToArray());
        Assert.True(File.Exists(Path.Combine(harness.ModsDirectory, "ModA", "ModA.dll")));
        // mod 自带的依赖程序集（与所在文件夹不同名）跟着一起装，但不会被当成另一个模组
        Assert.True(File.Exists(Path.Combine(harness.ModsDirectory, "ModB", "lib", "Helper.dll")));
        Assert.Equal(2, harness.Manager.GetStatus().Mods.Count);
        Assert.Contains("README.md", result.Skipped);
        Assert.Contains("docs/usage.txt", result.Skipped);
    }

    [Fact]
    public void ImportModPackage_AcceptsWrapperDirectory()
    {
        using var harness = new ImportHarness("ap-pkg-wrapper");
        var zip = CreatePackage(harness, "wrapped.zip",
            ("HandViewerMod-1.2.3/HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod-1.2.3/HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"version\":\"1.2.3\"}")));

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        var entry = Assert.Single(result.Mods);
        Assert.Equal("HandViewerMod", entry.DirectoryName);
        Assert.True(File.Exists(Path.Combine(harness.ModsDirectory, "HandViewerMod", "HandViewerMod.dll")));
        Assert.False(Directory.Exists(Path.Combine(harness.ModsDirectory, "HandViewerMod-1.2.3")));
    }

    [Fact]
    public void ImportModPackage_KeepsExistingFolderCasing()
    {
        using var harness = new ImportHarness("ap-pkg-casing");
        var modFolder = Path.Combine(harness.ModsDirectory, "handviewermod");
        Directory.CreateDirectory(modFolder);
        File.WriteAllText(Path.Combine(modFolder, "handviewermod.json"), "{\"id\":\"handviewermod\"}");

        var zip = CreatePackage(harness, "cased.zip",
            ("HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\",\"version\":\"1.0.0\"}")));
        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        Assert.Equal("handviewermod", result.Mods[0].DirectoryName);
        // 只有一个模组目录，没有大小写不同的第二份
        Assert.Single(Directory.EnumerateDirectories(harness.ModsDirectory));
    }

    [Fact]
    public void ImportModPackage_IgnoresMacJunkFiles()
    {
        using var harness = new ImportHarness("ap-pkg-junk");
        var zip = CreatePackage(harness, "mac.zip",
            ("__MACOSX/Mod/._Mod.dll", new byte[] { 0x00 }),
            ("Mod/._Mod.json", new byte[] { 0x00 }),
            ("Mod/Mod.dll", FakeDll),
            ("Mod/Mod.json", Text("{\"id\":\"Mod\",\"version\":\"1.0.0\"}")));

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        var entry = Assert.Single(result.Mods);
        Assert.Equal("Mod", entry.DirectoryName);
        Assert.False(File.Exists(Path.Combine(harness.ModsDirectory, "Mod", "._Mod.json")));
        Assert.Single(Directory.EnumerateDirectories(harness.ModsDirectory));
    }

    [Fact]
    public void ImportModPackage_RejectsUnsafePathsWithoutWriting()
    {
        using var harness = new ImportHarness("ap-pkg-unsafe");
        var zip = CreatePackage(harness, "evil.zip",
            ("Mod/Mod.dll", FakeDll));
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("../escape.dll");
            using var stream = entry.Open();
            stream.Write(FakeDll, 0, FakeDll.Length);
        }

        Assert.Throws<InvalidDataException>(() => harness.Manager.ImportModPackage(harness.GameDirectory, zip));
        Assert.False(Directory.Exists(harness.ModsDirectory));
        Assert.False(File.Exists(harness.Sandbox.Combine("escape.dll")));
    }

    [Fact]
    public void ImportModPackage_RejectsLoaderReleasePackage()
    {
        using var harness = new ImportHarness("ap-pkg-loader");
        var zip = CreatePackage(harness, "cesium-loader.zip",
            ("version.dll", FakeDll),
            ("AstralParty_ModLoader/doorstop_config.json", Text("{}")));

        var error = Assert.Throws<InvalidOperationException>(
            () => harness.Manager.ImportModPackage(harness.GameDirectory, zip));
        Assert.Contains("导入发布包", error.Message);
        Assert.False(Directory.Exists(harness.ModsDirectory));
    }

    [Fact]
    public void ImportModPackage_RejectsPackageWithoutModLayout()
    {
        using var harness = new ImportHarness("ap-pkg-nolayout");
        var zip = CreatePackage(harness, "docs.zip",
            ("README.md", Text("说明")),
            ("docs/usage.txt", Text("文档")));

        var error = Assert.Throws<InvalidDataException>(
            () => harness.Manager.ImportModPackage(harness.GameDirectory, zip));
        Assert.Contains("没有找到模组", error.Message);
        Assert.False(Directory.Exists(harness.ModsDirectory));
    }

    [Fact]
    public void ImportModPackage_RejectsDuplicateModsInOnePackage()
    {
        using var harness = new ImportHarness("ap-pkg-dup");
        var zip = CreatePackage(harness, "two-versions.zip",
            ("v1/Mod/Mod.dll", FakeDll),
            ("v2/Mod/Mod.dll", FakeDll));

        var error = Assert.Throws<InvalidDataException>(
            () => harness.Manager.ImportModPackage(harness.GameDirectory, zip));
        Assert.Contains("两个同名模组", error.Message);
    }

    [Fact]
    public void ImportModPackage_RejectsNonZipFile()
    {
        using var harness = new ImportHarness("ap-pkg-notzip");
        var dll = harness.Sandbox.WriteFile("Mod.dll", FakeDll);

        Assert.Throws<InvalidDataException>(() => harness.Manager.ImportModPackage(harness.GameDirectory, dll));
    }

    [Fact]
    public void ImportReleaseFile_PointsModPackageToTheRightButton()
    {
        using var harness = new ImportHarness("ap-pkg-wrong-button");
        var zip = CreatePackage(harness, "HandViewerMod.zip",
            ("HandViewerMod/HandViewerMod.dll", FakeDll),
            ("HandViewerMod/HandViewerMod.json", Text("{\"id\":\"HandViewerMod\"}")));

        // 在「导入发布包」窗口里选了模组包：要说清该用「导入模组」，而不是报 SDK 缺文件
        var error = Assert.Throws<InvalidOperationException>(
            () => harness.Manager.ImportReleaseFile(harness.GameDirectory, zip, overwriteDll: false));
        Assert.Contains("导入模组", error.Message);
        Assert.False(Directory.Exists(harness.ModsDirectory));
    }

    [RealModPackageFact]
    public void ImportModPackage_RealPackageInstalls()
    {
        using var harness = new ImportHarness("ap-pkg-real");
        var zip = RealModPackage.FindFile()!;

        var result = harness.Manager.ImportModPackage(harness.GameDirectory, zip);

        Assert.NotEmpty(result.Mods);
        foreach (var mod in result.Mods)
        {
            Assert.False(string.IsNullOrEmpty(mod.DirectoryName));
            Assert.True(File.Exists(Path.Combine(harness.ModsDirectory, mod.DirectoryName!, mod.DirectoryName + ".dll")),
                $"{mod.DirectoryName} 没有解压成加载器认识的布局");
        }
        var scanned = harness.Manager.GetStatus().Mods;
        Assert.Equal(result.Mods.Count, scanned.Count);
        Assert.All(scanned, mod => Assert.True(mod.Enabled));
    }

    private static string CreatePackage(ImportHarness harness, string fileName,
        params (string Name, byte[] Bytes)[] entries)
    {
        var path = harness.Sandbox.Combine(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(bytes, 0, bytes.Length);
        }
        return path;
    }

    private static byte[] Text(string content) => System.Text.Encoding.UTF8.GetBytes(content);

    /// <summary>读已装模组 sidecar 里的版本号（用于断言磁盘上的真实状态）。</summary>
    private static string ReadSidecarVersion(string modFolder)
    {
        var sidecar = Directory.GetFiles(modFolder, "*.json")
            .First(file => !Path.GetFileName(file).Equals("config.json", StringComparison.OrdinalIgnoreCase));
        using var document = JsonDocument.Parse(File.ReadAllText(sidecar));
        return document.RootElement.GetProperty("version").GetString() ?? "";
    }

    private sealed class ImportHarness : IDisposable
    {
        public ImportHarness(string sandboxName)
        {
            Sandbox = new TestSandbox(sandboxName);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            Manager = new ModManager(TestPaths.AppDirectory, Sandbox.EnsureDirectory("profile"));
            // 把沙箱游戏目录写进状态：GetStatus 才不会退回自动检测（本机真实游戏目录）
            Manager.SaveStoredGameDirectory(GameDirectory);
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public ModManager Manager { get; }

        /// <summary>模拟回收时交出去的文件（断言「走的是回收，不是硬删」）。</summary>
        public List<string> Recycled { get; } = new();

        public string ModsDirectory =>
            Path.Combine(GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);

        /// <summary>
        /// 接管「送进回收站」这一步，测试不往开发者真实回收站里塞临时文件。
        /// <paramref name="available"/>=true 时把文件从目录里移走（模拟回收成功）；false 时什么都不做（模拟该卷没有回收站）。
        /// </summary>
        public void SimulateRecycleBin(bool available)
        {
            Manager.RecycleFileHook = path =>
            {
                if (!available) return (false, "所在磁盘没有回收站");
                try
                {
                    File.Delete(path);
                    Recycled.Add(path);
                    return (true, "");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            };
        }

        public void Dispose() => Sandbox.Dispose();
    }
}

/// <summary>
/// 真实 mod 包取样：环境变量 <c>ASTRAL_TEST_MOD_PACKAGE</c> 指向一个现成的模组 ZIP。
/// 没有样本时报告为「已跳过」，不假装通过。
/// </summary>
internal static class RealModPackage
{
    public const string EnvironmentVariable = "ASTRAL_TEST_MOD_PACKAGE";

    public static string? FindFile()
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    public static string SkipReason =>
        $"没有可用的真实模组包：设置环境变量 {EnvironmentVariable} 指向一个模组 ZIP。";
}

/// <summary>需要真实模组包的测试：没有样本时标记为跳过。</summary>
public sealed class RealModPackageFactAttribute : FactAttribute
{
    public RealModPackageFactAttribute()
    {
        if (RealModPackage.FindFile() is null) Skip = RealModPackage.SkipReason;
    }
}

/// <summary>需要临时目录所在卷支持回收站的测试：与资源管理器删除一样能还原。</summary>
public sealed class RecycleBinFactAttribute : FactAttribute
{
    public RecycleBinFactAttribute()
    {
        if (!RecycleBinHelper.VolumeHasRecycleBin(Path.GetTempPath()))
            Skip = "当前临时目录所在卷没有回收站";
    }
}
