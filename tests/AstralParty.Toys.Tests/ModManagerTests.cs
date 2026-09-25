using System.IO.Compression;
using AstralParty.Toys.Services;
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
    public void EmbeddedResources_AreComplete()
    {
        Assert.True(ModManager.HasEmbeddedLoader, "内置 version.dll 资源缺失");
        Assert.True(ModManager.HasEmbeddedConfig, "内置 doorstop_config.json 资源缺失");
        Assert.True(ModManager.HasEmbeddedSdk, "内置 SDK 资源缺失");
        Assert.True(ModManager.HasEmbeddedSampleMod, "内置示例 mod 资源缺失");
        // 内置 mod 一个都不能少: 资源缺失会在 ReadBuiltInMods 里被静默跳过, 所以这里按列表逐一对齐
        Assert.True(ModManager.HasEmbeddedBuiltInMods, "内置 mod 资源缺失");
        Assert.Equal(ModManager.BuiltInModIds, ModManager.EmbeddedBuiltInModIdList);
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

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);

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
        Assert.Equal(ModManager.BuiltInModIds.Count, status.Mods.Count); // 行为日志 + 自由相机 + 变速热键 mod
        Assert.Contains(status.Mods, m => m.FileName == ModManager.SampleModDllName);
        Assert.Contains(status.Mods, m => m.FileName == "FreeCameraMod.dll");
        Assert.Contains(status.Mods, m => m.FileName == "SpeedHackMod.dll");
    }

    [Fact]
    public void Install_WithoutSample_LeavesModsEmpty()
    {
        using var harness = new ModHarness("ap-mod-install-nosample");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var status = harness.Manager.GetStatus();
        Assert.Empty(status.Mods);
        Assert.Single(status.Sdk); // SDK 始终安装
        Assert.True(File.Exists(Path.Combine(
            harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName)), "doorstop_config.json 始终安装");
    }

    [Fact]
    public void Install_RefusesToOverwriteForeignDll_UnlessForced()
    {
        using var harness = new ModHarness("ap-mod-overwrite");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<InvalidOperationException>(
            () => harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: true);
        Assert.True(harness.Manager.GetStatus().LoaderMatchesBundle, "覆盖安装后哈希应一致");
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);
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
    public void ImportMod_CopiesIntoModsAndAppearsInList()
    {
        using var harness = new ModHarness("ap-mod-import");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);
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
            () => harness.Manager.Install(harness.Sandbox.EnsureDirectory("empty"), overwriteDll: false, includeSampleMod: true));
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        // 放一个 mod + sidecar
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "MyMod.dll"), new byte[] { 0x4D, 0x5A });
        var sidecarPath = Path.Combine(modsDir, "MyMod.json");
        File.WriteAllText(sidecarPath, "{\"id\":\"MyMod\",\"name\":\"我的Mod\",\"version\":\"1.0.0\"}");

        Assert.True(harness.Manager.GetStatus().Mods[0].Enabled, "初始 enabled=true");

        harness.Manager.ToggleMod(harness.GameDirectory, "MyMod.dll", enabled: false);
        Assert.False(harness.Manager.GetStatus().Mods[0].Enabled, "禁用后 enabled=false");
        Assert.Contains("\"enabled\": false", File.ReadAllText(sidecarPath));

        harness.Manager.ToggleMod(harness.GameDirectory, "MyMod.dll", enabled: true);
        Assert.True(harness.Manager.GetStatus().Mods[0].Enabled, "重新启用后 enabled=true");
    }

    [Fact]
    public void ToggleMod_WithoutSidecar_CreatesOne()
    {
        using var harness = new ModHarness("ap-mod-toggle-nosidecar");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "Legacy.dll"), new byte[] { 0x4D, 0x5A });

        harness.Manager.ToggleMod(harness.GameDirectory, "Legacy.dll", enabled: false);

        var sidecarPath = Path.Combine(modsDir, "Legacy.json");
        Assert.True(File.Exists(sidecarPath), "无 sidecar 的 mod 禁用时应自动生成 sidecar");
        var json = File.ReadAllText(sidecarPath);
        Assert.Contains("\"id\":\"Legacy\"", json);
        Assert.Contains("\"enabled\": false", json);
        Assert.False(harness.Manager.GetStatus().Mods[0].Enabled);
    }

    [Fact]
    public void ModConfig_OpenCreatesFileAndReturnsPath()
    {
        using var harness = new ModHarness("ap-mod-config");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        harness.Sandbox.WriteFile(Path.Combine(modsDir, "CfgMod.dll"), new byte[] { 0x4D, 0x5A });

        // 无配置 → Open 创建并返回路径(配置必须与 mod 的 dll 同目录, 否则 mod 读不到)
        var path = harness.Manager.OpenModConfig(harness.GameDirectory, "CfgMod.dll");
        Assert.True(File.Exists(path), "OpenModConfig 应创建配置文件");
        Assert.EndsWith(Path.Combine("mods", "CfgMod", "config.json"), path);

        // 写内容后 Read 能读回
        File.WriteAllText(path, "{\"LogUi\": true}");
        Assert.Equal("{\"LogUi\": true}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));
    }

    [Fact]
    public void ModConfig_LivesBesideModDll_AndMigratesLegacyConfigsFolder()
    {
        using var harness = new ModHarness("ap-mod-config-path");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        var modDir = Path.Combine(loaderRoot, ModManager.ModsFolderName, "CfgMod");
        harness.Sandbox.WriteFile(Path.Combine(modDir, "CfgMod.dll"), new byte[] { 0x4D, 0x5A });

        // 现行约定: mods\{ModId}\config.json —— SDK 的 ModConfig 就是按 DLL 同目录落盘的
        var path = harness.Manager.OpenModConfig(harness.GameDirectory, "CfgMod.dll");
        Assert.Equal(Path.Combine(modDir, "config.json"), path);

        harness.Manager.SaveModConfigFields(harness.GameDirectory, "CfgMod.dll", new List<ModConfigField>
        {
            new() { Name = "LogUi", Kind = "bool", BoolValue = true }
        });
        Assert.Contains("\"LogUi\": true", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));
        Assert.Equal(Path.Combine(modDir, "config.json"),
            harness.Manager.ModConfigPath(harness.GameDirectory, "CfgMod.dll"));

        // 旧版集中存放(AstralParty_ModLoader\configs\{mod}.json)仍能读到, 打开时自动迁到 mod 目录
        File.Delete(Path.Combine(modDir, "config.json"));
        var legacy = Path.Combine(loaderRoot, "configs", "CfgMod.json");
        harness.Sandbox.WriteFile(legacy, System.Text.Encoding.UTF8.GetBytes("{\"LegacyKey\": 7}"));
        Assert.Equal("{\"LegacyKey\": 7}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));

        var reopened = harness.Manager.OpenModConfig(harness.GameDirectory, "CfgMod.dll");
        Assert.Equal(Path.Combine(modDir, "config.json"), reopened);
        Assert.Equal("{\"LegacyKey\": 7}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));
    }

    [Fact]
    public void Install_DetectsSpeedhackConflict()
    {
        using var harness = new ModHarness("ap-mod-conflict");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        // 模拟已安装独立变速器: version.dll + speedhack_config.json
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(harness.GameDirectory, "speedhack_config.json"), "{}");

        Assert.True(ModManager.HasSpeedhackInstalled(harness.GameDirectory), "应检测到变速器");
        var ex = Assert.Throws<InvalidOperationException>(
            () => harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: true));
        Assert.Contains("变速器", ex.Message);
        Assert.Contains("speedhackBaseSpeed", ex.Message);
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        // 用户设置变速 2.0
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);
        Assert.Equal(2.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);

        // 重新安装加载器 → doorstop_config 必须保留(不重置用户设置)
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: true);
        Assert.Equal(2.0, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SpeedhackBaseSpeed);
    }

    [Fact]
    public void InstallPackage_PreservesExistingUserConfig()
    {
        using var harness = new ModHarness("ap-mod-pkg-preserve");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);
        harness.Manager.SetSpeedhack(harness.GameDirectory, 2.0);

        var bundle = ModManager.BundleSdkVersion;
        Assert.False(string.IsNullOrEmpty(bundle), "内置 doorstop_config.json 模板没有 sdkVersion");

        // 模拟"老版本装出来的配置": sdkVersion 停在旧号(随包内置 mod 已声明更高的 SdkVersion)
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, File.ReadAllText(path).Replace(bundle, "2.1.0"));
        Assert.Equal("2.1.0", harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);

        // 重新安装 → sdkVersion 必须抬到随包版本, 否则加载器会拒绝加载新版内置 mod; 用户设置照旧
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: true);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(bundle, config.SdkVersion);
        Assert.Equal(2.0, config.SpeedhackBaseSpeed);
    }

    [Fact]
    public void Install_DoesNotDowngradeNewerSdkVersion()
    {
        using var harness = new ModHarness("ap-mod-sdk-nodowngrade");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var bundle = ModManager.BundleSdkVersion;
        Assert.False(string.IsNullOrEmpty(bundle), "内置 doorstop_config.json 模板没有 sdkVersion");

        // 用户自己装了更新的加载器(配置里的 SDK 号比随包版本还高) → 安装不得把它降回去
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        File.WriteAllText(path, File.ReadAllText(path).Replace(bundle, "9.9.9"));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: false);
        Assert.Equal("9.9.9", harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);
    }

    [Fact]
    public void InstallPackage_RaisesSdkVersion_FromPackage()
    {
        using var harness = new ModHarness("ap-mod-pkg-sdk-sync");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var before = harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion;
        Assert.False(string.IsNullOrEmpty(before), "内置 doorstop_config.json 模板没有 sdkVersion");

        // 老发布包(配置里没写 sdkVersion) → 不添不改(加载器会用自己编译进去的默认值)
        harness.Manager.InstallPackage(harness.GameDirectory, BuildFakePackage("5.0.0"), overwriteDll: true);

        Assert.Equal(before, harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);
    }

    // ============================== 老配置补齐 Steam 绕过开关 ==============================

    [Fact]
    public void Install_AddsMissingSteamBypassKeys_ButKeepsUserValuesAndComments()
    {
        using var harness = new ModHarness("ap-mod-steam-keys-add");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 模拟"旧版内置模板装出来的配置": 没有 steamBypass 键; 用户已把主开关关掉过, 还写了自己的注释
        File.WriteAllText(path,
            "{\n  // 这是我自己写的注释\n  \"enabled\": true,\n  \"speedhackBaseSpeed\": 2.0,\n"
            + "  \"steamBypassEnabled\": false,\n  \"sdkVersion\": \"2.1.7\"\n}");

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: false);

        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.False(config.SteamBypassEnabled);              // 用户显式关掉的值必须原样保留
        Assert.True(config.SteamBypassRestartCheck);          // 缺失的按键位补上(= 模板默认)
        Assert.True(config.SteamBypassMatchmaking);
        Assert.True(config.SteamBypassLobbyQuery);
        Assert.Equal("auto", config.SteamBypassTaskCtorMode);
        Assert.False(config.SteamBypassLobbyMethods);         // 地雷键必须补成 false
        Assert.Equal(2.0, config.SpeedhackBaseSpeed);         // 其它用户设置照旧
        Assert.Equal("2.2.1", config.SdkVersion);             // sdkVersion 同时被抬升

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 新模板装出来的配置 7 个键都在 → 再次安装必须一字不改(幂等)
        var before = File.ReadAllText(path);
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: false);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void InstallPackage_AddsMissingSteamBypassKeys()
    {
        using var harness = new ModHarness("ap-mod-pkg-steam-keys");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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

    // ============================== 首页「启动方式」单选框 → 加载器配置 ==============================
    //
    // 首页单选框写的是**一整套** Steam 绕过开关（主开关 + 匹配 + 房间列表查询），因为这个加载器的 hook 是
    // 启动时无条件安装、各自只受自己的键控制：只关主开关的话，从 Steam 启动仍然不建真大厅、房间列表照样返回空。
    // 写入必须做到：只动这些字面量（注释、其它键、缩进全保留），且写完自检通过才落盘。

    [Fact]
    public void SetSteamBypassProfile_FlipsTheWholeSet_AndKeepsComments()
    {
        using var harness = new ModHarness("ap-mod-set-steam-bypass");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        var before = File.ReadAllText(path);
        // 模板默认：主开关 / 匹配 / 房间列表查询都是开
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        var before = File.ReadAllText(path);

        // 已经是全开（模板默认）→ 无事可做，但算成功：前端不该弹"同步失败"
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        Assert.Equal(before, File.ReadAllText(path));            // 一字未改

        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, false));
        Assert.True(harness.Manager.SetSteamBypassProfile(harness.GameDirectory, true));
        Assert.True(ModManager.IsSteamBypassActive(
            harness.Manager.ReadLoaderConfig(harness.GameDirectory)));
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);

        // 混合状态（用户只手动关了主开关）→ 一次「从 Steam 启动」必须把整套收拾干净
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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

    [Fact]
    public void GetStatus_ReadsInstalledVersionFromLoaderLog_WhenManifestMissing()
    {
        using var harness = new ModHarness("ap-mod-version-from-log");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        // 模拟"0.4.7 之前装的老安装"：没有安装清单，只有加载器日志（加载器每次启动会追加版本行）
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        File.Delete(Path.Combine(loaderRoot, "cesium-loader.json"));
        Directory.CreateDirectory(Path.Combine(loaderRoot, "logs"));
        File.WriteAllText(Path.Combine(loaderRoot, "logs", "cesium-loader.log"),
            "[hijack] version.dll Doorstop 引导线程启动 (enabled=true)\n"
            + "  CesiumLoader loader v2.1.6 / SDK v2.1.6 — mod 加载报告\n"
            + "...（下一局游戏）\n"
            + "  CesiumLoader loader v2.1.7 / SDK v2.1.7 — mod 加载报告\n");

        var status = harness.Manager.GetStatus();
        Assert.Equal("2.1.7", status.InstalledVersion);       // 取最后一次运行，而不是第一次
        Assert.Equal("log", status.InstalledVersionSource);
    }

    [Fact]
    public void GetStatus_PrefersManifestOverLog()
    {
        using var harness = new ModHarness("ap-mod-version-manifest-wins");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        // 同时存在清单与日志（且日志是更旧的版本）→ 以清单为准
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Directory.CreateDirectory(Path.Combine(loaderRoot, "logs"));
        File.WriteAllText(Path.Combine(loaderRoot, "logs", "cesium-loader.log"),
            "  CesiumLoader loader v1.0.0 / SDK v1.0.0 — mod 加载报告\n");

        var status = harness.Manager.GetStatus();
        Assert.Equal("2.2.1", status.InstalledVersion);
        Assert.Equal("manifest", status.InstalledVersionSource);
    }

    [Fact]
    public void Install_RefusesDowngrade_UnlessExplicitlyAllowed()
    {
        using var harness = new ModHarness("ap-mod-downgrade-block");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
        OverwriteManifestVersion(harness, "9.9.9");   // 假装已装了更新的加载器

        var ex = Assert.Throws<InvalidOperationException>(() =>
            harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: false));
        Assert.Contains("降级", ex.Message);
        Assert.Contains("9.9.9", ex.Message);

        // 显式放行才允许降级
        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: false, allowDowngrade: true);
        Assert.Equal("2.2.1", harness.Manager.GetStatus().InstalledVersion);
    }

    [Fact]
    public void InstallPackage_RefusesDowngrade_UnlessExplicitlyAllowed()
    {
        using var harness = new ModHarness("ap-mod-pkg-downgrade-block");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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
    public void GetStatus_FallsBackToConfiguredSdkVersion_WhenNoManifestAndNoLog()
    {
        using var harness = new ModHarness("ap-mod-version-from-config");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        // 老安装、而且装完从没成功启动到 mod 加载阶段（日志里没有版本行）→ 用配置里的 sdkVersion 提示
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        File.Delete(Path.Combine(loaderRoot, "cesium-loader.json"));
        var configPath = Path.Combine(loaderRoot, "doorstop_config.json");
        File.WriteAllText(configPath, File.ReadAllText(configPath).Replace("\"2.2.1\"", "\"2.1.6\""));

        var status = harness.Manager.GetStatus();
        Assert.Equal("2.1.6", status.InstalledVersion);
        Assert.Equal("config", status.InstalledVersionSource);
    }

    [Fact]
    public void Install_DoesNotTreatConfiguredSdkVersionAsDowngradeEvidence()
    {
        using var harness = new ModHarness("ap-mod-config-not-authoritative");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        File.Delete(Path.Combine(loaderRoot, "cesium-loader.json"));   // 没有清单 → 只能靠配置
        var configPath = Path.Combine(loaderRoot, "doorstop_config.json");
        // 用户手改配置里的 sdkVersion（比如为了绕过 mod 的 SDK 校验）→ 不能因此把安装当成"降级"拦住
        File.WriteAllText(configPath, File.ReadAllText(configPath).Replace("\"2.2.1\"", "\"9.9.9\""));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: false);
        Assert.Equal("2.2.1", harness.Manager.GetStatus().InstalledVersion);
    }

    // ============================== 加载器配置 / 权限 / 配置表单 ==============================

    [Fact]
    public void ReadLoaderConfig_ParsesCommentedFile()
    {
        using var harness = new ModHarness("ap-mod-cfg-read");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var config = new LoaderConfig { SpeedhackBaseSpeed = 3.0, ConsoleTopmost = false, SpeedControlEnabled = false };
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        var read = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(3.0, read.SpeedhackBaseSpeed);
        Assert.False(read.ConsoleTopmost);
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
        // 老配置(升级上来的 doorstop_config.json)没有 speedControlEnabled -> 默认开, 热键可用
        Assert.True(config.SpeedControlEnabled);

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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);
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

        var json = File.ReadAllText(Path.Combine(modsDir, "KeySaveMod", "config.json"));
        Assert.Contains("\"toggleKey\": \"Mouse4\"", json);

        var field = Assert.Single(harness.Manager.ReadModConfigFields(harness.GameDirectory, "KeySaveMod.dll"));
        Assert.Equal("toggleKey", field.Name);
        Assert.Equal("key", field.Kind);
        Assert.Equal("Mouse4", field.StringValue);
    }

    // ============================== 联网更新：包解析 / 校验 / 安装 ==============================

    /// <summary>构造一个符合发布布局的内存 zip（version.dll Doorstop 式，含清单，可带/不带哈希校验）。</summary>
    private static byte[] BuildFakePackage(string version, bool includeHashes = true, string? configSdkVersion = null)
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

            Add("version.dll", loader);
            Add("AstralParty_ModLoader/doorstop_config.json", config);
            Add("AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll", sdk);
            Add("AstralParty_ModLoader/mods/ActivityLogMod/ActivityLogMod.dll", mod);
            Add("AstralParty_ModLoader/mods/ActivityLogMod/ActivityLogMod.json", sidecar);

            string Sha(byte[] data) => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();

            var manifest = new System.Text.StringBuilder();
            manifest.Append("{\"name\":\"CesiumLoader\",\"version\":\"").Append(version).Append("\",");
            manifest.Append("\"files\":{");
            if (includeHashes)
            {
                manifest.Append("\"version.dll\":\"").Append(Sha(loader)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/doorstop_config.json\":\"").Append(Sha(config)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll\":\"").Append(Sha(sdk)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/mods/ActivityLogMod/ActivityLogMod.dll\":\"").Append(Sha(mod)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/mods/ActivityLogMod/ActivityLogMod.json\":\"").Append(Sha(sidecar)).Append("\"");
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

        public void Dispose() => Sandbox.Dispose();
    }
}
