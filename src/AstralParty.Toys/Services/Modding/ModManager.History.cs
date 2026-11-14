using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 安装历史的自动清理。
///
/// <c>install-history</c> 里有两类东西：操作记录（每次安装 / 更新 / 停用取一份前后快照）和内容寻址的
/// 备份文件（按 SHA256 存一份 DLL / PDB / 发布包）。后者才是占空间的大头，而且旧版本的备份在
/// 当前安装和近期操作都不再引用之后就没用了。
///
/// 清理规则（保守，先删记录再回收备份）：
/// <list type="bullet">
///   <item>每个游戏目录只留最新 <see cref="DefaultKeepOperationsPerGame"/> 条操作记录，以及保留期内的记录；</item>
///   <item>备份文件只删「没有任何记录引用」且超过宽限期的（宽限期避免删掉正在进行的操作刚写进来的文件）；</item>
///   <item><c>current.json</c> 与它引用的备份永远保留 —— 停用 / 恢复加载器要靠它们。</item>
/// </list>
/// </summary>
public sealed partial class ModManager
{
    /// <summary>一次清理的结果（日志里会写，界面也可以用）。</summary>
    public sealed class HistoryPruneResult
    {
        public int RemovedOperations { get; set; }
        public int RemovedBlobs { get; set; }
        public long FreedBytes { get; set; }
        public long RemainingBytes { get; set; }
    }

    private static readonly TimeSpan DefaultOperationRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan DefaultBlobGrace = TimeSpan.FromDays(7);
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(6);
    private const int DefaultKeepOperationsPerGame = 20;

    private static readonly Regex HashPattern = new("^[a-fA-F0-9]{64}$", RegexOptions.Compiled);
    private static readonly JsonDocumentOptions HistoryJsonOptions = new() { AllowTrailingCommas = true };

    private readonly object _historyPruneGate = new();
    private DateTime _lastHistoryPruneUtc = DateTime.MinValue;

    /// <summary>顺手清理（每 6 小时最多一次）：状态刷新和每次受保留保护的操作都会触发。</summary>
    private void PruneHistoryIfDue()
    {
        var now = DateTime.UtcNow;
        if (now - _lastHistoryPruneUtc < PruneInterval) return;
        if (!Monitor.TryEnter(_historyPruneGate)) return;
        try
        {
            // 拿锁后再看一次：并发的状态请求不该重复扫目录
            if (DateTime.UtcNow - _lastHistoryPruneUtc < PruneInterval) return;
            _lastHistoryPruneUtc = DateTime.UtcNow;
            var result = PruneHistoryCore(now, DefaultOperationRetention, DefaultKeepOperationsPerGame, DefaultBlobGrace);
            if (result.RemovedOperations > 0 || result.RemovedBlobs > 0)
                ToysLog.Info("history",
                    $"清理安装历史：删除 {result.RemovedOperations} 条操作记录、{result.RemovedBlobs} 个备份文件，" +
                    $"释放 {FormatBytes(result.FreedBytes)}；剩余 {FormatBytes(result.RemainingBytes)}");
        }
        catch (Exception ex)
        {
            ToysLog.Warn("history", "清理安装历史失败：" + ex.Message);
        }
        finally
        {
            Monitor.Exit(_historyPruneGate);
        }
    }

    /// <summary>
    /// 立即清理安装历史。默认：操作记录保留 30 天 / 每个游戏目录最新 20 条，备份文件 7 天宽限期。
    /// 只删不再被任何记录引用的备份，当前安装用到的一个都不动。
    /// </summary>
    public HistoryPruneResult PruneHistory(TimeSpan? operationRetention = null,
        int keepOperationsPerGame = DefaultKeepOperationsPerGame, TimeSpan? blobGrace = null)
    {
        lock (_historyPruneGate)
            return PruneHistoryCore(DateTime.UtcNow,
                operationRetention ?? DefaultOperationRetention,
                Math.Max(1, keepOperationsPerGame),
                blobGrace ?? DefaultBlobGrace);
    }

    private HistoryPruneResult PruneHistoryCore(DateTime now, TimeSpan operationRetention,
        int keepOperationsPerGame, TimeSpan blobGrace)
    {
        var result = new HistoryPruneResult();
        var root = HistoryRoot;
        if (!Directory.Exists(root)) return result;

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gamesRoot = Path.Combine(root, "games");
        var operationCutoff = now - operationRetention;

        if (Directory.Exists(gamesRoot))
        {
            foreach (var gameDir in Directory.EnumerateDirectories(gamesRoot))
            {
                // 当前安装的快照是恢复功能的依据，无条件保留
                var current = Path.Combine(gameDir, "current.json");
                if (File.Exists(current)) CollectHashes(current, referenced);

                var operations = Path.Combine(gameDir, "operations");
                if (!Directory.Exists(operations)) continue;
                // 文件名以 yyyyMMddTHHmmssfffffff 开头，按名字倒序就是最新在前
                var files = Directory.GetFiles(operations, "*.json")
                    .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                    .ToList();
                for (var index = 0; index < files.Count; index++)
                {
                    var path = files[index];
                    if (index < keepOperationsPerGame || File.GetLastWriteTimeUtc(path) >= operationCutoff)
                    {
                        CollectHashes(path, referenced);
                        continue;
                    }
                    try
                    {
                        var length = new FileInfo(path).Length;
                        File.Delete(path);
                        result.RemovedOperations++;
                        result.FreedBytes += length;
                    }
                    catch
                    {
                        // 正在写 / 被占用：下次再清
                    }
                }
            }
        }

        var filesRoot = Path.Combine(root, "files");
        if (Directory.Exists(filesRoot))
        {
            var graceCutoff = now - blobGrace;
            foreach (var blob in Directory.EnumerateFiles(filesRoot))
            {
                try
                {
                    var info = new FileInfo(blob);
                    result.RemainingBytes += info.Length;
                    if (referenced.Contains(Path.GetFileName(blob)) || info.LastWriteTimeUtc >= graceCutoff) continue;
                    File.Delete(blob);
                    result.RemovedBlobs++;
                    result.FreedBytes += info.Length;
                    result.RemainingBytes -= info.Length;
                }
                catch
                {
                    // 单个文件删不掉不影响其它文件
                }
            }
        }

        return result;
    }

    /// <summary>把记录里出现的所有 SHA256 字符串收进引用集合（文件表、loaderHash、packageHash、inputs 等都在内）。</summary>
    private static void CollectHashes(string jsonPath, HashSet<string> hashes)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath), HistoryJsonOptions);
            CollectHashes(document.RootElement, hashes);
        }
        catch
        {
            // 损坏的记录不参与引用计算；它的备份按宽限期回收
        }
    }

    private static void CollectHashes(JsonElement element, HashSet<string> hashes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (value is { Length: 64 } && HashPattern.IsMatch(value)) hashes.Add(value.ToLowerInvariant());
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectHashes(item, hashes);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) CollectHashes(property.Value, hashes);
                break;
        }
    }
}
