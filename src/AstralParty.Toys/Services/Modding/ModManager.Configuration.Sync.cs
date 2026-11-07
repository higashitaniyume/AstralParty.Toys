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
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Speedhack;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    /// <summary>判断 <paramref name="index"/> 处是否落在 <c>//</c> 行注释里（这些配置只用行注释，没有块注释）。
    /// 就地替换时用它跳过注释里的同名串 —— 只改真正的键。</summary>
    private static bool IsInsideLineComment(string text, int index)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var comment = text.IndexOf("//", lineStart, StringComparison.Ordinal);
        return comment >= 0 && comment < index;
    }

    /// <summary>从配置文本里取 sdkVersion（容错：解析失败 / 没有该字段返回空串）。
    ///
    /// ★ 必须自行剥 UTF-8 BOM：本方法有两类调用方 —— File.ReadAllText（会剥 BOM）与
    /// ReadSdkVersionFromConfigBytes（Encoding.UTF8.GetString，**不剥** BOM）。带 BOM 的文本直接交给
    /// JsonDocument.Parse 会抛异常，于是内嵌模板的 sdkVersion 被读成空串，"把旧配置抬到随包 SDK 版本"
    /// 这条升级逻辑就静默失效（新版内置 mod 会被加载器判 SdkVersionTooNew 拒绝加载）。
    /// C++ 加载器侧的 jsonc.h 同样显式剥 BOM（记事本保存 UTF-8 会加 BOM），两边行为保持一致。</summary>
    private static string ParseSdkVersionFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(text.TrimStart('\uFEFF')));
            return doc.RootElement.TryGetProperty("sdkVersion", out var sv) && sv.ValueKind == JsonValueKind.String
                ? sv.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>从 doorstop_config.json 的字节里取 sdkVersion（容错，失败返回空串）。</summary>
    private static string ReadSdkVersionFromConfigBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return "";
        try
        {
            return ParseSdkVersionFromText(System.Text.Encoding.UTF8.GetString(bytes));
        }
        catch
        {
            return "";
        }
    }

    /// <summary>SemVer（主.次.修订）比较：a&gt;b 返回正数、相等 0、a&lt;b 负数；解析不出的段按 0 算。</summary>
    private static int CompareSemVer(string a, string b)
    {
        static int[] Parts(string value)
        {
            var parts = new int[3];
            var texts = (value ?? "").Split('.');
            for (var i = 0; i < 3 && i < texts.Length; i++)
                int.TryParse(texts[i].Trim(), out parts[i]);
            return parts;
        }

        var left = Parts(a);
        var right = Parts(b);
        for (var i = 0; i < 3; i++)
        {
            if (left[i] != right[i]) return left[i] > right[i] ? 1 : -1;
        }
        return 0;
    }

    /// <summary>读取当前加载器实际用于 mod 版本校验的 SDK 版本。
    ///
    /// 优先取 <c>doorstop_config.json</c> 的 <c>sdkVersion</c>（加载器真正读取并用于拒绝 mod 的值）；
    /// 配置缺失或没有该字段时回退到 <c>sdk/CesiumLoader.SDK.dll</c> 的文件版本。
    /// 两者都读不到（例如还没安装加载器）返回 false，调用方应跳过检查。</summary>
    public static bool TryReadInstalledSdkVersion(string gameDirectory, out string version)
    {
        version = "";
        try
        {
            if (string.IsNullOrWhiteSpace(gameDirectory)) return false;

            var configPath = Path.Combine(gameDirectory, LoaderFolderName, ConfigFileName);
            if (File.Exists(configPath))
            {
                var fromConfig = ParseSdkVersionFromText(File.ReadAllText(configPath));
                if (!string.IsNullOrEmpty(fromConfig))
                {
                    version = fromConfig;
                    return true;
                }
            }

            var sdkDll = Path.Combine(gameDirectory, LoaderFolderName, SdkFolderName, SdkDllName);
            if (File.Exists(sdkDll))
            {
                var normalized = NormalizeSdkVersion(FileVersionInfo.GetVersionInfo(sdkDll).FileVersion);
                if (!string.IsNullOrEmpty(normalized))
                {
                    version = normalized;
                    return true;
                }
            }
        }
        catch
        {
            // 读不到就当未知：不能因为配置损坏阻止所有模组安装。
        }
        return false;
    }

    /// <summary>mod 声明的 SDK 版本是否高于当前加载器可提供的版本（空值不拦）。</summary>
    public static bool IsSdkIncompatible(string? requiredSdk, string? installedSdk)
        => !string.IsNullOrWhiteSpace(requiredSdk)
           && !string.IsNullOrWhiteSpace(installedSdk)
           && CompareSemVer(requiredSdk!, installedSdk!) > 0;

    /// <summary>给安装结果界面用的 SDK 版本不兼容说明。</summary>
    public static string DescribeSdkMismatch(string modName, string requiredSdk, string installedSdk)
        => $"{modName} 要求 SDK {requiredSdk}，当前加载器为 {installedSdk}。请先更新加载器，或安装支持当前 SDK 的旧版本。";

    /// <summary>把 <c>3.2.0.0</c> 这类文件版本规范化成三段；无法解析时保留原值。</summary>
    private static string NormalizeSdkVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = value.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length < 3) return trimmed;
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(parts[i], out _)) return trimmed;
        return parts[0] + "." + parts[1] + "." + parts[2];
    }

    /// <summary>把已有 doorstop_config.json 的 sdkVersion 抬到随包版本（只升不降），其余字段与注释原样保留。
    ///
    /// 为什么必须做：加载器用配置里的 sdkVersion 校验 mod 声明的 SdkVersion，声明更高就直接拒绝加载该 mod
    /// （见 CesiumLoader 的 modmeta）。安装/更新会把内置 mod 覆盖成随包版本，而配置以前是"已存在就不动" ——
    /// 于是停在旧 SDK 号的老配置会让新版内置 mod 全部拒绝加载（mod 全失效）。这里只改这一个字段：
    /// 用户的变速倍率、开关等设置一律不动；配置里本来没有该字段时也不添加（加载器会用它自己编译进去的默认值）。
    /// 同步是尽力而为：失败也不影响安装本身。</summary>
    private static void SyncLoaderConfigSdkVersion(string configPath, string bundleSdkVersion)
    {
        try
        {
            if (string.IsNullOrEmpty(bundleSdkVersion) || !File.Exists(configPath)) return;

            var bytes = File.ReadAllBytes(configPath);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var offset = hasBom ? 3 : 0;
            var text = System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

            var current = ParseSdkVersionFromText(text);
            if (current.Length == 0) return;                              // 没有该字段：加载器用自身默认值，不必干预
            if (CompareSemVer(bundleSdkVersion, current) <= 0) return;    // 同版或更新：不降级

            // 用 MatchEvaluator 而不是 "$1...(版本号)...$2"：替换串里 "$1" 紧跟版本号首位数字时
            // .NET 会把它读成 $12 这种两位分组引用, 写坏 JSON。
            var updated = System.Text.RegularExpressions.Regex.Replace(
                text,
                "(\"sdkVersion\"\\s*:\\s*\")[^\"]*(\")",
                match => match.Groups[1].Value + bundleSdkVersion + match.Groups[2].Value);
            if (updated == text) return;
            // 写回前先自检: 改完必须还能解析出目标版本, 否则宁可不动(绝不能把配置写坏)
            if (!string.Equals(ParseSdkVersionFromText(updated), bundleSdkVersion, StringComparison.Ordinal)) return;

            var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
            if (hasBom)
            {
                var withBom = new byte[encoded.Length + 3];
                withBom[0] = 0xEF;
                withBom[1] = 0xBB;
                withBom[2] = 0xBF;
                Array.Copy(encoded, 0, withBom, 3, encoded.Length);
                encoded = withBom;
            }
            WriteAllBytesProtected(configPath, encoded);
        }
        catch
        {
            // 尽力而为：失败只是回到旧行为（mod 可能被拒），不能让安装整体失败
        }
    }

    /// <summary>把缺失的 Steam 绕过开关补进已有的 doorstop_config.json（保留注释与用户已有值）。
    ///
    /// ★ 这不是"让功能生效"——加载器读配置时缺失的键用**编译进去的默认值**，而这一组的默认值就是全开，
    ///   所以老配置（1.6 KB 那版模板、根本没有这些键）配新加载器照样能绕过 Steam。补齐是为了两件事：
    ///     1) 配置自文档化：用户在 Toys 之外的编辑器里也能看到有哪些开关、分别管什么；
    ///     2) 防回退：**旧版 Toys** 的 SaveLoaderConfig 是白名单字典、不认识这些键 —— 用户在旧版里
    ///        保存一次「加载器设置」就会把它们删掉，于是特意关掉的绕过被静默恢复成默认值（全开）。
    ///        新版装过一次之后键就在文件里了，此后旧版删掉也会被这里补回来。
    ///
    /// 安全性：只**追加**缺失的键（已有的一律不动）；文本插入而非重写，注释/顺序原样保留；
    /// 改完先自检（仍能解析 + 原有键值一字未改 + 新键都在），任何一步不满足就**不写盘**。
    /// 尽力而为：失败只是少补几个键，绝不能让安装整体失败。</summary>
    private static void SyncLoaderConfigSteamKeys(string configPath)
    {
        try
        {
            if (!File.Exists(configPath)) return;

            var bytes = File.ReadAllBytes(configPath);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var offset = hasBom ? 3 : 0;
            var text = System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

            Func<string, JsonDocument?> tryParse = value =>
            {
                try { return JsonDocument.Parse(StripJsonComments(value.TrimStart('\uFEFF'))); }
                catch { return null; }
            };

            using var before = tryParse(text);
            if (before is null || before.RootElement.ValueKind != JsonValueKind.Object) return;

            var existing = before.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);

            // 顺序与加载器配置模板一致；Literal 是写进 JSON 的字面量。
            (string Name, string Literal, string Comment)[] catalog =
            [
                ("steamBypassEnabled", "true", "Steam 绕过主开关: 无 Steam 启动游戏(拦截 SteamManager.Awake)"),
                ("steamBypassRestartCheck", "true", "附加保险: 让 SteamAPI_RestartAppIfNecessary 恒返回 0"),
                ("steamBypassMatchmaking", "true", "大厅匹配绕过: 修「点创建/加入房间没反应」"),
                ("steamBypassLobbyQuery", "true", "退房兜底: 修退房/被踢时 await 之后的 NRE"),
                ("steamBypassLobbyHasValue", "true", "阶段3(实测作废、hook 从未命中; 保留无害)"),
                ("steamBypassTaskCtorMode", "\"auto\"", "方案C 调用方式: auto / direct / invoke"),
                ("steamBypassLobbyMethods", "false", "方案B 备用安全网 —— 实测开启会导致启动崩溃, 必须保持 false")
            ];
            var missing = catalog.Where(item => !existing.Contains(item.Name)).ToArray();
            if (missing.Length == 0) return;

            // 插入到最后一个 '}' 之前（JSON 成员顺序无所谓），只动这一处。
            var closeIndex = text.LastIndexOf('}');
            if (closeIndex < 0) return;
            var head = text.Substring(0, closeIndex);
            var tail = text.Substring(closeIndex);

            // 前一个成员若没有尾逗号（模板里 sdkVersion 就是最后一个且无逗号），要补一个，否则 JSON 非法。
            // 逗号必须补在**行尾**而不是行尾空白之后 —— 否则会写出 "…2.2.0"\n  , 这种难看的怪格式。
            var headTrimmed = head.TrimEnd();
            var trailing = head.Substring(headTrimmed.Length);   // 原样的行尾空白(换行 + 可能的缩进)
            var needComma = headTrimmed.Length > 0 && headTrimmed[^1] is not (',' or '{');

            var eol = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = new List<string>
            {
                "  // ---- Steam 绕过（加载器原生功能, 非 mod）: 不装 Steam 也能启动游戏, 建房/退房正常 ----",
                "  // 键缺失时加载器用编译进去的默认值(= 全开), 这里补齐只为让配置自文档化。详见 docs/steam-bypass.md",
            };
            foreach (var item in missing)
            {
                lines.Add("  // " + item.Comment);
                lines.Add("  \"" + item.Name + "\": " + item.Literal + ",");
            }
            lines[^1] = lines[^1].TrimEnd(',');   // 我们这组是这个对象里的最后一批成员

            var updated = headTrimmed + (needComma ? "," : "") + eol + eol
                        + string.Join(eol, lines) + (trailing.Length > 0 ? trailing : eol) + tail;

            // 自检: 改完必须仍能解析、原有键值一字未改、新键都在；否则宁可不动(绝不能把配置写坏)。
            using var after = tryParse(updated);
            if (after is null || after.RootElement.ValueKind != JsonValueKind.Object) return;
            var afterValues = after.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetRawText(), StringComparer.Ordinal);
            foreach (var property in before.RootElement.EnumerateObject())
            {
                if (!afterValues.TryGetValue(property.Name, out var raw) || raw != property.Value.GetRawText()) return;
            }
            foreach (var item in missing)
            {
                if (!afterValues.ContainsKey(item.Name)) return;
            }

            var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
            if (hasBom)
            {
                var withBom = new byte[encoded.Length + 3];
                withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
                Array.Copy(encoded, 0, withBom, 3, encoded.Length);
                encoded = withBom;
            }
            WriteAllBytesProtected(configPath, encoded);
        }
        catch
        {
            // 尽力而为：少补几个键不影响任何功能（加载器有默认值）
        }
    }

    /// <summary>把缺失的 <c>profileWindowTitle</c> 补进已有的 doorstop_config.json（保留注释与用户已有值）。
    ///
    /// 与 <see cref="SyncLoaderConfigSteamKeys"/> 同样的理由：加载器读不到该键时用编译进去的默认值（开启），
    /// 所以老配置照样工作；补齐是为了让配置自文档化，并防止旧版 Toys 的白名单保存把它删掉
    /// （用户特意关掉的开关不会在旧版里静默恢复成开）。只追加缺失的键，改完自检，任何一步失败都不写盘。</summary>
    private static void SyncLoaderConfigProfileWindowTitle(string configPath)
    {
        try
        {
            if (!File.Exists(configPath)) return;

            var bytes = File.ReadAllBytes(configPath);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var offset = hasBom ? 3 : 0;
            var text = System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

            Func<string, JsonDocument?> tryParse = value =>
            {
                try { return JsonDocument.Parse(StripJsonComments(value.TrimStart('\uFEFF'))); }
                catch { return null; }
            };

            using var before = tryParse(text);
            if (before is null || before.RootElement.ValueKind != JsonValueKind.Object) return;
            if (before.RootElement.TryGetProperty("profileWindowTitle", out _)) return;

            // 插到 sdkVersion 之前（与随包模板顺序一致）；连 sdkVersion 都没有就插在对象末尾。
            var eol = text.Contains("\r\n") ? "\r\n" : "\n";
            var comment = "  // 多 profile: 在游戏窗口标题后追加 profile 名, 方便区分多开实例(默认开, 只影响标题显示)";
            var member = "  \"profileWindowTitle\": true";

            var updated = text;
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
                updated = text[..lineStart] + comment + eol + indent + "\"profileWindowTitle\": true," + eol
                          + text[lineStart..];
            }
            else
            {
                var close = text.LastIndexOf('}');
                if (close < 0) return;
                var head = text[..close].TrimEnd();
                var comma = head.EndsWith(',') || head.EndsWith('{') ? "" : ",";
                updated = head + comma + eol + eol + comment + eol + member + eol + text[close..];
            }

            // 自检: 改完必须仍能解析、原有键值一字未改、新键存在；否则宁可不动。
            using var after = tryParse(updated);
            if (after is null || after.RootElement.ValueKind != JsonValueKind.Object) return;
            var afterValues = after.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetRawText(), StringComparer.Ordinal);
            foreach (var property in before.RootElement.EnumerateObject())
            {
                if (!afterValues.TryGetValue(property.Name, out var raw) || raw != property.Value.GetRawText()) return;
            }
            if (!afterValues.ContainsKey("profileWindowTitle")) return;

            var encoded = System.Text.Encoding.UTF8.GetBytes(updated);
            if (hasBom)
            {
                var withBom = new byte[encoded.Length + 3];
                withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
                Array.Copy(encoded, 0, withBom, 3, encoded.Length);
                encoded = withBom;
            }
            WriteAllBytesProtected(configPath, encoded);
        }
        catch
        {
            // 尽力而为：少补一个键不影响功能（加载器有默认值）
        }
    }

}
