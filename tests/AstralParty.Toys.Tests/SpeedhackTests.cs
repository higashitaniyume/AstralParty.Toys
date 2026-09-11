using System.Text.Json.Nodes;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;
using Xunit.Abstractions;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 变速器：内嵌资源、安装 / 卸载 / 覆盖保护、配置往返。
/// 一律使用临时沙箱里的假游戏目录与假 profile，不碰真实游戏与 %AppData%。
/// </summary>
public sealed class SpeedhackTests
{
    [Fact]
    public void EmbeddedResources_AreComplete()
    {
        Assert.True(SpeedhackManager.HasEmbeddedDll, "内置 version.dll 资源缺失");
        Assert.True(SpeedhackManager.HasEmbeddedConfigTemplate, "内置配置模板资源缺失");
        Assert.True(SpeedhackManager.HasEmbeddedResources, "内置变速器资源不完整");
    }

    [Fact]
    public void Install_CopiesDllAndWritesConfig()
    {
        using var harness = new SpeedhackHarness("ap-speedhack-install");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var status = harness.Manager.GetStatus();
        Assert.Equal(harness.GameDirectory, status.GameDirectory);
        Assert.True(status.GameExeFound, "未识别出假 exe 目录");
        Assert.False(status.Installed);
        Assert.False(status.DllPresent);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        status = harness.Manager.GetStatus();
        Assert.True(status.Installed);
        Assert.True(status.DllMatchesBundle, "安装后哈希不一致");
        Assert.True(status.ConfigPresent, "安装后缺少 speedhack_config.json");
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")), "version.dll 未复制");
        Assert.True(File.Exists(harness.Manager.ProfileConfigPath), "主配置未生成");
    }

    [Fact]
    public void Install_RefusesToOverwriteForeignDll_UnlessForced()
    {
        using var harness = new SpeedhackHarness("ap-speedhack-overwrite");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<InvalidOperationException>(() => harness.Manager.Install(harness.GameDirectory, overwriteDll: false));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true);
        Assert.True(harness.Manager.GetStatus().DllMatchesBundle, "覆盖安装后哈希应一致");
    }

    [Fact]
    public void Uninstall_RefusesForeignDll_UnlessForced()
    {
        using var harness = new SpeedhackHarness("ap-speedhack-uninstall-guard");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "version.dll"), [9, 9, 9]);

        Assert.Throws<InvalidOperationException>(() => harness.Manager.Uninstall(harness.GameDirectory, force: false));

        harness.Manager.Uninstall(harness.GameDirectory, force: true);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")), "强制卸载后文件应删除");
    }

    [Fact]
    public void Uninstall_RemovesDllAndConfig()
    {
        using var harness = new SpeedhackHarness("ap-speedhack-uninstall");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var result = harness.Manager.Uninstall(harness.GameDirectory, force: false);

        Assert.True(result.RemovedDll);
        Assert.True(result.RemovedConfig);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "version.dll")));
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "speedhack_config.json")));
    }

    [Fact]
    public void ConfigRoundTrip_WritesExpectedJson()
    {
        using var harness = new SpeedhackHarness("ap-speedhack-config");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false);

        var model = SpeedhackEditorMapper.MapToEditor(harness.Manager.LoadProfileConfig());
        Assert.Equal(2.0, model.BaseSpeed);
        Assert.Equal(250, model.WaitMs);
        Assert.Single(model.SpeedStates);
        Assert.Equal(2.0, model.SpeedStates[0].Speed);

        model.BaseSpeed = 3.5;
        model.WaitMs = 1500;
        model.Startup.Enabled = true;
        model.Startup.Speed = 8;
        model.Startup.DurationSecs = 4;
        model.ReloadKeys = ["VK_CONTROL", "VK_SHIFT", "VK_T"];
        model.SpeedStates =
        [
            new SpeedhackStateEditorModel { Keys = ["VK_CONTROL"], Speed = 2, IsToggle = true },
            new SpeedhackStateEditorModel { Keys = ["VK_CONTROL", "VK_SHIFT"], Speed = 5, IsToggle = false }
        ];
        var config = harness.Manager.LoadProfileConfig();
        SpeedhackEditorMapper.ApplyToConfig(config, model);
        harness.Manager.SaveProfileConfig(config);
        harness.Manager.PushConfigToGame(harness.GameDirectory);

        var written = JsonNode.Parse(File.ReadAllText(Path.Combine(harness.GameDirectory, "speedhack_config.json")))!.AsObject();
        Assert.Equal(3.5, (double)written["base_speed"]!);
        Assert.Equal(1, (int)written["wait_with_hook"]!["secs"]!);
        Assert.Equal(500000000, (int)written["wait_with_hook"]!["nanos"]!);
        Assert.Equal(8, (double)written["startup_state"]!["speed"]!);
        Assert.Equal(4, (int)written["startup_state"]!["duration"]!["secs"]!);
        var states = written["speed_states"]!.AsArray();
        Assert.Equal(2, states.Count);
        Assert.False(states[1]!["is_toggle"]!.GetValue<bool>());
        Assert.Equal(2, states[1]!["keys"]!.AsArray().Count);
        Assert.Equal("VK_CONTROL,VK_SHIFT,VK_T",
            string.Join(",", written["reload_config_keys"]!.AsArray().Select(node => node!.GetValue<string>())));
    }

    /// <summary>沙箱化的假游戏目录 + 假 profile。</summary>
    private sealed class SpeedhackHarness : IDisposable
    {
        public SpeedhackHarness(string sandboxName)
        {
            Sandbox = new TestSandbox(sandboxName);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            Manager = new SpeedhackManager(TestPaths.AppDirectory, Sandbox.EnsureDirectory("profile"));
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public SpeedhackManager Manager { get; }

        public void Dispose() => Sandbox.Dispose();
    }
}

/// <summary>
/// 只读诊断（原 <c>--speedhack-diag</c>）：把内嵌资源、配置位置、Steam / 游戏目录探测结果打到测试输出里。
/// 保留它是因为排查「装不上 / 找不到游戏目录」时，这些现场信息比断言更有用。
/// </summary>
public sealed class SpeedhackDiagnosticsTests
{
    private readonly ITestOutputHelper _output;

    public SpeedhackDiagnosticsTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ReportsEnvironment()
    {
        var assembly = typeof(SpeedhackManager).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains("SpeedhackTools", StringComparison.Ordinal))
            .ToList();

        _output.WriteLine($"ASSEMBLY: {assembly.Location}");
        foreach (var name in resources) _output.WriteLine($"  RESOURCE {name}");
        _output.WriteLine($"HasEmbeddedDll={SpeedhackManager.HasEmbeddedDll} HasEmbeddedConfigTemplate={SpeedhackManager.HasEmbeddedConfigTemplate}");

        var profileDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AstralParty.Toys");
        _output.WriteLine($"PROFILE DIR: {profileDirectory} exists={Directory.Exists(profileDirectory)}");
        _output.WriteLine($"STATE FILE: exists={File.Exists(Path.Combine(profileDirectory, "speedhack-state.json"))}");
        _output.WriteLine($"PROFILE CONFIG: exists={File.Exists(Path.Combine(profileDirectory, "speedhack", "speedhack_config.json"))}");

        var detected = SpeedhackManager.DetectGameDirectory();
        _output.WriteLine($"DETECTED GAME DIR: {detected ?? "(null)"}");
        _output.WriteLine($"RUNNING: game={SpeedhackManager.IsGameRunning()}");

        Assert.True(SpeedhackManager.HasEmbeddedResources);
        Assert.NotEmpty(resources);
        if (detected is not null)
            Assert.True(Directory.Exists(detected), $"探测到的游戏目录不存在：{detected}");
    }
}
