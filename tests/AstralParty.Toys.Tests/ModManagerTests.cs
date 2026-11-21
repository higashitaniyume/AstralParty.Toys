using System.IO.Compression;
using AstralParty.Toys.Models;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// Mod 加载器（CesiumLoader version.dll Doorstop 式）：内嵌资源、安装 / 卸载 / 覆盖保护、
/// mod 列表（含 sidecar 元数据）/ 导入 / 删除。
/// 一律使用临时沙箱里的假游戏目录与假 profile，不碰真实游戏。
/// </summary>
public sealed class ModManagerTests
{
    [Fact]
    public void EmbeddedPackage_IsComplete()
    {
        // 内置加载器现在就是**一整个 CesiumLoader 发布包**（cesium-loader.zip），
        // 「安装内置版本」= 解压这份包，所以这里的断言都从包本身派生。
        Assert.True(ModManager.HasEmbeddedPackage, "内置发布包资源缺失");
        Assert.True(ModManager.HasEmbeddedLoader, "发布包里没有 version.dll");
        Assert.True(ModManager.HasEmbeddedConfig, "发布包里没有 doorstop_config.json");
        Assert.True(ModManager.HasEmbeddedSdk, "发布包里没有 CesiumLoader.SDK.dll");
        Assert.True(ModManager.HasEmbeddedBuiltInMods, "发布包里没有任何内置 mod");
        Assert.True(ModManager.HasEmbeddedSampleMod, "发布包里没有 ActivityLogMod（行为日志）");
        Assert.False(string.IsNullOrWhiteSpace(ModManager.BundleLoaderVersion), "读不出包内版本号");
        Assert.False(string.IsNullOrWhiteSpace(ModManager.BundleSdkVersion), "读不出包内 SDK 版本");
        Assert.False(string.IsNullOrWhiteSpace(ModManager.BundledLoaderChangelog), "读不出包内加载器更新日志");

        var manifest = ModManager.EmbeddedPackageManifest;
        Assert.NotNull(manifest);
        Assert.Equal(ModManager.BundleLoaderVersion, manifest!.Version);
        Assert.Contains("version.dll", manifest.Files.Keys);

        // ★ 回归(2.4.3): 加载器发布包里已有的内置 mod 曾经在 Toys 侧漏装（PinnedRelicsMod / PvePanelMod /
        //   AngelModeUnlockMod 只存在于加载器包，Toys 的 BuiltInMods.props 没有）。现在清单**直接来自包**，
        //   这里再点名一次，并核对清单与包内 mods\ 目录完全一致。
        Assert.Contains("ActivityLogMod", ModManager.BuiltInModIds);
        Assert.Contains("CombatOddsMod", ModManager.BuiltInModIds);
        Assert.Contains("FontMod", ModManager.BuiltInModIds);
        Assert.Contains("PinnedRelicsMod", ModManager.BuiltInModIds);
        Assert.Contains("PvePanelMod", ModManager.BuiltInModIds);
        Assert.Contains("AngelModeUnlockMod", ModManager.BuiltInModIds);

        using var stream = new MemoryStream(ModManager.EmbeddedPackageBytes!);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        const string prefix = "AstralParty_ModLoader/mods/";
        var packagedIds = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && e.FullName.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => e.FullName.Substring(prefix.Length).Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(packagedIds);
        Assert.Equal(packagedIds, ModManager.BuiltInModIds);
        // 每个内置 mod 都要有成对的 dll + sidecar，否则装出来就是个半残的模组
        foreach (var id in packagedIds)
        {
            Assert.True(archive.GetEntry($"{prefix}{id}/{id}.dll") is not null, $"{id} 缺 dll");
            Assert.True(archive.GetEntry($"{prefix}{id}/{id}.json") is not null, $"{id} 缺 sidecar");
        }
    }

    [Fact]
    public void Install_CopiesLoaderAndCreatesStructure()
    {
        using var harness = new ModHarness("ap-mod-install");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var status = harness.Manager.GetStatus();
        Assert.Equal(harness.GameDirectory, status.GameDirectory);
        Assert.True(status.GameExeFound, "未识别出假 exe 目录");
        Assert.False(status.Installed);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        status = harness.Manager.GetStatus();
        Assert.True(status.Installed);
        Assert.True(status.LoaderMatchesBundle, "安装后 version.dll 哈希不一致");
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")), "version.dll 未复制");

        // 目录结构 + doorstop_config + SDK + 示例 mod(含 sidecar)
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Assert.True(Directory.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName)), "mods 目录未创建");
        Assert.True(Directory.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName)), "sdk 目录未创建");
        Assert.True(Directory.Exists(Path.Combine(loaderRoot, ModManager.LogsFolderName)), "logs 目录未创建");
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ConfigFileName)), "doorstop_config.json 未复制");
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, ModManager.SdkDllName)), "SDK 未复制");
        // 内置 mod 全部装进各自的 mod 文件夹（DLL + sidecar）
        foreach (var modId in ModManager.BuiltInModIds)
        {
            var modDir = Path.Combine(loaderRoot, ModManager.ModsFolderName, modId);
            Assert.True(File.Exists(Path.Combine(modDir, modId + ".dll")), $"{modId} 未复制");
            Assert.True(File.Exists(Path.Combine(modDir, modId + ".json")), $"{modId} sidecar 未复制");
        }

        // 状态里的列表
        Assert.Single(status.Sdk);
        Assert.Equal(ModManager.BuiltInModIds.Count, status.Mods.Count); // 内置 mod 全部落地
        Assert.Contains(status.Mods, m => m.FileName == ModManager.SampleModDllName);
        Assert.Contains(status.Mods, m => m.FileName == "FreeCameraMod.dll");
        Assert.Contains(status.Mods, m => m.FileName == "SpeedHackMod.dll");
    }

    [Fact]
    public void Install_InstallsEveryBuiltInModFromPackage()
    {
        using var harness = new ModHarness("ap-mod-install-nosample");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 内置安装 = 解压发布包：包里的内置模组全都要落地（不再有「只装加载器」的选项）
        var status = harness.Manager.GetStatus();
        Assert.NotEmpty(ModManager.BuiltInModIds);
        Assert.Equal(ModManager.BuiltInModIds.Count, status.Mods.Count);
        foreach (var id in ModManager.BuiltInModIds)
            Assert.True(File.Exists(Path.Combine(
                harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName, id, id + ".dll")), $"{id} 未落地");
        Assert.Single(status.Sdk); // SDK 始终安装
        Assert.True(File.Exists(Path.Combine(
            harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName)), "doorstop_config.json 始终安装");
    }

    [Fact]
    public void Install_ExtractsWholePackageIncludingRootFilesAndSymbols()
    {
        using var harness = new ModHarness("ap-mod-install-wholesale");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var game = harness.GameDirectory;
        var loaderRoot = Path.Combine(game, ModManager.LoaderFolderName);

        // 包内根级文件 → 游戏 exe 目录（整包解压，不再只挑 version.dll）
        Assert.True(File.Exists(Path.Combine(game, "version.pdb")), "version.pdb 未安装");
        Assert.True(File.Exists(Path.Combine(game, ModManager.NoticeFileName)), "CesiumLoader-NOTICE.txt 未安装");
        Assert.True(File.Exists(Path.Combine(game, ModManager.LicenseFileName)), "CesiumLoader-LICENSE.txt 未安装");
        Assert.True(File.Exists(Path.Combine(game, "README.txt")), "README.txt 未安装");
        Assert.True(File.Exists(Path.Combine(game, "CHANGELOG.md")), "CHANGELOG.md 未安装");

        // 包内 AstralParty_ModLoader\** → 加载器目录（bootstrap / 许可 / SDK 符号与 XML 文档都照装）
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.NoticeFileName)));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.LicenseFileName)));
        Assert.True(File.Exists(Path.Combine(loaderRoot, "bootstrap", "CesiumLoader.Bootstrap.dll")), "bootstrap 未安装");
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, "CesiumLoader.SDK.pdb")));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, "CesiumLoader.SDK.xml")));

        // 内置 mod 的符号文件也随包落地（旧的内嵌实现只装 dll/json/data）
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName,
            "ActivityLogMod", "ActivityLogMod.pdb")));
    }

    [Fact]
    public void InstallPackage_KeepsPlayerOwnGenericRootFile()
    {
        using var harness = new ModHarness("ap-mod-root-collision");
        var readme = Path.Combine(harness.GameDirectory, "README.txt");
        File.WriteAllText(readme, "玩家自己的说明");

        var result = harness.Manager.InstallPackage(harness.GameDirectory,
            BuildFakePackage("9.0.0", includeSymbols: true,
                extraEntries: new[] { ("README.txt", System.Text.Encoding.UTF8.GetBytes("包内说明")) }),
            overwriteDll: false);

        Assert.True(result.HasSkippedFiles);
        Assert.Contains("README.txt", result.SkippedFiles);
        Assert.Equal("玩家自己的说明", File.ReadAllText(readme));   // 玩家的文件没被覆盖
        // 加载器自己的文件照常安装
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName)));
    }

    [Fact]
    public void Uninstall_KeepsPlayerOwnRootFiles_RemovesLoaderOwned()
    {
        using var harness = new ModHarness("ap-mod-uninstall-root-files");
        var readme = Path.Combine(harness.GameDirectory, "README.txt");
        File.WriteAllText(readme, "玩家自己的说明");

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        harness.Manager.Uninstall(harness.GameDirectory, force: false);

        // 本工具落的根级文件随卸载清掉
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, ModManager.NoticeFileName)));
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, ModManager.LicenseFileName)));
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.pdb")));
        // 从来不是本工具落的同名文件必须原样保留
        Assert.True(File.Exists(readme), "玩家自己的 README.txt 不能被卸载删掉");
        Assert.Equal("玩家自己的说明", File.ReadAllText(readme));
    }

    [Fact]
    public void Install_RecordsPackageHash_SoBuiltInVersionIsRollbackable()
    {
        using var harness = new ModHarness("ap-mod-embedded-history");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        // 再装一个更新的包，内置版本应当作为「可回滚版本」留在安装历史里
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("99.0.0"),
            overwriteDll: true, allowDowngrade: true);

        var versions = harness.Manager.GetRetainedVersions(harness.GameDirectory);
        var builtIn = Assert.Single(versions, v => v.Version == ModManager.BundleLoaderVersion);
        Assert.True(builtIn.PackageAvailable, "内置版本应记录发布包哈希，才能整包回滚");
        Assert.False(string.IsNullOrEmpty(builtIn.PackageHash));
    }

    [Fact]
    public void Install_RefusesToOverwriteForeignDll_UnlessForced()
    {
        using var harness = new ModHarness("ap-mod-overwrite");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<InvalidOperationException>(
            () => harness.Manager.Install(harness.GameDirectory, overwriteDll: false));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.True(harness.Manager.GetStatus().LoaderMatchesBundle, "覆盖安装后哈希应一致");
    }

    [Fact]
    public void Install_WritesBuiltInModDataFiles()
    {
        using var harness = new ModHarness("ap-mod-data-files");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var dataDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName,
            ModManager.ModsFolderName, "LanguageUnlockMod", "data");
        Assert.True(Directory.Exists(dataDir), "内置模组数据目录缺失");
        Assert.True(File.Exists(Path.Combine(dataDir, "en.pack")));
        Assert.True(File.Exists(Path.Combine(dataDir, "ja.xml")));

        // PVE 面板的图标资源同样随程序集内置（资源名 data.PvePanelMod.{文件名}）
        var panelDataDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName,
            ModManager.ModsFolderName, "PvePanelMod", "data");
        Assert.True(Directory.Exists(panelDataDir), "PvePanelMod 数据目录缺失");
        Assert.True(File.Exists(Path.Combine(panelDataDir, "relicbase_Purple.png")));
        Assert.True(File.Exists(Path.Combine(panelDataDir, "star_on.png")));

        var manifest = File.ReadAllText(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName,
            ModManager.InstalledManifestFileName));
        Assert.Contains("LanguageUnlockMod/data/en.pack", manifest);
        Assert.Contains("PvePanelMod/data/relicbase_Purple.png", manifest);
    }

    [Fact]
    public void Uninstall_RefusesForeignDll_UnlessForced()
    {
        using var harness = new ModHarness("ap-mod-uninstall-guard");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [9, 9, 9]);

        Assert.Throws<InvalidOperationException>(() => harness.Manager.Uninstall(harness.GameDirectory, force: false));

        var result = harness.Manager.Uninstall(harness.GameDirectory, force: true);
        Assert.True(result.RemovedDll, "强制卸载应删除 DLL");
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
    }

    [Fact]
    public void Uninstall_RemovesLoaderAndFolder()
    {
        using var harness = new ModHarness("ap-mod-uninstall");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        Assert.True(harness.Manager.GetStatus().Installed);

        var result = harness.Manager.Uninstall(harness.GameDirectory, force: false);
        Assert.True(result.RemovedDll);
        Assert.True(result.RemovedConfig);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        Assert.False(Directory.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName)));

        var status = harness.Manager.GetStatus();
        Assert.False(status.Installed);
        Assert.Empty(status.Mods);
    }

    [Fact]
    public void Uninstall_RemovesSiblingPdbOfLoader()
    {
        using var harness = new ModHarness("ap-mod-uninstall-pdb");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.Manager.InstallPackage(harness.GameDirectory,
            BuildFakePackage("2.5.0", includeSymbols: true), overwriteDll: false);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.pdb")));

        var result = harness.Manager.Uninstall(harness.GameDirectory, force: false);

        Assert.True(result.RemovedDll);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        // 与 version.dll 成对的符号文件也应被一并清理
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.pdb")));
    }

    [Fact]
    public void ImportMod_CopiesIntoModsAndAppearsInList()
    {
        using var harness = new ModHarness("ap-mod-import");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var source = harness.Sandbox.WriteFile("my-custom-mod.dll", new byte[] { 0x4D, 0x5A, 0x00, 0x01, 0x02, 0x03 });
        var entry = harness.Manager.ImportMod(harness.GameDirectory, source);

        Assert.Equal("my-custom-mod", entry.Name);
        Assert.Equal("my-custom-mod.dll", entry.FileName);
        Assert.Equal("my-custom-mod", entry.DirectoryName);
        // 新布局: mods\my-custom-mod\my-custom-mod.dll
        Assert.True(File.Exists(Path.Combine(
            harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName,
            "my-custom-mod", "my-custom-mod.dll")));

        var status = harness.Manager.GetStatus();
        Assert.Contains(status.Mods, m => m.FileName == "my-custom-mod.dll" && m.DirectoryName == "my-custom-mod");
    }

    [Fact]
    public void DeleteMod_RemovesFromMods()
    {
        using var harness = new ModHarness("ap-mod-delete");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        Assert.Equal(ModManager.BuiltInModIds.Count, harness.Manager.GetStatus().Mods.Count);

        // 逐个删掉内置 mod，最后 mods 目录应为空
        foreach (var modId in ModManager.BuiltInModIds)
        {
            var removed = harness.Manager.DeleteMod(harness.GameDirectory, modId + ".dll");
            Assert.NotNull(removed);
            Assert.Equal(modId + ".dll", removed.FileName);
        }
        Assert.Empty(harness.Manager.GetStatus().Mods);
    }

    [Fact]
    public void Install_RejectsNonGameDirectory()
    {
        using var harness = new ModHarness("ap-mod-reject");
        // 没有 exe 的目录
        Assert.Throws<InvalidOperationException>(
            () => harness.Manager.Install(harness.Sandbox.EnsureDirectory("empty"), overwriteDll: false));
    }

    [Fact]
    public void GetStatus_WithoutInstall_ReportsNotInstalled()
    {
        using var harness = new ModHarness("ap-mod-status");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var status = harness.Manager.GetStatus();
        Assert.True(status.BundleLoaderPresent);
        Assert.False(status.Installed);
        Assert.False(status.LoaderPresent);
        Assert.Equal(harness.GameDirectory, status.GameDirectory);
    }

    [Fact]
    public void ScanMods_ReadsSidecarMetadata()
    {
        using var harness = new ModHarness("ap-mod-manifest");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.InstallLoaderOnly();

        // 放一个带完整 sidecar 的 mod（新格式: id/name/version/permissions/sdkVersion/dependencies）
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "MyMod.dll"), new byte[] { 0x4D, 0x5A, 0x01 });
        File.WriteAllText(Path.Combine(modsDir, "MyMod.json"),
            "{\"id\":\"MyMod\",\"name\":\"我的Mod\",\"version\":\"1.2.0\",\"author\":\"小明\"," +
            "\"description\":\"测试用\",\"permissions\":6,\"sdkVersion\":\"2.0.0\"," +
            "\"dependencies\":[{\"id\":\"LibMod\",\"minVersion\":\"1.0.0\"}]}");

        var status = harness.Manager.GetStatus();
        var entry = Assert.Single(status.Mods);
        Assert.Equal("MyMod", entry.Id);
        Assert.Equal("我的Mod", entry.DisplayName);
        Assert.Equal("1.2.0", entry.Version);
        Assert.Equal("小明", entry.Author);
        Assert.Equal("测试用", entry.Description);
        Assert.Equal("2.0.0", entry.SdkVersion);
        Assert.Equal(6, entry.Permissions); // GameActions(2) | SpeedHack(4)
        var dep = Assert.Single(entry.Dependencies);
        Assert.Equal("LibMod", dep.Id);
        Assert.Equal("1.0.0", dep.MinVersion);
    }

    [Fact]
    public void ScanMods_NewFolderLayout_ReadsModFromSubdirectory()
    {
        using var harness = new ModHarness("ap-mod-folderlayout");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.InstallLoaderOnly();

        // 新布局: mods\MyMod\MyMod.dll + MyMod.json(sidecar 在 mod 文件夹内)
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        var modFolder = Path.Combine(modsDir, "MyMod");
        Directory.CreateDirectory(modFolder);
        harness.Sandbox.WriteFile(Path.Combine(modFolder, "MyMod.dll"), new byte[] { 0x4D, 0x5A, 0x01 });
        File.WriteAllText(Path.Combine(modFolder, "MyMod.json"),
            "{\"id\":\"MyMod\",\"name\":\"我的Mod\",\"version\":\"1.2.0\",\"permissions\":1,\"sdkVersion\":\"2.0.0\"}");

        var status = harness.Manager.GetStatus();
        var entry = Assert.Single(status.Mods);
        Assert.Equal("MyMod", entry.Id);
        Assert.Equal("我的Mod", entry.DisplayName);
        Assert.Equal("1.2.0", entry.Version);
        Assert.Equal("MyMod", entry.DirectoryName, ignoreCase: true);
        Assert.Equal("MyMod.dll", entry.FileName);
    }

    [Fact]
    public void ScanMods_IgnoresNonModFolders()
    {
        using var harness = new ModHarness("ap-mod-nonmodfolder");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.InstallLoaderOnly();

        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        // 文件夹里没有同名 dll → 不是 mod 文件夹, 应忽略
        Directory.CreateDirectory(Path.Combine(modsDir, "README"));
        File.WriteAllText(Path.Combine(modsDir, "README", "readme.txt"), "hello");
        // 有同名 dll 但还有其它文件 → 正常识别
        Directory.CreateDirectory(Path.Combine(modsDir, "RealMod"));
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "RealMod", "RealMod.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllText(Path.Combine(modsDir, "RealMod", "extra.txt"), "extra");

        var mods = harness.Manager.GetStatus().Mods;
        var entry = Assert.Single(mods);
        Assert.Equal("RealMod", entry.Name);
    }

    [Fact]
    public void DeleteMod_FolderLayout_RemovesWholeFolder()
    {
        using var harness = new ModHarness("ap-mod-del-folder");
        harness.InstallLoaderOnly();

        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        var modFolder = Path.Combine(modsDir, "MyMod");
        Directory.CreateDirectory(modFolder);
        harness.Sandbox.WriteFile(Path.Combine(modFolder, "MyMod.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllText(Path.Combine(modFolder, "MyMod.json"), "{\"id\":\"MyMod\",\"name\":\"我的Mod\",\"version\":\"1.0.0\"}");
        Assert.Single(harness.Manager.GetStatus().Mods);

        var removed = harness.Manager.DeleteMod(harness.GameDirectory, "MyMod.dll");
        Assert.NotNull(removed);
        Assert.Equal("MyMod", removed.DirectoryName);
        Assert.False(Directory.Exists(modFolder), "删除 mod 应移除整个文件夹");
        Assert.Empty(harness.Manager.GetStatus().Mods);
    }

    [Fact]
    public void ScanMods_ToleratesCorruptSidecar()
    {
        using var harness = new ModHarness("ap-mod-badsidecar");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.InstallLoaderOnly();

        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "Broken.dll"), new byte[] { 0x4D, 0x5A, 0x01 });
        File.WriteAllText(Path.Combine(modsDir, "Broken.json"), "{ 这不是合法 JSON !!!");

        var status = harness.Manager.GetStatus();
        var entry = Assert.Single(status.Mods);
        Assert.Equal("Broken", entry.Name);
        Assert.Equal("", entry.DisplayName); // sidecar 损坏按无 sidecar 处理
    }

    // ============================== 启用/禁用 + 配置 + 互斥 ==============================

    [Fact]
    public void ToggleMod_DisablesAndEnablesViaSidecar()
    {
        using var harness = new ModHarness("ap-mod-toggle");
        harness.InstallLoaderOnly();

        // 放一个 mod + sidecar
        var modsDir = ModsDir(harness);
        var disabledDir = DisabledModsDir(harness);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "MyMod.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllText(Path.Combine(modsDir, "MyMod.json"),
            "{\"id\":\"MyMod\",\"name\":\"我的Mod\",\"version\":\"1.0.0\"}");

        Assert.True(harness.Manager.GetStatus().Mods[0].Enabled, "初始 enabled=true");

        harness.Manager.ToggleMod(harness.GameDirectory, "MyMod.dll", enabled: false);
        // 停用 = 整个 mod 搬进 disabled_mods（加载器只扫 mods，等于彻底不加载）
        Assert.False(File.Exists(Path.Combine(modsDir, "MyMod.dll")));
        Assert.True(File.Exists(Path.Combine(disabledDir, "MyMod.dll")));
        var sidecarPath = Path.Combine(disabledDir, "MyMod.json");
        Assert.Contains("\"enabled\": false", File.ReadAllText(sidecarPath));
        var disabledEntry = Assert.Single(harness.Manager.GetStatus().Mods);
        Assert.False(disabledEntry.Enabled, "禁用后 enabled=false");
        Assert.True(disabledEntry.DisabledFolder, "禁用后应标记来自 disabled_mods");

        harness.Manager.ToggleMod(harness.GameDirectory, "MyMod.dll", enabled: true);
        Assert.True(File.Exists(Path.Combine(modsDir, "MyMod.dll")), "启用后应搬回 mods");
        Assert.False(File.Exists(Path.Combine(disabledDir, "MyMod.dll")));
        var enabledEntry = Assert.Single(harness.Manager.GetStatus().Mods);
        Assert.True(enabledEntry.Enabled, "重新启用后 enabled=true");
        Assert.False(enabledEntry.DisabledFolder);
    }

    [Fact]
    public void ToggleMod_WithoutSidecar_CreatesOne()
    {
        using var harness = new ModHarness("ap-mod-toggle-nosidecar");
        harness.InstallLoaderOnly();

        var modsDir = ModsDir(harness);
        var disabledDir = DisabledModsDir(harness);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "Legacy.dll"), new byte[] { 0x4D, 0x5A });

        harness.Manager.ToggleMod(harness.GameDirectory, "Legacy.dll", enabled: false);

        var sidecarPath = Path.Combine(disabledDir, "Legacy.json");
        Assert.True(File.Exists(sidecarPath), "无 sidecar 的 mod 禁用时应自动生成 sidecar 并一起搬走");
        Assert.True(File.Exists(Path.Combine(disabledDir, "Legacy.dll")));
        var json = File.ReadAllText(sidecarPath);
        Assert.Contains("\"id\":\"Legacy\"", json);
        Assert.Contains("\"enabled\": false", json);
        var entry = Assert.Single(harness.Manager.GetStatus().Mods);
        Assert.False(entry.Enabled);
        Assert.True(entry.DisabledFolder);
    }

    [Fact]
    public void ToggleMod_FolderLayout_MovesWholeModFolderAndDeleteFindsIt()
    {
        using var harness = new ModHarness("ap-mod-toggle-folder");
        harness.InstallLoaderOnly();

        var modsDir = ModsDir(harness);
        var disabledDir = DisabledModsDir(harness);
        var folder = Path.Combine(modsDir, "FolderMod");
        Directory.CreateDirectory(folder);
        harness.Sandbox.WriteFile(Path.Combine(folder, "FolderMod.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllText(Path.Combine(folder, "FolderMod.json"), "{\"id\":\"FolderMod\",\"version\":\"1.0.0\"}");
        File.WriteAllText(Path.Combine(folder, "config.json"), "{\"enabled\":true}");

        harness.Manager.ToggleMod(harness.GameDirectory, "FolderMod.dll", enabled: false);

        // 整个文件夹一起搬走，配置和附属文件都跟着
        Assert.False(Directory.Exists(folder));
        Assert.True(File.Exists(Path.Combine(disabledDir, "FolderMod", "FolderMod.dll")));
        Assert.True(File.Exists(Path.Combine(disabledDir, "FolderMod", "config.json")));

        // 停用的 mod 也要能在界面上直接删除
        var deleted = harness.Manager.DeleteMod(harness.GameDirectory, "FolderMod.dll");
        Assert.NotNull(deleted);
        Assert.True(deleted!.DisabledFolder);
        Assert.False(Directory.Exists(Path.Combine(disabledDir, "FolderMod")));
        Assert.Empty(harness.Manager.GetStatus().Mods);
    }

    private static string ModsDir(ModHarness harness) =>
        Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);

    private static string DisabledModsDir(ModHarness harness) =>
        Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.DisabledModsFolderName);

    [Fact]
    public void ToggleMods_BatchContinuesAfterFailure()
    {
        using var harness = new ModHarness("ap-mod-toggle-batch");
        harness.InstallLoaderOnly();
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "A.dll"), new byte[] { 0x4D, 0x5A });
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "B.dll"), new byte[] { 0x4D, 0x5A });

        var (ok, failed) = harness.Manager.ToggleMods(
            harness.GameDirectory, new[] { "A.dll", "Missing.dll", "B.dll", "A.dll", " " }, enabled: false);

        Assert.Equal(2, ok);
        Assert.Equal(new[] { "Missing.dll" }, failed);
        var status = harness.Manager.GetStatus();
        Assert.All(status.Mods, m => Assert.False(m.Enabled));
    }

    [Fact]
    public void DeleteMods_BatchContinuesAfterFailure()
    {
        using var harness = new ModHarness("ap-mod-delete-batch");
        harness.InstallLoaderOnly();
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "A.dll"), new byte[] { 0x4D, 0x5A });
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "B.dll"), new byte[] { 0x4D, 0x5A });

        var (ok, failed) = harness.Manager.DeleteMods(
            harness.GameDirectory, new[] { "A.dll", "Missing.dll", "B.dll", "B.dll" });

        Assert.Equal(2, ok);
        Assert.Equal(new[] { "Missing.dll" }, failed);
        Assert.Empty(harness.Manager.GetStatus().Mods);
    }

    [Fact]
    public void ModConfig_OpenCreatesFileAndReturnsPath()
    {
        using var harness = new ModHarness("ap-mod-config");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "CfgMod.dll"), new byte[] { 0x4D, 0x5A });

        // 无配置 → 创建并返回路径(配置落在 configs 下, 这是加载器唯一会优先读的位置)
        var path = harness.Manager.EnsureModConfig(harness.GameDirectory, "CfgMod.dll");
        Assert.True(File.Exists(path), "EnsureModConfig 应创建配置文件");
        Assert.EndsWith(Path.Combine("configs", "CfgMod.json"), path);

        // 写内容后 Read 能读回
        File.WriteAllText(path, "{\"LogUi\": true}");
        Assert.Equal("{\"LogUi\": true}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));
    }

    [Fact]
    public void ModConfig_LivesInConfigsFolder_AndSeedsFromPackagedDefault()
    {
        using var harness = new ModHarness("ap-mod-config-path");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        var modDir = Path.Combine(loaderRoot, ModManager.ModsFolderName, "CfgMod");
        harness.Sandbox.WriteFile(Path.Combine(modDir, "CfgMod.dll"), new byte[] { 0x4D, 0x5A });
        // 随包默认值: 模组目录里的 config.json(没有 configs 时加载器会读它)
        harness.Sandbox.WriteFile(Path.Combine(modDir, "config.json"),
            System.Text.Encoding.UTF8.GetBytes("{\"PackagedKey\": 1}"));

        // 现行约定: configs\{ModId}.json —— 加载器 SDK 的 ModConfig 只要看到它就只读它
        var expected = Path.Combine(loaderRoot, "configs", "CfgMod.json");
        Assert.Equal("{\"PackagedKey\": 1}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));

        // 打开(首次编辑)时把模组目录那份播种到 configs, 之后读到的是同一份内容
        var path = harness.Manager.EnsureModConfig(harness.GameDirectory, "CfgMod.dll");
        Assert.Equal(expected, path);
        Assert.Equal("{\"PackagedKey\": 1}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));

        harness.Manager.SaveModConfigFields(harness.GameDirectory, "CfgMod.dll", new List<ModConfigField>
        {
            new() { Name = "LogUi", Kind = "bool", BoolValue = true }
        });
        Assert.Contains("\"LogUi\": true", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));
        Assert.Equal(expected, harness.Manager.ModConfigPath(harness.GameDirectory, "CfgMod.dll"));
        // 双写：模组目录里那份也要跟着改，两边内容完全一致
        var packaged = Path.Combine(modDir, "config.json");
        Assert.Equal(File.ReadAllText(expected), File.ReadAllText(packaged));
        Assert.Contains("\"LogUi\": true", File.ReadAllText(packaged));
    }

    [Fact]
    public void ModConfig_DualWriteFollowsDisabledFolderAndSkipsMissingMods()
    {
        using var harness = new ModHarness("ap-mod-config-dualwrite");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        var modsDir = Path.Combine(loaderRoot, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "CfgMod", "CfgMod.dll"), new byte[] { 0x4D, 0x5A });

        // 停用的模组：第二份写进它当前所在的 disabled_mods，不把它搬回 mods
        harness.Manager.ToggleMod(harness.GameDirectory, "CfgMod.dll", false);
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.DisabledModsFolderName, "CfgMod", "CfgMod.dll")));
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "CfgMod.dll", new List<ModConfigField>
        {
            new() { Name = "LogUi", Kind = "bool", BoolValue = true }
        });
        var canonical = Path.Combine(loaderRoot, "configs", "CfgMod.json");
        var disabled = Path.Combine(loaderRoot, ModManager.DisabledModsFolderName, "CfgMod", "config.json");
        Assert.Equal(File.ReadAllText(canonical), File.ReadAllText(disabled));
        Assert.False(File.Exists(Path.Combine(modsDir, "CfgMod", "config.json")));

        // 没装的模组只写 configs，不凭空建一个没有 DLL 的模组目录
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "GhostMod.dll", new List<ModConfigField>
        {
            new() { Name = "LogUi", Kind = "bool", BoolValue = true }
        });
        Assert.True(File.Exists(Path.Combine(loaderRoot, "configs", "GhostMod.json")));
        Assert.False(Directory.Exists(Path.Combine(modsDir, "GhostMod")));
    }

    [Fact]
    public void SetFontModConfig_PreservesCommentsAndOtherKeys()
    {
        using var harness = new ModHarness("ap-fontmod-config");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var modDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName, "FontMod");
        harness.Sandbox.WriteFile(Path.Combine(modDir, "FontMod.dll"), new byte[] { 0x4D, 0x5A });
        // 玩家配置在 configs\FontMod.json(加载器优先读这里)
        var configDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "configs");
        Directory.CreateDirectory(configDir);
        var path = Path.Combine(configDir, "FontMod.json");
        File.WriteAllText(path,
            "{\n  // 玩家手写注释\n  \"enabled\": true,\n  \"fontFamily\": \"SimSun\",\n  \"applyToAllFonts\": 1.0,\n  \"scale\": 1.0\n}\n");

        harness.Manager.SetFontModConfig(harness.GameDirectory, "Microsoft YaHei", false);

        var text = File.ReadAllText(path);
        // 文本定点替换：注释、其它键与 1.0 的写法都保留
        Assert.Contains("// 玩家手写注释", text);
        Assert.Contains("\"fontFamily\": \"Microsoft YaHei\"", text);
        Assert.Contains("\"applyToAllFonts\": false", text);
        Assert.Contains("\"scale\": 1.0", text);
        // 游戏内的 FontMod 读的就是这份文件
        Assert.Equal(path, harness.Manager.ModConfigPath(harness.GameDirectory, "FontMod.dll"));
    }

    [Fact]
    public void SetFontModConfig_CreatesDefaultsWhenMissing()
    {
        using var harness = new ModHarness("ap-fontmod-config-new");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var modDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName, "FontMod");
        harness.Sandbox.WriteFile(Path.Combine(modDir, "FontMod.dll"), new byte[] { 0x4D, 0x5A });

        harness.Manager.SetFontModConfig(harness.GameDirectory, "Microsoft YaHei", true);

        var text = harness.Manager.ReadModConfig(harness.GameDirectory, "FontMod.dll");
        Assert.Contains("\"enabled\": true", text);
        Assert.Contains("\"fontFamily\": \"Microsoft YaHei\"", text);
        Assert.Contains("\"applyToAllFonts\": true", text);
    }

    [Fact]
    public void SetFontModConfig_WritesExistingLegacyConfigInPlace()
    {
        using var harness = new ModHarness("ap-fontmod-config-legacy");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        var modDir = Path.Combine(loaderRoot, ModManager.ModsFolderName, "FontMod");
        harness.Sandbox.WriteFile(Path.Combine(modDir, "FontMod.dll"), new byte[] { 0x4D, 0x5A });

        // SDK 在全新安装时会把配置写到 configs/FontMod.json；页面必须改同一份文件，
        // 否则游戏读旧文件、Toys 写新文件，用户在页面上的选择不会生效。
        var legacy = Path.Combine(loaderRoot, "configs", "FontMod.json");
        harness.Sandbox.WriteFile(legacy, System.Text.Encoding.UTF8.GetBytes(
            "{\n  \"enabled\": true,\n  \"fontFamily\": \"SimSun\"\n}\n"));

        harness.Manager.SetFontModConfig(harness.GameDirectory, "Microsoft YaHei", true);

        Assert.Contains("\"fontFamily\": \"Microsoft YaHei\"", File.ReadAllText(legacy));
        // 双写：模组目录里那份也写一份同样的内容（个别模组直接按路径读它）
        var packaged = Path.Combine(modDir, "config.json");
        Assert.True(File.Exists(packaged), "配置应同时写到模组目录的 config.json");
        Assert.Equal(File.ReadAllText(legacy), File.ReadAllText(packaged));
    }

    [Fact]
    public void Install_IgnoresLeftoverSpeedhackConfig()
    {
        using var harness = new ModHarness("ap-mod-conflict");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        // 模拟已安装独立变速器: version.dll + speedhack_config.json
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(harness.GameDirectory, "speedhack_config.json"), "{}");

        Assert.True(ModManager.HasSpeedhackInstalled(harness.GameDirectory), "应检测到变速器");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.True(harness.Manager.GetStatus().Installed);
        Assert.Equal("{}", File.ReadAllText(Path.Combine(harness.GameDirectory, "speedhack_config.json")));
    }

    [Fact]
    public void InstallPackage_IgnoresLeftoverSpeedhackConfig()
    {
        using var harness = new ModHarness("ap-mod-json-package");
        var config = Path.Combine(harness.GameDirectory, "speedhack_config.json");
        File.WriteAllText(config, "legacy settings");
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.0.0"), overwriteDll: false);
        Assert.Equal("legacy settings", File.ReadAllText(config));
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderDllName)));
    }

    [Theory]
    [InlineData("CesiumLoader.SDK.dll")]
    [InlineData("AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll")]
    public void ImportSdk_ImportsReleaseLayoutsWithoutTools(string sdkEntry)
    {
        using var harness = new ModHarness("ap-sdk-import");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var sdkPath = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "sdk", ModManager.SdkDllName);
        var sdk = File.ReadAllBytes(sdkPath);
        var zip = Path.Combine(harness.GameDirectory, "sdk.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var stream = archive.CreateEntry(sdkEntry).Open()) stream.Write(sdk);
            using (var stream = archive.CreateEntry("cesium.exe").Open()) stream.Write(new byte[] { 1 });
        }
        Assert.NotEmpty(harness.Manager.ImportSdk(harness.GameDirectory, zip));
        Assert.Equal(sdk, File.ReadAllBytes(sdkPath));
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "cesium.exe")));
    }

    [Theory]
    [InlineData("../CesiumLoader.SDK.dll")]
    [InlineData("CesiumLoader.SDK.dll")]
    public void ImportSdk_InvalidArchiveDoesNotReplaceSdk(string entry)
    {
        using var harness = new ModHarness("ap-sdk-invalid");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var sdkPath = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "sdk", ModManager.SdkDllName);
        var before = File.ReadAllBytes(sdkPath);
        var zip = Path.Combine(harness.GameDirectory, "invalid.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var stream = archive.CreateEntry(entry).Open()) stream.Write(new byte[] { 1, 2, 3 });
        Assert.ThrowsAny<Exception>(() => harness.Manager.ImportSdk(harness.GameDirectory, zip));
        Assert.Equal(before, File.ReadAllBytes(sdkPath));
    }

    [Fact]
    public void ImportSdk_DuplicateEntriesAreRejectedBeforeWriting()
    {
        using var harness = new ModHarness("ap-sdk-dupes");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var sdkPath = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "sdk", ModManager.SdkDllName);
        var before = File.ReadAllBytes(sdkPath);
        var zip = Path.Combine(harness.GameDirectory, "duplicate.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var stream = archive.CreateEntry(ModManager.SdkDllName).Open()) stream.Write(before);
            using (var stream = archive.CreateEntry(ModManager.SdkDllName.ToUpperInvariant()).Open()) stream.Write(before);
        }
        Assert.Throws<InvalidOperationException>(() => harness.Manager.ImportSdk(harness.GameDirectory, zip));
        Assert.Equal(before, File.ReadAllBytes(sdkPath));
    }

    [Fact]
    public void ImportSdk_RequiresLoader()
    {
        using var harness = new ModHarness("ap-sdk-no-loader");
        Assert.Throws<InvalidOperationException>(() => harness.Manager.ImportSdk(harness.GameDirectory, "unused.zip"));
        Assert.False(Directory.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName)));
    }

    [Fact]
    public void ImportSdk_DirectDllPreservesModsAndConfig()
    {
        using var harness = new ModHarness("ap-sdk-direct");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var sdkPath = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "sdk", ModManager.SdkDllName);
        var configPath = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName);
        var before = File.ReadAllBytes(configPath);
        var mods = harness.Manager.GetStatus().Mods.Count;
        Assert.NotEmpty(harness.Manager.ImportSdk(harness.GameDirectory, sdkPath));
        Assert.Equal(mods, harness.Manager.GetStatus().Mods.Count);
        // 同一 SDK 的导入不改变已有配置。
        Assert.Equal(before, File.ReadAllBytes(configPath));
    }

    [Fact]
    public void SpeedhackInstall_DetectsModLoaderConflict()
    {
        using var harness = new ModHarness("ap-mod-conflict2");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        // 模拟已安装 Mod 加载器: version.dll + AstralParty_ModLoader\doorstop_config.json
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3]);
        Directory.CreateDirectory(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName));
        File.WriteAllText(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName), "{}");

        var speedhack = new SpeedhackManager(TestPaths.AppDirectory, harness.Sandbox.EnsureDirectory("profile2"));
        Assert.True(SpeedhackManager.HasModLoaderInstalled(harness.GameDirectory), "应检测到加载器");
        var ex = Assert.Throws<InvalidOperationException>(
            () => speedhack.Install(harness.GameDirectory, overwriteDll: true));
        Assert.Contains("加载器", ex.Message);
        Assert.Contains("speedhackBaseSpeed", ex.Message);
    }

    [Fact]
    public void SetSpeedhack_EnablesAndDisablesViaConfig()
    {
        using var harness = new ModHarness("ap-mod-speedhack");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 默认 1.0 = 未启用
        Assert.Equal(1.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);

        // 启用: 2.0
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);
        Assert.Equal(2.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);

        // 禁用: 1.0
        harness.Manager.SetSpeedhack(harness.GameDirectory, 1.0);
        Assert.Equal(1.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);

        // 非法值拒绝
        Assert.Throws<InvalidOperationException>(() => harness.Manager.SetSpeedhack(harness.GameDirectory, 0));
        Assert.Throws<InvalidOperationException>(() => harness.Manager.SetSpeedhack(harness.GameDirectory, 101));
    }

    [Fact]
    public void Install_PreservesExistingUserConfig()
    {
        using var harness = new ModHarness("ap-mod-preserve-cfg");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 用户设置变速 2.0
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);
        Assert.Equal(2.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);

        // 重新安装加载器 → doorstop_config 必须保留(不重置用户设置)
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.Equal(2.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);
    }

    [Fact]
    public void InstallPackage_PreservesExistingUserConfig()
    {
        using var harness = new ModHarness("ap-mod-pkg-preserve");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 用户设置变速 2.0
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);

        // 从发布包更新(包内 doorstop 模板是 1.0) → 必须保留用户的 2.0
        var bytes = BuildFakePackage("5.0.0");
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: true);
        Assert.Equal(2.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);
    }

    [Fact]
    public void Install_RaisesSdkVersion_ButKeepsUserSettings()
    {
        using var harness = new ModHarness("ap-mod-sdk-sync");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);

        var bundle = ModManager.BundleSdkVersion;
        Assert.False(string.IsNullOrEmpty(bundle), "内置 doorstop_config.json 模板没有 sdkVersion");

        // 模拟"老版本装出来的配置": sdkVersion 停在旧号(随包内置 mod 已声明更高的 SdkVersion)
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, File.ReadAllText(path).Replace(bundle, "2.1.0"));
        Assert.Equal("2.1.0", harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);

        // 重新安装 → sdkVersion 必须抬到随包版本, 否则加载器会拒绝加载新版内置 mod; 用户设置照旧
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(bundle, config.SdkVersion);
        Assert.Equal(2.0, config.SpeedhackBaseSpeed);
    }

    [Fact]
    public void Install_DoesNotDowngradeNewerSdkVersion()
    {
        using var harness = new ModHarness("ap-mod-sdk-nodowngrade");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var bundle = ModManager.BundleSdkVersion;
        Assert.False(string.IsNullOrEmpty(bundle), "内置 doorstop_config.json 模板没有 sdkVersion");

        // 用户自己装了更新的加载器(配置里的 SDK 号比随包版本还高) → 安装不得把它降回去
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, File.ReadAllText(path).Replace(bundle, "9.9.9"));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.Equal("9.9.9", harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);
    }

    [Fact]
    public void InstallPackage_RaisesSdkVersion_FromPackage()
    {
        using var harness = new ModHarness("ap-mod-pkg-sdk-sync");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);

        // 老配置停在旧 SDK 号 → 从发布包更新时抬到包内声明的版本
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, File.ReadAllText(path).Replace(ModManager.BundleSdkVersion, "2.0.0"));

        var bytes = BuildFakePackage("5.0.0", configSdkVersion: "9.9.9");
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal("9.9.9", config.SdkVersion);
        Assert.Equal(2.0, config.SpeedhackBaseSpeed); // 用户设置仍保留
    }

    [Fact]
    public void InstallPackage_WithoutSdkVersion_LeavesConfigAlone()
    {
        using var harness = new ModHarness("ap-mod-pkg-sdk-keep");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var before = harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion;
        Assert.False(string.IsNullOrEmpty(before), "内置 doorstop_config.json 模板没有 sdkVersion");

        // 老发布包(配置里没写 sdkVersion) → 不添不改(加载器会用自己编译进去的默认值)
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("5.0.0"), overwriteDll: true);

        Assert.Equal(before, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstInstall_DefaultsToSteamLaunch(bool fromPackage)
    {
        using var harness = new ModHarness("ap-mod-first-steam");
        if (fromPackage)
            harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.0.0"), overwriteDll: false);
        else
            harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.False(config.SteamBypassEnabled);
        Assert.False(config.SteamBypassMatchmaking);
        Assert.False(config.SteamBypassLobbyQuery);
        Assert.False(ModManager.IsSteamBypassActive(config));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Reinstall_PreservesSteamLaunchChoice(bool fromPackage, bool bypass)
    {
        using var harness = new ModHarness("ap-mod-keep-steam-choice");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, bypass));

        if (fromPackage)
            harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.0.0"), overwriteDll: true);
        else
            harness.Manager.Install(harness.GameDirectory, overwriteDll: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(bypass, config.SteamBypassEnabled);
        Assert.Equal(bypass, config.SteamBypassMatchmaking);
        Assert.Equal(bypass, config.SteamBypassLobbyQuery);
    }

    // ============================== 老配置补齐 Steam 绕过开关 ==============================

    [Fact]
    public void Install_AddsMissingSteamBypassKeys_ButKeepsUserValuesAndComments()
    {
        using var harness = new ModHarness("ap-mod-steam-keys-add");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 模拟"旧版内置模板装出来的配置": 没有 steamBypass 键; 用户已把主开关关掉过, 还写了自己的注释
        File.WriteAllText(path,
            "{\n  // 这是我自己写的注释\n  \"enabled\": true,\n  \"speedhackBaseSpeed\": 2.0,\n"
            + "  \"steamBypassEnabled\": false,\n  \"sdkVersion\": \"2.1.7\"\n}");

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.False(config.SteamBypassEnabled);              // 用户显式关掉的值必须原样保留
        Assert.True(config.SteamBypassRestartCheck);          // 缺失的按键位补上(= 模板默认)
        Assert.True(config.SteamBypassMatchmaking);
        Assert.True(config.SteamBypassLobbyQuery);
        Assert.Equal("auto", config.SteamBypassTaskCtorMode);
        Assert.False(config.SteamBypassLobbyMethods);         // 地雷键必须补成 false
        Assert.Equal(2.0, config.SpeedhackBaseSpeed);         // 其它用户设置照旧
        Assert.Equal(ModManager.BundleSdkVersion, config.SdkVersion); // sdkVersion 同时被抬升

        var text = File.ReadAllText(path);
        Assert.Contains("这是我自己写的注释", text);           // 原注释保留(文本插入而不是重写)
        // ★ 必须断言文件文本: ReadLoaderConfig 对"缺失的键"返回的默认值恰好就是要断言的默认值,
        //   只看解析结果无法区分"键被补上了"和"键根本不存在"。
        Assert.Contains("\"steamBypassMatchmaking\":", text);
        Assert.Contains("\"steamBypassLobbyQuery\":", text);
        Assert.Contains("\"steamBypassTaskCtorMode\": \"auto\"", text);
        Assert.Contains("\"steamBypassLobbyMethods\": false", text);
        Assert.Contains("\"steamBypassEnabled\": false", text);   // 用户的值没被覆盖
    }

    [Fact]
    public void Install_LeavesFullySpecifiedSteamKeysUntouched()
    {
        using var harness = new ModHarness("ap-mod-steam-keys-keep");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 新模板装出来的配置 7 个键都在 → 再次安装必须一字不改(幂等)
        var before = File.ReadAllText(path);
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void InstallPackage_AddsMissingSteamBypassKeys()
    {
        using var harness = new ModHarness("ap-mod-pkg-steam-keys");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, "{\n  \"enabled\": true,\n  \"sdkVersion\": \"2.1.7\"\n}");

        // 走发布包这条路(旧版 Toys 的"下载更新"就是它)也要补齐
        harness.Manager.InstallPackage(harness.GameDirectory,
            BuildFakePackage("5.0.0", configSdkVersion: "9.9.9"), overwriteDll: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.True(config.SteamBypassEnabled);
        Assert.True(config.SteamBypassMatchmaking);
        Assert.False(config.SteamBypassLobbyMethods);
        Assert.Equal("9.9.9", config.SdkVersion);

        var text = File.ReadAllText(path);
        Assert.Contains("\"steamBypassEnabled\": true", text);    // 确实写进了文件
        Assert.Contains("\"steamBypassLobbyMethods\": false", text);
    }

    // ============================== 老配置补齐 profileWindowTitle ==============================

    [Fact]
    public void Install_AddsMissingProfileWindowTitle_ButKeepsUserValuesAndComments()
    {
        using var harness = new ModHarness("ap-mod-window-title-add");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 模拟旧版模板装出来的配置：没有 profileWindowTitle；用户还写了自己的注释和其它设置
        File.WriteAllText(path,
            "{\n  // 这是我自己写的注释\n  \"enabled\": true,\n  \"speedhackBaseSpeed\": 2.0,\n"
            + "  \"profile\": \"P2\",\n  \"sdkVersion\": \"2.1.7\"\n}");

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.True(config.ProfileWindowTitle);               // 缺失 → 补成默认（开）
        Assert.Equal("P2", config.Profile);                   // 其它设置照旧
        Assert.Equal(2.0, config.SpeedhackBaseSpeed);

        var text = File.ReadAllText(path);
        Assert.Contains("这是我自己写的注释", text);           // 注释保留（文本插入而不是重写）
        // ★ 断言文件文本：ReadLoaderConfig 对缺失键返回的默认值恰好也是 true，
        //   只看解析结果区分不了"补上了"和"根本不存在"。
        Assert.Contains("\"profileWindowTitle\": true", text);
    }

    [Fact]
    public void Install_KeepsProfileWindowTitleTurnedOff()
    {
        using var harness = new ModHarness("ap-mod-window-title-keep");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        Assert.True(harness.Manager.SetProfileWindowTitle(harness.GameDirectory, false));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);

        Assert.False(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
        Assert.Contains("\"profileWindowTitle\": false", File.ReadAllText(path));
    }

    [Fact]
    public void InstallPackage_AddsMissingProfileWindowTitle()
    {
        using var harness = new ModHarness("ap-mod-pkg-window-title");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, "{\n  \"enabled\": true,\n  \"sdkVersion\": \"2.1.7\"\n}");

        // 走发布包这条路也要补齐
        harness.Manager.InstallPackage(harness.GameDirectory,
            BuildFakePackage("5.0.0", configSdkVersion: "9.9.9"), overwriteDll: true);

        Assert.True(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
        Assert.Contains("\"profileWindowTitle\": true", File.ReadAllText(path));
    }

    // ============================== 首页「启动方式」单选框 → 加载器配置 ==============================
    //
    // 首页单选框写的是**一整套** Steam 绕过开关（主开关 + 匹配 + 房间列表查询），因为这个加载器的 hook 是
    // 启动时无条件安装、各自只受自己的键控制：只关主开关的话，从 Steam 启动仍然不建真大厅、房间列表照样返回空。
    // 写入必须做到：只动这些字面量（注释、其它键、缩进全保留），且写完自检通过才落盘。

    [Fact]
    public void SetSteamBypassProfile_FlipsTheWholeSet_AndKeepsComments()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 用户主动选择绕过后，再验证切回 Steam 时整套开关关闭且保留注释。
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        var before = File.ReadAllText(path);
        // 绕过启用：主开关 / 匹配 / 房间列表查询都是开
        Assert.Contains("\"steamBypassEnabled\": true", before);
        Assert.Contains("\"steamBypassMatchmaking\": true", before);
        Assert.Contains("\"steamBypassLobbyQuery\": true", before);
        var beforeConfig = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.True(ModManager.IsSteamBypassActive(beforeConfig));

        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));

        var text = File.ReadAllText(path);
        // 三个键必须一起关 —— 少关任何一个，"从 Steam 启动就用真大厅"都不成立
        Assert.Contains("\"steamBypassEnabled\": false", text);
        Assert.Contains("\"steamBypassMatchmaking\": false", text);
        Assert.Contains("\"steamBypassLobbyQuery\": false", text);
        // 模板里的注释必须在（这正是不能用 SaveLoaderConfig 的原因：它是白名单字典整体重写、会清掉注释）
        Assert.Contains("本是 Steamworks 的无害样板调用", text);
        // 每个键 "true" → "false" 只多一个字符 == 文件几乎原样，没有被整体重写成无注释版本
        Assert.Equal(before.Length + 3, text.Length);

        var afterConfig = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.False(ModManager.IsSteamBypassActive(afterConfig));                    // 整套都关了
        Assert.Equal(beforeConfig.SpeedhackBaseSpeed, afterConfig.SpeedhackBaseSpeed);
        Assert.Equal(beforeConfig.SteamBypassRestartCheck, afterConfig.SteamBypassRestartCheck);
        Assert.Equal(beforeConfig.SteamBypassLobbyMethods, afterConfig.SteamBypassLobbyMethods);
        Assert.Equal(beforeConfig.SdkVersion, afterConfig.SdkVersion);
    }

    [Fact]
    public void SetSteamBypassProfile_IsIdempotent_AndCountsAlreadyTargetAsSuccess()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass-idem");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        var before = File.ReadAllText(path);

        // 首次安装已经全关 → 无事可做，但算成功：前端不该弹"同步失败"
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));
        Assert.Equal(before, File.ReadAllText(path));            // 一字未改

        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        Assert.True(ModManager.IsSteamBypassActive(
            harness.Manager.ReadLoaderConfig(harness.GameDirectory)));
    }

    [Fact]
    public void SetTapTapBypassProfile_FlipsTheSet_AndKeepsComments()
    {
        using var harness = new ModHarness("ap-mod-set-taptap-bypass");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        Assert.True(harness.Manager.SetTapTapBypassProfile(harness.GameDirectory, true));
        var before = File.ReadAllText(path);
        Assert.Contains("\"tapTapBypassEnabled\": true", before);
        Assert.Contains("\"tapTapBypassRestartCheck\": true", before);
        var beforeConfig = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.True(ModManager.IsTapTapBypassActive(beforeConfig));

        Assert.True(harness.Manager.SetTapTapBypassProfile(harness.GameDirectory, false));
        var text = File.ReadAllText(path);
        Assert.Contains("\"tapTapBypassEnabled\": false", text);
        Assert.Contains("\"tapTapBypassRestartCheck\": false", text);
        Assert.Contains("TapTap 自杀门在 AOT 类", text);

        var afterConfig = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.False(ModManager.IsTapTapBypassActive(afterConfig));
    }

    [Fact]
    public void IsTapTapBypassActive_IsTrue_WhenAnyKeyIsOn()
    {
        Assert.True(ModManager.IsTapTapBypassActive(
            new LoaderConfig { TapTapBypassEnabled = true, TapTapBypassRestartCheck = false }));
        Assert.True(ModManager.IsTapTapBypassActive(
            new LoaderConfig { TapTapBypassEnabled = false, TapTapBypassRestartCheck = true }));
        Assert.False(ModManager.IsTapTapBypassActive(
            new LoaderConfig { TapTapBypassEnabled = false, TapTapBypassRestartCheck = false }));
    }

    [Fact]
    public void IsSteamBypassActive_IsTrue_WhenAnySingleKeyIsStillOn()
    {
        // 只关主开关、匹配绕过还开着 → 加载器照样不建真大厅，这种情况首页绝不能显示成「从 Steam 启动」
        Assert.True(ModManager.IsSteamBypassActive(
            new LoaderConfig { SteamBypassEnabled = false, SteamBypassMatchmaking = true, SteamBypassLobbyQuery = false }));
        Assert.True(ModManager.IsSteamBypassActive(
            new LoaderConfig { SteamBypassEnabled = false, SteamBypassMatchmaking = false, SteamBypassLobbyQuery = true }));
        Assert.False(ModManager.IsSteamBypassActive(
            new LoaderConfig { SteamBypassEnabled = false, SteamBypassMatchmaking = false, SteamBypassLobbyQuery = false }));
    }

    [Fact]
    public void SetSteamBypassProfile_NormalizesAPartiallyOnConfig()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass-hybrid");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 混合状态（用户启用绕过后只手动关了主开关）→ 一次「从 Steam 启动」必须把整套收拾干净
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        var text = File.ReadAllText(path)
            .Replace("\"steamBypassEnabled\": true", "\"steamBypassEnabled\": false");
        File.WriteAllText(path, text);
        Assert.True(ModManager.IsSteamBypassActive(harness.Manager.ReadLoaderConfig(harness.GameDirectory)));

        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));
        Assert.False(ModManager.IsSteamBypassActive(harness.Manager.ReadLoaderConfig(harness.GameDirectory)));
        var after = File.ReadAllText(path);
        Assert.Contains("\"steamBypassEnabled\": false", after);
        Assert.Contains("\"steamBypassMatchmaking\": false", after);
        Assert.Contains("\"steamBypassLobbyQuery\": false", after);
    }

    [Fact]
    public void SetSteamBypassProfile_ReturnsFalse_WhenConfigMissing_AndDoesNotCreateIt()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass-nofile");

        // 还没安装加载器：不能凭空造一个 doorstop_config.json 出来
        Assert.False(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        Assert.False(File.Exists(harness.Manager.LoaderConfigPath(harness.GameDirectory)));
    }

    [Fact]
    public void SetSteamBypassProfile_ReturnsFalse_WhenKeysMissing_AndLeavesFileUntouched()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass-nokey");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        var text = "{\n  // 老模板：没有 steamBypass 这一组键\n  \"enabled\": true\n}";
        File.WriteAllText(path, text);

        // 不添加键（加载器用编译进去的默认值，补齐是安装流程的活），也不许改坏文件
        Assert.False(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        Assert.Equal(text, File.ReadAllText(path));
    }

    [Fact]
    public void SetSteamBypassProfile_DoesNotRewriteCommentMentions()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass-comment");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 只有**注释里**提到这些键（真实键不存在）：就地替换会先命中注释 —— 自检必须拦住，不能写盘
        var text = "{\n  // 说明: \"steamBypassEnabled\": true / \"steamBypassMatchmaking\": true 才是绕过\n"
                 + "  \"enabled\": true\n}";
        File.WriteAllText(path, text);

        Assert.False(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));
        Assert.Equal(text, File.ReadAllText(path));
    }

    [Fact]
    public void SetSteamBypassProfile_PreservesUtf8Bom()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass-bom");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 用户在记事本里存过一遍 → 文件带 BOM；改写后 BOM 必须还在（加载器两侧都会剥 BOM，但别去动它）
        var text = File.ReadAllText(path);
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes(text)]);

        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));
        var after = File.ReadAllBytes(path);
        Assert.True(after.Length >= 3 && after[0] == 0xEF && after[1] == 0xBB && after[2] == 0xBF);
        Assert.False(ModManager.IsSteamBypassActive(
            harness.Manager.ReadLoaderConfig(harness.GameDirectory)));
    }

    // ============================== 老安装(没有清单)的版本回读 与 降级闸门 ==============================

    /// <summary>把安装清单里的版本改掉 —— 用来模拟"目录里装的是另一个版本"（老安装没有清单时改成删除）。</summary>
    private static void OverwriteManifestVersion(ModHarness harness, string version)
    {
        var manifestPath = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "cesium-loader.json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        node["version"] = version;
        File.WriteAllText(manifestPath, node.ToJsonString());
    }

    /// <summary>模拟玩家手工把加载器删干净：version.dll / version.pdb / AstralParty_ModLoader 全删。
    /// 注意 Toys 自己的安装历史（数据根 install-history）不会被玩家的手动删除清掉。</summary>
    private static void DeleteLoaderFiles(ModHarness harness)
    {
        foreach (var name in new[] { ModManager.LoaderDllName, "version.pdb" })
        {
            var path = Path.Combine(harness.GameDirectory, name);
            if (File.Exists(path)) File.Delete(path);
        }
        var root = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Install_AfterManualDeletion_IsNotBlockedAsDowngrade()
    {
        using var harness = new ModHarness("ap-mod-manual-delete-reinstall");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        // 先装一个更新版本的发布包：安装历史里留下 9.9.9
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.9.9"), overwriteDll: false);

        // 玩家把加载器整个删掉（游戏目录空了，但 Toys 的安装历史还在）
        DeleteLoaderFiles(harness);

        // 内置版本比历史里的 9.9.9 旧，可目录是空的 —— 这是重新安装，不是降级，不该拦
        var result = harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        Assert.Equal(ModManager.BundleLoaderVersion, result.Version);
        Assert.Equal(ModManager.BundleLoaderVersion, harness.Manager.GetStatus().InstalledVersion);
    }

    [Fact]
    public void InstallPackage_AfterManualDeletion_IsNotBlockedAsDowngrade()
    {
        using var harness = new ModHarness("ap-mod-pkg-manual-delete");
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.9.9"), overwriteDll: false);
        DeleteLoaderFiles(harness);

        var result = harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.1.0"), overwriteDll: false);
        Assert.Equal("9.1.0", result.Version);
    }

    [Fact]
    public void Install_AfterDisabledThenManualDeletion_WritesLoaderDllAgain()
    {
        using var harness = new ModHarness("ap-mod-disabled-then-deleted");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        // 用「停用加载器」停用（只删 version.dll，目录与历史都留着 Disabled=true）
        harness.Manager.DisableLoader(harness.GameDirectory);
        // 玩家随后把整个加载器目录也删了：目录都不在了就不再算「停用保留」，重装必须写回 version.dll
        DeleteLoaderFiles(harness);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderDllName)),
            "加载器目录被手工删掉后重装，必须重新写 version.dll");
    }

    [Fact]
    public void GetStatus_ReportsUnknownVersion_WhenManifestMissing()
    {
        using var harness = new ModHarness("ap-mod-version-unknown");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 模拟旧安装：既没有游戏清单，也没有本地安装历史。
        // 即使日志有版本行也不回读，缺少可靠记录时如实显示未知。
        Directory.Delete(harness.Sandbox.Combine("profile", "install-history"), recursive: true);
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        File.Delete(Path.Combine(loaderRoot, "cesium-loader.json"));
        Directory.CreateDirectory(Path.Combine(loaderRoot, "logs"));
        File.WriteAllText(Path.Combine(loaderRoot, "logs", "cesium-loader.log"),
            "  CesiumLoader loader v2.1.7 / SDK v2.1.7 — mod 加载报告\n");

        var status = harness.Manager.GetStatus();
        Assert.True(status.Installed);
        Assert.Equal("", status.InstalledVersion);
    }

    [Fact]
    public void GetStatus_ReadsInstalledVersionFromManifestOnly()
    {
        using var harness = new ModHarness("ap-mod-version-manifest-only");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 同时存在清单与日志（且日志是另一个版本）→ 仍然只认清单
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Directory.CreateDirectory(Path.Combine(loaderRoot, "logs"));
        File.WriteAllText(Path.Combine(loaderRoot, "logs", "cesium-loader.log"),
            "  CesiumLoader loader v1.0.0 / SDK v1.0.0 — mod 加载报告\n");

        Assert.Equal(ModManager.BundleLoaderVersion, harness.Manager.GetStatus().InstalledVersion);
    }

    [Fact]
    public void Install_RefusesDowngrade_UnlessExplicitlyAllowed()
    {
        using var harness = new ModHarness("ap-mod-downgrade-block");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        OverwriteManifestVersion(harness, "9.9.9");   // 假装已装了更新的加载器

        var ex = Assert.Throws<InvalidOperationException>(() =>
            harness.Manager.Install(harness.GameDirectory, overwriteDll: true));
        Assert.Contains("降级", ex.Message);
        Assert.Contains("9.9.9", ex.Message);

        // 显式放行才允许降级
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, allowDowngrade: true);
        Assert.Equal(ModManager.BundleLoaderVersion, harness.Manager.GetStatus().InstalledVersion);
    }

    [Fact]
    public void InstallPackage_RefusesDowngrade_UnlessExplicitlyAllowed()
    {
        using var harness = new ModHarness("ap-mod-pkg-downgrade-block");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        OverwriteManifestVersion(harness, "9.9.9");

        // 发布包 5.0.0 < 已装 9.9.9 → 降级，默认拒绝
        var ex = Assert.Throws<InvalidOperationException>(() =>
            harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("5.0.0"), overwriteDll: true));
        Assert.Contains("降级", ex.Message);

        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("5.0.0"),
            overwriteDll: true, allowDowngrade: true);
        Assert.Equal("5.0.0", harness.Manager.GetStatus().InstalledVersion);
    }

    [Fact]
    public void GetStatus_DoesNotSubstituteConfiguredSdkVersionForInstalledVersion()
    {
        using var harness = new ModHarness("ap-mod-version-config-ignored");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        Directory.Delete(harness.Sandbox.Combine("profile", "install-history"), recursive: true);
        // 老安装（没有清单和历史）+ 配置里手写着另一个 sdkVersion：
        // 它是**另一个字段**（用户还能手改），不能拿来当「已装版本」显示
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        File.Delete(Path.Combine(loaderRoot, "cesium-loader.json"));
        var configPath = Path.Combine(loaderRoot, "doorstop_config.json");
        File.WriteAllText(configPath, File.ReadAllText(configPath).Replace($"\"{ModManager.BundleSdkVersion}\"", "\"2.1.6\""));

        Assert.Equal("", harness.Manager.GetStatus().InstalledVersion);
    }

    [Fact]
    public void Install_WithoutManifest_DoesNotBlockOnUnknownInstalledVersion()
    {
        using var harness = new ModHarness("ap-mod-no-manifest-install");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        File.Delete(Path.Combine(loaderRoot, "cesium-loader.json"));   // 没有清单 → 版本未知
        var configPath = Path.Combine(loaderRoot, "doorstop_config.json");
        // 用户手改配置里的 sdkVersion（比如为了绕过 mod 的 SDK 校验）→ 不能因此把安装当成"降级"拦住
        File.WriteAllText(configPath, File.ReadAllText(configPath).Replace($"\"{ModManager.BundleSdkVersion}\"", "\"9.9.9\""));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.Equal(ModManager.BundleLoaderVersion, harness.Manager.GetStatus().InstalledVersion);   // 重装后清单回来了
    }

    // ============================== 加载器来源判定（本工具 vs 外来文件）==============================
    //
    // 这一组修的是一个真实误报：点「从 GitHub 更新」装的是**比本程序内置更新**的发布包，
    // 磁盘上的 version.dll 当然和内置的那份不同 —— 早先只比内置哈希，于是更新完界面一直显示
    // 「目录里是其它 version.dll」，小白用户看不懂，老用户以为更新失败了。

    [Fact]
    public void GetStatus_DistinguishesOwnUpdatedLoaderFromForeignDll()
    {
        using var harness = new ModHarness("ap-mod-origin");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        // (1) 陌生文件：既不是内置那份，也没有清单记录 → 才报「不是本工具装的」
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3, 4, 5]);
        var foreign = harness.Manager.GetStatus();
        Assert.True(foreign.Installed);
        Assert.False(foreign.LoaderManaged);
        Assert.Equal("", foreign.LoaderOrigin);
        Assert.Contains("不是本工具装的", foreign.Message);

        // (2) 本工具更新过的加载器（发布包的 version.dll 与内置不同，但清单记着它的哈希）→ 不算冲突
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.0.0"), overwriteDll: true);
        var updated = harness.Manager.GetStatus();
        Assert.True(updated.Installed);
        Assert.False(updated.LoaderMatchesBundle, "发布包的 version.dll 与内置的不同");
        Assert.True(updated.LoaderManaged, "更新过的加载器是本工具装的，不能报成外来文件");
        Assert.Equal("installed", updated.LoaderOrigin);
        Assert.DoesNotContain("不是本工具装的", updated.Message);
    }

    [Fact]
    public void Uninstall_RemovesOwnUpdatedLoader_WithoutForce()
    {
        using var harness = new ModHarness("ap-mod-pkg-uninstall-noForce");
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("9.0.0"), overwriteDll: false);

        // 卸载保护只该拦"外来文件"；这个是我们自己装的，不需要「强制卸载」
        var result = harness.Manager.Uninstall(harness.GameDirectory, force: false);
        Assert.True(result.RemovedDll);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
    }

    // ============================== 加载器配置 / 权限 / 配置表单 ==============================

    [Fact]
    public void ReadLoaderConfig_ParsesCommentedFile()
    {
        using var harness = new ModHarness("ap-mod-cfg-read");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 写一份带注释的 doorstop_config.json（加载器真实格式）
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path,
            "{\n  // 这是注释\n  \"enabled\": true,\n  \"speedhackBaseSpeed\": 2.5,\n" +
            "  /* 块注释 */\n  \"consoleEnabled\": false,\n  \"sdkVersion\": \"2.0.0\"\n}\n");

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.True(config.Enabled);
        Assert.Equal(2.5, config.SpeedhackBaseSpeed);
        Assert.False(config.ConsoleEnabled);
        Assert.Equal("2.0.0", config.SdkVersion);
        Assert.Equal(60, config.GameAssemblyTimeoutSec); // 缺省值保留
    }

    [Fact]
    public void SaveLoaderConfig_RoundTripsAndValidatesSpeed()
    {
        using var harness = new ModHarness("ap-mod-cfg-save");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var config = new LoaderConfig { SpeedhackBaseSpeed = 3.0, ConsoleTopmost = false, SpeedControlEnabled = false, DebugShell = false, TapTapBypassEnabled = true };
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        var read = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(3.0, read.SpeedhackBaseSpeed);
        Assert.False(read.ConsoleTopmost);
        Assert.True(read.TapTapBypassEnabled);
        // Debug Shell 开关必须能往返(老配置没有该键时默认开)
        Assert.False(read.DebugShell);
        // 变速控制文件通道开关必须能往返(老配置没有该键时默认开)
        Assert.False(read.SpeedControlEnabled);

        // 非法倍速拒绝
        config.SpeedhackBaseSpeed = 0;
        Assert.Throws<InvalidOperationException>(() => harness.Manager.SaveLoaderConfig(harness.GameDirectory, config));
        config.SpeedhackBaseSpeed = 101;
        Assert.Throws<InvalidOperationException>(() => harness.Manager.SaveLoaderConfig(harness.GameDirectory, config));
    }

    [Fact]
    public void LoaderConfig_DeserializesFromCamelCaseWebMessage()
    {
        // 模拟 Web 前端发的 camelCase JSON（HybridWindow 用 WebReadOptions 反序列化）
        // 关键: 大小写不敏感映射, 否则 consoleTopmost=false 无法映射到 ConsoleTopmost
        var json = "{\"enabled\":true,\"consoleTopmost\":false,\"speedhackBaseSpeed\":2.5,\"gameAssemblyTimeoutSec\":90}";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = System.Text.Json.JsonSerializer.Deserialize<LoaderConfig>(json, options)!;

        Assert.True(config.Enabled);
        Assert.False(config.ConsoleTopmost);
        Assert.Equal(2.5, config.SpeedhackBaseSpeed);
        Assert.Equal(90, config.GameAssemblyTimeoutSec);
        // 老配置(升级上来的 doorstop_config.json)没有 speedControlEnabled / debugShell -> 默认开
        Assert.True(config.SpeedControlEnabled);
        Assert.True(config.DebugShell);

        // 无该选项时 camelCase 无法映射(回归保护)
        var strict = System.Text.Json.JsonSerializer.Deserialize<LoaderConfig>(json)!;
        Assert.True(strict.ConsoleTopmost, "大小写敏感时 consoleTopmost 应保持默认 true(证明测试有效)");
    }

    [Fact]
    public void ModConfigFields_DeserializesFromCamelCaseWebMessage()
    {
        var json = "[{\"name\":\"Enabled\",\"kind\":\"bool\",\"boolValue\":false},{\"name\":\"BaseSpeed\",\"kind\":\"number\",\"numberValue\":1.5}]";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var fields = System.Text.Json.JsonSerializer.Deserialize<List<ModConfigField>>(json, options)!;

        Assert.Equal(2, fields.Count);
        Assert.Equal("Enabled", fields[0].Name);
        Assert.Equal("bool", fields[0].Kind);
        Assert.False(fields[0].BoolValue);
        Assert.Equal("BaseSpeed", fields[1].Name);
        Assert.Equal(1.5, fields[1].NumberValue);
    }

    [Fact]
    public void ModEntryInfo_ExposesPermissionsForWarning()
    {
        using var harness = new ModHarness("ap-mod-perm-warn");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);

        // 声明「操作游戏」(GameActions=2) 的 mod
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "ActionMod.dll"), new byte[] { 0x4D, 0x5A });
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "ActionMod.json"),
            "{\"id\":\"ActionMod\",\"name\":\"操作器\",\"version\":\"1.0.0\",\"permissions\":2,\"enabled\":true}");

        // 声明只读(ReadGameState=1) 的 mod
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "ReadMod.dll"), new byte[] { 0x4D, 0x5A });
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "ReadMod.json"),
            "{\"id\":\"ReadMod\",\"name\":\"观察器\",\"version\":\"1.0.0\",\"permissions\":1,\"enabled\":true}");

        var mods = harness.Manager.GetStatus().Mods;
        var action = mods.Single(m => m.FileName == "ActionMod.dll");
        var read = mods.Single(m => m.FileName == "ReadMod.dll");

        // 权限位暴露给前端, 用于「⚠️ 可操作游戏」警告(仅提示, 不阻止加载)
        Assert.Equal(2, action.Permissions);
        Assert.Equal(1, read.Permissions);
    }

    [Fact]
    public void ModEntryInfo_DefaultPermissionsZero_WhenSidecarMissing()
    {
        using var harness = new ModHarness("ap-mod-perm-zero");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);

        // 无 sidecar 的 mod → permissions 为 0(无声明, 不警告)
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "NoMeta.dll"), new byte[] { 0x4D, 0x5A });

        var mod = harness.Manager.GetStatus().Mods.Single(m => m.FileName == "NoMeta.dll");
        Assert.Equal(0, mod.Permissions);
    }

    [Fact]
    public void ModConfigFields_RoundTripByKind()
    {
        using var harness = new ModHarness("ap-mod-fields");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "FieldMod.dll"), new byte[] { 0x4D, 0x5A });

        // 初始无配置 → 空字段
        Assert.Empty(harness.Manager.ReadModConfigFields(harness.GameDirectory, "FieldMod.dll"));

        // 保存混合类型字段
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "FieldMod.dll", new List<ModConfigField>
        {
            new() { Name = "Enabled", Kind = "bool", BoolValue = true },
            new() { Name = "BaseSpeed", Kind = "number", NumberValue = 1.5 },
            new() { Name = "SpeedUpKey", Kind = "string", StringValue = "F1" }
        });

        var fields = harness.Manager.ReadModConfigFields(harness.GameDirectory, "FieldMod.dll");
        Assert.Equal(3, fields.Count);
        Assert.Contains(fields, f => f.Name == "Enabled" && f.Kind == "bool" && f.BoolValue);
        Assert.Contains(fields, f => f.Name == "BaseSpeed" && f.Kind == "number" && f.NumberValue == 1.5);
        // SpeedUpKey 以 Key 结尾 → 读回来是 kind=key(UI 用"按键捕获"控件而不是普通文本框)
        Assert.Contains(fields, f => f.Name == "SpeedUpKey" && f.Kind == "key" && f.StringValue == "F1");

        // 改布尔值后回读
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "FieldMod.dll", new List<ModConfigField>
        {
            new() { Name = "Enabled", Kind = "bool", BoolValue = false }
        });
        var readBack = harness.Manager.ReadModConfigFields(harness.GameDirectory, "FieldMod.dll");
        var enabled = Assert.Single(readBack);
        Assert.False(enabled.BoolValue);
    }

    [Fact]
    public void ReadModConfigFields_MarksKeyBindingsForCaptureUi()
    {
        using var harness = new ModHarness("ap-mod-keykind");
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "KeyMod.dll"), new byte[] { 0x4D, 0x5A });

        harness.Manager.SaveModConfigFields(harness.GameDirectory, "KeyMod.dll", new List<ModConfigField>
        {
            new() { Name = "toggleKey", Kind = "string", StringValue = "Mouse3" },   // 鼠标侧键
            new() { Name = "speedUpKey", Kind = "string", StringValue = "Equals" },
            new() { Name = "note", Kind = "string", StringValue = "随便写点啥" },
            new() { Name = "key", Kind = "string", StringValue = "名字太短, 不当键位" }
        });

        var fields = harness.Manager.ReadModConfigFields(harness.GameDirectory, "KeyMod.dll");
        Assert.Contains(fields, f => f.Name == "toggleKey" && f.Kind == "key" && f.StringValue == "Mouse3");
        Assert.Contains(fields, f => f.Name == "speedUpKey" && f.Kind == "key" && f.StringValue == "Equals");
        // 不以 Key 结尾的字符串照旧是普通文本
        Assert.Contains(fields, f => f.Name == "note" && f.Kind == "string" && f.StringValue == "随便写点啥");
        Assert.Contains(fields, f => f.Name == "key" && f.Kind == "string");
    }

    [Fact]
    public void SaveModConfigFields_WritesKeyKindAsPlainString()
    {
        using var harness = new ModHarness("ap-mod-keysave");
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "KeySaveMod.dll"), new byte[] { 0x4D, 0x5A });

        // UI 采集到的键位最终和手写 JSON 完全一样(就是普通字符串), kind=key 直接存也要能落盘
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "KeySaveMod.dll", new List<ModConfigField>
        {
            new() { Name = "toggleKey", Kind = "key", StringValue = "Mouse4" }
        });

        var json = File.ReadAllText(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "configs", "KeySaveMod.json"));
        Assert.Contains("\"toggleKey\": \"Mouse4\"", json);

        var field = Assert.Single(harness.Manager.ReadModConfigFields(harness.GameDirectory, "KeySaveMod.dll"));
        Assert.Equal("toggleKey", field.Name);
        Assert.Equal("key", field.Kind);
        Assert.Equal("Mouse4", field.StringValue);
    }

    [Fact]
    public void ReadModConfigFields_ResolvesCommentsByLanguageAndFallback()
    {
        using var harness = new ModHarness("ap-mod-comments");
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        var configDir = Path.Combine(modsDir, "DocMod");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.json"), """
            {
              "Enabled": true,
              "cooldownSeconds": 2,
              "speed": 1.5,
              "note": "hello",
              "plain": 1,
              "_comments": {
                "Enabled": { "zh-Hans": "启用", "zh-Hant": "啟用", "en": "Enable", "ja": "有効" },
                "cooldownSeconds": { "en": "Cooldown in seconds" },
                "speed": "Common note",
                "note": { "zh-TW": "繁體說明" }
              }
            }
            """);

        string Comment(string language, string name) =>
            harness.Manager.ReadModConfigFields(harness.GameDirectory, "DocMod.dll", language)
                .Single(f => f.Name == name).Comment;

        Assert.Equal("启用", Comment("zh-Hans", "Enabled"));
        Assert.Equal("啟用", Comment("zh-Hant", "Enabled"));
        Assert.Equal("Enable", Comment("en", "Enabled"));
        Assert.Equal("有効", Comment("ja", "Enabled"));
        // 当前语言没有时: ja -> zh-Hans -> en
        Assert.Equal("Cooldown in seconds", Comment("ja", "cooldownSeconds"));
        // 字符串简写对所有语言都生效
        Assert.Equal("Common note", Comment("ja", "speed"));
        Assert.Equal("Common note", Comment("en", "speed"));
        // 语言别名
        Assert.Equal("繁體說明", Comment("zh-Hant", "note"));
        // 没有说明的字段保持空串
        Assert.Equal("", Comment("zh-Hans", "plain"));

        // _comments 不进入可编辑字段
        var fields = harness.Manager.ReadModConfigFields(harness.GameDirectory, "DocMod.dll", "zh-Hans");
        Assert.DoesNotContain(fields, f => f.Name == "_comments");
        Assert.Equal(5, fields.Count);
    }

    [Fact]
    public void SaveModConfigFields_PreservesCommentsAndNestedOtherValues()
    {
        using var harness = new ModHarness("ap-mod-comments-save");
        var configDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "configs");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "DocMod.json");
        File.WriteAllText(configPath, """
            {
              "Enabled": true,
              "nested": { "a": 1, "b": [true, "x"] },
              "_comments": {
                "Enabled": { "zh-Hans": "启用", "en": "Enable" }
              }
            }
            """);

        var fields = harness.Manager.ReadModConfigFields(harness.GameDirectory, "DocMod.dll", "zh-Hans");
        var enabled = fields.Single(f => f.Name == "Enabled");
        enabled.BoolValue = false;

        // 模拟前端误带一个 _comments 字段：后台必须忽略，不能覆盖原说明。
        fields.Add(new ModConfigField { Name = "_comments", Kind = "string", StringValue = "oops" });
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "DocMod.dll", fields);

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
        var root = doc.RootElement;
        Assert.False(root.GetProperty("Enabled").GetBoolean());
        Assert.Equal(1, root.GetProperty("nested").GetProperty("a").GetInt32());
        Assert.True(root.GetProperty("nested").GetProperty("b")[0].GetBoolean());
        Assert.Equal("x", root.GetProperty("nested").GetProperty("b")[1].GetString());

        var comments = root.GetProperty("_comments");
        Assert.Equal("启用", comments.GetProperty("Enabled").GetProperty("zh-Hans").GetString());
        Assert.Equal("Enable", comments.GetProperty("Enabled").GetProperty("en").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Object, comments.ValueKind);
    }

    [Fact]
    public void SaveModConfigFields_RejectsInvalidOtherJson()
    {
        using var harness = new ModHarness("ap-mod-other-json");
        var configDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, "configs");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "BadMod.json");
        File.WriteAllText(configPath, "{\"bad\":{\"a\":1}}");

        var fields = harness.Manager.ReadModConfigFields(harness.GameDirectory, "BadMod.dll");
        fields.Single(f => f.Name == "bad").StringValue = "{not json";

        Assert.Throws<InvalidOperationException>(() =>
            harness.Manager.SaveModConfigFields(harness.GameDirectory, "BadMod.dll", fields));
    }

    // ============================== 联网更新：包解析 / 校验 / 安装 ==============================

    /// <summary>构造一个符合发布布局的内存 zip（version.dll Doorstop 式，含清单，可带/不带哈希校验）。</summary>
    private static byte[] BuildFakePackage(string version, bool includeHashes = true, string? configSdkVersion = null, bool includeSymbols = false, bool includeOptionalMod = false, IEnumerable<(string Name, byte[] Data)>? extraEntries = null)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, byte[] content)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }

            var loader = new byte[] { 0x4D, 0x5A, 0x01, 0x02, 0x03 }; // 假 version.dll
            // 假 doorstop_config.json: 默认 "{}"(老包不带 sdkVersion), 也可指定包内声明的 SDK 版本
            var config = configSdkVersion is null
                ? new byte[] { 0x7B, 0x7D }
                : System.Text.Encoding.UTF8.GetBytes("{\"sdkVersion\":\"" + configSdkVersion + "\"}");
            var sdk = new byte[] { 0x53, 0x44, 0x4B, 0x01 };          // 假 SDK
            var mod = new byte[] { 0x4D, 0x4F, 0x44, 0x01 };          // 假 mod
            var sidecar = new byte[] { 0x7B, 0x7D };                  // 假 sidecar "{}"

            var entries = new List<(string Name, byte[] Data)>
            {
                ("version.dll", loader),
                ("AstralParty_ModLoader/doorstop_config.json", config),
                ("AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll", sdk),
                ("AstralParty_ModLoader/mods/ActivityLogMod/ActivityLogMod.dll", mod),
                ("AstralParty_ModLoader/mods/ActivityLogMod/ActivityLogMod.json", sidecar),
            };
            if (includeOptionalMod)
            {
                entries.Add(("AstralParty_ModLoader/mods/OptionalMod/OptionalMod.dll", mod));
                entries.Add(("AstralParty_ModLoader/mods/OptionalMod/OptionalMod.json", sidecar));
                entries.Add(("AstralParty_ModLoader/mods/OptionalMod/OptionalMod.pdb", mod));
            }
            if (includeSymbols)
            {
                // 与 dll 成对的 .pdb 符号文件 + bootstrap\（模拟 2.2.5 起带符号的真实发布包）
                entries.Add(("version.pdb", new byte[] { 0x50, 0x44, 0x42, 0x01 }));
                entries.Add(("AstralParty_ModLoader/sdk/CesiumLoader.SDK.pdb", new byte[] { 0x50, 0x53, 0x01 }));
                entries.Add(("AstralParty_ModLoader/bootstrap/CesiumLoader.Bootstrap.dll", new byte[] { 0x42, 0x54, 0x01 }));
                entries.Add(("AstralParty_ModLoader/bootstrap/CesiumLoader.Bootstrap.pdb", new byte[] { 0x42, 0x50, 0x01 }));
            }
            if (extraEntries is not null)
            {
                entries.AddRange(extraEntries);
            }
            foreach (var (name, data) in entries) Add(name, data);

            string Sha(byte[] data) => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();

            var manifest = new System.Text.StringBuilder();
            manifest.Append("{\"name\":\"CesiumLoader\",\"version\":\"").Append(version).Append("\",");
            manifest.Append("\"files\":{");
            if (includeHashes)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (i > 0) manifest.Append(',');
                    manifest.Append('"').Append(entries[i].Name).Append("\":\"").Append(Sha(entries[i].Data)).Append('"');
                }
            }
            manifest.Append("}}");
            Add(ModManager.InstalledManifestFileName, System.Text.Encoding.UTF8.GetBytes(manifest.ToString()));
        }
        return memory.ToArray();
    }

    [Fact]
    public void ParsePackageManifest_ReadsVersionAndFiles()
    {
        var bytes = BuildFakePackage("1.2.3");
        var info = ModManager.ParsePackageManifest(bytes);

        Assert.NotNull(info);
        Assert.Equal("1.2.3", info!.Version);
        Assert.Equal(5, info.Files.Count);
        Assert.Contains("version.dll", info.Files.Keys);
    }

    [Fact]
    public void InstallPackage_CopiesContentsAndWritesInstalledManifest()
    {
        using var harness = new ModHarness("ap-mod-pkg-install");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("2.0.0");
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: false);

        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ConfigFileName)));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, ModManager.SdkDllName)));
        // 新布局: 示例 mod 在 mods\ActivityLogMod\ 文件夹里
        var sampleDir = Path.Combine(loaderRoot, ModManager.ModsFolderName, "ActivityLogMod");
        Assert.True(File.Exists(Path.Combine(sampleDir, ModManager.SampleModDllName)));
        Assert.True(File.Exists(Path.Combine(sampleDir, ModManager.SampleModDllName.Replace(".dll", ".json"))));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.InstalledManifestFileName)));

        // 已装版本被 GetStatus 读出
        var status = harness.Manager.GetStatus();
        Assert.Equal("2.0.0", status.InstalledVersion);
    }

    [Fact]
    public void InstallPackage_InstallsEveryModInPackage()
    {
        using var harness = new ModHarness("ap-mod-package-all");
        harness.Manager.InstallPackage(harness.GameDirectory,
            BuildFakePackage("9.0.0", includeSymbols: true, includeOptionalMod: true), overwriteDll: false);

        var modsRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        Assert.True(File.Exists(Path.Combine(modsRoot, "ActivityLogMod", "ActivityLogMod.dll")));
        // 发布包里有什么就装什么: 不在内置清单里的 OptionalMod 也要落地。
        Assert.True(File.Exists(Path.Combine(modsRoot, "OptionalMod", "OptionalMod.dll")));
        Assert.True(File.Exists(Path.Combine(modsRoot, "OptionalMod", "OptionalMod.json")));
        Assert.True(File.Exists(Path.Combine(modsRoot, "OptionalMod", "OptionalMod.pdb")));
    }

    [Fact]
    public void InstallPackage_RejectsTamperedFile()
    {
        using var harness = new ModHarness("ap-mod-pkg-tamper");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("3.0.0");
        // 篡改 version.dll 内容（保持清单里的哈希不变 → 校验应失败）
        var tampered = TamperZipEntry(bytes, "version.dll");

        Assert.Throws<InvalidDataException>(
            () => harness.Manager.InstallPackage(harness.GameDirectory, tampered, overwriteDll: false));
    }

    [Fact]
    public void InstallPackage_WithoutHashes_StillInstalls()
    {
        using var harness = new ModHarness("ap-mod-pkg-nohash");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("4.0.0", includeHashes: false);
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: false);

        var status = harness.Manager.GetStatus();
        Assert.Equal("4.0.0", status.InstalledVersion);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
    }

    [Fact]
    public void InstallPackage_AlsoDeploysSiblingPdbAndBootstrap()
    {
        using var harness = new ModHarness("ap-mod-pkg-symbols");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("2.5.0", includeSymbols: true);
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: false);

        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        // version.dll 的符号文件落在游戏 exe 同目录（与 dll 成对）
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.pdb")));
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        // SDK 的符号文件随 SDK dll 一起落盘
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, "CesiumLoader.SDK.pdb")));
        // bootstrap\ 整套（dll + pdb）
        Assert.True(File.Exists(Path.Combine(loaderRoot, "bootstrap", "CesiumLoader.Bootstrap.dll")));
        Assert.True(File.Exists(Path.Combine(loaderRoot, "bootstrap", "CesiumLoader.Bootstrap.pdb")));
    }

    [Fact]
    public void InstallPackage_WithoutSymbols_SkipsPdbSilently()
    {
        using var harness = new ModHarness("ap-mod-pkg-noSymbols");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        // 老发布包没有 .pdb / bootstrap\ 目录：安装照常成功，不该因此抛错
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("2.5.0"), overwriteDll: false);

        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.pdb")));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, ModManager.SdkDllName)));
    }

    [Fact]
    public void ImportReleaseFile_LoaderZipInstallsWholePackageWithoutExistingLoader()
    {
        using var harness = new ModHarness("ap-local-loader");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        var zip = Path.Combine(harness.GameDirectory, "cesium-loader.zip");
        File.WriteAllBytes(zip, BuildFakePackage("9.0.0", includeSymbols: true));
        File.WriteAllText(Path.Combine(harness.GameDirectory, "speedhack_config.json"), "legacy");
        Assert.Contains("9.0.0", harness.Manager.ImportReleaseFile(harness.GameDirectory, zip, overwriteDll: false));
        var root = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Assert.Equal("9.0.0", harness.Manager.GetStatus().InstalledVersion);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.pdb")));
        Assert.True(File.Exists(Path.Combine(root, "sdk", ModManager.SdkDllName)));
        Assert.True(File.Exists(Path.Combine(root, "bootstrap", "CesiumLoader.Bootstrap.dll")));
        Assert.NotEmpty(harness.Manager.GetStatus().Mods);
        Assert.Equal("legacy", File.ReadAllText(Path.Combine(harness.GameDirectory, "speedhack_config.json")));
    }

    [Fact]
    public void ImportReleaseFile_InvalidPathsWriteNothing()
    {
        using var harness = new ModHarness("ap-local-loader-bad");
        var zip = Path.Combine(harness.GameDirectory, "loader.zip");
        File.WriteAllBytes(zip, BuildFakePackage("9.0.0"));
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) archive.CreateEntry("../escape.dll");
        Assert.Throws<InvalidOperationException>(() => harness.Manager.ImportReleaseFile(harness.GameDirectory, zip, overwriteDll: true));
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        Assert.False(Directory.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName)));
    }

    [Fact]
    public void Install_PreservesModEnabledState_AcrossReinstalls()
    {
        using var harness = new ModHarness("ap-mod-install-preserve-enabled");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 默认情况下所有内置 mod 均处于启用状态
        var status = harness.Manager.GetStatus();
        var activityMod = status.Mods.FirstOrDefault(m => m.Name == "ActivityLogMod");
        Assert.NotNull(activityMod);
        Assert.True(activityMod.Enabled);

        // 用户在界面上关闭 ActivityLogMod
        harness.Manager.ToggleMod(harness.GameDirectory, "ActivityLogMod.dll", false);
        status = harness.Manager.GetStatus();
        Assert.False(status.Mods.First(m => m.Name == "ActivityLogMod").Enabled);

        // 重新安装/更新内置加载器（模拟覆盖更新）
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);

        // 验证：已禁用的 mod 状态得到保留，未改动的 mod 依然保持启用
        status = harness.Manager.GetStatus();
        Assert.False(status.Mods.First(m => m.Name == "ActivityLogMod").Enabled, "更新内置加载器后已禁用的 mod 被重置为开启");
        Assert.True(status.Mods.First(m => m.Name == "SpeedHackMod").Enabled, "其余正常 mod 状态异常");

        // 重新开启后再更新，依然保持开启
        harness.Manager.ToggleMod(harness.GameDirectory, "ActivityLogMod.dll", true);
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        status = harness.Manager.GetStatus();
        Assert.True(status.Mods.First(m => m.Name == "ActivityLogMod").Enabled);
    }

    [Fact]
    public void InstallPackage_PreservesModEnabledState()
    {
        using var harness = new ModHarness("ap-mod-pkg-preserve-enabled");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 用户禁用 ActivityLogMod
        harness.Manager.ToggleMod(harness.GameDirectory, "ActivityLogMod.dll", false);
        Assert.False(harness.Manager.GetStatus().Mods.First(m => m.Name == "ActivityLogMod").Enabled);

        // 从发布包更新（包内包含 ActivityLogMod 但没有 enabled 字段即默认启用，且带可选的新 mod）
        var packageBytes = BuildFakePackage("9.0.0", includeSymbols: true, includeOptionalMod: true);
        harness.Manager.InstallPackage(harness.GameDirectory, packageBytes, overwriteDll: true);

        // 验证：更新加载器后，已禁用的 mod 依然保持禁用，新增加的 mod 默认启用
        var status = harness.Manager.GetStatus();
        Assert.Equal("9.0.0", status.InstalledVersion);
        Assert.False(status.Mods.First(m => m.Name == "ActivityLogMod").Enabled, "发布包更新重置了 mod 开关");
        Assert.True(status.Mods.First(m => m.Name == "OptionalMod").Enabled, "包内新 mod 应该默认启用");
    }

    [Fact]
    public void InstallPackage_PreservesExistingModConfigFile()
    {
        using var harness = new ModHarness("ap-mod-pkg-preserve-config");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        // 用户给 ActivityLogMod 写入自定义配置
        var modDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName, "ActivityLogMod");
        var configPath = Path.Combine(modDir, "config.json");
        File.WriteAllText(configPath, "{\"userCustom\": 12345}");

        // 发布包内带了一份默认 config.json（{"default": 0}）
        var extra = new[]
        {
            ("AstralParty_ModLoader/mods/ActivityLogMod/config.json", System.Text.Encoding.UTF8.GetBytes("{\"default\": 0}"))
        };
        var packageBytes = BuildFakePackage("9.0.0", extraEntries: extra);
        harness.Manager.InstallPackage(harness.GameDirectory, packageBytes, overwriteDll: true);

        // 验证：用户的 config.json 未被覆盖
        Assert.True(File.Exists(configPath));
        Assert.Equal("{\"userCustom\": 12345}", File.ReadAllText(configPath));
    }

    /// <summary>重建 zip 并把指定条目内容换成别的字节（清单哈希不变 → 校验必失败）。</summary>
    private static byte[] TamperZipEntry(byte[] zipBytes, string entryName)
    {
        using var input = new MemoryStream(zipBytes);
        using (var archive = new ZipArchive(input, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(entryName);
            Assert.NotNull(entry);
            using var stream = entry.Open();
            stream.SetLength(0);
            stream.Write(new byte[] { 0x00, 0x01, 0x02 }, 0, 3);
        }
        input.Position = 0;
        return input.ToArray();
    }

    private sealed class ModHarness : IDisposable
    {
        public ModHarness(string sandboxName)
        {
            Sandbox = new TestSandbox(sandboxName);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            Manager = new ModManager(TestPaths.AppDirectory, Sandbox.EnsureDirectory("profile"));
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public ModManager Manager { get; }

        /// <summary>装好加载器但清空 mods\，得到「只有加载器」的干净环境。
        /// 内置包现在一律连内置模组一起装（不再有「只装加载器」的选项），所以需要隔离环境的用例
        /// 自己把 mods\ 清掉，而不是让产品保留一个只为测试存在的开关。</summary>
        public void InstallLoaderOnly()
        {
            Manager.Install(GameDirectory, overwriteDll: false);
            var modsRoot = Path.Combine(GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
            if (Directory.Exists(modsRoot)) Directory.Delete(modsRoot, recursive: true);
            Directory.CreateDirectory(modsRoot);
        }

        public void Dispose() => Sandbox.Dispose();
    }
}
