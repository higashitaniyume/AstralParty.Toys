using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace AstralParty.Toys.Services.Skins;

public sealed partial class AudioSkinSchemeManager
{
    public void ExportZip(string gameDirectory, string schemeId, string zipPath)
    {
        var dir = SchemeDirectory(gameDirectory, schemeId);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("方案目录不存在: " + dir);

        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(dir, zipPath, CompressionLevel.Optimal, false);
    }

    public void ImportZip(string gameDirectory, string zipPath)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("ZIP 文件未找到: " + zipPath);

        string schemeId = Path.GetFileNameWithoutExtension(zipPath);
        var targetDir = SchemeDirectory(gameDirectory, schemeId);
        int index = 1;
        while (Directory.Exists(targetDir))
        {
            targetDir = SchemeDirectory(gameDirectory, $"{schemeId}_{index++}");
        }

        Directory.CreateDirectory(targetDir);
        ZipFile.ExtractToDirectory(zipPath, targetDir, true);

        // Validate scheme.json
        var manifestPath = Path.Combine(targetDir, "audio-scheme.json");
        if (!File.Exists(manifestPath) && File.Exists(Path.Combine(targetDir, "scheme.json")))
        {
            File.Move(Path.Combine(targetDir, "scheme.json"), manifestPath);
        }

        if (!File.Exists(manifestPath))
        {
            var scheme = new AudioSkinScheme
            {
                Id = Path.GetFileName(targetDir),
                Name = Path.GetFileName(targetDir),
                Description = "从 ZIP 导入的音效方案"
            };
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(scheme, JsonOptions));
        }
    }

    /// <summary>
    /// 从工作区预提取目录或游戏资源中同步/提取音频资源至本地 audio-cache。
    /// </summary>
    public int ExtractGameAudio(string gameDirectory, Action<string, double>? progress = null)
    {
        var cacheDir = AudioCacheDirectory(gameDirectory);
        Directory.CreateDirectory(cacheDir);

        progress?.Invoke("正在扫描游戏音频素材…", 0.05);

        int count = 0;
        var exportRoot = FindExportRoot(gameDirectory);

        if (exportRoot != null)
        {
            var catalog = LoadCatalog();
            var total = catalog.Count;
            int i = 0;

            foreach (var c in catalog)
            {
                i++;
                if (i % 25 == 0)
                {
                    progress?.Invoke($"正在整理原版音频: {c.Label}", 0.05 + 0.90 * ((double)i / total));
                }

                string? src = null;
                if (!string.IsNullOrEmpty(c.RelOgg))
                {
                    var candidate = Path.Combine(exportRoot, c.RelOgg);
                    if (File.Exists(candidate)) src = candidate;
                }

                if (src != null)
                {
                    var dest = Path.Combine(cacheDir, $"{c.Id}.ogg");
                    if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(src).Length)
                    {
                        File.Copy(src, dest, true);
                    }
                    count++;
                }
            }
        }

        progress?.Invoke($"原版音频整理完成，共提取 {count} 首音频", 1.0);
        return count;
    }

    /// <summary>
    /// 将提取好的原版音频打包导出为 ZIP 文件。
    /// </summary>
    public int ExportOriginalAudioZip(string gameDirectory, string destZipPath, Action<string, double>? progress = null)
    {
        ExtractGameAudio(gameDirectory, progress);

        var cacheDir = AudioCacheDirectory(gameDirectory);
        var catalog = LoadCatalog();
        int exported = 0;

        progress?.Invoke("正在打包原版音频压缩包…", 0.95);

        if (File.Exists(destZipPath)) File.Delete(destZipPath);
        using (var zip = ZipFile.Open(destZipPath, ZipArchiveMode.Create))
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("AstralParty.Toys.AudioCatalog.json");
                if (stream != null)
                {
                    var entry = zip.CreateEntry("AudioCatalog.json");
                    using var entryStream = entry.Open();
                    stream.CopyTo(entryStream);
                }
            }
            catch { }

            foreach (var c in catalog)
            {
                var cached = Path.Combine(cacheDir, $"{c.Id}.ogg");
                if (File.Exists(cached))
                {
                    string entryName;
                    if (c.Category == "Voice" && !string.IsNullOrEmpty(c.CharacterName))
                    {
                        var s = string.IsNullOrEmpty(c.SkinName) ? "常规默认" : c.SkinName;
                        entryName = $"Voice/{c.CharacterName}/{s}/{c.Id}_{c.Name}.ogg";
                    }
                    else
                    {
                        entryName = $"{c.Category}/{c.Id}_{c.Name}.ogg";
                    }
                    zip.CreateEntryFromFile(cached, entryName);
                    exported++;
                }
            }
        }

        progress?.Invoke($"原版音频导出完成，共打包 {exported} 首音频", 1.0);
        return exported;
    }

    /// <summary>
    /// 从外部原版音频 ZIP 压缩包导入音频至本地 audio-cache 目录。
    /// </summary>
    public int ImportOriginalAudioZip(string gameDirectory, string zipPath, Action<string, double>? progress = null)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("ZIP 文件未找到: " + zipPath);

        var cacheDir = AudioCacheDirectory(gameDirectory);
        Directory.CreateDirectory(cacheDir);

        var catalog = LoadCatalog();
        var byName = catalog.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        progress?.Invoke("正在读取原版音频压缩包…", 0.05);

        int imported = 0;
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var validEntries = zip.Entries.Where(e =>
                !string.IsNullOrEmpty(e.Name) &&
                (e.Name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                 e.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))).ToList();

            var total = validEntries.Count;
            int i = 0;

            foreach (var entry in validEntries)
            {
                i++;
                if (i % 25 == 0 || i == total)
                {
                    progress?.Invoke($"正在导入音频素材 ({i}/{total})…", 0.05 + 0.90 * ((double)i / total));
                }

                var stem = Path.GetFileNameWithoutExtension(entry.Name);
                int targetId = 0;

                var underscoreIdx = stem.IndexOf('_');
                var idStr = underscoreIdx > 0 ? stem.Substring(0, underscoreIdx) : stem;
                if (int.TryParse(idStr, out int num) && num > 0)
                {
                    targetId = num;
                }
                else if (byName.TryGetValue(stem, out var catItem))
                {
                    targetId = catItem.Id;
                }
                else if (underscoreIdx > 0 && byName.TryGetValue(stem.Substring(underscoreIdx + 1), out var catItem2))
                {
                    targetId = catItem2.Id;
                }

                if (targetId > 0)
                {
                    var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                    var dest = Path.Combine(cacheDir, $"{targetId}{ext}");
                    entry.ExtractToFile(dest, overwrite: true);
                    imported++;
                }
            }
        }

        progress?.Invoke($"原版音频导入完成，共导入 {imported} 首音频", 1.0);
        return imported;
    }
}
