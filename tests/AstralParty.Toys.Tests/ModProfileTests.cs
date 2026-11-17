using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Modding;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>模组方案：捕获 / 套用 / 命名 / 全局共享 / 脏标记。</summary>
public sealed class ModProfileTests
{
    private const string ConfigA = "{\r\n  // A 的说明\r\n  \"x\": 1\r\n}\r\n";

    [Fact]
    public void Capture_RecordsEnabledModsConfigsAndLoader()
    {
        using var harness = new ModProfileHarness("ap-profile-capture");
        harness.AddMod("ModA", enabled: true, config: ConfigA);
        harness.AddMod("ModB", enabled: false, config: "{\"y\": 2}");

        var summary = harness.Service.Capture("PVE 日常", "只开两个", includeConfigs: true, includeLoader: true);

        Assert.Equal("PVE 日常", summary.Name);
        Assert.Equal(2, summary.ModCount);
        Assert.Equal(1, summary.EnabledCount);
        Assert.Equal(0, summary.MissingCount);
        Assert.True(summary.Active);
        Assert.False(summary.Dirty);

        var profile = Assert.Single(harness.Service.Load().Profiles);
        Assert.True(profile.IncludesConfigs);
        Assert.True(profile.IncludesLoader);

        var a = Assert.Single(profile.Mods, m => m.Name == "ModA");
        Assert.True(a.Enabled);
        Assert.Equal(ConfigA, a.Config);

        var b = Assert.Single(profile.Mods, m => m.Name == "ModB");
        Assert.False(b.Enabled);
        Assert.Equal("{\"y\": 2}", b.Config);

        Assert.Equal(1.0, profile.Loader.SpeedhackBaseSpeed);
        // 加载器配置里的"账号档案"字段不在方案里，套用时不会动
        Assert.Equal("P2", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    [Fact]
    public void Apply_RestoresEnabledStateAndWritesConfigVerbatim()
    {
        using var harness = new ModProfileHarness("ap-profile-apply");
        harness.AddMod("ModA", enabled: true, config: ConfigA);
        harness.AddMod("ModB", enabled: false, config: "{\"y\": 2}");
        harness.Service.Capture("PVE", "", includeConfigs: true, includeLoader: true);

        // 打乱当前状态：A 停用、B 启用、配置被改、加载器倍速改成 3x
        harness.Manager.ToggleMod(harness.GameDirectory, "ModA.dll", enabled: false);
        harness.Manager.ToggleMod(harness.GameDirectory, "ModB.dll", enabled: true);
        File.WriteAllText(harness.ModConfigPath("ModA", enabled: false), "{\"x\": 99}");
        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        config.SpeedhackBaseSpeed = 3.0;
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        var result = harness.Service.Apply("PVE");

        Assert.True(result.Ok, result.Message + " || " + string.Join(" | ", result.Failed) + " || " + string.Join(" | ", result.Skipped));
        Assert.True(File.Exists(Path.Combine(harness.ModsRoot, "ModA", "ModA.dll")), "A 应搬回 mods");
        Assert.True(File.Exists(Path.Combine(harness.DisabledRoot, "ModB", "ModB.dll")), "B 应搬进 disabled_mods");
        Assert.Equal(ConfigA, File.ReadAllText(harness.ModConfigPath("ModA")));
        Assert.Equal("{\"y\": 2}", File.ReadAllText(harness.ModConfigPath("ModB", enabled: false)));

        var after = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(1.0, after.SpeedhackBaseSpeed);
        Assert.Equal("P2", after.Profile);            // 账号档案字段保持当前值
        Assert.True(after.ProfileWindowTitle);
        Assert.Equal("PVE", harness.Service.Load().Active);
    }

    [Fact]
    public void Apply_TracksDirtyState()
    {
        using var harness = new ModProfileHarness("ap-profile-dirty");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("P", "", includeConfigs: true, includeLoader: true);

        Assert.False(harness.Service.Find("P")!.Dirty);
        harness.Manager.ToggleMod(harness.GameDirectory, "ModA.dll", enabled: false);
        Assert.True(harness.Service.Find("P")!.Dirty);

        harness.Service.Apply("P");
        Assert.False(harness.Service.Find("P")!.Dirty);
    }

    [Fact]
    public void Apply_SkipsMissingModsAndLeavesUnknownModsAlone()
    {
        using var harness = new ModProfileHarness("ap-profile-missing");
        harness.AddMod("ModA", enabled: true);
        harness.AddMod("ModB", enabled: true);
        harness.Service.Capture("P", "", includeConfigs: false, includeLoader: false);

        harness.Manager.DeleteMod(harness.GameDirectory, "ModB.dll");
        harness.AddMod("ModC", enabled: true);

        var result = harness.Service.Apply("P");

        Assert.True(result.Ok, result.Message + " || " + string.Join(" | ", result.Failed) + " || " + string.Join(" | ", result.Skipped));
        Assert.Contains(result.Skipped, line => line.Contains("ModB"));
        Assert.True(File.Exists(Path.Combine(harness.ModsRoot, "ModC", "ModC.dll")), "方案里没记录的新模组不该被动");
        Assert.True(harness.Service.Find("P")!.Dirty == false);
    }

    [Fact]
    public void Apply_BlockedWhenLoaderRootMissing()
    {
        using var harness = new ModProfileHarness("ap-profile-blocked");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("P", "", includeConfigs: false, includeLoader: false);

        var bare = harness.Sandbox.EnsureDirectory("bare");
        var service = new ModProfileService(harness.Manager, bare);

        var plan = service.Plan("P");
        Assert.True(plan.Blocked);
        Assert.Contains("加载器", plan.BlockReason);
    }

    [Theory]
    [InlineData("PVE 日常", true)]
    [InlineData("pve-daily_2", true)]
    [InlineData("日常", true)]
    [InlineData("  PVE  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a/b", false)]
    [InlineData("a:b", false)]
    [InlineData("a*b", false)]
    public void IsValidName_AllowsCjkAndSafePunctuation(string name, bool expected)
    {
        Assert.Equal(expected, ModProfileService.IsValidName(name, out _));
    }

    [Fact]
    public void IsValidName_RejectsTooLong()
    {
        Assert.False(ModProfileService.IsValidName(new string('字', 33), out var error));
        Assert.Contains("32", error);
    }

    [Fact]
    public void Capture_RejectsDuplicateNamesCaseInsensitively()
    {
        using var harness = new ModProfileHarness("ap-profile-duplicate");
        harness.AddMod("ModA");
        harness.Service.Capture("PVE", "", false, false);
        var error = Assert.Throws<InvalidOperationException>(() => harness.Service.Capture("pve", "", false, false));
        Assert.Contains("已经有一个", error.Message);
    }

    [Fact]
    public void Rename_UpdatesActiveAndRejectsDuplicate()
    {
        using var harness = new ModProfileHarness("ap-profile-rename");
        harness.AddMod("ModA");
        harness.Service.Capture("旧名字", "", false, false);
        harness.Service.Capture("另一个", "", false, false);

        harness.Service.Rename("旧名字", "新名字");
        Assert.NotNull(harness.Service.Find("新名字"));
        Assert.Null(harness.Service.Find("旧名字"));
        // 两次捕获让「另一个」成为当前方案；改名的是另一个方案，当前方案不变
        Assert.Equal("另一个", harness.Service.Load().Active);

        Assert.Throws<InvalidOperationException>(() => harness.Service.Rename("新名字", "另一个"));
    }

    [Fact]
    public void Delete_ClearsActiveAndRemovesFromList()
    {
        using var harness = new ModProfileHarness("ap-profile-delete");
        harness.AddMod("ModA");
        harness.Service.Capture("P", "", false, false);

        harness.Service.Delete("P");

        Assert.Empty(harness.Service.List());
        Assert.Equal("", harness.Service.Load().Active);
    }

    [Fact]
    public void Update_RefreshesDefinitionAndKeepsNote()
    {
        using var harness = new ModProfileHarness("ap-profile-update");
        harness.AddMod("ModA");
        harness.Service.Capture("P", "备注", includeConfigs: true, includeLoader: true);

        harness.AddMod("ModNew", enabled: false);
        harness.Service.Update("P", includeConfigs: true, includeLoader: true);

        var profile = Assert.Single(harness.Service.Load().Profiles);
        Assert.Equal("备注", profile.Note);
        Assert.Contains(profile.Mods, m => m.Name == "ModNew");
        Assert.False(profile.Mods.Single(m => m.Name == "ModNew").Enabled);
    }

    [Fact]
    public void ClearActive_KeepsModsUntouched()
    {
        using var harness = new ModProfileHarness("ap-profile-clear");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("P", "", false, false);

        harness.Service.ClearActive();

        Assert.Equal("", harness.Service.Load().Active);
        Assert.True(File.Exists(Path.Combine(harness.ModsRoot, "ModA", "ModA.dll")));
    }

    [Fact]
    public void Profiles_AreGlobalAcrossGameDirectories()
    {
        using var harness = new ModProfileHarness("ap-profile-global");
        harness.AddMod("ModA", enabled: true, config: "{\"a\":1}");
        harness.Service.Capture("共享", "", includeConfigs: true, includeLoader: true);

        var second = harness.CreateGameDirectory("game2");
        var mods2 = Path.Combine(second, ModManager.LoaderFolderName, ModManager.ModsFolderName, "ModA");
        Directory.CreateDirectory(mods2);
        File.WriteAllBytes(Path.Combine(mods2, "ModA.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllText(Path.Combine(mods2, "ModA.json"), "{\"id\":\"ModA\",\"name\":\"ModA\",\"version\":\"1.0.0\"}");
        File.WriteAllText(Path.Combine(mods2, "config.json"), "{\"a\":2}");

        var service2 = new ModProfileService(harness.Manager, second);
        var listed = Assert.Single(service2.List());
        Assert.Equal("共享", listed.Name);

        var result = service2.Apply("共享");
        Assert.True(result.Ok, result.Message + " || " + string.Join(" | ", result.Failed) + " || " + string.Join(" | ", result.Skipped));
        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(mods2, "config.json")));
        Assert.Equal("共享", service2.Load().Active);
    }

    [Fact]
    public void Apply_DoesNotWriteSdkVersion()
    {
        using var harness = new ModProfileHarness("ap-profile-sdk");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("P", "", includeConfigs: false, includeLoader: true);

        // 模拟更新加载器：配置里的 sdkVersion 升到新版本
        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        config.SdkVersion = "9.9.9";
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        var result = harness.Service.Apply("P");

        Assert.True(result.Ok, result.Message);
        // sdkVersion 是安装元数据，不属于方案：套用旧方案不能把它写回旧值
        Assert.Equal("9.9.9", harness.Manager.ReadLoaderConfig(harness.GameDirectory).SdkVersion);
    }

    [Fact]
    public void LoaderSettings_ApplyToPreservesAccountFieldsAndClamps()
    {
        var target = new LoaderConfig
        {
            Enabled = false,
            Profile = "P2",
            ProfileWindowTitle = false,
            SpeedhackBaseSpeed = 1.0,
            DebugWaitTimeoutSec = 120
        };
        var settings = new ModProfileLoaderSettings
        {
            SpeedhackBaseSpeed = 250,
            DebugWaitTimeoutSec = 0,
            LogLevel = "warning",
            SteamBypassTaskCtorMode = "bogus"
        };

        settings.ApplyTo(target);

        Assert.False(target.Enabled);                          // 加载器停用开关不动
        Assert.Equal("P2", target.Profile);                    // 账号档案不动
        Assert.False(target.ProfileWindowTitle);               // 窗口标题开关不动
        Assert.Equal(ModManager.MaxBaseSpeed, target.SpeedhackBaseSpeed);
        Assert.Equal(ModManager.MinTimeoutSec, target.DebugWaitTimeoutSec);
        Assert.Equal("Warn", target.LogLevel);
        Assert.Equal("auto", target.SteamBypassTaskCtorMode);
    }
}

