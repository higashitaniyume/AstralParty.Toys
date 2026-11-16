using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Skins;

/// <summary>批量替换：一个文件对上的音效条目。</summary>
public sealed record AudioBatchMatch(int Id, string Name, string Label, string FilePath);

/// <summary>批量替换：没对上的文件和原因。</summary>
public sealed record AudioBatchSkip(string FilePath, string Reason);

public sealed partial class AudioSkinSchemeManager
{
    public static readonly string[] ReplaceExtensions = [".wav", ".ogg", ".mp3"];

    // 「编号」或「编号_音效名」，编号前可以带 #；分隔符允许 _ - 空格
    private static readonly Regex BatchIdPattern = new(@"^#?(?<id>\d+)(?:[_\-\s]+(?<rest>.+))?$", RegexOptions.Compiled);

    /// <summary>
    /// 按文件名把一批音频对应到音效条目。规则故意定得严格（宁可没对上，也不能对错）：
    /// <list type="bullet">
    /// <item>文件名就是音效名，例如 <c>Voice_101_achieve.wav</c>；</item>
    /// <item>文件名就是编号，例如 <c>1010101.ogg</c>、<c>#1010101.ogg</c>；</item>
    /// <item>「编号_音效名」，两部分必须是同一条，例如 <c>1010101_Voice_101_achieve.ogg</c>
    /// （「导出原版音频包」和「导出这条原版音频」就是这个格式）。</item>
    /// </list>
    /// 像 <c>01_achieve.ogg</c> 这种编号和名字对不上的，不会被当成编号 1。
    /// </summary>
    public (List<AudioBatchMatch> Matches, List<AudioBatchSkip> Skipped) MatchBatchFiles(IEnumerable<string> filePaths)
    {
        var catalog = LoadCatalog();
        var byId = catalog.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var byName = catalog.Where(c => !string.IsNullOrEmpty(c.Name))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var matches = new List<AudioBatchMatch>();
        var skipped = new List<AudioBatchSkip>();
        var taken = new Dictionary<int, string>();

        foreach (var path in filePaths)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!ReplaceExtensions.Contains(ext))
            {
                skipped.Add(new(path, "不是 wav / ogg / mp3 文件"));
                continue;
            }

            var stem = Path.GetFileNameWithoutExtension(path).Trim();
            AudioSkinCatalogItem? item = null;
            string? reason = null;

            if (byName.TryGetValue(stem, out var named))
            {
                if (named.Count == 1) item = named[0];
                else reason = $"音效名「{stem}」对应 {named.Count} 条，请在文件名前加上编号";
            }
            else if (BatchIdPattern.Match(stem) is { Success: true } m && int.TryParse(m.Groups["id"].Value, out var id))
            {
                var rest = m.Groups["rest"].Success ? m.Groups["rest"].Value.Trim() : "";
                if (!byId.TryGetValue(id, out var byNumber))
                    reason = $"没有编号为 {id} 的音效";
                else if (rest.Length == 0 || string.Equals(rest, byNumber.Name, StringComparison.OrdinalIgnoreCase))
                    item = byNumber;
                else
                    reason = $"编号 {id} 是「{byNumber.Label}」，但文件名后半段不是它的音效名 {byNumber.Name}";
            }
            else
            {
                reason = "文件名里没有音效名或编号";
            }

            if (item == null)
            {
                skipped.Add(new(path, reason ?? "没有对应的音效"));
                continue;
            }
            if (taken.TryGetValue(item.Id, out var first))
            {
                skipped.Add(new(path, $"和 {Path.GetFileName(first)} 对应同一条（#{item.Id}），只用第一个"));
                continue;
            }
            taken[item.Id] = path;
            matches.Add(new(item.Id, item.Name, string.IsNullOrEmpty(item.Label) ? item.Name : item.Label, path));
        }

        return (matches.OrderBy(x => x.Id).ToList(), skipped);
    }

    /// <summary>把确认过的批量结果写进方案。返回成功替换的条数；读不了的文件放进 <paramref name="failed"/>。</summary>
    public int ApplyBatchReplace(string gameDirectory, string schemeId, IEnumerable<AudioBatchMatch> matches, List<string> failed)
    {
        int done = 0;
        foreach (var match in matches)
        {
            try
            {
                var bytes = File.ReadAllBytes(match.FilePath);
                SaveCustomAudio(gameDirectory, schemeId, match.Id, match.Name, bytes, $"{match.Id}_{Path.GetFileName(match.FilePath)}", 1.0f);
                done++;
            }
            catch (Exception ex)
            {
                failed.Add($"{Path.GetFileName(match.FilePath)}：{ex.Message}");
            }
        }
        return done;
    }
}
