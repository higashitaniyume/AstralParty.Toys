using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Modding;

internal sealed record SpeedModeSettings(string Mode, double Speed, double UnityMinSpeed, double UnityMaxSpeed);

public sealed partial class ModManager
{
    internal const string SpeedModeModFileName = "SpeedHackMod.dll";
    private const string NativeSpeedMode = "native";
    private const string UnitySpeedMode = "unity";
    private static readonly JsonDocumentOptions SpeedModeJsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    internal SpeedModeSettings ReadSpeedModeSettings(ModStatus status)
    {
        var mod = ReadSpeedModeConfig(SpeedModeConfigPath(status)).Config;
        var loader = ReadSpeedModeConfig(LoaderConfigPath(status.GameDirectory)).Config;
        var mode = mod["accelerationMode"] is JsonValue value
                   && value.TryGetValue<string>(out var storedMode)
                   && string.Equals(storedMode, UnitySpeedMode, StringComparison.OrdinalIgnoreCase)
            ? UnitySpeedMode : NativeSpeedMode;
        var baseSpeed = Math.Clamp(ReadSpeedModeNumber(loader, "speedhackBaseSpeed", MinBaseSpeed), MinBaseSpeed, MaxBaseSpeed);
        var (unityMin, unityMax) = ReadUnitySpeedRange(mod);
        // 旧配置若仍设置原生启动倍率，模组启动时会优先接管这个倍率。
        var speed = mode == UnitySpeedMode
            ? Math.Clamp(baseSpeed > 1.0001 ? baseSpeed : ReadSpeedModeNumber(mod, "speed", 2), unityMin, unityMax)
            : baseSpeed;
        return new SpeedModeSettings(mode, speed, unityMin, unityMax);
    }

    internal void SaveSpeedModeSettings(ModStatus status, string mode, double speed)
    {
        if (mode is not (NativeSpeedMode or UnitySpeedMode))
            throw new InvalidOperationException("无法识别所选的变速模式。");

        var modPath = SpeedModeConfigPath(status);
        var (mod, _) = ReadSpeedModeConfig(modPath);
        var (min, max) = mode == UnitySpeedMode ? ReadUnitySpeedRange(mod) : (MinBaseSpeed, MaxBaseSpeed);
        if (!double.IsFinite(speed) || speed < min || speed > max)
            throw new InvalidOperationException($"所选模式的倍速必须在 {min:0.##}x 到 {max:0.##}x 之间。");

        var loaderPath = LoaderConfigPath(status.GameDirectory);
        var (loader, originalLoader) = ReadSpeedModeConfig(loaderPath);
        var oldBaseSpeed = ReadSpeedModeNumber(loader, "speedhackBaseSpeed", MinBaseSpeed);
        var baseSpeed = mode == UnitySpeedMode ? MinBaseSpeed : speed;
        mod["accelerationMode"] = mode;
        mod["speed"] = speed;

        // Unity 由模组应用倍率，加载器应以 1x 启动，避免先对全局计时加速。
        var loaderChanged = oldBaseSpeed != baseSpeed;
        if (loaderChanged)
        {
            loader["speedhackBaseSpeed"] = baseSpeed;
            ToysConfigStore.WriteAtomic(loaderPath, loader.ToJsonString(ModConfigWriteOptions));
        }
        try
        {
            ToysConfigStore.WriteAtomic(modPath, mod.ToJsonString(ModConfigWriteOptions));
        }
        catch
        {
            if (loaderChanged)
            {
                if (originalLoader is null) File.Delete(loaderPath);
                else ToysConfigStore.WriteAtomic(loaderPath, originalLoader);
            }
            throw;
        }
    }

    private static (double Min, double Max) ReadUnitySpeedRange(JsonObject mod)
    {
        var min = Math.Clamp(ReadSpeedModeNumber(mod, "minSpeed", MinBaseSpeed), MinBaseSpeed, MaxBaseSpeed);
        var max = Math.Clamp(ReadSpeedModeNumber(mod, "maxSpeed", 10), min, MaxBaseSpeed);
        return (min, max);
    }

    private static double ReadSpeedModeNumber(JsonObject config, string key, double defaultValue) =>
        config[key] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number)
            ? number : defaultValue;

    private static (JsonObject Config, string? Text) ReadSpeedModeConfig(string path)
    {
        if (!File.Exists(path)) return (new JsonObject(), null);
        var text = File.ReadAllText(path);
        return (JsonNode.Parse(text, documentOptions: SpeedModeJsonOptions) as JsonObject
                ?? throw new InvalidOperationException($"配置文件不是 JSON 对象：{Path.GetFileName(path)}"), text);
    }

    private static string SpeedModeConfigPath(ModStatus status)
    {
        if (string.IsNullOrWhiteSpace(status.GameDirectory))
            throw new InvalidOperationException("请先在「系统设置」选择游戏目录。");
        if (!status.Installed || status.LoaderDisabled || status.CoreMissing)
            throw new InvalidOperationException("请先在「模组」页安装或启用可用的 CesiumLoader。");

        var mod = status.Mods.FirstOrDefault(entry =>
            string.Equals(entry.FileName, SpeedModeModFileName, StringComparison.OrdinalIgnoreCase));
        if (mod is null || !mod.Enabled || mod.DisabledFolder)
            throw new InvalidOperationException("请先在「模组」页安装并启用「变速」模组。");
        if (CompareSemVer(mod.Version, "2.5.4") < 0)
            throw new InvalidOperationException("Native / Unity 切换需要 SpeedHackMod 2.5.4 或更新版本。");

        return ModConfigWritePath(status.GameDirectory, mod.FileName);
    }
}
