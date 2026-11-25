using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Modding;

internal sealed record SpeedModeSettings(
    string Mode, double Speed, double UnityMinSpeed, double UnityMaxSpeed, bool CanSelectUnity = true)
{
    public bool CanSelectNative { get; init; }
    public double NativeMinSpeed { get; init; } = 1;
    public double NativeMaxSpeed { get; init; } = 10;
    public string ToggleKey { get; init; } = "Delete";
}

public sealed partial class ModManager
{
    internal const string SpeedModeModFileName = "SpeedHackMod.dll";
    internal const string NativeSpeedModFileName = "NativeSpeedMod.dll";
    private const string NativeSpeedMode = "native";
    private const string UnitySpeedMode = "unity";
    private const string MinimumUnityModVersion = "2.5.4";
    private const string UnityOnlySpeedModeVersion = "2.5.7";
    private static readonly object SpeedModeConfigGate = new();
    private static readonly JsonDocumentOptions SpeedModeJsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    internal static bool CanConfigureSpeedMode(ModStatus status) =>
        !string.IsNullOrWhiteSpace(status.GameDirectory) && status.Installed && !status.LoaderDisabled && !status.CoreMissing;

    private static void RequireSpeedModeLoader(ModStatus status)
    {
        if (string.IsNullOrWhiteSpace(status.GameDirectory))
            throw new InvalidOperationException("请先在「系统设置」选择游戏目录。");
        if (!CanConfigureSpeedMode(status))
            throw new InvalidOperationException("请先在「模组」页安装或启用可用的 CesiumLoader。");
    }

    private static ModEntryInfo? FindSpeedMod(ModStatus status, string fileName) =>
        status.Mods.FirstOrDefault(mod => string.Equals(mod.FileName, fileName, StringComparison.OrdinalIgnoreCase));

    private static bool SupportsUnityMode(ModEntryInfo? mod) =>
        mod is not null && CompareSemVer(mod.Version, MinimumUnityModVersion) >= 0;

    private static bool UsesLegacyModeOption(ModEntryInfo mod) =>
        CompareSemVer(mod.Version, UnityOnlySpeedModeVersion) < 0;

    private static bool IsSpeedModEnabled(ModEntryInfo? mod) =>
        mod is { Enabled: true, DisabledFolder: false };

    internal SpeedModeSettings ReadSpeedModeSettings(ModStatus status, bool migrate = false)
    {
        lock (SpeedModeConfigGate)
        {
            RequireSpeedModeLoader(status);
            var unity = FindSpeedMod(status, SpeedModeModFileName);
            var native = FindSpeedMod(status, NativeSpeedModFileName);
            var canSelectUnity = SupportsUnityMode(unity);

            // 同时安装且都启用时优先保留默认 Unity，避免两个模组争用变速。
            var mode = canSelectUnity && IsSpeedModEnabled(unity) ? UnitySpeedMode
                : IsSpeedModEnabled(native) ? NativeSpeedMode : "";
            var directory = status.GameDirectory;
            var unityConfig = ReadSpeedModConfig(directory, unity, mode == UnitySpeedMode);
            var nativeConfig = ReadSpeedModConfig(directory, native, mode == NativeSpeedMode);
            var (unityMin, unityMax) = ReadSpeedRange(unityConfig);
            var (nativeMin, nativeMax) = ReadSpeedRange(nativeConfig);
            var (loader, _) = ReadSpeedModeConfig(LoaderConfigPath(directory));
            var baseSpeed = ReadSpeedModeNumber(loader, "speedhackBaseSpeed", MinBaseSpeed);

            var activeConfig = mode == UnitySpeedMode ? unityConfig : nativeConfig;
            var legacyNativeDefault = mode == UnitySpeedMode && UsesLegacyModeOption(unity!) &&
                !string.Equals(ReadSpeedModeString(unityConfig, "accelerationMode"), UnitySpeedMode, StringComparison.OrdinalIgnoreCase);
            var configuredSpeed = ReadSpeedModeNumber(activeConfig, "speed", legacyNativeDefault ? MinBaseSpeed : 2);
            var (min, max) = mode == UnitySpeedMode ? (unityMin, unityMax) : (nativeMin, nativeMax);
            var speed = mode.Length == 0 ? Math.Clamp(baseSpeed, MinBaseSpeed, MaxBaseSpeed)
                : Math.Clamp(baseSpeed > 1.0001 ? baseSpeed : legacyNativeDefault ? MinBaseSpeed : configuredSpeed, min, max);
            if (migrate && !status.GameRunning && mode.Length != 0 && (
                baseSpeed != MinBaseSpeed || activeConfig["speed"] is null || speed != configuredSpeed ||
                (mode == UnitySpeedMode
                    ? IsSpeedModEnabled(native) || legacyNativeDefault
                    : IsSpeedModEnabled(unity))))
            {
                SaveSpeedModeSettings(status, mode, speed);
            }

            return new SpeedModeSettings(mode, speed, unityMin, unityMax, canSelectUnity)
            {
                CanSelectNative = native is not null,
                NativeMinSpeed = nativeMin,
                NativeMaxSpeed = nativeMax,
                ToggleKey = ReadSpeedModeString(activeConfig, "toggleKey") ?? "Delete"
            };
        }
    }

    internal bool SaveSpeedModeSettings(ModStatus status, string mode, double speed)
    {
        if (mode is not (NativeSpeedMode or UnitySpeedMode))
            throw new InvalidOperationException("请先选择一种变速模式。");

        lock (SpeedModeConfigGate)
        {
            RequireSpeedModeLoader(status);
            var unity = FindSpeedMod(status, SpeedModeModFileName);
            var native = FindSpeedMod(status, NativeSpeedModFileName);
            var useUnity = mode == UnitySpeedMode;
            if (useUnity && !SupportsUnityMode(unity))
                throw new InvalidOperationException("请先在「模组」页安装 SpeedHackMod 2.5.4 或更新版本。");
            if (!useUnity && native is null)
                throw new InvalidOperationException("请先从市场安装 NativeSpeedMod。");

            var directory = status.GameDirectory;
            var target = (useUnity ? unity : native)!;
            var other = useUnity ? native : unity;
            var targetConfigPath = SpeedModConfigReadPath(directory, target);
            var (config, targetConfigText) = ReadSpeedModeConfig(targetConfigPath);
            var (min, max) = ReadSpeedRange(config);
            if (!double.IsFinite(speed) || speed < min || speed > max)
                throw new InvalidOperationException($"所选模式的倍速必须在 {min:0.##}x 到 {max:0.##}x 之间。");

            var changedKeys = new List<string>(3) { "speed" };
            if (useUnity)
            {
                if (UsesLegacyModeOption(target)) config["accelerationMode"] = UnitySpeedMode;
                else config.Remove("accelerationMode");
                changedKeys.Add("accelerationMode");
            }
            var disableOther = IsSpeedModEnabled(other);
            var enableTarget = !IsSpeedModEnabled(target);
            if (disableOther && enableTarget &&
                ReadSpeedModeString(ReadSpeedModConfig(directory, other, active: false), "toggleKey") is { } key)
            {
                config["toggleKey"] = key;
                changedKeys.Add("toggleKey");
            }
            config["speed"] = speed;

            var targetWritePath = target is { DirectoryName: null, DisabledFolder: true }
                ? ActiveFlatSpeedConfigPath(directory, target.FileName) : targetConfigPath;
            var loaderPath = LoaderConfigPath(directory);
            var (loader, loaderText) = ReadSpeedModeConfig(loaderPath);
            var rollback = new Stack<Action>();
            try
            {
                WriteSpeedModeFile(targetWritePath, config, targetConfigText, rollback, changedKeys);
                if (ReadSpeedModeNumber(loader, "speedhackBaseSpeed", MinBaseSpeed) != MinBaseSpeed)
                {
                    // 旧版 Toys 的原生启动倍率迁入所选模组。现在两个模式都由各自的模组应用倍率。
                    loader["speedhackBaseSpeed"] = MinBaseSpeed;
                    WriteSpeedModeFile(loaderPath, loader, loaderText, rollback, ["speedhackBaseSpeed"]);
                }
                if (disableOther) ChangeSpeedModEnabled(directory, other!, false, rollback);
                if (enableTarget) ChangeSpeedModEnabled(directory, target, true, rollback);
                return disableOther || enableTarget;
            }
            catch (Exception failure)
            {
                var restoreFailures = new List<Exception>();
                while (rollback.TryPop(out var restore))
                {
                    try { restore(); }
                    catch (Exception ex) { restoreFailures.Add(ex); }
                }
                if (restoreFailures.Count > 0)
                    throw new AggregateException("切换变速模式失败，恢复原配置时也发生错误。", restoreFailures.Prepend(failure));
                throw;
            }
        }
    }

    internal void SaveGameSpeed(ModStatus status, double speed)
    {
        lock (SpeedModeConfigGate)
            SaveSpeedModeSettings(status, ReadSpeedModeSettings(status).Mode, speed);
    }

    private static JsonObject ReadSpeedModConfig(string directory, ModEntryInfo? mod, bool active)
    {
        if (mod is null) return new JsonObject();
        try
        {
            return ReadSpeedModeConfig(SpeedModConfigReadPath(directory, mod)).Config;
        }
        catch (Exception ex) when (!active && ex is (IOException or UnauthorizedAccessException or JsonException or InvalidOperationException))
        {
            return new JsonObject();
        }
    }

    private static void WriteSpeedModeFile(
        string path, JsonObject config, string? originalText, Stack<Action> rollback, IReadOnlyList<string> changedKeys)
    {
        var original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var hasBom = original is { Length: >= 3 } && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
        var updatedText = originalText is not null
            ? TryUpdateSpeedJsonText(originalText, config, changedKeys)
            : null;
        if (updatedText is not null)
        {
            var encoded = new System.Text.UTF8Encoding(hasBom).GetBytes(updatedText);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteAllBytesProtected(path, encoded);
        }
        else
        {
            ToysConfigStore.WriteAtomic(path, config.ToJsonString(ModConfigWriteOptions));
        }

        rollback.Push(() =>
        {
            if (original is null) File.Delete(path);
            else WriteAllBytesProtected(path, original);
        });
    }

    private static string? TryUpdateSpeedJsonText(string text, JsonObject expected, IReadOnlyList<string> changedKeys)
    {
        var updated = text.TrimStart('\uFEFF');
        var newline = updated.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        foreach (var key in changedKeys)
        {
            var node = expected[key];
            if (node is null)
            {
                updated = RemoveJsonProperty(updated, key);
                continue;
            }
            var literal = node.ToJsonString(ModConfigWriteOptions);
            updated = SetJsonPropertyLiteral(updated, key, literal, newline);
        }

        try
        {
            if (JsonNode.Parse(updated, documentOptions: SpeedModeJsonOptions) is not JsonObject parsed) return null;
            if (parsed.Count != expected.Count) return null;
            foreach (var pair in expected)
            {
                if (!parsed.TryGetPropertyValue(pair.Key, out var actual) || !JsonNode.DeepEquals(actual, pair.Value))
                    return null;
            }
            return updated;
        }
        catch
        {
            return null;
        }
    }

    private static string SetJsonPropertyLiteral(string text, string key, string literal, string newline)
    {
        var escapedKey = Regex.Escape(JsonSerializer.Serialize(key, ModConfigWriteOptions));
        var pattern = $@"({escapedKey}\s*:\s*)(""([^""\\]|\\.)*""|[^,\}}\r\n/]+)";
        foreach (Match match in Regex.Matches(text, pattern))
        {
            if (IsInsideLineComment(text, match.Index)) continue;
            var value = match.Groups[2].Value.TrimEnd();
            var trailing = match.Groups[2].Value[value.Length..];
            return text[..match.Groups[2].Index] + literal + trailing + text[(match.Index + match.Length)..];
        }

        var close = text.LastIndexOf('}');
        if (close < 0) return text;
        var head = text[..close].TrimEnd();
        var comma = head.EndsWith(',') || head.EndsWith('{') ? "" : ",";
        return head + comma + newline + "  " + JsonSerializer.Serialize(key, ModConfigWriteOptions) + ": " + literal + newline + text[close..];
    }

    private static string RemoveJsonProperty(string text, string key)
    {
        var escapedKey = Regex.Escape(JsonSerializer.Serialize(key, ModConfigWriteOptions));
        var pattern = $@"(?m)^[ \t]*{escapedKey}\s*:\s*(?:""(?:[^""\\]|\\.)*""|[^,\}}\r\n]+)[ \t]*,?[ \t]*(?://[^\r\n]*)?\r?\n?";
        foreach (Match match in Regex.Matches(text, pattern))
        {
            if (IsInsideLineComment(text, match.Index)) continue;
            var candidate = text[..match.Index] + text[(match.Index + match.Length)..];
            // 如果删除的是最后一个带尾逗号的前驱之后的末项，修掉多余尾逗号（自检兜底也会拦截非法 JSON）。
            return Regex.Replace(candidate, @",(\s*\})", "$1");
        }
        return text;
    }

    private void ChangeSpeedModEnabled(string directory, ModEntryInfo mod, bool enabled, Stack<Action> rollback)
    {
        var sidecar = ModSidecarPath(directory, mod.FileName)
            ?? throw new FileNotFoundException($"找不到变速模组：{mod.FileName}");
        var original = File.Exists(sidecar) ? File.ReadAllBytes(sidecar) : null;
        var wasEnabled = mod.Enabled;
        var wasDisabledFolder = mod.DisabledFolder;
        var moved = false;
        rollback.Push(() =>
        {
            if (moved && !SetModDirectoryEnabled(directory, mod.FileName, !wasDisabledFolder))
                throw new IOException($"无法还原变速模组所在目录：{mod.FileName}");
            if (original is null) File.Delete(sidecar);
            else WriteAllBytesProtected(sidecar, original);
            mod.Enabled = wasEnabled;
            mod.DisabledFolder = wasDisabledFolder;
        });

        // 平铺模组用 enabled 标记保留配置路径。已经位于停用目录的平铺模组仍需要搬回。
        var moveFiles = mod.DirectoryName is not null || wasDisabledFolder;
        ToggleMod(directory, mod.FileName, enabled, moveFiles);
        var disabledFolder = moveFiles ? !enabled : wasDisabledFolder;
        moved = wasDisabledFolder != disabledFolder;
        mod.Enabled = enabled;
        mod.DisabledFolder = disabledFolder;
    }

    private static string ActiveFlatSpeedConfigPath(string directory, string fileName) =>
        Path.Combine(ModsRoot(directory), Path.GetFileNameWithoutExtension(fileName), ModConfigFileName);

    private static string SpeedModConfigReadPath(string directory, ModEntryInfo mod)
    {
        var path = ModConfigWritePath(directory, mod.FileName);
        return mod is { DirectoryName: null, DisabledFolder: true } && !File.Exists(path)
            ? ActiveFlatSpeedConfigPath(directory, mod.FileName) : path;
    }

    private static (double Min, double Max) ReadSpeedRange(JsonObject mod)
    {
        var min = Math.Clamp(ReadSpeedModeNumber(mod, "minSpeed", MinBaseSpeed), MinBaseSpeed, MaxBaseSpeed);
        var max = Math.Clamp(ReadSpeedModeNumber(mod, "maxSpeed", 10), min, MaxBaseSpeed);
        return (min, max);
    }

    private static double ReadSpeedModeNumber(JsonObject config, string key, double defaultValue) =>
        config[key] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number)
            ? number : defaultValue;

    private static string? ReadSpeedModeString(JsonObject config, string key) =>
        config[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static (JsonObject Config, string? Text) ReadSpeedModeConfig(string path)
    {
        if (!File.Exists(path)) return (new JsonObject(), null);
        var text = File.ReadAllText(path);
        return (JsonNode.Parse(text, documentOptions: SpeedModeJsonOptions) as JsonObject
                ?? throw new InvalidOperationException($"配置文件不是 JSON 对象：{Path.GetFileName(path)}"), text);
    }
}
