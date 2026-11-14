using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 安装历史里的「可回滚版本」。
///
/// 每次走发布包安装 / 更新都会把整包留在 <c>install-history\files</c>，操作记录里同时记下了
/// 操作前后的版本号和两个哈希。这里把记录翻出来，只保留**整包还在本机**的版本：
/// 回滚就是把当时那份发布包重新走一遍 <see cref="InstallPackage"/>，加载器、SDK、内置模组
/// 一起回到那个版本，模组配置与停用状态照旧保留。
///
/// 只有 DLL 备份、没有发布包的版本不列出来 —— 单换 version.dll 会和当前 SDK 对不上，
/// 不如让用户改用「从 GitHub 更新」或导入发布包。
/// </summary>
public sealed partial class ModManager
{
    /// <summary>一个仍可回滚的版本。</summary>
    public sealed class RetainedVersionEntry
    {
        public string Version { get; set; } = "";
        public DateTime TimeUtc { get; set; }
        public bool PackageAvailable { get; set; }
        public bool Current { get; set; }
        public string PackageHash { get; set; } = "";
    }

    /// <summary>当前游戏目录的安装历史里还能回滚的版本（最新的在前）。</summary>
    public IReadOnlyList<RetainedVersionEntry> GetRetainedVersions(string gameDirectory)
    {
        var versions = new List<RetainedVersionEntry>();
        if (string.IsNullOrWhiteSpace(gameDirectory)) return versions;

        var seen = new Dictionary<string, RetainedVersionEntry>(StringComparer.OrdinalIgnoreCase);
        void Consider(string? version, string? packageHash, DateTime time)
        {
            var value = (version ?? "").Trim();
            if (value.Length == 0) return;
            if (!seen.TryGetValue(value, out var entry))
                seen[value] = entry = new RetainedVersionEntry { Version = value };

            var available = IsBlobAvailable(packageHash);
            // 同一个版本可能出现在多条记录里：保留「有整包」的那条，时间取最新
            if (available && !entry.PackageAvailable)
            {
                entry.PackageAvailable = true;
                entry.PackageHash = packageHash!;
                if (time > entry.TimeUtc) entry.TimeUtc = time;
            }
            else if (available && time > entry.TimeUtc)
            {
                entry.PackageHash = packageHash!;
                entry.TimeUtc = time;
            }
            else if (!entry.PackageAvailable && time > entry.TimeUtc)
            {
                entry.TimeUtc = time;
            }
        }

        var operations = Path.Combine(GameHistoryDirectory(gameDirectory), "operations");
        if (Directory.Exists(operations))
        {
            foreach (var path in Directory.GetFiles(operations, "*.json")
                         .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    var root = document.RootElement;
                    var time = root.TryGetProperty("timeUtc", out var timeElement)
                               && timeElement.TryGetDateTime(out var parsed)
                        ? parsed.ToUniversalTime()
                        : File.GetLastWriteTimeUtc(path);
                    foreach (var name in new[] { "after", "before" })
                    {
                        if (!root.TryGetProperty(name, out var snapshot) || snapshot.ValueKind != JsonValueKind.Object)
                            continue;
                        // InstallationRecord 没有改命名策略，序列化出来是 PascalCase
                        Consider(GetString(snapshot, "Version"), GetString(snapshot, "PackageHash"), time);
                    }
                }
                catch
                {
                    // 损坏的记录直接跳过，不影响其它版本
                }
            }
        }

        var currentVersion = ReadCurrentInstallationVersion(gameDirectory);
        var record = ReadRetainedInstallation(gameDirectory);
        if (currentVersion.Length > 0)
        {
            var packageHash = record?.PackageHash ?? "";
            if (packageHash.Length > 0 && IsBlobAvailable(packageHash))
            {
                if (!seen.TryGetValue(currentVersion, out var current))
                    seen[currentVersion] = current = new RetainedVersionEntry { Version = currentVersion };
                current.PackageAvailable = true;
                current.PackageHash = packageHash;
                if (record is not null && record.UpdatedUtc > current.TimeUtc) current.TimeUtc = record.UpdatedUtc;
            }
            else if (!seen.ContainsKey(currentVersion))
            {
                seen[currentVersion] = new RetainedVersionEntry
                {
                    Version = currentVersion,
                    TimeUtc = record?.UpdatedUtc ?? DateTime.UtcNow
                };
            }
            seen[currentVersion].Current = true;
        }

        versions.AddRange(seen.Values.OrderByDescending(entry => entry.TimeUtc));
        return versions;
    }

    /// <summary>用安装历史里的发布包把加载器回滚到指定版本。当前已经是该版本时抛异常。</summary>
    public string RollbackLoader(string gameDirectory, string version, LoaderProgressCallback? progress = null)
    {
        var target = (version ?? "").Trim();
        if (target.Length == 0) throw new ArgumentException("请选择要回滚到的版本。");
        var entry = GetRetainedVersions(gameDirectory)
            .FirstOrDefault(item => item.Version.Equals(target, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"安装历史里找不到 v{target}，可能已经超过保留期被清理。");
        if (entry.Current)
            throw new InvalidOperationException($"当前已经是 v{target}，不需要回滚。");
        if (!entry.PackageAvailable)
            throw new InvalidOperationException(
                $"安装历史里 v{target} 只留下了加载器 DLL，没有完整发布包，无法整包回滚。" +
                "可以改用「从 GitHub 更新」，或者导入那个版本的发布包。");

        var blob = BlobPath(entry.PackageHash);
        if (!File.Exists(blob))
            throw new InvalidOperationException($"安装历史里的 v{target} 发布包已经不在了，可能被清理掉了。");

        var bytes = File.ReadAllBytes(blob);
        var manifest = ParsePackageManifest(bytes)
            ?? throw new InvalidDataException("安装历史里的发布包缺少版本清单，无法确认版本。");
        if (!string.Equals(manifest.Version, target, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"安装历史里的发布包版本是 v{manifest.Version}，与记录的 v{target} 不符，已停止回滚。");

        progress?.Invoke("verify", 5, $"正在准备回滚到 v{target}…");
        ToysLog.Info("modloader", $"从安装历史回滚加载器：{ReadCurrentInstallationVersion(gameDirectory)} → {target}");
        InstallPackage(gameDirectory, bytes, overwriteDll: true, allowDowngrade: true, progress);
        return $"已从安装历史回滚到 CesiumLoader v{target}，模组配置与停用状态保持不变";
    }

    private bool IsBlobAvailable(string? hash)
    {
        if (string.IsNullOrEmpty(hash) || !Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) return false;
        try { return File.Exists(BlobPath(hash)); }
        catch { return false; }
    }

    private static string GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
