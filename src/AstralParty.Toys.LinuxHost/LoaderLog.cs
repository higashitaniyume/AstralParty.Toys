using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Modding;

namespace AstralParty.Toys.LinuxHost;

internal sealed record LoaderLog(string Name, string Path, DateTime ModifiedUtc, string Tail)
{
    private const int SummaryBytes = 256 * 1024;
    private const int TailBytes = 16 * 1024;
    private const int TailLines = 24;

    internal static (LoaderLog? Log, LoaderLogSummary? Summary, string Warning) ReadLatest(string loaderRoot)
    {
        if (loaderRoot.Length == 0) return (null, null, "");
        var logs = System.IO.Path.Combine(loaderRoot, ModManager.LogsFolderName);
        if (!InspectionFiles.DirectoryExists(logs)) return (null, null, "");
        FileInfo? newest = null;
        try
        {
            foreach (var path in Directory.EnumerateFiles(logs, "cesium-loader-*.log"))
            {
                if (!InspectionFiles.FileExists(path)) continue;
                var entry = new FileInfo(path);
                if (newest is null || entry.LastWriteTimeUtc > newest.LastWriteTimeUtc ||
                    (entry.LastWriteTimeUtc == newest.LastWriteTimeUtc && string.CompareOrdinal(entry.Name, newest.Name) > 0))
                    newest = entry;
            }
            if (newest is null) return (null, null, "");

            using var file = InspectionFiles.OpenRead(newest.FullName);
            if (file is null) return (ToLog(newest, ""), null, "");
            var length = file.Length;
            var head = ReadSegment(file, (int)Math.Min(length, SummaryBytes));
            var tailOffset = Math.Max(0, length - TailBytes);
            var tail = head;
            if (tailOffset > 0)
            {
                file.Position = tailOffset;
                tail = ReadSegment(file, TailBytes);
            }
            var summary = ParseSummary(head) ?? (tailOffset == 0 ? null : ParseSummary(tail));
            return (ToLog(newest, LastLines(tail, tailOffset > 0)), summary, "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (newest is null ? null : ToLog(newest, ""), null, "暂时无法读取加载器日志，请刷新后重试。");
        }
    }

    private static LoaderLog ToLog(FileInfo info, string tail) => new(info.Name, info.FullName, info.LastWriteTimeUtc, tail);

    private static string ReadSegment(Stream stream, int length)
    {
        var buffer = new byte[length];
        var count = stream.ReadAtLeast(buffer, length, throwOnEndOfStream: false);
        return Encoding.UTF8.GetString(buffer, 0, count);
    }

    private static string LastLines(string text, bool partialFirstLine)
    {
        var span = text.AsSpan().TrimEnd("\r\n");
        var cursor = span.Length;
        for (var line = 0; line < TailLines && cursor > 0; line++)
        {
            cursor = span[..cursor].LastIndexOf('\n');
            if (cursor < 0) break;
        }
        if (cursor >= 0 && cursor < span.Length) return span[(cursor + 1)..].ToString();
        var firstBreak = partialFirstLine ? span.IndexOf('\n') : -1;
        return span[(firstBreak + 1)..].ToString();
    }

    private static LoaderLogSummary? ParseSummary(string text)
    {
        // The native runtime emits this line once on completion. Do not infer a result from inventory counts.
        var match = Regex.Match(text, @"(?m)^(?:\[[^\r\n\]]{1,32}\] )?summary ([0-9]+) ok · ([0-9]+) failed(?: ·|\r?$)", RegexOptions.CultureInvariant);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var loaded) ||
            !int.TryParse(match.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var failed))
            return null;
        return new LoaderLogSummary(loaded, failed);
    }
}

internal sealed record LoaderLogSummary(int Loaded, int Failed);
