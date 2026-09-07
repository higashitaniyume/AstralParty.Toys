namespace AstralParty.Toys.Services;

/// <summary>WebUI 配置编辑器的传输模型（CamelCase 序列化到网页），字段含义见 SpeedhackConfig。</summary>
public sealed class SpeedhackEditorModel
{
    public bool Console { get; set; }
    public double BaseSpeed { get; set; } = 1.0;
    public double WaitMs { get; set; } = 250;
    public SpeedhackStartupEditorModel Startup { get; set; } = new();
    public List<string> ReloadKeys { get; set; } = ["VK_CONTROL", "VK_SHIFT", "VK_R"];
    public List<SpeedhackStateEditorModel> SpeedStates { get; set; } = [];
}

public sealed class SpeedhackStartupEditorModel
{
    public bool Enabled { get; set; }
    public double Speed { get; set; } = 10.0;
    public int DurationSecs { get; set; } = 5;
}

public sealed class SpeedhackStateEditorModel
{
    public List<string> Keys { get; set; } = [];
    public double Speed { get; set; } = 2.0;
    public bool IsToggle { get; set; } = true;
}

public static class SpeedhackEditorMapper
{
    public static SpeedhackEditorModel MapToEditor(SpeedhackConfig config)
    {
        var startup = config.StartupState;
        return new SpeedhackEditorModel
        {
            Console = config.Console,
            BaseSpeed = config.BaseSpeed,
            WaitMs = Math.Round(config.WaitWithHook.Secs * 1000d + config.WaitWithHook.Nanos / 1_000_000d),
            Startup = new SpeedhackStartupEditorModel
            {
                Enabled = startup is not null,
                Speed = startup?.Speed ?? 10.0,
                DurationSecs = startup?.Duration.Secs is > 0 ? startup.Duration.Secs : 5
            },
            ReloadKeys = [.. config.ReloadConfigKeys],
            SpeedStates = config.SpeedStates.Select(state => new SpeedhackStateEditorModel
            {
                Keys = [.. state.Keys],
                Speed = state.Speed,
                IsToggle = state.IsToggle
            }).ToList()
        };
    }

    public static void ApplyToConfig(SpeedhackConfig config, SpeedhackEditorModel model)
    {
        if (model.BaseSpeed <= 0)
            throw new InvalidOperationException("基础倍速必须大于 0。");
        if (model.WaitMs < 0)
            throw new InvalidOperationException("挂接延迟不能为负数。");

        config.Console = model.Console;
        config.BaseSpeed = model.BaseSpeed;
        config.WaitWithHook = new SpeedhackDuration
        {
            Secs = (int)(model.WaitMs / 1000),
            Nanos = (int)(model.WaitMs % 1000 * 1_000_000)
        };
        config.StartupState = model.Startup.Enabled
            ? new SpeedhackStartupState
            {
                Speed = model.Startup.Speed > 0 ? model.Startup.Speed : 10.0,
                Duration = new SpeedhackDuration { Secs = Math.Max(1, model.Startup.DurationSecs), Nanos = 0 }
            }
            : null;
        config.ReloadConfigKeys = [.. model.ReloadKeys];

        var states = new List<SpeedhackSpeedState>();
        foreach (var state in model.SpeedStates)
        {
            if (state.Speed <= 0)
                throw new InvalidOperationException("倍速档位的速度必须大于 0。");
            states.Add(new SpeedhackSpeedState
            {
                Keys = [.. state.Keys],
                Speed = state.Speed,
                IsToggle = state.IsToggle
            });
        }
        config.SpeedStates = states;
    }
}
