using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Services.Skins;

public sealed partial class SkinManager
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

        var totalPoolCount = GetCards(gameDirectory, "default").Count;

        var subDirs = Directory.GetDirectories(skinsDir);
        foreach (var subDir in subDirs)
        {
            var folderName = Path.GetFileName(subDir);
            if (folderName.StartsWith(".animation-", StringComparison.Ordinal)) continue;
            var isDefault = folderName.Equals("default", StringComparison.OrdinalIgnoreCase);
            var isActive = folderName.Equals(activeSkin, StringComparison.OrdinalIgnoreCase);

            var meta = ReadSkinMetadata(subDir, folderName);
            var imageCount = GetCards(gameDirectory, folderName).Count(c => c.IsCustomized);

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
    /// 合并官方配表、default 与当前方案的卡面，即使旧默认包缺图也能选择新增卡牌。
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

        // 配表是新卡选项的来源；默认皮肤包可能尚未包含新卡的图片。
        // 使用官方 Image/ImageSfw 键，保留已有扩展名，避免同一卡面重复出现。
        if (_catalog is not null)
        {
            var representedKeys = new HashSet<string>(fileMap.Keys.Select(name => Path.GetFileNameWithoutExtension(name)), StringComparer.OrdinalIgnoreCase);
            foreach (var key in _catalog.CardAssetKeys)
            {
                if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || CategorizeCard(key) == "卡面") continue;
                if (representedKeys.Add(key)) fileMap[key + ".png"] = (defaultDir, false);
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

        if (Directory.Exists(targetDir))
        {
            var animations = new List<CardAnimationPackage>();
            foreach (var manifest in Directory.GetFiles(targetDir, "*.cardanim", SearchOption.TopDirectoryOnly))
            {
                try { animations.Add(CardAnimationPackage.Read(manifest)); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException) { }
            }
            var represented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in list)
            {
                var animation = animations.OrderBy(a => AnimationMatchPriority(card, a.Binding))
                    .FirstOrDefault(a => AnimationMatchPriority(card, a.Binding) < int.MaxValue);
                if (animation == null) continue;
                represented.Add(animation.Binding);
                ApplyAnimationMetadata(card, animation, skinName, targetDir);
            }
            foreach (var animation in animations.Where(a => !represented.Contains(a.Binding)))
            {
                var id = ExtractCardId(animation.Binding, out var sfw);
                var category = CategorizeCard(animation.Binding);
                var card = new SkinCardInfo { FileName = animation.Binding + ".png", AssetKey = animation.Binding, CardId = id, IsSfw = sfw, Category = category, CardName = ResolveCardName(id, category, animation.Binding) };
                ApplyAnimationMetadata(card, animation, skinName, targetDir);
                list.Add(card);
            }
        }

        return list
            .OrderByDescending(c => c.IsCustomized)
            .ThenBy(c => GetCategoryOrder(c.Category))
            .ThenBy(c => c.CardId > 0 ? c.CardId : int.MaxValue)
            .ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int AnimationMatchPriority(SkinCardInfo card, string binding)
    {
        if (card.AssetKey.Equals(binding, StringComparison.OrdinalIgnoreCase)) return 0;
        if (card.CardId > 0 && binding == card.CardId.ToString()) return 1;
        if (card.Category == "异画道具")
            return binding.Equals("UT_Item_AltArt_" + card.CardId, StringComparison.OrdinalIgnoreCase)
                || binding.Equals("UT_IAltArt_" + card.CardId, StringComparison.OrdinalIgnoreCase) ? 2 : int.MaxValue;
        if (card.CardId > 0)
        {
            var keys = new[] { "UT_HandCard_", "UT_Event_", "UT_Destiny_", "UT_MapEvent_" };
            for (var index = 0; index < keys.Length; index++)
                if (binding.Equals(keys[index] + card.CardId, StringComparison.OrdinalIgnoreCase)) return 2 + index;
        }
        return card.CardName.Equals(binding, StringComparison.OrdinalIgnoreCase) ? 6 : int.MaxValue;
    }

    private static void ApplyAnimationMetadata(SkinCardInfo card, CardAnimationPackage animation, string skinName, string targetDir)
    {
        var manifest = Path.Combine(targetDir, animation.Binding + ".cardanim");
        card.IsCustomized = true;
        card.IsAnimated = true;
        card.AnimationBinding = animation.Binding;
        card.AnimationFps = animation.Fps;
        card.AnimationFrameCount = animation.FrameCount;
        var firstFrame = animation.Binding + ".frames/000000.png";
        card.PreviewUrl = GetPreviewUrl(skinName, firstFrame, Path.Combine(targetDir, firstFrame));
        card.AnimationFrameUrlPrefix = $"https://skins.astral.local/{Uri.EscapeDataString(skinName)}/{Uri.EscapeDataString(animation.Binding + ".frames")}/";
        card.FileSizeBytes = new FileInfo(manifest).Length + Directory.GetFiles(Path.Combine(targetDir, animation.Binding + ".frames"), "*.png").Sum(f => new FileInfo(f).Length);
        card.LastModified = File.GetLastWriteTime(manifest);
        card.CanRestore = DefaultSkinPackage.Contains(card.FileName);
    }

    private string CurrentAnimationBinding(string gameDirectory, string skinName, string fileName) =>
        GetCards(gameDirectory, skinName).FirstOrDefault(c => c.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))?.AnimationBinding ?? "";

    private static string GetPreviewUrl(string skinName, string fileName, string filePath)
    {
        var encodedFile = string.Join('/', fileName.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
        var url = $"https://skins.astral.local/{Uri.EscapeDataString(skinName)}/{encodedFile}";
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

        var previousBinding = CurrentAnimationBinding(gameDirectory, skinName, fileName);
        var targetFilePath = Path.Combine(targetDir, fileName);
        File.Copy(sourceImagePath, targetFilePath, overwrite: true);
        CardAnimationPackage.Remove(targetDir, Path.GetFileNameWithoutExtension(fileName));
        if (!string.IsNullOrEmpty(previousBinding)) CardAnimationPackage.Remove(targetDir, previousBinding);

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
        var previousBinding = CurrentAnimationBinding(gameDirectory, skinName, fileName);
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
        CardAnimationPackage.Remove(targetDir, Path.GetFileNameWithoutExtension(fileName));
        if (!string.IsNullOrEmpty(previousBinding)) CardAnimationPackage.Remove(targetDir, previousBinding);
        return GetCards(gameDirectory, skinName).First(c => c.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>从应用内置默认包恢复单卡，与磁盘 default 的修改无关。</summary>
    public SkinCardInfo RevertCardImage(string gameDirectory, string skinName, string fileName)
    {
        if (skinName == GameDefault) throw new InvalidOperationException("游戏默认方案无需恢复外部卡面。");
        var directory = Path.Combine(GetSkinsDirectory(gameDirectory), skinName);
        var previousBinding = CurrentAnimationBinding(gameDirectory, skinName, fileName);
        DefaultSkinPackage.RestoreCard(directory, fileName);
        CardAnimationPackage.Remove(directory, Path.GetFileNameWithoutExtension(fileName));
        if (!string.IsNullOrEmpty(previousBinding)) CardAnimationPackage.Remove(directory, previousBinding);
        return GetCards(gameDirectory, skinName).First(c => c.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public int RestoreDefaultProfile(string gameDirectory)
    {
        var directory = Path.Combine(GetSkinsDirectory(gameDirectory), "default");
        var count = DefaultSkinPackage.Install(directory);
        foreach (var manifest in Directory.GetFiles(directory, "*.cardanim"))
            CardAnimationPackage.Remove(directory, Path.GetFileNameWithoutExtension(manifest));
        return count;
    }
}