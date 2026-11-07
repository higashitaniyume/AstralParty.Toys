using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayLibraryService
{
    // ============================== 归档 / 放回 ==============================

    /// <summary>把游戏目录里的回放移动到库（腾出席位）。</summary>
    public ReplayOperationResult Archive(IReadOnlyCollection<string> replayIds)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;

        foreach (var id in replayIds)
        {
            var source = GameFile(id);
            if (!File.Exists(source))
            {
                result.Skipped++;
                result.Messages.Add($"{id}：游戏目录里没有这个回放，已跳过。");
                continue;
            }

            var target = LibraryFile(id);
            try
            {
                if (File.Exists(target))
                {
                    if (FilesIdentical(source, target))
                    {
                        DeleteReplayFolder(Path.GetDirectoryName(source)!, out _);
                        result.Applied++;
                        result.Messages.Add($"{id}：库里已有同一份回放，已直接腾出游戏席位。");
                        continue;
                    }
                    result.Skipped++;
                    result.Messages.Add($"{id}：库里存在同 ID 但内容不同的回放，为避免覆盖已跳过（请手动处理）。");
                    continue;
                }

                var bytes = File.ReadAllBytes(source);
                CopyVerified(source, target);
                var meta = BuildMeta(id, bytes, "game-archive");
                meta.SizeBytes = new FileInfo(target).Length;
                meta.ModifiedUtcTicks = File.GetLastWriteTimeUtc(target).Ticks;
                WriteSidecar(meta);
                _cache[target] = new CacheEntry(meta.SizeBytes, meta.ModifiedUtcTicks, meta);

                if (!DeleteReplayFolder(Path.GetDirectoryName(source)!, out var error))
                {
                    result.Failed++;
                    result.Messages.Add($"{id}：已复制进库，但删除游戏目录副本失败（{error}）。库里有备份，可手动删除。");
                    continue;
                }

                result.Applied++;
                result.Messages.Add($"{id}：已归档到回放库。");
                Log("archive", id, $"{source} -> {target}");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"{id}：归档失败（{ex.Message}）。");
            }
        }

        return result;
    }

    /// <summary>
    /// 把库里的回放复制回游戏目录（库里永远保留副本）。
    /// allowUnparseable：工具解析不出结算帧时也照样放回——需要玩家确认过才传 true。
    /// </summary>
    public ReplayOperationResult Restore(IReadOnlyCollection<string> replayIds, bool allowUnparseable = false)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;

        foreach (var id in replayIds)
        {
            var source = LibraryFile(id);
            if (!File.Exists(source))
            {
                result.Skipped++;
                result.Messages.Add($"{id}：回放库里没有这个回放，已跳过。");
                continue;
            }

            var target = GameFile(id);
            try
            {
                // 游戏列本地回放时解析不出结算帧就会把整个目录删掉，
                // 所以解析不了的先拦一次问清楚，而不是直接放回去。
                var meta = ReadMeta(id, source, useSidecar: true);
                if (!meta.Healthy && !allowUnparseable)
                {
                    result.Ok = false;
                    result.Skipped++;
                    result.NeedsConfirmation = true;
                    result.PendingIds.Add(id);
                    result.Messages.Add($"{id}：本工具解析不出它的结算帧（工具自带的协议版本可能比游戏旧）。" +
                                        "如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。");
                    continue;
                }

                if (File.Exists(target))
                {
                    if (FilesIdentical(source, target))
                    {
                        result.Skipped++;
                        result.Messages.Add($"{id}：游戏目录里已经有这一局了。");
                        continue;
                    }
                    result.Skipped++;
                    result.Messages.Add($"{id}：游戏目录里已有同 ID 但内容不同的文件，为避免覆盖已跳过。");
                    continue;
                }

                CopyVerified(source, target);
                result.Applied++;
                result.Messages.Add($"{id}：已放回游戏目录，游戏内「本地回放」可直接观看。");
                Log("restore", id, $"{source} -> {target}");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"{id}：放回失败（{ex.Message}）。");
            }
        }

        if (result.Applied > 0 && CountGameSlots() > GameSlotCapacity)
        {
            result.Messages.Add($"注意：现在游戏目录里有 {CountGameSlots()} 局，超过游戏上限 {GameSlotCapacity}，" +
                                "继续保存新回放会失败。播放不受影响，建议归档几局。");
        }

        return result;
    }

    private int CountGameSlots() => EnumerateReplays(GameDirectory).Count();

    /// <summary>
    /// 把旧回放的结算版本标记升级到本工具当前支持的版本。
    ///
    /// 游戏能不能播由 <c>BattleShortRecord.IsValidVersion()</c> 决定，它严格比较
    /// <c>APP_VERSION-RES_VERSION</c>；旧回放的帧格式/协议可能完全兼容，只是版本字符串不等。
    /// 这里只改 1016 的 Version 字段，帧流其余字节原样保留，并在库内 <c>_backup</c> 留原文件。
    /// </summary>
    public ReplayOperationResult Upgrade(IReadOnlyCollection<string> replayIds)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;
        if (!EnsureProtocol())
            return ReplayOperationResult.Fail("协议程序集不可用，无法升级回放。请确认程序目录下存在 Protocol\\AstralParty.Runtime.dll。");

        var targetVersion = SupportedGameVersion;
        if (string.IsNullOrWhiteSpace(targetVersion))
            return ReplayOperationResult.Fail("读不到本工具支持的版本（GameData\\Version.bin 缺失或无法解析），无法确定升级目标。");

        foreach (var id in replayIds)
        {
            var sources = new List<(string Path, string Kind, string Label)>(2);
            var gameFile = GameFile(id);
            var libraryFile = LibraryFile(id);
            if (File.Exists(gameFile)) sources.Add((gameFile, "game", "游戏目录"));
            if (File.Exists(libraryFile)) sources.Add((libraryFile, "library", "回放库"));
            if (sources.Count == 0)
            {
                result.Skipped++;
                result.Messages.Add($"{id}：游戏目录和回放库里都没有这个回放，已跳过。");
                continue;
            }

            var appliedForId = false;
            foreach (var (path, kind, label) in sources)
            {
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    if (!_protocol!.TryParseSettlement(bytes, out var finish, out _))
                    {
                        result.Skipped++;
                        result.Messages.Add($"{id}：{label}副本解析不出结算帧，无法升级。");
                        continue;
                    }

                    var oldVersion = ReflectionValue.Text(finish, "Version");
                    if (string.Equals(oldVersion, targetVersion, StringComparison.Ordinal))
                    {
                        result.Skipped++;
                        result.Messages.Add($"{id}：{label}副本已经是当前支持的版本 {targetVersion}。");
                        continue;
                    }

                    if (CompareGameVersion(oldVersion, targetVersion) > 0)
                    {
                        result.Skipped++;
                        result.Messages.Add($"{id}：{label}副本版本 {oldVersion} 高于本工具支持的 {targetVersion}，未降级。");
                        continue;
                    }

                    if (!ReplayVersionUpgrader.TryUpgrade(bytes, targetVersion, out var upgraded, out var frameVersion, out var error))
                    {
                        result.Failed++;
                        result.Messages.Add($"{id}：{label}副本升级失败（{error}）。");
                        continue;
                    }

                    if (!_protocol.TryParseSettlement(upgraded, out var verify, out _) ||
                        !string.Equals(ReflectionValue.Text(verify, "Version"), targetVersion, StringComparison.Ordinal))
                    {
                        result.Failed++;
                        result.Messages.Add($"{id}：{label}副本升级后校验失败，未写入。");
                        continue;
                    }

                    BackupBeforeUpgrade(id, path, kind, frameVersion);
                    WriteBytesAtomic(path, upgraded);

                    var meta = BuildMeta(id, upgraded, "version-upgrade");
                    meta.SizeBytes = new FileInfo(path).Length;
                    meta.ModifiedUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;
                    _cache[path] = new CacheEntry(meta.SizeBytes, meta.ModifiedUtcTicks, meta);
                    if (kind == "library") WriteSidecar(meta);

                    appliedForId = true;
                    var libraryOnlyHint = kind == "library" && !File.Exists(gameFile)
                        ? "要进游戏观看，请再点「放回游戏」。"
                        : "";
                    result.Messages.Add($"{id}：{label}副本已从 {frameVersion} 升级到 {targetVersion}，原文件已备份。{libraryOnlyHint}");
                    Log("upgrade", id, $"{path}: {frameVersion} -> {targetVersion}");
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"{id}：{label}副本升级失败（{ex.Message}）。");
                }
            }

            if (appliedForId) result.Applied++;
        }

        if (result.Applied == 0 && result.Failed == 0 && result.Skipped == 0)
            result.Messages.Add("没有可升级的回放。");
        return result;
    }

    /// <summary>版本号形如 <c>3.3.0-0.0.0</c>；解析失败时退回序数比较。</summary>
    internal static int CompareGameVersion(string left, string right)
    {
        if (TryParseGameVersion(left, out var leftApp, out var leftRes) &&
            TryParseGameVersion(right, out var rightApp, out var rightRes))
        {
            var compared = leftApp.CompareTo(rightApp);
            return compared != 0 ? compared : leftRes.CompareTo(rightRes);
        }
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseGameVersion(string value, out Version app, out Version res)
    {
        app = new Version(0, 0, 0);
        res = new Version(0, 0, 0);
        var parts = value.Split('-', 2);
        if (parts.Length != 2) return false;
        if (!Version.TryParse(parts[0], out var parsedApp) || parsedApp is null) return false;
        if (!Version.TryParse(parts[1], out var parsedRes) || parsedRes is null) return false;
        app = parsedApp;
        res = parsedRes;
        return true;
    }

    /// <summary>升级前把原文件复制到库内 <c>_backup\&lt;id&gt;</c>；同名备份已存在时保留最早那份。</summary>
    private void BackupBeforeUpgrade(string replayId, string sourceFile, string kind, string oldVersion)
    {
        var safeVersion = new string(oldVersion.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray());
        if (safeVersion.Length == 0) safeVersion = "unknown";
        var directory = Path.Combine(LibraryRoot, "_backup", replayId);
        Directory.CreateDirectory(directory);
        var backup = Path.Combine(directory, $"{replayId}.{safeVersion}.{kind}.bak");
        if (!File.Exists(backup)) CopyVerified(sourceFile, backup);
    }

    private static void WriteBytesAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".upgrade.tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            TryDeleteFile(temp);
        }
    }

    /// <summary>归档游戏目录里最旧的若干局，只保留最新 keepCount 个。</summary>
    public ReplayOperationResult Maintain(int? keepCount = null)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;

        var keep = Math.Clamp(keepCount ?? Settings.KeepInGame, 1, GameSlotCapacity);
        var snapshot = Snapshot();
        var excess = snapshot.Entries.Where(x => x.InGame).Skip(keep).ToList();
        if (excess.Count == 0)
        {
            result.Messages.Add($"游戏目录里只有 {snapshot.GameCount} 局，未超过保留名额 {keep}，无需整理。");
            return result;
        }

        var archived = Archive(excess.Select(x => x.ReplayId).ToList());
        result.Applied = archived.Applied;
        result.Skipped = archived.Skipped;
        result.Failed = archived.Failed;
        result.Ok = archived.Ok;
        result.Messages.AddRange(archived.Messages);
        result.Messages.Insert(0, $"整理完成：游戏目录保留最新 {keep} 局，归档 {archived.Applied} 局。");
        Log("maintain", string.Join(',', excess.Select(x => x.ReplayId)), $"keep={keep}");
        return result;
    }

    /// <summary>把一个外部回放文件导入库（文件名不必是回放 ID）。</summary>
    public ReplayOperationResult Import(string filePath)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;
        if (!File.Exists(filePath)) return ReplayOperationResult.Fail($"文件不存在：{filePath}");

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var meta = BuildMeta(Path.GetFileNameWithoutExtension(filePath), bytes, "manual-import");
            if (!meta.Healthy)
                return ReplayOperationResult.Fail("这个文件不是能解出结算帧的回放，已拒绝导入。");

            var id = meta.FileReplayId is { Length: > 0 } fileReplayId
                ? fileReplayId
                : Path.GetFileName(filePath);
            if (id.Length == 0) return ReplayOperationResult.Fail("无法确定回放 ID。");

            var target = LibraryFile(id);
            if (File.Exists(target) && FilesIdentical(filePath, target))
            {
                result.Skipped++;
                result.Messages.Add($"{id}：库里已经有这一局了。");
                return result;
            }

            CopyVerified(filePath, target);
            meta.ReplayId = id;
            meta.SizeBytes = new FileInfo(target).Length;
            meta.ModifiedUtcTicks = File.GetLastWriteTimeUtc(target).Ticks;
            WriteSidecar(meta);
            _cache[target] = new CacheEntry(meta.SizeBytes, meta.ModifiedUtcTicks, meta);
            result.Applied++;
            result.Messages.Add($"已导入 {id} 到回放库。");
            Log("import", id, filePath);
        }
        catch (Exception ex)
        {
            return ReplayOperationResult.Fail($"导入失败：{ex.Message}");
        }

        return result;
    }

    // ============================== 删除 ==============================

    public ReplayOperationResult Delete(IReadOnlyCollection<string> replayIds, ReplayDeleteTarget target)
    {
        var result = new ReplayOperationResult();
        var touchedLibrary = false;

        foreach (var id in replayIds)
        {
            if (target is ReplayDeleteTarget.Game or ReplayDeleteTarget.Both)
            {
                var folder = Path.GetDirectoryName(GameFile(id))!;
                if (Directory.Exists(folder))
                {
                    var outcome = RecycleBinHelper.Delete(folder, out var error);
                    if (outcome != DeleteOutcome.Failed)
                    {
                        result.Applied++;
                        result.Messages.Add($"{id}：已从游戏目录删除{RecycleBinHelper.Describe(outcome, toRecycleBin: true)}。");
                        Log("delete-game", id, folder);
                    }
                    else
                    {
                        result.Failed++;
                        result.Messages.Add($"{id}：删除游戏目录副本失败（{error}）。");
                    }
                }
            }

            if (target is ReplayDeleteTarget.Library or ReplayDeleteTarget.Both)
            {
                var folder = Path.GetDirectoryName(LibraryFile(id))!;
                if (Directory.Exists(folder))
                {
                    var outcome = RecycleBinHelper.Delete(folder, out var error);
                    if (outcome != DeleteOutcome.Failed)
                    {
                        result.Applied++;
                        result.Messages.Add($"{id}：已从回放库删除{RecycleBinHelper.Describe(outcome, toRecycleBin: true)}。");
                        touchedLibrary = true;
                        Log("delete-library", id, folder);
                    }
                    else
                    {
                        result.Failed++;
                        result.Messages.Add($"{id}：删除库内副本失败（{error}）。");
                    }
                }
                try
                {
                    var sidecar = SidecarFile(id);
                    if (File.Exists(sidecar)) File.Delete(sidecar);
                }
                catch { /* 索引残留无害 */ }
            }
        }

        if (!touchedLibrary && result.Applied == 0 && result.Failed == 0)
            result.Messages.Add("没有可删除的回放。");
        return result;
    }

    // ============================== 工具方法 ==============================

    private ReplayOperationResult? Guard(ReplayOperationResult result)
    {
        if (IsSameFolder())
            return ReplayOperationResult.Fail("回放库目录与游戏回放目录不能是同一个路径，操作已拒绝。请先在设置里修改库目录。");
        return null;
    }

    private static bool FilesIdentical(string left, string right)
    {
        try
        {
            if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(left)))
                == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(right)));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>复制到目标（先写 .part、校验哈希、再改名），保证库/游戏目录里不会出现半截文件。</summary>
    private static void CopyVerified(string sourceFile, string targetFile)
    {
        var directory = Path.GetDirectoryName(targetFile)!;
        Directory.CreateDirectory(directory);
        var expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile)));
        var temp = targetFile + ".part";
        File.Copy(sourceFile, temp, overwrite: true);
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temp)));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(temp); } catch { }
            throw new IOException("复制后校验失败，目标文件已清理，源文件保持不动。");
        }
        File.Move(temp, targetFile, overwrite: true);
    }

    private static bool DeleteReplayFolder(string folder, out string error)
    {
        error = "";
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void Log(string action, string replayIds, string detail)
    {
        try
        {
            EnsureLibrary();
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{action}\t{replayIds}\t{detail}{Environment.NewLine}";
            File.AppendAllText(OperationsLogPath, line, Encoding.UTF8);
        }
        catch { /* 日志失败不影响操作结果 */ }
    }

    /// <summary>临时文件 + 原子替换（MoveFileEx 覆盖），避免断电/崩溃留下半个 JSON。</summary>
    internal static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}
