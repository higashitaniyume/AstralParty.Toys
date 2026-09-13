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
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName, ModManager.SampleModDllName)), "示例 mod 未复制");

        // 状态里的列表
        Assert.Single(status.Sdk);
        Assert.Single(status.Mods); // ActivityLogMod(变速已是加载器内置功能, 不再是 mod)
        Assert.Contains(status.Mods, m => m.FileName == ModManager.SampleModDllName);
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
        Assert.True(File.Exists(Path.Combine(
            harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName, "my-custom-mod.dll")));

        var status = harness.Manager.GetStatus();
        Assert.Contains(status.Mods, m => m.FileName == "my-custom-mod.dll");
    }

    [Fact]
    public void DeleteMod_RemovesFromMods()
    {
        using var harness = new ModHarness("ap-mod-delete");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);
        Assert.Single(harness.Manager.GetStatus().Mods);

        var removed = harness.Manager.DeleteMod(harness.GameDirectory, ModManager.SampleModDllName);
        Assert.NotNull(removed);
        Assert.Equal(ModManager.SampleModDllName, removed.FileName);
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

        // 无配置 → Open 创建并返回路径
        var path = harness.Manager.OpenModConfig(harness.GameDirectory, "CfgMod.dll");
        Assert.True(File.Exists(path), "OpenModConfig 应创建配置文件");
        Assert.EndsWith(Path.Combine("configs", "CfgMod.json"), path);

        // 写内容后 Read 能读回
        File.WriteAllText(path, "{\"LogUi\": true}");
        Assert.Equal("{\"LogUi\": true}", harness.Manager.ReadModConfig(harness.GameDirectory, "CfgMod.dll"));
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

        var config = new LoaderConfig { SpeedhackBaseSpeed = 3.0, ConsoleTopmost = false };
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        var read = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(3.0, read.SpeedhackBaseSpeed);
        Assert.False(read.ConsoleTopmost);

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
        Assert.Contains(fields, f => f.Name == "SpeedUpKey" && f.Kind == "string" && f.StringValue == "F1");

        // 改布尔值后回读
        harness.Manager.SaveModConfigFields(harness.GameDirectory, "FieldMod.dll", new List<ModConfigField>
        {
            new() { Name = "Enabled", Kind = "bool", BoolValue = false }
        });
        var readBack = harness.Manager.ReadModConfigFields(harness.GameDirectory, "FieldMod.dll");
        var enabled = Assert.Single(readBack);
        Assert.False(enabled.BoolValue);
    }

    // ============================== 联网更新：包解析 / 校验 / 安装 ==============================

    /// <summary>构造一个符合发布布局的内存 zip（version.dll Doorstop 式，含清单，可带/不带哈希校验）。</summary>
    private static byte[] BuildFakePackage(string version, bool includeHashes = true)
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
            var config = new byte[] { 0x7B, 0x7D };                    // 假 doorstop_config.json "{}"
            var sdk = new byte[] { 0x53, 0x44, 0x4B, 0x01 };          // 假 SDK
            var mod = new byte[] { 0x4D, 0x4F, 0x44, 0x01 };          // 假 mod
            var sidecar = new byte[] { 0x7B, 0x7D };                  // 假 sidecar "{}"

            Add("version.dll", loader);
            Add("AstralParty_ModLoader/doorstop_config.json", config);
            Add("AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll", sdk);
            Add("AstralParty_ModLoader/mods/ActivityLogMod.dll", mod);
            Add("AstralParty_ModLoader/mods/ActivityLogMod.json", sidecar);

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
                manifest.Append("\"AstralParty_ModLoader/mods/ActivityLogMod.dll\":\"").Append(Sha(mod)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/mods/ActivityLogMod.json\":\"").Append(Sha(sidecar)).Append("\"");
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
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName, ModManager.SampleModDllName)));
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
