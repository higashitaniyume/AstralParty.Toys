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
    // ============================== mod 配置表单 (mods\{ModId}\config.json 键值编辑) ==============================

    /// <summary>读取 mod 配置为表单字段列表(按 JSON 值类型分类); 文件不存在返回空列表。
    /// 每个字段带 name + kind(bool/number/string/key) + 当前值。
    /// kind=key 的是键位绑定(名字以 Key 结尾, 如 toggleKey/speedUpKey): 值本身是字符串,
    /// 但 UI 用"按一下要设的键"来采集(含鼠标侧键), 而不是让用户手打 KeyCode 名字。</summary>
    public List<ModConfigField> ReadModConfigFields(string gameDirectory, string fileName, string? language = null)
    {
        var list = new List<ModConfigField>();
        var text = ReadModConfig(gameDirectory, fileName);
        if (string.IsNullOrWhiteSpace(text)) return list;
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(text));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return list;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // _comments 是元数据，不是可编辑的配置项。
                if (string.Equals(prop.Name, ModConfigComments.Key, StringComparison.Ordinal)) continue;
                var field = new ModConfigField
                {
                    Name = prop.Name,
                    Comment = ModConfigComments.Resolve(doc.RootElement, prop.Name, language)
                };
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.True:
                        field.Kind = "bool"; field.BoolValue = true; break;
                    case JsonValueKind.False:
                        field.Kind = "bool"; field.BoolValue = false; break;
                    case JsonValueKind.Number:
                        field.Kind = "number";
                        field.NumberValue = prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble();
                        break;
                    case JsonValueKind.String:
                        field.Kind = IsKeyBindingField(prop.Name) ? "key" : "string";
                        field.StringValue = prop.Value.GetString() ?? "";
                        break;
                    default:
                        field.Kind = "other"; field.StringValue = prop.Value.GetRawText(); break;
                }
                list.Add(field);
            }
        }
        catch
        {
            // 损坏配置按空处理(用户可重新编辑)
        }
        return list;
    }

    /// <summary>
    /// 字段名是否是键位绑定。约定: 以 <c>Key</c> 结尾(toggleKey/speedUpKey/resetKey/dumpAllKey...)。
    ///
    /// 这只是"UI 用哪种控件"的提示, 认错了也不会坏数据 —— 值仍然是字符串, 保存路径完全一样,
    /// 用户随时可以退回文本框手写 KeyCode 名字(或点"高级编辑"直接改 JSON)。
    /// </summary>
    private static bool IsKeyBindingField(string name)
        => name.Length > 3 && name.EndsWith("Key", StringComparison.OrdinalIgnoreCase);

    /// <summary>按表单字段保存 mod 配置(序列化回 JSON; 类型由字段 Kind 决定)。
    /// 落盘位置与 mod 自己读的完全一致: <c>mods\{ModId}\config.json</c>(DLL 旁边)。
    /// <c>_comments</c> 不来自表单，保存时从原文件原样带回并放在末尾；<c>other</c> 字段按 JSON 解析，
    /// 不会把嵌套对象/数组写成字符串。</summary>
    public void SaveModConfigFields(string gameDirectory, string fileName, List<ModConfigField> fields)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");
        var path = ModConfigWritePath(gameDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        JsonNode? comments = null;
        var existing = ReadModConfig(gameDirectory, fileName);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            try
            {
                using var doc = JsonDocument.Parse(StripJsonComments(existing));
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty(ModConfigComments.Key, out var node))
                {
                    comments = JsonNode.Parse(node.GetRawText());
                }
            }
            catch
            {
                // 旧文件损坏时仍按表单内容保存；说明丢失比整份配置写不进去更容易恢复。
            }
        }

        var root = new JsonObject();
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name)) continue;
            // 保留键不接受前端覆盖，避免把说明写坏。
            if (string.Equals(field.Name, ModConfigComments.Key, StringComparison.Ordinal)) continue;
            switch (field.Kind)
            {
                case "bool": root[field.Name] = JsonValue.Create(field.BoolValue); break;
                case "number": root[field.Name] = JsonValue.Create(field.NumberValue); break;
                // 键位和普通字符串一样落盘(kind=key 只是 UI 的"用捕获控件"提示)
                case "key": root[field.Name] = JsonValue.Create(field.StringValue); break;
                case "other":
                    try
                    {
                        root[field.Name] = JsonNode.Parse(field.StringValue);
                    }
                    catch (JsonException ex)
                    {
                        throw new InvalidOperationException(
                            $"字段 {field.Name} 的 JSON 无法解析，请检查括号、引号与逗号：{ex.Message}", ex);
                    }
                    break;
                default: root[field.Name] = JsonValue.Create(field.StringValue); break;
            }
        }
        if (comments != null) root[ModConfigComments.Key] = comments;

        var json = root.ToJsonString(ModConfigWriteOptions);
        WriteAllBytesProtected(path, System.Text.Encoding.UTF8.GetBytes(json));
    }

    private static readonly JsonSerializerOptions ModConfigWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>剥掉 JSON 里的 // 与 /* */ 注释(加载器配置文件带注释, System.Text.Json 不认)。</summary>
    private static string StripJsonComments(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        var sb = new System.Text.StringBuilder(json.Length);
        var inString = false;
        var i = 0;
        while (i < json.Length)
        {
            var c = json[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < json.Length) { sb.Append(json[i + 1]); i += 2; continue; }
                if (c == '"') inString = false;
                i++;
                continue;
            }
            if (c == '"') { inString = true; sb.Append(c); i++; continue; }
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                while (i < json.Length && json[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++;
                i += 2;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// mod 配置文件名(与 mod 的 DLL 同目录)。SDK 的 <c>ModConfig</c> 就是按
    /// <c>modDirectory\config.json</c> 落盘的, 所以这里必须同名同目录 —— 否则 mod 读的
    /// 和 Toys 写的是两个文件, 界面上改了配置游戏里根本不生效。
    /// </summary>
    private const string ModConfigFileName = "config.json";

    /// <summary>mod 配置的落盘位置(mods\{ModId}\config.json; 不要求已存在)。</summary>
    private static string ModConfigWritePath(string gameDirectory, string fileName)
    {
        var modsDir = ModsRoot(gameDirectory);
        var (_, directoryName, root) = ResolveModPath(modsDir, fileName);
        var folder = directoryName ?? Path.GetFileNameWithoutExtension(fileName);
        return Path.Combine(root, folder, ModConfigFileName);
    }

    /// <summary>旧版集中存放位置(AstralParty_ModLoader\configs\{modName}.json); 仅用于读取兼容。</summary>
    private static string LegacyModConfigPath(string gameDirectory, string fileName)
    {
        return Path.Combine(gameDirectory, LoaderFolderName, "configs",
            Path.GetFileNameWithoutExtension(fileName) + ".json");
    }

    /// <summary>
    /// mod 配置文件路径(现行约定 <c>mods\{ModId}\config.json</c>, 与 mod 的 DLL 同目录)。
    /// 只有旧版 <c>configs\{modName}.json</c> 存在时返回旧路径(读取兼容; 一旦保存或"高级编辑"
    /// 就会迁到新位置); 两处都不存在则返回 null。
    /// </summary>
    public string? ModConfigPath(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName)) return null;
        var path = ModConfigWritePath(gameDirectory, fileName);
        if (File.Exists(path)) return path;
        var legacy = LegacyModConfigPath(gameDirectory, fileName);
        return File.Exists(legacy) ? legacy : null;
    }

    /// <summary>读取 mod 配置内容(mods\{ModId}\config.json; 尚未迁移的旧配置也能读到); 不存在返回空串。</summary>
    public string ReadModConfig(string gameDirectory, string fileName)
    {
        var path = ModConfigPath(gameDirectory, fileName);
        if (path is null) return "";
        try { return File.ReadAllText(path); } catch { return ""; }
    }

    /// <summary>
    /// 确保 mod 配置文件存在并返回其路径（不打开资源管理器，纯文件逻辑，方便测试）：
    /// 不存在则创建一份带注释的空配置；若只有旧版 <c>configs\{modName}.json</c>，
    /// 先把它复制到 <c>mods\{ModId}\config.json</c>（否则用户改的是一份 mod 根本不会读的文件）。
    /// </summary>
    public string EnsureModConfig(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");

        var path = ModConfigWritePath(gameDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            var legacy = LegacyModConfigPath(gameDirectory, fileName);
            var migrated = false;
            if (File.Exists(legacy))
            {
                try { File.Copy(legacy, path); migrated = true; }
                catch { migrated = false; }
            }
            if (!migrated)
                File.WriteAllText(path, "{\n  // 配置说明见对应 mod 文档; 公开字段(public field)才会被读取\n}\n");
        }

        return path;
    }

    /// <summary>用系统默认编辑器 / 资源管理器打开 mod 配置文件；不存在则先创建（见 <see cref="EnsureModConfig"/>）。</summary>
    public string OpenModConfig(string gameDirectory, string fileName)
    {
        var path = EnsureModConfig(gameDirectory, fileName);
        OpenInExplorer(path);
        return path;
    }

    /// <summary>在 JSON 文本里设置一个布尔字段(保留其它字段与格式; 无该字段则追加)。极简实现, 不做完整 JSON 解析。</summary>
    private static string SetJsonBool(string json, string key, bool value)
    {
        var needle = "\"" + key + "\"";
        var colon = json.IndexOf(needle, StringComparison.Ordinal);
        if (colon >= 0)
        {
            // 找到字段, 替换其值(直到 , 或 })
            var valueStart = json.IndexOf(':', colon) + 1;
            var i = valueStart;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n')) i++;
            var j = i;
            while (j < json.Length && json[j] != ',' && json[j] != '}') j++;
            return json[..i] + (value ? "true" : "false") + json[j..];
        }
        // 追加: 插到最后一个 } 前
        var insert = json.LastIndexOf('}');
        if (insert < 0) return json;
        var suffix = json[insert..];
        var prefix = json[..insert].TrimEnd();
        var separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "" : ",";
        return prefix + separator + "\n  \"" + key + "\": " + (value ? "true" : "false") + "\n" + suffix;
    }

    /// <summary>把单个 mod DLL 复制进游戏 mods 目录（新布局: mods\{Name}\{Name}.dll）。
    /// 带 sidecar / 配置 / 资源的完整模组包（ZIP）走 <see cref="ImportModPackage"/>。</summary>
    public ModEntryInfo ImportMod(string gameDirectory, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException("游戏目录不存在，请先安装加载器或选择游戏目录。");
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("找不到要导入的 mod 文件。");
        if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("整个模组包（.zip）请用 ImportModPackage 导入，这里只接收单个 DLL。");
        if (!sourcePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("mod 必须是 .dll 文件（模组包请用 .zip）。");

        var modsDir = ModsRoot(gameDirectory);

        var fileName = Path.GetFileName(sourcePath);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        // 每 mod 一个文件夹; 若已有同名 mod(文件夹或平铺), 覆盖其 DLL（停用的 mod 也写回 disabled_mods）
        var (existing, existingDir, root) = ResolveModPath(modsDir, fileName);
        var dirName = existingDir ?? stem;
        var modFolder = Path.Combine(root, dirName);
        var bytes = File.ReadAllBytes(sourcePath);
        using var retained = BeginRetainedOperation(gameDirectory, "import-mod", bytes);
        Directory.CreateDirectory(modFolder);
        var target = Path.Combine(modFolder, fileName);
        WriteAllBytesProtected(target, bytes);
        retained.Complete();

        return new ModEntryInfo
        {
            Name = stem,
            FileName = fileName,
            DirectoryName = dirName,
            SizeBytes = new FileInfo(target).Length,
            ModifiedUtc = File.GetLastWriteTimeUtc(target)
        };
    }

}
