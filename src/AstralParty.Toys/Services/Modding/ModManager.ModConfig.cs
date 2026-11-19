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
    // ============================== mod 配置表单 (configs\{ModId}.json 键值编辑) ==============================

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
    /// 落盘 <b>两份</b>: <c>configs\{ModId}.json</c>(加载器优先读的位置) 与模组目录里的
    /// <c>config.json</c>(个别模组仍直接读这份), 保证两边内容一致。
    /// <c>_comments</c> 不来自表单，保存时从原文件原样带回并放在末尾；<c>other</c> 字段按 JSON 解析，
    /// 不会把嵌套对象/数组写成字符串。</summary>
    public void SaveModConfigFields(string gameDirectory, string fileName, List<ModConfigField> fields)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");

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
        WriteModConfigBytes(gameDirectory, fileName, System.Text.Encoding.UTF8.GetBytes(json));
    }

    private static readonly JsonSerializerOptions ModConfigWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 模组目录里那份 <c>config.json</c>(随包默认值, 也是个别模组直接读的位置);
    /// 模组没装(新布局的文件夹不存在)时返回 null, 避免凭空建一个没有 DLL 的模组目录。
    /// 停用的模组返回 <c>disabled_mods</c> 那份, 不把它搬回 <c>mods</c>。
    /// </summary>
    public string? ModPackagedConfigPathFor(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName)) return null;
        var modsDir = ModsRoot(gameDirectory);
        var (_, directoryName, root) = ResolveModPath(modsDir, fileName);
        if (directoryName is null) return null;   // 旧式平铺布局没有模组目录
        var directory = Path.Combine(root, directoryName);
        return Directory.Exists(directory) ? Path.Combine(directory, ModConfigFileName) : null;
    }

    /// <summary>
    /// mod 配置双写: <c>configs\{ModId}.json</c>(加载器优先读) 与模组目录里的 <c>config.json</c>
    /// (个别模组仍直接读这份) 各写一次, 字节完全相同。返回 configs 路径。
    /// </summary>
    public string WriteModConfigBytes(string gameDirectory, string fileName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");

        var path = ModConfigWritePath(gameDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteAllBytesProtected(path, bytes);

        var packaged = ModPackagedConfigPathFor(gameDirectory, fileName);
        if (packaged is not null && !string.Equals(packaged, path, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packaged)!);
            WriteAllBytesProtected(packaged, bytes);
        }
        return path;
    }

    /// <summary>mod 配置双写(文本; <paramref name="bom"/> 为 true 时带 UTF-8 BOM)。返回 configs 路径。</summary>
    public string WriteModConfigText(string gameDirectory, string fileName, string text, bool bom)
        => WriteModConfigBytes(gameDirectory, fileName, new System.Text.UTF8Encoding(bom).GetBytes(text));

    /// <summary>
    /// mod 配置文件路径(现行约定 <c>configs\{ModId}.json</c>)。只有 configs 下还没有、
    /// 而模组目录里那份仍在时返回旧路径(读取兼容; 一旦保存就会写进 configs);
    /// 两处都不存在则返回 null。
    /// </summary>
    public string? ModConfigPath(string gameDirectory, string fileName)
        => LocateModConfig(gameDirectory, fileName);

    /// <summary>读取 mod 配置内容(configs\{ModId}.json; 尚未迁到 configs 的旧安装读模组目录那份); 不存在返回空串。</summary>
    public string ReadModConfig(string gameDirectory, string fileName)
    {
        var path = ModConfigPath(gameDirectory, fileName);
        if (path is null) return "";
        try { return File.ReadAllText(path); } catch { return ""; }
    }

    /// <summary>
    /// 确保 mod 配置文件存在并返回其路径（不打开资源管理器，纯文件逻辑，方便测试）：
    /// 不存在则创建一份带注释的空配置；若只有模组目录里的那份 <c>mods\{ModId}\config.json</c>，
    /// 先把它复制到 <c>configs\{ModId}.json</c>；随后把模组目录那份补齐，让两处内容一致。
    /// </summary>
    public string EnsureModConfig(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");

        var path = ModConfigWritePath(gameDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            var packaged = ModPackagedConfigPathFor(gameDirectory, fileName);
            var migrated = false;
            if (packaged is not null && File.Exists(packaged))
            {
                try { File.Copy(packaged, path); migrated = true; }
                catch { migrated = false; }
            }
            if (!migrated)
                File.WriteAllText(path, "{\n  // 配置说明见对应 mod 文档; 公开字段(public field)才会被读取\n}\n");
        }

        // 模组目录里那份也补一份：个别模组直接按路径读它，两处内容必须一致。
        var packagedPath = ModPackagedConfigPathFor(gameDirectory, fileName);
        if (packagedPath is not null && !File.Exists(packagedPath))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(packagedPath)!);
                File.Copy(path, packagedPath);
            }
            catch
            {
                // 补不上不影响 configs 那份（加载器优先读它）
            }
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

    // ============================== 系统字体（FontMod） ==============================

    private static readonly JsonSerializerOptions FontStringOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 写入 FontMod 的字体配置。<b>只改这两个标量</b>：保留文件里的注释、空行和其它键
    /// （整份反序列化再序列化会把注释和 `1` / `1.0` 这类写法改掉）。
    /// 文件不存在时按 FontMod 的默认结构创建，避免写出一份只有字体名的空壳。
    /// </summary>
    public string SetFontModConfig(string gameDirectory, string fontFamily, bool applyToAllFonts)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("游戏目录无效。");

        // 双写 configs\FontMod.json 与模组目录里的 config.json; 文本从现有那份带过来,
        // 保证注释、空行与 1 / 1.0 这类写法不被重新序列化改掉。
        var readPath = ModConfigPath(gameDirectory, "FontMod.dll");

        var text = readPath is not null && File.Exists(readPath) ? File.ReadAllText(readPath) : "";
        if (string.IsNullOrWhiteSpace(text))
            text = "{\n  \"enabled\": true,\n  \"fontFamily\": \"\",\n  \"applyToAllFonts\": true\n}\n";

        text = SetJsonString(text, "fontFamily", fontFamily ?? "");
        text = SetJsonBool(text, "applyToAllFonts", applyToAllFonts);
        return WriteModConfigBytes(gameDirectory, "FontMod.dll", new System.Text.UTF8Encoding(false).GetBytes(text));
    }

    /// <summary>把 JSON 文本里某个字符串字段换成新值（找不到就追加），其余内容逐字保留。</summary>
    private static string SetJsonString(string json, string key, string value)
    {
        var literal = JsonSerializer.Serialize(value, FontStringOptions);
        var needle = "\"" + key + "\"";
        var keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex >= 0)
        {
            var colon = json.IndexOf(':', keyIndex + needle.Length);
            if (colon >= 0)
            {
                var i = colon + 1;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i < json.Length && json[i] == '"')
                {
                    var end = FindJsonStringEnd(json, i + 1);
                    if (end >= 0)
                        return json[..i] + literal + json[(end + 1)..];
                }
                var j = i;
                while (j < json.Length && json[j] != ',' && json[j] != '}') j++;
                return json[..i] + literal + json[j..];
            }
        }

        var insert = json.LastIndexOf('}');
        if (insert < 0) return "{\n  " + needle + ": " + literal + "\n}\n";
        var prefix = json[..insert].TrimEnd();
        var separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "" : ",";
        return prefix + separator + "\n  " + needle + ": " + literal + "\n" + json[insert..];
    }

    private static int FindJsonStringEnd(string json, int start)
    {
        for (var i = start; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '\\') { i++; continue; }
            if (c == '"') return i;
        }
        return -1;
    }
}
