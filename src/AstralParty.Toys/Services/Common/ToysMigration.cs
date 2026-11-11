using System.Security.Cryptography;
using System.Text.Json;

namespace AstralParty.Toys.Services;

public sealed class ToysMigrationReport
{
    public List<string> Actions { get; } = [];
    public List<string> Problems { get; } = [];

    public bool Changed => Actions.Count > 0 || Problems.Count > 0;

    public string Describe() => Actions.Count == 0 && Problems.Count == 0
        ? "无需迁移"
        : string.Join("；", Actions.Concat(Problems.Select(p => "未完成：" + p)));
}

/// <summary>
/// 把旧布局（文档\AstralPartyReplays + %APPDATA% 里的零散 json + %LOCALAPPDATA% 的封面缓存）
/// 收拢到新的数据根。可以反复执行：每一步都先看目标在不在，不会覆盖已有数据。
///
/// 同一卷内直接用 <see cref="Directory.Move"/>（改名，不会出现"搬了一半"）；跨卷才走
/// 复制 → 校验（大小 + SHA256）→ 删除源。
/// </summary>
public static class ToysMigration
{
    private static readonly string[] StateFiles =
        ["game-profiles.json", "modloader-state.json", "speedhack-state.json"];

    private static readonly string[] ConfigDirectories =
    [
        ToysPaths.ReplaysFolderName, ToysPaths.CacheFolderName, ToysPaths.LogsFolderName,
        ToysPaths.InstallHistoryFolderName, ToysPaths.StateFolderName, ToysPaths.BackgroundsFolderName,
        ToysPaths.ToolsFolderName, ToysPaths.SpeedhackFolderName
    ];

    public static ToysMigrationReport Run(string root, ToysConfigStore store,
        string? legacyDocumentsRoot = null, string? legacyRoamingRoot = null, string? legacyLocalCache = null)
    {
        var report = new ToysMigrationReport();
        root = Path.GetFullPath(root);
        legacyDocumentsRoot ??= ToysPaths.LegacyDocumentsRoot;
        legacyRoamingRoot ??= ToysPaths.LegacyRoamingRoot;
        legacyLocalCache ??= ToysPaths.LegacyWorkshopCacheDirectory;

        try
        {
            RelocateLegacyRoot(root, legacyDocumentsRoot, report);
            Reorganize(root, report);
            MergeLegacyRoaming(root, legacyRoamingRoot, report);
            MoveLegacyWorkshopCache(root, legacyLocalCache, report);
            ImportLegacySettings(root, store, legacyRoamingRoot, report);
        }
        catch (Exception ex)
        {
            report.Problems.Add(ex.Message);
            ToysLog.Error("migration", "迁移旧数据时出错", ex);
        }

        try
        {
            var config = store.Load();
            var note = config.Migration.CompletedUtc is null
                ? $"数据文件夹：{root}"
                : $"数据文件夹：{root}（已从 {config.Migration.FromRoot} 迁移）";
            ToysLog.Info("migration", report.Changed ? $"{note}；{report.Describe()}" : note);
        }
        catch
        {
            // 日志组件还没配置好也一样继续
        }

        return report;
    }

    // ============================== 1. 旧「文档」根 ==============================

    private static void RelocateLegacyRoot(string root, string legacy, ToysMigrationReport report)
    {
        if (IsSamePath(root, legacy) || !Directory.Exists(legacy)) return;

        if (!Directory.Exists(root))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            }
            catch (Exception ex)
            {
                report.Problems.Add($"无法创建 {root}：{ex.Message}");
                return;
            }
        }

        // 新目录已存在但是空的：先删掉这个空壳，整体改名才走得通；否则 Directory.Move 会失败，
        // 退化成逐文件复制 → 校验 → 删除的慢路径。
        if (Directory.Exists(root) && IsEmptyDirectory(root))
        {
            try
            {
                Directory.Delete(root, false);
            }
            catch (Exception ex)
            {
                // 删不掉就老实走合并，至少不会丢数据
                report.Problems.Add($"无法清理空目录 {root}：{ex.Message}");
            }
        }

        if (!Directory.Exists(root))
        {
            if (TryMoveDirectory(legacy, root, report, $"旧数据目录 {legacy} → {root}"))
                report.Actions.Add($"旧数据目录已整体搬到 {root}");
            return;
        }

        // 新目录已经有东西：逐个合并，已有的不覆盖
        MergeDirectory(legacy, root, report);
        TryDeleteEmpty(legacy);
    }

    // ============================== 2. 根目录内部整理 ==============================

    private static void Reorganize(string root, ToysMigrationReport report)
    {
        Directory.CreateDirectory(root);

        var history = Path.Combine(root, "modloader-history");
        var historyTarget = Path.Combine(root, ToysPaths.InstallHistoryFolderName);
        if (Directory.Exists(history) && !Directory.Exists(historyTarget))
        {
            if (TryMoveDirectory(history, historyTarget, report, "安装历史目录改名"))
                report.Actions.Add("modloader-history → install-history");
        }

        var replays = Path.Combine(root, ToysPaths.ReplaysFolderName);
        var indexSource = Path.Combine(root, "_index");
        var indexTarget = Path.Combine(replays, "_index");

        var moved = 0;
        foreach (var directory in Directory.EnumerateDirectories(root).ToArray())
        {
            var name = Path.GetFileName(directory);
            if (ConfigDirectories.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

            // 回放的形态是「目录名 == 内部文件名」；配置目录永远不会满足这一条
            if (!File.Exists(Path.Combine(directory, name))) continue;

            var target = Path.Combine(replays, name);
            if (File.Exists(target) || Directory.Exists(target)) continue;

            Directory.CreateDirectory(replays);
            if (TryMoveDirectory(directory, target, report, $"回放 {name} → replays"))
                moved++;
        }
        if (moved > 0) report.Actions.Add($"{moved} 局回放移入 replays\\");

        if (Directory.Exists(indexSource) && !Directory.Exists(indexTarget))
        {
            Directory.CreateDirectory(replays);
            if (TryMoveDirectory(indexSource, indexTarget, report, "回放索引移入 replays\\_index"))
                report.Actions.Add("_index → replays\\_index");
        }

        var state = Path.Combine(root, ToysPaths.StateFolderName);
        foreach (var name in StateFiles)
        {
            var source = Path.Combine(root, name);
            var target = Path.Combine(state, name);
            if (!File.Exists(source) || File.Exists(target)) continue;
            if (TryMoveFile(source, target, report, $"状态文件 {name} → state\\"))
                report.Actions.Add($"{name} → state\\");
        }
    }

    // ============================== 3. 旧 %APPDATA% 配置 ==============================

    private static void MergeLegacyRoaming(string root, string roaming, ToysMigrationReport report)
    {
        if (!Directory.Exists(roaming)) return;

        var speedhack = Path.Combine(roaming, "speedhack", "speedhack_config.json");
        var speedhackTarget = Path.Combine(root, ToysPaths.SpeedhackFolderName, "speedhack_config.json");
        if (File.Exists(speedhack) && !File.Exists(speedhackTarget))
        {
            if (TryMoveFile(speedhack, speedhackTarget, report, "旧主配置迁入 speedhack\\"))
                report.Actions.Add("speedhack_config.json 迁入 speedhack\\");
        }

        foreach (var name in StateFiles)
        {
            var source = Path.Combine(roaming, name);
            var target = Path.Combine(root, ToysPaths.StateFolderName, name);
            if (!File.Exists(source) || File.Exists(target)) continue;
            if (TryMoveFile(source, target, report, $"旧状态文件 {name} 迁入 state\\"))
                report.Actions.Add($"{name} 迁入 state\\");
        }
    }

    // ============================== 4. 旧封面缓存 ==============================

    private static void MoveLegacyWorkshopCache(string root, string legacy, ToysMigrationReport report)
    {
        if (!Directory.Exists(legacy)) return;
        var target = Path.Combine(root, ToysPaths.CacheFolderName, "workshop");
        if (IsSamePath(legacy, target)) return;

        if (!Directory.Exists(target))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            }
            catch (Exception ex)
            {
                report.Problems.Add($"无法创建缓存目录 {target}：{ex.Message}");
                return;
            }

            if (TryMoveDirectory(legacy, target, report, "封面缓存迁入 cache\\workshop"))
            {
                report.Actions.Add("封面缓存 → cache\\workshop");
                return;
            }
        }

        MergeDirectory(legacy, target, report);
        TryDeleteEmpty(legacy);
    }

    // ============================== 5. 旧设置文件 → 统一配置 ==============================

    private static void ImportLegacySettings(string root, ToysConfigStore store, string? roaming,
        ToysMigrationReport report)
    {
        var configPath = store.FilePath;
        var legacyReplay = FirstExisting(
            Path.Combine(root, "replay-library.json"),
            roaming is null ? null : Path.Combine(roaming, "replay-library.json"));
        var legacyTheme = FirstExisting(Path.Combine(root, "ui-theme.txt"));
        var legacyConsent = FirstExisting(
            Path.Combine(root, "account-consent.txt"),
            roaming is null ? null : Path.Combine(roaming, "account-consent.txt"));

        if (legacyReplay is null && legacyTheme is null && legacyConsent is null) return;

        if (!File.Exists(configPath))
        {
            var config = new ToysConfig();

            if (legacyReplay is not null)
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(legacyReplay));
                    ReadLegacyReplay(document.RootElement, config);
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    report.Problems.Add($"旧回放设置无法解析：{ex.Message}");
                }
            }

            if (legacyTheme is not null)
            {
                try
                {
                    if (File.ReadAllText(legacyTheme).Trim() == "light") config.App.Theme = "light";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 读不到就当没设过
                }
            }

            if (legacyConsent is not null)
            {
                try
                {
                    config.App.MyAccountConsent = File.ReadAllText(legacyConsent).Trim() == "1";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 同上
                }
            }

            config.Migration.CompletedUtc = DateTimeOffset.UtcNow;
            config.Migration.FromRoot = ToysPaths.LegacyDocumentsRoot;

            store.Save(config);

            // 写得出来、读得回来才算成功，之后才删旧文件
            var verified = store.Load();
            if (verified.SchemaVersion != ToysConfigStore.CurrentSchemaVersion)
            {
                report.Problems.Add("统一配置写入后校验失败，旧设置文件已保留");
                return;
            }

            report.Actions.Add("旧设置合并进 AstralParty.Toys.Config.json");
        }

        DeleteIfExists(legacyReplay, report);
        DeleteIfExists(legacyTheme, report);
        DeleteIfExists(legacyConsent, report);

        if (roaming is not null)
        {
            DeleteIfExists(Path.Combine(roaming, "replay-library.json"), report);
            DeleteIfExists(Path.Combine(roaming, "ui-theme.txt"), report);
            DeleteIfExists(Path.Combine(roaming, "account-consent.txt"), report);
        }
    }

    private static void ReadLegacyReplay(JsonElement root, ToysConfig config)
    {
        string? text(string name) =>
            root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        bool flag(string name) =>
            root.TryGetProperty(name, out var element) && element.ValueKind is JsonValueKind.True;
        int number(string name, int fallback) =>
            root.TryGetProperty(name, out var element) && element.TryGetInt32(out var value) ? value : fallback;

        config.App.Language = AppLanguage.Normalize(text("language"));
        config.Replay.AutoMaintain = flag("autoMaintain");
        config.Replay.KeepInGame = number("keepInGame", ReplayServiceLimits.GameSlotCapacity);
        config.Workshop.Url = text("workshopUrl") ?? WorkshopService.DefaultBaseUrl;
        config.Workshop.ConsentVersion = number("workshopConsentVersion", 0);
        if (root.TryGetProperty("workshopConsentAt", out var consent) && consent.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(consent.GetString(), out var consentAt))
        {
            config.Workshop.ConsentAtUtc = consentAt;
        }

        config.Update.AutoCheck = !root.TryGetProperty("autoCheckUpdates", out var check)
            || check.ValueKind != JsonValueKind.False;
        config.Update.AutoDownload = flag("autoDownloadUpdates");
        config.Update.AutoInstall = flag("autoInstallUpdates");
        config.Update.GitHubMirrorType = text("githubMirrorType") ?? GitHubMirrorHelper.DefaultMirrorType;
        config.Update.CustomGitHubMirrorUrl = text("customGitHubMirrorUrl") ?? "";
    }

    // ============================== 文件操作 ==============================

    private static bool TryMoveDirectory(string source, string target, ToysMigrationReport report, string what)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(source, target);
            ToysLog.Info("migration", what);
            return true;
        }
        catch (IOException)
        {
            // 多半是跨卷：改用复制 → 校验 → 删除
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or NotSupportedException)
        {
            report.Problems.Add($"{what}：{ex.Message}");
            return false;
        }

        return CopyVerifyDelete(source, target, report, what);
    }

    private static bool TryMoveFile(string source, string target, ToysMigrationReport report, string what)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target);
            return true;
        }
        catch (IOException)
        {
            // 跨卷或目标被占用，走复制校验
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or NotSupportedException)
        {
            report.Problems.Add($"{what}：{ex.Message}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: false);
            if (SameFileContents(source, target))
            {
                File.Delete(source);
                return true;
            }
            report.Problems.Add($"{what}：复制后校验不一致，源文件已保留");
            return false;
        }
        catch (Exception ex)
        {
            report.Problems.Add($"{what}：{ex.Message}");
            return false;
        }
    }

    private static bool CopyVerifyDelete(string source, string target, ToysMigrationReport report, string what)
    {
        try
        {
            CopyDirectory(source, target);
            if (!SameTree(source, target))
            {
                report.Problems.Add($"{what}：复制后校验不一致，源目录已保留");
                return false;
            }

            Directory.Delete(source, recursive: true);
            ToysLog.Info("migration", $"{what}（复制校验后删除源）");
            return true;
        }
        catch (Exception ex)
        {
            report.Problems.Add($"{what}：{ex.Message}");
            return false;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static bool SameTree(string source, string target)
    {
        var sourceFiles = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToArray();
        var targetFiles = Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).ToArray();
        if (sourceFiles.Length != targetFiles.Length) return false;

        foreach (var file in sourceFiles)
        {
            var relative = Path.GetRelativePath(source, file);
            var counterpart = Path.Combine(target, relative);
            if (!File.Exists(counterpart) || !SameFileContents(file, counterpart)) return false;
        }
        return true;
    }

    private static bool SameFileContents(string left, string right)
    {
        try
        {
            if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
            using var a = File.OpenRead(left);
            using var b = File.OpenRead(right);
            return SHA256.HashData(a).AsSpan().SequenceEqual(SHA256.HashData(b));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>把 <paramref name="source"/> 合并进已存在的 <paramref name="target"/>，冲突时保留目标。</summary>
    private static void MergeDirectory(string source, string target, ToysMigrationReport report)
    {
        if (!Directory.Exists(target)) Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source).ToArray())
        {
            var destination = Path.Combine(target, Path.GetFileName(file));
            if (File.Exists(destination)) continue;
            TryMoveFile(file, destination, report, $"合并 {Path.GetFileName(file)}");
        }

        foreach (var directory in Directory.EnumerateDirectories(source).ToArray())
        {
            var destination = Path.Combine(target, Path.GetFileName(directory));
            if (!Directory.Exists(destination))
            {
                TryMoveDirectory(directory, destination, report, $"合并目录 {Path.GetFileName(directory)}");
            }
            else
            {
                MergeDirectory(directory, destination, report);
                TryDeleteEmpty(directory);
            }
        }
    }

    private static void TryDeleteEmpty(string directory)
    {
        try
        {
            if (Directory.Exists(directory)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch
        {
            // 删不掉就留着
        }
    }

    private static void DeleteIfExists(string? path, ToysMigrationReport report)
    {
        if (path is null) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            report.Problems.Add($"旧文件删除失败：{path}（{ex.Message}）");
        }
    }

    private static string? FirstExisting(params string?[] candidates) =>
        candidates.FirstOrDefault(path => path is not null && File.Exists(path));

    private static bool IsEmptyDirectory(string directory)
    {
        try { return !Directory.EnumerateFileSystemEntries(directory).Any(); }
        catch { return false; }
    }

    public static bool IsSamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd('\\'),
                Path.GetFullPath(right).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
