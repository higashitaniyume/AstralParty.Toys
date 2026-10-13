using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Services;

public sealed record LegacyCardSkinImportResult(string Name, int Images, string[] Normalizations);

/// <summary>Converts static card replacement bundles without changing the active card skin.</summary>
public static class LegacyCardSkinImporter
{
    public static Task<LegacyCardSkinImportResult> ImportAsync(string game, string zipPath,
        Action<CharacterExportProgress> progress, CancellationToken cancellationToken = default)
        => ImportForTestingAsync(game, zipPath, LegacyCharacterSkinImporter.ReadTextures, progress, cancellationToken);

    internal static Task<LegacyCardSkinImportResult> ImportForTestingAsync(string game, string zipPath,
        LegacyCharacterSkinImporter.ReadBundle reader, Action<CharacterExportProgress> progress, CancellationToken cancellationToken = default)
        => Task.Run(() => Import(game, zipPath, reader, progress, cancellationToken), cancellationToken);

    internal static bool IsCardAsset(string name) => Regex.IsMatch(name,
        @"^(?:UT_HandCard_|UT_Destiny_|UT_Event_|UT_MapEvent_|UT_Item_AltArt_|UT_IAltArt_|Card_)[0-9]{1,10}(?:_sfw)?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
        int.TryParse(Regex.Match(name, @"[0-9]+").Value, out int id) && id > 0;

    private static LegacyCardSkinImportResult Import(string game, string zipPath,
        LegacyCharacterSkinImporter.ReadBundle reader, Action<CharacterExportProgress> progress, CancellationToken token)
    {
        void Report(int percent, string message) { token.ThrowIfCancellationRequested(); progress(new(percent, message)); token.ThrowIfCancellationRequested(); }
        Report(1, "检查旧式卡牌皮肤 ZIP");
        var manager = new SkinManager();
        string skins = manager.GetSkinsDirectory(game);
        string name = "legacy-cards-" + Guid.NewGuid().ToString("N");
        string destination = Path.Combine(skins, name);
        // CardSkinManager enumerates every direct child of skins, including hidden directories.
        // Use its sibling on the same volume so only the complete package becomes visible after commit.
        string staging = Path.Combine(Path.GetDirectoryName(skins)!, ".legacy-cards-import-" + Guid.NewGuid().ToString("N"));
        using var archive = LegacyCharacterSkinImporter.OpenArchive(zipPath);
        var entries = LegacyCharacterSkinImporter.ValidateArchive(archive);
        if (!entries.Any(x => x.Relative.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("ZIP 中没有 Unity .bundle 卡牌资源。");
        Directory.CreateDirectory(staging);
        try
        {
            var bundles = new List<(string Relative, string Path)>();
            long extracted = 0;
            foreach (var (entry, relative) in entries)
            {
                token.ThrowIfCancellationRequested();
                string path = CharacterSkinRules.SafePath(staging, "source/" + relative);
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var input = entry.Open())
                using (var output = File.Create(path))
                {
                    byte[] buffer = new byte[81920]; int count; long written = 0;
                    while ((count = input.Read(buffer)) > 0)
                    {
                        token.ThrowIfCancellationRequested(); written += count; extracted += count;
                        if (written > entry.Length || extracted > LegacyCharacterSkinImporter.MaxArchiveBytes)
                            throw new InvalidDataException("ZIP 解压大小超过声明或限制。");
                        output.Write(buffer, 0, count);
                    }
                    if (written != entry.Length) throw new InvalidDataException("ZIP 文件长度不完整：" + relative);
                }
                if (relative.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)) bundles.Add((relative, path));
            }
            var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var normalizations = new List<string>();
            long decodedBytes = 0;
            string work = Path.Combine(staging, ".work"); Directory.CreateDirectory(work);
            for (int i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                Report(5 + 85 * i / bundles.Count, $"转换卡面 {i + 1} / {bundles.Count}：{bundle.Relative}");
                string workingPath = Path.Combine(work, "current.bundle");
                byte[] bytes = LegacyCharacterSkinImporter.NormalizeBundle(File.ReadAllBytes(bundle.Path), bundle.Relative, out string? notice);
                if (notice != null) normalizations.Add(notice);
                File.WriteAllBytes(workingPath, bytes);
                IReadOnlyList<LegacyCharacterSkinImporter.Texture> textures;
                try { textures = reader(workingPath, token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new InvalidDataException("无法完整读取卡牌资源包 " + bundle.Relative + "：" + ex.Message, ex); }
                if (textures.Count == 0) throw new InvalidDataException("资源包没有可转换的 Texture2D：" + bundle.Relative);
                foreach (var texture in textures)
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsCardAsset(texture.Name)) throw new InvalidDataException("资源不是支持的卡牌贴图，拒绝部分导入：" + texture.Name);
                    if (!assets.TryAdd(texture.Name, texture.Name + ".png")) throw new InvalidDataException("存在重复卡牌资源名：" + texture.Name);
                    long size = (long)texture.Width * texture.Height * 4;
                    if (texture.Width <= 0 || texture.Height <= 0 || texture.Width > 16384 || texture.Height > 16384 ||
                        size > 64L * 1024 * 1024 || size != texture.Pixels.LongLength ||
                        (decodedBytes += size) > LegacyCharacterSkinImporter.MaxArchiveBytes)
                        throw new InvalidDataException("卡面尺寸或解码大小超限：" + texture.Name);
                    CharacterUnityResources.SavePng(CharacterSkinRules.SafePath(staging, texture.Name + ".png"), texture.Width, texture.Height, texture.Pixels);
                }
                // Only paths are retained across bundles; decoded pixel arrays are released each iteration.
            }
            Report(95, "保存卡面方案与原作者文件");
            File.WriteAllText(Path.Combine(staging, "skin.json"), JsonSerializer.Serialize(new
            {
                name = Path.GetFileNameWithoutExtension(zipPath), author = "",
                description = "从旧式 Unity bundle 卡牌替换包转换。原作者文件和原 bundle 保存在 source/。"
            }, CharacterSkinManager.JsonOptions));
            File.WriteAllText(Path.Combine(staging, "legacy-source.json"), JsonSerializer.Serialize(new
            {
                Archive = Path.GetFileName(zipPath), Textures = assets, Normalizations = normalizations
            }, CharacterSkinManager.JsonOptions));
            Directory.Delete(work, true);
            Report(99, "提交完整卡牌方案");
            Directory.CreateDirectory(skins);
            Directory.Move(staging, destination);
            return new(name, assets.Count, normalizations.ToArray());
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
