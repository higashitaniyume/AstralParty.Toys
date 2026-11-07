using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Skins;

public sealed partial class SkinManager
{
    /// <summary>安全导入皮肤 ZIP 压缩包，自动识别各种层级结构。不设文件数量或总大小上限，只受磁盘可用空间约束。</summary>
    public string ImportSkinZip(string gameDirectory, string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("皮肤压缩包不存在", zipPath);

        var skinsDir = GetSkinsDirectory(gameDirectory);
        Directory.CreateDirectory(skinsDir);

        using var archive = CompatZipArchive.OpenRead(zipPath);
        if (archive.Entries.Count == 0)
            throw new InvalidOperationException("压缩包内没有任何文件。");

        // 解压前只确认目标盘放得下；不再按固定 MB 数或单文件大小挡包
        long declaredBytes = 0;
        foreach (var declaredEntry in archive.Entries) declaredBytes += declaredEntry.Length;
        ImportLimits.EnsureFreeSpace(skinsDir, declaredBytes);

        // 校验路径安全性，防止路径遍历攻击
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Contains(':')
                || normalized.Split('/').Any(seg => seg is ".." or "."))
            {
                throw new InvalidOperationException("压缩包包含不安全的相对路径。");
            }
        }

        // 分析 ZIP 内的结构形态
        // 形态 A: 完整路径 AstralParty_ModLoader/skins/<skinName>/... (如 cesium-default-skins.zip)
        var hasLoaderSkinsPath = archive.Entries.Any(e =>
            e.FullName.Replace('\\', '/').StartsWith("AstralParty_ModLoader/skins/", StringComparison.OrdinalIgnoreCase));

        if (hasLoaderSkinsPath)
        {
            var importedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var rel = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrEmpty(entry.Name)) continue; // 目录项

                if (rel.StartsWith("AstralParty_ModLoader/skins/", StringComparison.OrdinalIgnoreCase))
                {
                    var subPath = rel["AstralParty_ModLoader/skins/".Length..];
                    var skinName = subPath.Split('/')[0];
                    if (!string.IsNullOrEmpty(skinName)) importedNames.Add(skinName);

                    var destFile = Path.Combine(skinsDir, subPath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    entry.ExtractToFile(destFile, overwrite: true);
                }
            }
            return importedNames.FirstOrDefault() ?? "default";
        }

        // 形态 B: 单层或多层子目录 <skinName>/...
        var topDirectories = archive.Entries
            .Select(e => e.FullName.Replace('\\', '/').Split('/')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (topDirectories.Count == 1 && !string.IsNullOrEmpty(topDirectories[0]) &&
            archive.Entries.Any(e => e.FullName.Replace('\\', '/').Contains('/')))
        {
            var rootDirName = topDirectories[0];
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var rel = entry.FullName.Replace('\\', '/');
                var destFile = Path.Combine(skinsDir, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                entry.ExtractToFile(destFile, overwrite: true);
            }
            return rootDirName;
        }

        // 形态 C: 根目录平铺文件，以 ZIP 文件名作为新方案名称
        var defaultName = Regex.Replace(Path.GetFileNameWithoutExtension(zipPath), @"[^a-zA-Z0-9_\-\u4e00-\u9fa5]", "_");
        if (string.IsNullOrWhiteSpace(defaultName)) defaultName = "custom_skin_" + DateTime.Now.ToString("yyyyMMdd");

        var targetSkinDir = Path.Combine(skinsDir, defaultName);
        Directory.CreateDirectory(targetSkinDir);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var relative = entry.FullName.Replace('\\', '/');
            var destFile = Path.Combine(targetSkinDir, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            entry.ExtractToFile(destFile, overwrite: true);
        }

        return defaultName;
    }

    /// <summary>导出当前方案及其默认回退卡面，ZIP 可直接重新导入。</summary>
    public void ExportSkinZip(string gameDirectory, string skinName, string zipPath)
    {
        if (skinName == GameDefault) throw new InvalidOperationException("游戏默认方案没有外部图片可导出。");
        if (string.IsNullOrWhiteSpace(skinName) || Path.GetFileName(skinName) != skinName || skinName.Contains('\\'))
            throw new ArgumentException("方案名称不正确。");
        var skins = GetSkinsDirectory(gameDirectory);
        var directory = Path.Combine(skins, skinName);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("皮肤方案目录不存在。");
        var cards = GetCards(gameDirectory, skinName);
        if (cards.Count == 0) throw new InvalidOperationException("该方案没有可导出的卡面。");
        var temporary = Path.GetFullPath(zipPath) + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                var exportedAnimations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var card in cards)
                {
                    var source = Path.Combine(card.IsCustomized ? directory : Path.Combine(skins, "default"), card.FileName);
                    if (!File.Exists(source)) source = Path.Combine(skins, "default", card.FileName);
                    if (File.Exists(source)) archive.CreateEntryFromFile(source, skinName + "/" + card.FileName, CompressionLevel.Optimal);
                    if (card.IsAnimated && exportedAnimations.Add(card.AnimationBinding))
                    {
                        var manifest = Path.Combine(directory, card.AnimationBinding + ".cardanim");
                        var animation = CardAnimationPackage.Read(manifest);
                        archive.CreateEntryFromFile(manifest, skinName + "/" + card.AnimationBinding + ".cardanim", CompressionLevel.Optimal);
                        for (var index = 0; index < animation.FrameCount; index++)
                        {
                            var relative = card.AnimationBinding + ".frames/" + index.ToString("D6") + ".png";
                            archive.CreateEntryFromFile(Path.Combine(directory, relative), skinName + "/" + relative, CompressionLevel.Optimal);
                        }
                    }
                }
                var metadata = Path.Combine(directory, "skin.json");
                if (File.Exists(metadata)) archive.CreateEntryFromFile(metadata, skinName + "/skin.json", CompressionLevel.Optimal);
                else
                {
                    using var writer = new StreamWriter(archive.CreateEntry(skinName + "/skin.json").Open(), new UTF8Encoding(false));
                    writer.Write(JsonSerializer.Serialize(new { name = skinName, description = "从 Toys 导出的皮肤方案" }));
                }
            }
            File.Move(temporary, Path.GetFullPath(zipPath), true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>从应用内置默认包创建自定义方案；保留旧 source 参数兼容桥接。</summary>
    public string DuplicateProfile(string gameDirectory, string sourceSkinName, string newSkinName,
        string displayName, string author, string description)
    {
        if (string.IsNullOrWhiteSpace(newSkinName))
            throw new ArgumentException("新方案标识不能为空", nameof(newSkinName));

        var cleanName = Regex.Replace(newSkinName.Trim(), @"[^a-zA-Z0-9_\-\u4e00-\u9fa5]", "_");
        if (string.IsNullOrWhiteSpace(cleanName))
            throw new ArgumentException("新方案标识包含非法字符", nameof(newSkinName));

        var skinsDir = GetSkinsDirectory(gameDirectory);

        var destDir = Path.Combine(skinsDir, cleanName);
        if (Directory.Exists(destDir))
            throw new InvalidOperationException($"目标方案已存在: {cleanName}");

        Directory.CreateDirectory(destDir);
        DefaultSkinPackage.Install(destDir);

        // 写入新方案的元数据 skin.json
        var meta = new
        {
            name = string.IsNullOrWhiteSpace(displayName) ? cleanName : displayName.Trim(),
            author = string.IsNullOrWhiteSpace(author) ? Environment.UserName : author.Trim(),
            description = string.IsNullOrWhiteSpace(description) ? "基于应用内置默认包创建" : description.Trim()
        };

        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(destDir, "skin.json"), json, new UTF8Encoding(false));

        return cleanName;
    }

    /// <summary>
    /// 修改已有方案的可展示信息（显示名称、作者、描述）。方案目录名保持不变，
    /// skin.json 中其它字段（例如自定义包元数据）原样保留。
    /// </summary>
    public void UpdateProfileMetadata(string gameDirectory, string skinName, string displayName, string author, string description)
    {
        if (string.IsNullOrWhiteSpace(skinName) || skinName == GameDefault ||
            skinName.Equals("default", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("游戏默认方案没有可编辑的方案信息", nameof(skinName));
        string skinsDir = GetSkinsDirectory(gameDirectory);
        string directory = CharacterSkinRules.SafePath(skinsDir, skinName);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("皮肤方案不存在: " + skinName);

        displayName = (displayName ?? "").Trim();
        author = (author ?? "").Trim();
        description = (description ?? "").Trim();
        if (displayName.Length == 0) throw new ArgumentException("方案显示名称不能为空", nameof(displayName));
        if (displayName.Length > 100) throw new ArgumentException("方案显示名称最长 100 个字符", nameof(displayName));
        if (author.Length > 100) throw new ArgumentException("作者最长 100 个字符", nameof(author));
        if (description.Length > 2000) throw new ArgumentException("方案描述最长 2000 个字符", nameof(description));

        string metaFile = Path.Combine(directory, "skin.json");
        var meta = new JsonObject();
        if (File.Exists(metaFile))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(metaFile, Encoding.UTF8)) is not JsonObject existing)
                    throw new InvalidOperationException("skin.json 根节点不是对象");
                meta = existing;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("skin.json 格式无效，请先修复后再编辑方案信息", ex);
            }
        }
        meta["name"] = displayName;
        meta["author"] = author;
        meta["description"] = description;
        File.WriteAllText(metaFile, meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    }

    /// <summary>删除指定的皮肤方案（default 方案禁止删除）。</summary>
    public void DeleteProfile(string gameDirectory, string skinName)
    {
        if (skinName.Equals("default", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("默认方案 (default) 禁止删除。");

        var skinsDir = GetSkinsDirectory(gameDirectory);
        var targetDir = Path.Combine(skinsDir, skinName);

        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }

        // 若被删除的方案刚好是当前激活方案，则自动回退到 default
        if (GetActiveSkin(gameDirectory).Equals(skinName, StringComparison.OrdinalIgnoreCase))
        {
            SetActiveSkin(gameDirectory, "default");
        }
    }

    /// <summary>在 Windows 资源管理器中打开指定皮肤方案目录。</summary>
    public void OpenFolder(string gameDirectory, string? skinName = null)
    {
        var skinsDir = GetSkinsDirectory(gameDirectory);
        Directory.CreateDirectory(skinsDir);

        var targetPath = string.IsNullOrEmpty(skinName) ? skinsDir : Path.Combine(skinsDir, skinName);
        if (!Directory.Exists(targetPath)) targetPath = skinsDir;

        Process.Start(new ProcessStartInfo("explorer.exe", targetPath) { UseShellExecute = true });
    }

    private string ResolveCardName(int cardId, string category, string assetKey)
    {
        if (cardId > 0 && _catalog != null)
        {
            if (category == "异画道具")
            {
                var itemName = _catalog.Item(cardId);
                if (!itemName.StartsWith("物品 ", StringComparison.Ordinal)) return itemName;
            }

            var cardName = _catalog.Card(cardId);
            if (!cardName.StartsWith("卡牌 ", StringComparison.Ordinal)) return cardName;
        }

        return category switch
        {
            "手牌卡" => cardId > 0 ? $"手牌 #{cardId}" : assetKey,
            "命运卡" => cardId > 0 ? $"命运 #{cardId}" : assetKey,
            "事件卡" => cardId > 0 ? $"事件 #{cardId}" : assetKey,
            "地图事件" => cardId > 0 ? $"地图事件 #{cardId}" : assetKey,
            "异画道具" => cardId > 0 ? $"异画 #{cardId}" : assetKey,
            _ => assetKey
        };
    }

    private static int GetCategoryOrder(string category) => category switch
    {
        "手牌卡" => 1,
        "命运卡" => 2,
        "事件卡" => 3,
        "地图事件" => 4,
        "异画道具" => 5,
        _ => 9
    };

    private static (string Name, string Author, string Description) ReadSkinMetadata(string dir, string fallbackName)
    {
        var metaFile = Path.Combine(dir, "skin.json");
        if (!File.Exists(metaFile))
            return (fallbackName, "", "");

        try
        {
            var text = File.ReadAllText(metaFile, Encoding.UTF8);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : fallbackName;
            var author = root.TryGetProperty("author", out var a) ? a.GetString() : "";
            var desc = root.TryGetProperty("description", out var d) ? d.GetString() : "";
            return (string.IsNullOrWhiteSpace(name) ? fallbackName : name, author ?? "", desc ?? "");
        }
        catch
        {
            return (fallbackName, "", "");
        }
    }

    private static int ExtractCardId(string name, out bool isSfw)
    {
        isSfw = false;
        if (string.IsNullOrEmpty(name)) return -1;

        var s = name;
        if (s.EndsWith("_sfw", StringComparison.OrdinalIgnoreCase))
        {
            isSfw = true;
            s = s[..^4];
        }

        foreach (var prefix in KnownPrefixes)
        {
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                s = s[prefix.Length..];
                break;
            }
        }

        return int.TryParse(s, out var id) ? id : -1;
    }

    private static string CategorizeCard(string name)
    {
        if (name.StartsWith("UT_HandCard_", StringComparison.OrdinalIgnoreCase)) return "手牌卡";
        if (name.StartsWith("UT_Destiny_", StringComparison.OrdinalIgnoreCase)) return "命运卡";
        if (name.StartsWith("UT_Event_", StringComparison.OrdinalIgnoreCase)) return "事件卡";
        if (name.StartsWith("UT_MapEvent_", StringComparison.OrdinalIgnoreCase)) return "地图事件";
        if (name.StartsWith("UT_Item_AltArt_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("UT_IAltArt_", StringComparison.OrdinalIgnoreCase)) return "异画道具";
        return "卡面";
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destDir, rel));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            File.Copy(file, Path.Combine(destDir, rel), overwrite: true);
        }
    }
}
