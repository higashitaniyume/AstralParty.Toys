using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services;

public sealed class SkinProfile
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public int CardCount { get; set; }
    public int TotalPoolCount { get; set; }
    public double CoverageRate { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime LastModified { get; set; }
}

public sealed class SkinCardInfo
{
    public string FileName { get; set; } = "";
    public string AssetKey { get; set; } = "";
    public int CardId { get; set; }
    public string CardName { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsSfw { get; set; }
    public bool IsCustomized { get; set; }
    public bool CanRestore { get; set; }
    public long FileSizeBytes { get; set; }
    public string PreviewUrl { get; set; } = "";
    public string FallbackUrl { get; set; } = "";
    public DateTime LastModified { get; set; }
}

public sealed class SkinManager
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp"
    };

    private static readonly string[] KnownPrefixes =
    [
        "UT_HandCard_",
        "UT_Destiny_",
        "UT_MapEvent_",
        "UT_Event_",
        "UT_Item_AltArt_",
        "UT_IAltArt_",
        "Card_",
        "card_"
    ];

    private readonly ConfigCatalog? _catalog;

    public SkinManager(string appDirectory = "")
    {
        if (!string.IsNullOrEmpty(appDirectory))
        {
            try
            {
                var protocolDir = Path.Combine(appDirectory, "Protocol");
                var dataDir = Path.Combine(appDirectory, "GameData");
                if (Directory.Exists(protocolDir) && Directory.Exists(dataDir))
                {
                    var protocol = new GameProtocolContext(protocolDir);
                    _catalog = new ConfigCatalog(protocol, dataDir);
                }
            }
            catch
            {
                // Catalog optional
            }
        }
    }

    public SkinManager(ConfigCatalog catalog)
    {
        _catalog = catalog;
    }

    public string GetSkinsDirectory(string gameDirectory) =>
        Path.Combine(gameDirectory, "AstralParty_ModLoader", "skins");

    public string GetModCfgPath(string gameDirectory) =>
        Path.Combine(gameDirectory, "AstralParty_ModLoader", "mods", "CardSkinMod", "mod.cfg");

    public const string GameDefault = "__game_default__";
    public string GetModConfigPath(string gameDirectory) =>
        Path.Combine(gameDirectory, "AstralParty_ModLoader", "mods", "CardSkinMod", "config.json");

    public string GetActiveSkin(string gameDirectory)
    {
        var path = GetModConfigPath(gameDirectory);
        if (!File.Exists(path)) return "default";
        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))!;
        if (config.TryGetValue("Enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False) return GameDefault;
        return config.TryGetValue("ActiveSkin", out var skin) ? skin.GetString() ?? "default" : "default";
    }

    public void SetActiveSkin(string gameDirectory, string skinName)
    {
        if (string.IsNullOrWhiteSpace(skinName)) throw new ArgumentException("皮肤方案名称不能为空");
        var path = GetModConfigPath(gameDirectory);
        var config = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))!
            : new Dictionary<string, JsonElement>();
        config["Enabled"] = JsonSerializer.SerializeToElement(skinName != GameDefault);
        if (skinName != GameDefault) config["ActiveSkin"] = JsonSerializer.SerializeToElement(skinName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
    /// <summary>获取指定游戏目录下的所有可用皮肤方案。</summary>
    public List<SkinProfile> GetProfiles(string gameDirectory)
    {
        var skinsDir = GetSkinsDirectory(gameDirectory);
        var activeSkin = GetActiveSkin(gameDirectory);
        var result = new List<SkinProfile> { new() { Name = GameDefault, DisplayName = "游戏默认", Description = "仅使用游戏内部卡面，停用外部图片。重启游戏后生效。", IsActive = activeSkin == GameDefault, IsDefault = true } };

        if (!Directory.Exists(skinsDir))
            return result;

        var defaultDir = Path.Combine(skinsDir, "default");
        var totalPoolCount = Directory.Exists(defaultDir)
            ? Directory.EnumerateFiles(defaultDir, "*.*", SearchOption.TopDirectoryOnly)
                .Count(f => SupportedImageExtensions.Contains(Path.GetExtension(f)))
            : 0;

        var subDirs = Directory.GetDirectories(skinsDir);
        foreach (var subDir in subDirs)
        {
            var folderName = Path.GetFileName(subDir);
            var isDefault = folderName.Equals("default", StringComparison.OrdinalIgnoreCase);
            var isActive = folderName.Equals(activeSkin, StringComparison.OrdinalIgnoreCase);

            var meta = ReadSkinMetadata(subDir, folderName);
            var imageCount = Directory.EnumerateFiles(subDir, "*.*", SearchOption.TopDirectoryOnly)
                .Count(f => SupportedImageExtensions.Contains(Path.GetExtension(f)));

            var pool = totalPoolCount > 0 ? totalPoolCount : imageCount;
            var coverage = pool > 0 ? Math.Round((double)imageCount / pool * 100, 1) : 0;

            var dirInfo = new DirectoryInfo(subDir);

            result.Add(new SkinProfile
            {
                Name = folderName,
                DisplayName = string.IsNullOrWhiteSpace(meta.Name) ? folderName : meta.Name,
                Author = meta.Author,
                Description = meta.Description,
                DirectoryPath = subDir,
                CardCount = imageCount,
                TotalPoolCount = pool,
                CoverageRate = coverage,
                IsActive = isActive,
                IsDefault = isDefault,
                LastModified = dirInfo.LastWriteTime
            });
        }

        return result
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.IsActive)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 获取指定皮肤方案中的完整卡牌列表。
    /// 以 default 方案为全卡池母本，精准比对出哪些牌已加载独立皮肤，哪些牌回退使用默认。
    /// </summary>
    public List<SkinCardInfo> GetCards(string gameDirectory, string skinName)
    {
        if (skinName == GameDefault) return new List<SkinCardInfo>();
        var skinsDir = GetSkinsDirectory(gameDirectory);
        var defaultDir = Path.Combine(skinsDir, "default");
        var targetDir = Path.Combine(skinsDir, skinName);

        // 收集所有已知的卡面文件（以 default 方案为基准全集，同时补全 targetDir 中独有的卡牌）
        var fileMap = new Dictionary<string, (string SourceDir, bool IsCustomized)>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(defaultDir))
        {
            foreach (var file in Directory.EnumerateFiles(defaultDir, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(f => SupportedImageExtensions.Contains(Path.GetExtension(f))))
            {
                var fileName = Path.GetFileName(file);
                var isCustom = skinName.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                               File.Exists(Path.Combine(targetDir, fileName));
                fileMap[fileName] = (defaultDir, isCustom);
            }
        }

        if (Directory.Exists(targetDir) && !targetDir.Equals(defaultDir, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(targetDir, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(f => SupportedImageExtensions.Contains(Path.GetExtension(f))))
            {
                var fileName = Path.GetFileName(file);
                fileMap[fileName] = (targetDir, true);
            }
        }

        var list = new List<SkinCardInfo>();
        foreach (var (fileName, (_, isCustomized)) in fileMap)
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var cardId = ExtractCardId(baseName, out var isSfw);
            var category = CategorizeCard(baseName);
            var cardName = ResolveCardName(cardId, category, baseName);

            var actualFilePath = isCustomized && !skinName.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(targetDir, fileName)
                : Path.Combine(defaultDir, fileName);

            if (!File.Exists(actualFilePath) && File.Exists(Path.Combine(targetDir, fileName)))
            {
                actualFilePath = Path.Combine(targetDir, fileName);
            }

            var fileInfo = File.Exists(actualFilePath) ? new FileInfo(actualFilePath) : null;
            var size = fileInfo?.Length ?? 0;
            var modified = fileInfo?.LastWriteTime ?? DateTime.MinValue;

            var hostSkin = isCustomized ? skinName : "default";
            var previewUrl = GetPreviewUrl(hostSkin, fileName, actualFilePath);
            var fallbackUrl = GetPreviewUrl("default", fileName, Path.Combine(defaultDir, fileName));

            list.Add(new SkinCardInfo
            {
                FileName = fileName,
                AssetKey = baseName,
                CardId = cardId,
                CardName = cardName,
                Category = category,
                IsSfw = isSfw,
                IsCustomized = isCustomized,
                CanRestore = DefaultSkinPackage.Contains(fileName),
                FileSizeBytes = size,
                PreviewUrl = previewUrl,
                FallbackUrl = fallbackUrl,
                LastModified = modified
            });
        }

        return list
            .OrderByDescending(c => c.IsCustomized)
            .ThenBy(c => GetCategoryOrder(c.Category))
            .ThenBy(c => c.CardId > 0 ? c.CardId : int.MaxValue)
            .ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetPreviewUrl(string skinName, string fileName, string filePath)
    {
        var url = $"https://skins.astral.local/{Uri.EscapeDataString(skinName)}/{Uri.EscapeDataString(fileName)}";
        if (!File.Exists(filePath)) return url;
        // File.Copy 可保留源文件时间；用内容版本避免同尺寸/同时间的新图复用旧地址。
        using var stream = File.OpenRead(filePath);
        return url + "?v=" + Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>替换方案中的单张卡牌贴图，支持多种图片格式，直接写入该方案目录。</summary>
    public SkinCardInfo ReplaceCardImage(string gameDirectory, string skinName, string fileName, string sourceImagePath)
    {
        if (string.IsNullOrWhiteSpace(skinName)) throw new ArgumentException("方案名不能为空", nameof(skinName));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("卡牌文件名不能为空", nameof(fileName));
        if (!File.Exists(sourceImagePath)) throw new FileNotFoundException("源图片文件不存在", sourceImagePath);

        var skinsDir = GetSkinsDirectory(gameDirectory);
        var targetDir = Path.Combine(skinsDir, skinName);
        Directory.CreateDirectory(targetDir);

        var targetFilePath = Path.Combine(targetDir, fileName);
        File.Copy(sourceImagePath, targetFilePath, overwrite: true);

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var cardId = ExtractCardId(baseName, out var isSfw);
        var category = CategorizeCard(baseName);
        var cardName = ResolveCardName(cardId, category, baseName);
        var size = new FileInfo(targetFilePath).Length;
        var previewUrl = GetPreviewUrl(skinName, fileName, targetFilePath);
        var fallbackUrl = GetPreviewUrl("default", fileName, Path.Combine(skinsDir, "default", fileName));

        return new SkinCardInfo
        {
            FileName = fileName,
            AssetKey = baseName,
            CardId = cardId,
            CardName = cardName,
            Category = category,
            IsSfw = isSfw,
            IsCustomized = true,
            FileSizeBytes = size,
            PreviewUrl = previewUrl,
            FallbackUrl = fallbackUrl,
            LastModified = DateTime.UtcNow
        };
    }

    public SkinCardInfo SaveCroppedCard(string gameDirectory, string skinName, string fileName, System.Windows.Media.Imaging.BitmapSource image)
    {
        if (skinName == GameDefault) throw new InvalidOperationException("游戏默认方案不可修改");
        if (image.PixelWidth != image.PixelHeight) throw new ArgumentException("卡面必须是正方形");
        var targetDir = Path.Combine(GetSkinsDirectory(gameDirectory), skinName);
        Directory.CreateDirectory(targetDir);
        System.Windows.Media.Imaging.BitmapEncoder encoder = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new System.Windows.Media.Imaging.BmpBitmapEncoder(),
            _ => new System.Windows.Media.Imaging.PngBitmapEncoder()
        };
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using (var output = File.Create(Path.Combine(targetDir, fileName))) encoder.Save(output);
        return GetCards(gameDirectory, skinName).First(c => c.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>从应用内置默认包恢复单卡，与磁盘 default 的修改无关。</summary>
    public SkinCardInfo RevertCardImage(string gameDirectory, string skinName, string fileName)
    {
        if (skinName == GameDefault) throw new InvalidOperationException("游戏默认方案无需恢复外部卡面。");
        var directory = Path.Combine(GetSkinsDirectory(gameDirectory), skinName);
        DefaultSkinPackage.RestoreCard(directory, fileName);
        return GetCards(gameDirectory, skinName).First(c => c.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public int RestoreDefaultProfile(string gameDirectory) =>
        DefaultSkinPackage.Install(Path.Combine(GetSkinsDirectory(gameDirectory), "default"));
    /// <summary>安全导入皮肤 ZIP 压缩包，自动识别各种层级结构。</summary>
    public string ImportSkinZip(string gameDirectory, string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("皮肤压缩包不存在", zipPath);

        var fileInfo = new FileInfo(zipPath);
        if (fileInfo.Length > 256 * 1024 * 1024)
            throw new InvalidOperationException("皮肤压缩包不能超过 256 MB。");

        var skinsDir = GetSkinsDirectory(gameDirectory);
        Directory.CreateDirectory(skinsDir);

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count == 0)
            throw new InvalidOperationException("压缩包内没有任何文件。");

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
            var destFile = Path.Combine(targetSkinDir, entry.Name);
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
                foreach (var card in cards)
                {
                    var source = Path.Combine(card.IsCustomized ? directory : Path.Combine(skins, "default"), card.FileName);
                    archive.CreateEntryFromFile(source, skinName + "/" + card.FileName, CompressionLevel.Optimal);
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
