using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Speedhack;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    // ============================== 加载器配置 (doorstop_config.json) ==============================

    /// <summary>加载器配置完整路径。</summary>
    public string LoaderConfigPath(string gameDirectory)
        => LoaderConfigFilePath(gameDirectory);

    /// <summary>读取加载器配置; 文件缺失时用内置模板的默认值。</summary>
    public LoaderConfig ReadLoaderConfig(string gameDirectory)
    {
        TryReadLoaderConfig(gameDirectory, out var config, out _);
        return config;
    }

    /// <summary>保存加载器配置(写回 doorstop_config.json, 无注释)。
    /// speedhackBaseSpeed 限制在 [1,100]: 加载器不支持减速, 小于 1.0 的值写了也会被忽略。</summary>
    public void SaveLoaderConfig(string gameDirectory, LoaderConfig config)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (config.SpeedhackBaseSpeed < MinBaseSpeed || config.SpeedhackBaseSpeed > MaxBaseSpeed)
            throw new InvalidOperationException(
                $"变速基础倍率必须在 [{MinBaseSpeed:0.#},{MaxBaseSpeed:0.#}] 之间（1.0 = 正常速度，不支持减速）。");
        if (config.DebugWaitTimeoutSec < MinTimeoutSec || config.DebugWaitTimeoutSec > MaxTimeoutSec)
            throw new InvalidOperationException(
                $"等待调试器超时必须在 [{MinTimeoutSec},{MaxTimeoutSec}] 秒之间。");
        if (config.NativeWatchdogTimeoutSec < MinTimeoutSec || config.NativeWatchdogTimeoutSec > MaxTimeoutSec)
            throw new InvalidOperationException(
                $"看门狗判定时间必须在 [{MinTimeoutSec},{MaxTimeoutSec}] 秒之间。");

        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["enabled"] = config.Enabled,
            ["useManagedBootstrap"] = config.UseManagedBootstrap,
            ["bootstrapAssembly"] = config.BootstrapAssembly,
            ["bootstrapType"] = config.BootstrapType,
            ["bootstrapMethod"] = config.BootstrapMethod,
            ["gameAssemblyTimeoutSec"] = config.GameAssemblyTimeoutSec,
            ["domainTimeoutSec"] = config.DomainTimeoutSec,
            ["hybridclrTimeoutSec"] = config.HybridclrTimeoutSec,
            ["consoleEnabled"] = config.ConsoleEnabled,
            ["consoleTopmost"] = config.ConsoleTopmost,
            ["debugShell"] = config.DebugShell,
            ["forwardActivityLog"] = config.ForwardActivityLog,
            ["speedhackBaseSpeed"] = config.SpeedhackBaseSpeed,
            ["speedControlEnabled"] = config.SpeedControlEnabled,
            // 原生窗口置前 / 音频控制通道(加载器功能): 同上, 白名单里漏掉就会在保存时把用户关掉的通道
            // 静默恢复成默认值(true)。
            ["windowControlEnabled"] = config.WindowControlEnabled,
            ["audioControlEnabled"] = config.AudioControlEnabled,
            // Steam 绕过(加载器功能): 必须显式写出 —— 本方法是白名单字典, 漏掉的键会在保存时被删除,
            // 于是用户关掉过的主开关会被静默恢复成默认值(true)。
            ["steamBypassEnabled"] = config.SteamBypassEnabled,
            ["steamBypassRestartCheck"] = config.SteamBypassRestartCheck,
            ["steamBypassMatchmaking"] = config.SteamBypassMatchmaking,
            ["steamBypassLobbyQuery"] = config.SteamBypassLobbyQuery,
            ["steamBypassLobbyHasValue"] = config.SteamBypassLobbyHasValue,
            ["steamBypassLobbyMethods"] = config.SteamBypassLobbyMethods,
            ["steamBypassTaskCtorMode"] = config.SteamBypassTaskCtorMode,
            ["tapTapBypassEnabled"] = config.TapTapBypassEnabled,
            ["tapTapBypassRestartCheck"] = config.TapTapBypassRestartCheck,
            ["tapTapBypassLogin"] = config.TapTapBypassLogin,
            ["sdkVersion"] = config.SdkVersion,
            // 多 profile(加载器功能): 同上, 白名单里漏掉就会在保存时把用户选的 profile 静默清空。
            ["profile"] = config.Profile,
            // 多 profile 窗口标题开关: 同上, 漏掉会在保存时把用户关掉的开关静默恢复成 true。
            ["profileWindowTitle"] = config.ProfileWindowTitle,
            // 排查类(加载器原生层): 崩溃转储 / 卡死看门狗 / 日志级别 / 等待调试器。
            // 同样是白名单字典, 漏键 = 保存时删掉 = 用户设置被静默重置成默认值。
            ["crashDumpEnabled"] = config.CrashDumpEnabled,
            ["crashDumpFullMemory"] = config.CrashDumpFullMemory,
            ["nativeWatchdogEnabled"] = config.NativeWatchdogEnabled,
            ["nativeWatchdogTimeoutSec"] = config.NativeWatchdogTimeoutSec,
            ["logLevel"] = NormalizeLogLevel(config.LogLevel),
            ["debugWaitForDebugger"] = config.DebugWaitForDebugger,
            ["debugWaitTimeoutSec"] = config.DebugWaitTimeoutSec
        }, new JsonSerializerOptions { WriteIndented = true });
        WriteAllBytesProtected(LoaderConfigPath(gameDirectory), System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>首页「启动方式」单选框要写的那一组键 —— 它们是**一套**，必须同开同关（见
    /// <see cref="SetSteamBypassProfile"/> 的说明：只关主开关不足以恢复原版行为）。</summary>
    public static readonly string[] SteamBypassProfileKeys =
    [
        "steamBypassEnabled",      // 阶段1: SteamManager.Awake -> no-op + RestartAppIfNecessary -> 0
        "steamBypassMatchmaking",  // 阶段2/3/方案B: CreateLobbyAsync / JoinLobbyAsync -> 已完成的空 Task
        "steamBypassLobbyQuery"    // 阶段5: LobbyQuery.RequestAsync -> 结果为空 Lobby[] 的已完成 Task
    ];

    /// <summary>把「Steam 绕过」这一套开关写成同一个值：开 = 绕过模式（不装 Steam 也能玩），关 = 完全原版行为。
    /// 注释、其它键、缩进统统原样保留（就地替换字面量，不整体重写）。
    ///
    /// ★ 为什么是**三个键一起**而不是只动主开关：加载器的这些 hook 都是**启动时无条件安装**的，
    ///   不检测 Steam 是否在运行（加载器 <c>docs/steam-bypass.md</c> 第 6 节第 4 条），而且各自只受自己的键控制：
    ///     · <c>steamBypassEnabled</c>   → 阶段1（拦 <c>SteamManager.Awake</c>、<c>SteamAPI_RestartAppIfNecessary</c>）
    ///     · <c>steamBypassMatchmaking</c> → 阶段2/3/方案B（建房 / 进房返回空 Task → <c>steamLobbyId</c> 恒为 0）
    ///     · <c>steamBypassLobbyQuery</c>  → 阶段5（房间列表查询返回空，源码注释写明"只受它自己控制"）
    ///   所以只把主开关关掉时，**从 Steam 启动仍然不会创建真大厅、房间列表照样返回空** ——
    ///   Steam 好友邀请 / 大厅分享进房依然是坏的。要做到"从 Steam 启动就用真大厅"，必须三个一起关。
    ///   （其余几个键不用动：<c>steamBypassRestartCheck</c> 只在主开关为真时有意义，
    ///   <c>steamBypassLobbyHasValue</c> / 方案B 都挂在 <c>steamBypassMatchmaking</c> 分支里，
    ///   <c>steamBypassLobbyMethods</c> 无论如何都必须保持 false。）
    ///
    /// 为什么不用 SaveLoaderConfig：那是"白名单字典整体重写"，会把模板里那一大段中文注释全丢掉 ——
    /// 首页切一下启动方式不该顺手清掉用户的配置注释。做法与 SyncLoaderConfigSdkVersion /
    /// SyncLoaderConfigSteamKeys 一致：文本就地替换，写完自检，任一步不满足就不写盘。
    ///
    /// 返回：确实让这一套变成了目标值返回 true；配置不存在（加载器还没装）或三个键一个都不在（老模板）
    /// 返回 false，由调用方提示用户（键缺失时加载器用它编译进去的默认值，安装/更新流程会把键补齐）。</summary>
    public bool SetSteamBypassProfile(string gameDirectory, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return false;

        var configPath = LoaderConfigPath(gameDirectory);
        if (!File.Exists(configPath)) return false;

        var bytes = File.ReadAllBytes(configPath);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var offset = hasBom ? 3 : 0;
        var text = System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

        // 用 MatchEvaluator（不是 "$1true"）：替换串里 "$1" 紧跟字母时容易被 .NET 读成别的分组引用。
        // ★ 跳过注释里的同名串：模板注释里就写着 steamBypassEnabled=true 这类说明，直接替换会把注释改花。
        //   跳过的风险由下面的自检兜住：某个真键若被误判成注释而没改到，解析出来的值就不等于目标值 → 不写盘。
        var literal = enabled ? "true" : "false";
        var updated = text;
        foreach (var key in SteamBypassProfileKeys)
        {
            updated = System.Text.RegularExpressions.Regex.Replace(
                updated,
                "(\"" + key + "\"\\s*:\\s*)(true|false)",
                match => IsInsideLineComment(updated, match.Index)
                    ? match.Value
                    : match.Groups[1].Value + literal);
        }

        // 写回前自检（同时用来判断"本来就对"）：
        //   · 改完必须仍能解析；
        //   · 配置里**出现过**的键，值必须都等于目标值（否则宁可不动 —— 绝不能把配置写坏）；
        //   · 一个键都没出现 = 老模板（没有这套开关）→ 返回 false，交给安装/更新流程补齐，也不动文件。
        var presentKeys = 0;
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(updated.TrimStart('\uFEFF')));
            foreach (var key in SteamBypassProfileKeys)
            {
                if (!doc.RootElement.TryGetProperty(key, out var value)) continue;
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
                if ((value.ValueKind == JsonValueKind.True) != enabled) return false;
                presentKeys++;
            }
        }
        catch
        {
            return false;
        }

        if (presentKeys == 0) return false;
        if (updated == text) return true;   // 本来就全是目标值：无事可做，算成功（前端不该弹"同步失败"）

        var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
        if (hasBom)
        {
            var withBom = new byte[encoded.Length + 3];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Array.Copy(encoded, 0, withBom, 3, encoded.Length);
            encoded = withBom;
        }

        WriteAllBytesProtected(configPath, encoded);
        return true;
    }

    /// <summary>TapTap 绕过开关集合。</summary>
    public static readonly string[] TapTapBypassProfileKeys =
    [
        "tapTapBypassEnabled",
        "tapTapBypassRestartCheck"
    ];

    /// <summary>把「TapTap 绕过」这一套开关写成同一个值：开 = 绕过模式，关 = 完全原版行为。
    /// 保留注释、其它键与缩进（就地替换字面量，不整体重写）。</summary>
    public bool SetTapTapBypassProfile(string gameDirectory, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return false;

        var configPath = LoaderConfigPath(gameDirectory);
        if (!File.Exists(configPath)) return false;

        var bytes = File.ReadAllBytes(configPath);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var offset = hasBom ? 3 : 0;
        var text = System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

        var literal = enabled ? "true" : "false";
        var updated = text;
        foreach (var key in TapTapBypassProfileKeys)
        {
            updated = System.Text.RegularExpressions.Regex.Replace(
                updated,
                "(\"" + key + "\"\\s*:\\s*)(true|false)",
                match => IsInsideLineComment(updated, match.Index)
                    ? match.Value
                    : match.Groups[1].Value + literal);
        }

        var presentKeys = 0;
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(updated.TrimStart('\uFEFF')));
            foreach (var key in TapTapBypassProfileKeys)
            {
                if (!doc.RootElement.TryGetProperty(key, out var value)) continue;
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
                if ((value.ValueKind == JsonValueKind.True) != enabled) return false;
                presentKeys++;
            }
        }
        catch
        {
            return false;
        }

        if (presentKeys == 0) return false;
        if (updated == text) return true;

        var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
        if (hasBom)
        {
            var withBom = new byte[encoded.Length + 3];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Array.Copy(encoded, 0, withBom, 3, encoded.Length);
            encoded = withBom;
        }

        WriteAllBytesProtected(configPath, encoded);
        return true;
    }

    /// <summary>把「多 profile」写进加载器配置（值 = 档案名，空串 = 游戏写自己的目录）。
    ///
    /// 就地替换字面量而不是整体重写：<see cref="SaveLoaderConfig"/> 是白名单字典，会把模板里那一大段
    /// 中文注释全丢掉，切换 profile 不该顺手清掉用户的配置注释。配置里没有该键时（老模板 / 用户手删）
    /// 插在 <c>sdkVersion</c> 之前（与随包模板顺序一致）；连 sdkVersion 都没有就插在对象末尾。
    /// 写完自检：必须仍能解析、且读出来的 profile 等于目标值，否则还原原文件并返回 false。
    ///
    /// 返回 false = 配置不存在（加载器没装）或写入自检失败，由调用方提示用户。</summary>
    public bool SetLoaderProfile(string gameDirectory, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return false;

        var configPath = LoaderConfigPath(gameDirectory);
        if (!File.Exists(configPath)) return false;

        var target = (profileName ?? "").Trim();
        var literal = JsonSerializer.Serialize(target);   // 带引号并正确转义

        var original = File.ReadAllBytes(configPath);
        var hasBom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
        var offset = hasBom ? 3 : 0;
        var text = System.Text.Encoding.UTF8.GetString(original, offset, original.Length - offset);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        // 1) 已经存在 profile 键：就地换值（跳过注释里的同名串）
        var updated = text;
        var replaced = false;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     text, "(\"profile\"\\s*:\\s*)([^,}\\r\\n]*)"))
        {
            if (IsInsideLineComment(text, match.Index)) continue;
            updated = text[..match.Index] + match.Groups[1].Value + literal +
                      text[(match.Index + match.Length)..];
            replaced = true;
            break;
        }

        // 2) 没有该键：插到 sdkVersion 之前（保持模板的字段顺序与缩进）
        if (!replaced)
        {
            var anchor = -1;
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, "\"sdkVersion\""))
            {
                if (IsInsideLineComment(text, match.Index)) continue;
                anchor = match.Index;
                break;
            }

            if (anchor >= 0)
            {
                var lineStart = text.LastIndexOf('\n', Math.Max(0, anchor - 1)) + 1;
                var indent = text[lineStart..anchor];
                if (indent.Trim().Length != 0) indent = "  ";
                updated = text[..lineStart] + indent + "\"profile\": " + literal + "," + newline + text[lineStart..];
            }
            else
            {
                var close = text.LastIndexOf('}');
                if (close < 0) return false;
                var head = text[..close].TrimEnd();
                var comma = head.EndsWith(',') || head.EndsWith('{') ? "" : ",";
                updated = head + comma + newline + "  \"profile\": " + literal + newline + text[close..];
            }
        }

        // 3) 自检：能解析、键存在且值等于目标
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(updated.TrimStart('\uFEFF')));
            if (!doc.RootElement.TryGetProperty("profile", out var value) || value.ValueKind != JsonValueKind.String)
                return false;
            if (!string.Equals(value.GetString(), target, StringComparison.Ordinal)) return false;
        }
        catch
        {
            return false;
        }

        if (updated == text) return true;   // 本来就是目标值

        var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
        if (hasBom)
        {
            var withBom = new byte[encoded.Length + 3];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Array.Copy(encoded, 0, withBom, 3, encoded.Length);
            encoded = withBom;
        }

        WriteAllBytesProtected(configPath, encoded);
        return true;
    }

    /// <summary>多 profile: 设置「游戏窗口标题后追加 profile 名」开关（doorstop_config.json 的
    /// <c>profileWindowTitle</c>）。
    ///
    /// 与 <see cref="SetLoaderProfile"/> 同一套做法：就地替换字面量，注释原样保留；键不存在时插在
    /// <c>sdkVersion</c> 之前（连它也没有就插在对象末尾）。写完自检 —— 必须仍能解析、且读出来的值
    /// 等于目标值，否则不写盘。
    ///
    /// 返回 false = 配置不存在（加载器没装）或写入自检失败，由调用方提示用户。</summary>
    public bool SetProfileWindowTitle(string gameDirectory, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return false;

        var configPath = LoaderConfigPath(gameDirectory);
        if (!File.Exists(configPath)) return false;

        var original = File.ReadAllBytes(configPath);
        var hasBom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
        var offset = hasBom ? 3 : 0;
        var text = System.Text.Encoding.UTF8.GetString(original, offset, original.Length - offset);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var literal = enabled ? "true" : "false";

        // 1) 已经存在该键：就地换值（跳过注释里的同名串）
        var updated = text;
        var replaced = false;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     text, "(\"profileWindowTitle\"\\s*:\\s*)(true|false)"))
        {
            if (IsInsideLineComment(text, match.Index)) continue;
            updated = text[..match.Index] + match.Groups[1].Value + literal +
                      text[(match.Index + match.Length)..];
            replaced = true;
            break;
        }

        // 2) 没有该键：插到 sdkVersion 之前（保持模板的字段顺序与缩进）
        if (!replaced)
        {
            var anchor = -1;
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, "\"sdkVersion\""))
            {
                if (IsInsideLineComment(text, match.Index)) continue;
                anchor = match.Index;
                break;
            }

            if (anchor >= 0)
            {
                var lineStart = text.LastIndexOf('\n', Math.Max(0, anchor - 1)) + 1;
                var indent = text[lineStart..anchor];
                if (indent.Trim().Length != 0) indent = "  ";
                updated = text[..lineStart] + indent + "\"profileWindowTitle\": " + literal + "," + newline + text[lineStart..];
            }
            else
            {
                var close = text.LastIndexOf('}');
                if (close < 0) return false;
                var head = text[..close].TrimEnd();
                var comma = head.EndsWith(',') || head.EndsWith('{') ? "" : ",";
                updated = head + comma + newline + "  \"profileWindowTitle\": " + literal + newline + text[close..];
            }
        }

        // 3) 自检：能解析、键存在且值等于目标
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(updated.TrimStart('\uFEFF')));
            if (!doc.RootElement.TryGetProperty("profileWindowTitle", out var value) ||
                value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return false;
            if ((value.ValueKind == JsonValueKind.True) != enabled) return false;
        }
        catch
        {
            return false;
        }

        if (updated == text) return true;   // 本来就是目标值

        var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
        if (hasBom)
        {
            var withBom = new byte[encoded.Length + 3];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Array.Copy(encoded, 0, withBom, 3, encoded.Length);
            encoded = withBom;
        }

        WriteAllBytesProtected(configPath, encoded);
        return true;
    }

}
