using System.Text.Json.Serialization;

namespace AstralParty.Toys.Services.Speedhack;

public sealed class SpeedhackConfig
{
    [JsonPropertyName("console")] public bool Console { get; set; } = false;

    [JsonPropertyName("base_speed")] public double BaseSpeed { get; set; } = 1.0;

    [JsonPropertyName("reload_config_keys")] public List<string> ReloadConfigKeys { get; set; } = ["VK_CONTROL", "VK_SHIFT", "VK_R"];

    [JsonPropertyName("wait_with_hook")] public SpeedhackDuration WaitWithHook { get; set; } = new() { Nanos = 250_000_000 };

    [JsonPropertyName("startup_state")] public SpeedhackStartupState? StartupState { get; set; }

    [JsonPropertyName("speed_states")] public List<SpeedhackSpeedState> SpeedStates { get; set; } = [];
}

public sealed class SpeedhackDuration
{
    [JsonPropertyName("secs")] public int Secs { get; set; }
    [JsonPropertyName("nanos")] public int Nanos { get; set; }
}

public sealed class SpeedhackStartupState
{
    [JsonPropertyName("speed")] public double Speed { get; set; } = 10.0;
    [JsonPropertyName("duration")] public SpeedhackDuration Duration { get; set; } = new() { Secs = 5 };
}

public sealed class SpeedhackSpeedState
{
    [JsonPropertyName("keys")] public List<string> Keys { get; set; } = [];
    [JsonPropertyName("speed")] public double Speed { get; set; } = 2.0;
    [JsonPropertyName("is_toggle")] public bool IsToggle { get; set; } = true;
}

public sealed class SpeedhackStatus
{
    public bool BundleDllPresent { get; set; }
    public bool TemplateConfigPresent { get; set; }
    public string ProfileConfigPath { get; set; } = "";
    public bool ProfileConfigPresent { get; set; }
    public string GameDirectory { get; set; } = "";
    public string AutoDetectedDirectory { get; set; } = "";
    public bool GameExeFound { get; set; }
    public bool GameRunning { get; set; }
    public bool Installed { get; set; }
    public bool DllPresent { get; set; }
    public bool DllMatchesBundle { get; set; }
    public bool ConfigPresent { get; set; }
    public string DllPath { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public string BundleHash { get; set; } = "";
    public string Message { get; set; } = "";

    /// <summary>配置根目录（主配置、speedhack-state、回放库设置都在这下面）。</summary>
    public string ProfileDirectory { get; set; } = "";

    /// <summary>配置是否保存在用户「文档」目录（正常情况都是 true）。</summary>
    public bool ProfileLocationDocuments { get; set; }

    /// <summary>配置位置的人话说明（便携 / 回退 AppData / 沿用旧位置）。</summary>
    public string ProfileLocationNote { get; set; } = "";

    /// <summary>游戏正在运行时，安装会发生什么（两个界面共用同一套文案）。</summary>
    public string RunningInstallHint => SpeedhackManager.DescribeRunningInstall();

    /// <summary>游戏正在运行时，卸载会发生什么。</summary>
    public string RunningUninstallHint => SpeedhackManager.DescribeRunningUninstall();

    /// <summary>游戏正在运行时，同步配置会发生什么。</summary>
    public string RunningPushConfigHint => SpeedhackManager.DescribeRunningPushConfig();
}

public sealed class UninstallResult
{
    public bool RemovedDll { get; set; }
    public bool RemovedConfig { get; set; }
    public string Message { get; set; } = "";
}
