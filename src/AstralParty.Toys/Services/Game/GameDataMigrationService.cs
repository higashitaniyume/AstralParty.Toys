using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace AstralParty.Toys.Services.Game;

public sealed record GameDataDriveOption(
    string Root,
    string Label,
    long FreeBytes,
    long TotalBytes,
    bool IsSystemDrive,
    string SuggestedTarget);

public sealed record GameDataStorageStatus(
    bool Available,
    string? Error,
    string Company,
    string? Product,
    string SourcePath,
    bool SourceExists,
    bool IsMigrated,
    string? TargetPath,
    bool TargetExists,
    long TotalBytes,
    int FileCount,
    bool GameRunning,
    string? LocalLowDrive,
    long LocalLowFreeBytes,
    IReadOnlyList<GameDataDriveOption> Drives);

public sealed record GameDataTargetPreview(
    bool Ok,
    string Message,
    string PickedFolder,
    string FinalTarget,
    string? DriveRoot,
    string? DriveFormat,
    long FreeBytes,
    long RequiredBytes);

public sealed record GameDataMigrationProgress(
    string Stage,
    string Message,
    long CopiedBytes,
    long TotalBytes,
    int CopiedFiles,
    int TotalFiles,
    int Percent);

public sealed record GameDataMigrationResult(
    bool Ok,
    string Message,
    GameDataStorageStatus? Status = null,
    string? Warning = null);

/// <summary>
/// 游戏本地数据（%USERPROFILE%\AppData\LocalLow\&lt;Company&gt;）一站式 C 盘迁移服务：
/// 整目录搬迁 + 原位建立 NTFS 目录联接（Junction），无需管理员权限。
/// 迁移对象是 &lt;Company&gt;（例如 feimo）而非单个游戏子目录，确保游戏数据与多开档案处于同一 NTFS 卷，
/// 使多开缓存硬链接、体积统计与热更新均保持正常。
/// </summary>
public static class GameDataMigrationService
{
    public const long DefaultSafetyMarginBytes = 512L * 1024 * 1024;
    private const int CopyBufferSize = 128 * 1024;

    private readonly record struct NtfsFileId(uint VolumeSerialNumber, uint FileIndexHigh, uint FileIndexLow);

    private sealed record CopiedFileRecord(
        string TargetFile,
        long Length,
        string? Sha256Hex,
        bool IsHardLinkDuplicate,
        string? PrimaryTarget);

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out ByHandleFileInformation lpFileInformation);

    [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDiskFreeSpaceExW(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailableToCaller,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    /// <summary>读取当前游戏数据占用、迁移状态与可用目标盘。</summary>
    public static GameDataStorageStatus Inspect(
        string? gameDirectory,
        string? localLowRoot = null,
        Func<bool>? isGameRunning = null)
    {
        var running = SafeCheckRunning(gameDirectory, isGameRunning);
        if (!TryResolveCompanyFolder(gameDirectory, localLowRoot, out var company, out var product, out var sourcePath, out var resolveError))
        {
            return new GameDataStorageStatus(
                Available: false,
                Error: resolveError,
                Company: company,
                Product: product,
                SourcePath: sourcePath,
                SourceExists: false,
                IsMigrated: false,
                TargetPath: null,
                TargetExists: false,
                TotalBytes: 0,
                FileCount: 0,
                GameRunning: running,
                LocalLowDrive: null,
                LocalLowFreeBytes: 0,
                Drives: Array.Empty<GameDataDriveOption>());
        }

        var localLowDrive = NormalizeDriveRoot(Path.GetPathRoot(sourcePath));
        var localLowFree = GetAvailableFreeBytes(Path.GetDirectoryName(sourcePath) ?? sourcePath);
        var drives = EnumerateDriveOptions(company, localLowDrive);

        var isReparse = IsDirectoryReparsePoint(sourcePath);
        var sourceExists = Directory.Exists(sourcePath) || isReparse;
        string? targetPath = null;
        var targetExists = false;

        if (isReparse)
        {
            targetPath = TryResolveLinkTarget(sourcePath);
            targetExists = !string.IsNullOrWhiteSpace(targetPath) && Directory.Exists(targetPath);
        }

        var statsRoot = isReparse
            ? (targetExists ? targetPath! : "")
            : (Directory.Exists(sourcePath) ? sourcePath : "");

        var (totalBytes, fileCount, _) = string.IsNullOrEmpty(statsRoot)
            ? (0L, 0, 0L)
            : ScanDirectoryStats(statsRoot);

        return new GameDataStorageStatus(
            Available: true,
            Error: null,
            Company: company,
            Product: product,
            SourcePath: sourcePath,
            SourceExists: sourceExists,
            IsMigrated: isReparse,
            TargetPath: targetPath,
            TargetExists: targetExists,
            TotalBytes: totalBytes,
            FileCount: fileCount,
            GameRunning: running,
            LocalLowDrive: localLowDrive,
            LocalLowFreeBytes: localLowFree,
            Drives: drives);
    }

    /// <summary>校验用户选择（或推荐）的目标文件夹，并返回最终落盘路径与空间检查结果。</summary>
    public static GameDataTargetPreview ValidateTarget(
        string? gameDirectory,
        string pickedFolder,
        string? localLowRoot = null,
        Func<bool>? isGameRunning = null,
        bool allowSameVolumeForTest = false,
        bool allowNonNtfsForTest = false,
        long safetyMarginBytes = DefaultSafetyMarginBytes)
    {
        var status = Inspect(gameDirectory, localLowRoot, isGameRunning);
        var trimmed = (pickedFolder ?? "").Trim();
        if (!status.Available)
        {
            return new GameDataTargetPreview(false, status.Error ?? "无法识别游戏数据目录。", trimmed, "", null, null, 0, 0);
        }

        if (!status.IsMigrated && !status.SourceExists)
        {
            return new GameDataTargetPreview(false, "原 C 盘游戏数据目录还不存在，请先启动一次游戏后再迁移。", trimmed, "", null, null, 0, 0);
        }

        if (status.IsMigrated && !status.TargetExists)
        {
            return new GameDataTargetPreview(false, "当前原位置的目录联接指向的目标已失效，请先还原或手动修复后再迁移。", trimmed, "", null, null, 0, 0);
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new GameDataTargetPreview(false, "请选择一个有效的目标文件夹。", "", "", null, null, 0, 0);
        }

        string normalizedPick;
        try
        {
            normalizedPick = NormalizePath(trimmed);
        }
        catch (Exception ex)
        {
            return new GameDataTargetPreview(false, $"目标路径无效：{ex.Message}", trimmed, "", null, null, 0, 0);
        }

        var pickLeaf = Path.GetFileName(normalizedPick.TrimEnd('\\', '/'));
        var pickIsCompanyAndEmpty =
            string.Equals(pickLeaf, status.Company, StringComparison.OrdinalIgnoreCase) &&
            !IsDirectoryReparsePoint(normalizedPick) &&
            !File.Exists(normalizedPick) &&
            (!Directory.Exists(normalizedPick) || !IsDirectoryNonEmpty(normalizedPick));

        var finalTarget = pickIsCompanyAndEmpty
            ? normalizedPick
            : NormalizePath(Path.Combine(normalizedPick, status.Company));

        if (IsSameOrInside(finalTarget, status.SourcePath) || IsSameOrInside(status.SourcePath, finalTarget))
        {
            return new GameDataTargetPreview(false, "目标路径不能与原 C 盘游戏数据目录相同或互相包含。", normalizedPick, finalTarget, null, null, 0, 0);
        }

        if (status.IsMigrated && !string.IsNullOrWhiteSpace(status.TargetPath))
        {
            var currentTarget = NormalizePath(status.TargetPath);
            if (PathsEqual(finalTarget, currentTarget))
            {
                return new GameDataTargetPreview(false, "目标位置与当前已迁移的存放位置相同。", normalizedPick, finalTarget, null, null, 0, 0);
            }
            if (IsSameOrInside(finalTarget, currentTarget) || IsSameOrInside(currentTarget, finalTarget))
            {
                return new GameDataTargetPreview(false, "目标路径不能与当前存放目录互相包含。", normalizedPick, finalTarget, null, null, 0, 0);
            }
        }

        if (File.Exists(finalTarget) || IsDirectoryReparsePoint(finalTarget) || (Directory.Exists(finalTarget) && IsDirectoryNonEmpty(finalTarget)))
        {
            return new GameDataTargetPreview(false, $"目标目录「{finalTarget}」不是空的，请更换一个空目录。", normalizedPick, finalTarget, null, null, 0, 0);
        }

        var driveRoot = NormalizeDriveRoot(Path.GetPathRoot(finalTarget));
        string? driveFormat = null;
        if (!string.IsNullOrWhiteSpace(driveRoot))
        {
            try
            {
                var drive = new DriveInfo(driveRoot);
                if (!drive.IsReady)
                {
                    return new GameDataTargetPreview(false, $"目标磁盘 {driveRoot} 未就绪。", normalizedPick, finalTarget, driveRoot, null, 0, 0);
                }
                driveFormat = drive.DriveFormat;
                if (!allowNonNtfsForTest)
                {
                    if (drive.DriveType != DriveType.Fixed)
                    {
                        return new GameDataTargetPreview(false, $"目标磁盘 {driveRoot} 不是本地固定硬盘，无法稳定承载游戏数据和硬链接。", normalizedPick, finalTarget, driveRoot, driveFormat, 0, 0);
                    }
                    if (!string.Equals(driveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                    {
                        return new GameDataTargetPreview(false, $"目标磁盘 {driveRoot} 文件系统为 {driveFormat}，仅支持 NTFS 分区（多开硬链接需要 NTFS）。", normalizedPick, finalTarget, driveRoot, driveFormat, 0, 0);
                    }
                }
            }
            catch (Exception ex) when (!allowNonNtfsForTest)
            {
                return new GameDataTargetPreview(false, $"无法检查目标磁盘：{ex.Message}", normalizedPick, finalTarget, driveRoot, null, 0, 0);
            }
        }

        if (!allowSameVolumeForTest && !status.IsMigrated && !string.IsNullOrEmpty(status.LocalLowDrive) &&
            string.Equals(driveRoot, status.LocalLowDrive, StringComparison.OrdinalIgnoreCase))
        {
            return new GameDataTargetPreview(false, $"不能迁移到与原位置相同的磁盘（{status.LocalLowDrive}），请选择非 C 盘分区。", normalizedPick, finalTarget, driveRoot, driveFormat, 0, 0);
        }

        var freeBytes = GetAvailableFreeBytes(finalTarget, driveRoot);
        var sameVolumeRelocation = status.IsMigrated && !string.IsNullOrWhiteSpace(status.TargetPath) &&
                                   string.Equals(driveRoot, NormalizeDriveRoot(Path.GetPathRoot(status.TargetPath)), StringComparison.OrdinalIgnoreCase);
        var margin = Math.Max(0L, safetyMarginBytes);
        var requiredBytes = sameVolumeRelocation ? margin : status.TotalBytes + margin;

        if (freeBytes < requiredBytes)
        {
            return new GameDataTargetPreview(
                false,
                $"目标磁盘可用空间不足（剩余 {FormatBytes(freeBytes)}，至少需要 {FormatBytes(requiredBytes)}）。",
                normalizedPick,
                finalTarget,
                driveRoot,
                driveFormat,
                freeBytes,
                requiredBytes);
        }

        return new GameDataTargetPreview(
            true,
            "目标目录可用。",
            normalizedPick,
            finalTarget,
            driveRoot,
            driveFormat,
            freeBytes,
            requiredBytes);
    }

    /// <summary>执行一键迁移（或迁移后更换目标位置）。</summary>
    public static async Task<GameDataMigrationResult> MigrateAsync(
        string? gameDirectory,
        string pickedFolder,
        string? localLowRoot = null,
        Func<bool>? isGameRunning = null,
        IProgress<GameDataMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool allowSameVolumeForTest = false,
        bool allowNonNtfsForTest = false,
        long safetyMarginBytes = DefaultSafetyMarginBytes)
    {
        var status = Inspect(gameDirectory, localLowRoot, isGameRunning);
        if (status.GameRunning)
        {
            return new GameDataMigrationResult(false, "检测到游戏正在运行，请先完全退出游戏后再执行迁移。", status);
        }

        var preview = ValidateTarget(
            gameDirectory,
            pickedFolder,
            localLowRoot,
            isGameRunning,
            allowSameVolumeForTest,
            allowNonNtfsForTest,
            safetyMarginBytes);

        if (!preview.Ok)
        {
            return new GameDataMigrationResult(false, preview.Message, status);
        }

        var sourcePath = status.SourcePath;
        var finalTarget = preview.FinalTarget;

        return await Task.Run(() =>
        {
            if (SafeCheckRunning(gameDirectory, isGameRunning))
            {
                return new GameDataMigrationResult(false, "检测到游戏正在运行，已中止迁移。", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }

            if (!status.IsMigrated)
            {
                return MigrateFromLocal(sourcePath, finalTarget, gameDirectory, localLowRoot, isGameRunning, progress, cancellationToken);
            }

            return RelocateExistingMigration(sourcePath, status.TargetPath!, finalTarget, gameDirectory, localLowRoot, isGameRunning, progress, cancellationToken);
        }, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>将已迁移的数据一键迁回 C 盘原位置，并移除目录联接。</summary>
    public static async Task<GameDataMigrationResult> RestoreToLocalAsync(
        string? gameDirectory,
        string? localLowRoot = null,
        Func<bool>? isGameRunning = null,
        IProgress<GameDataMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default,
        long safetyMarginBytes = DefaultSafetyMarginBytes)
    {
        var status = Inspect(gameDirectory, localLowRoot, isGameRunning);
        if (!status.Available)
        {
            return new GameDataMigrationResult(false, status.Error ?? "无法识别游戏数据目录。", status);
        }
        if (status.GameRunning)
        {
            return new GameDataMigrationResult(false, "检测到游戏正在运行，请先完全退出游戏后再迁回 C 盘。", status);
        }
        if (!status.IsMigrated)
        {
            return new GameDataMigrationResult(false, "当前游戏数据原本就在 C 盘，无需迁回。", status);
        }
        if (!status.TargetExists || string.IsNullOrWhiteSpace(status.TargetPath))
        {
            return new GameDataMigrationResult(false, "原目录联接指向的目标目录已不存在，无法自动迁回。", status);
        }

        var sourcePath = status.SourcePath;
        var currentTarget = NormalizePath(status.TargetPath);
        var margin = Math.Max(0L, safetyMarginBytes);
        var required = status.TotalBytes + margin;
        if (status.LocalLowFreeBytes < required)
        {
            return new GameDataMigrationResult(
                false,
                $"C 盘可用空间不足（剩余 {FormatBytes(status.LocalLowFreeBytes)}，迁回至少需要 {FormatBytes(required)}）。",
                status);
        }

        return await Task.Run(() =>
        {
            if (SafeCheckRunning(gameDirectory, isGameRunning))
            {
                return new GameDataMigrationResult(false, "检测到游戏正在运行，已中止迁回。", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }

            var stagingPath = sourcePath + ".__toys_restoring_" + Guid.NewGuid().ToString("N")[..8];
            var createdStaging = false;
            try
            {
                CopyAndVerifyDirectory(currentTarget, stagingPath, progress, cancellationToken, ref createdStaging);

                ReportProgress(progress, "switching", "正在移除目录联接并恢复原目录…", 0, 0, 0, 0, 95);
                RemoveJunctionOnly(sourcePath);

                try
                {
                    Directory.Move(stagingPath, sourcePath);
                }
                catch (Exception ex)
                {
                    // 原位重命名失败时尝试恢复旧 Junction，保障游戏仍可访问
                    try { CreateJunction(sourcePath, currentTarget); } catch { /* 尽力恢复 */ }
                    TryDeleteRealDirectorySafe(stagingPath);
                    return new GameDataMigrationResult(false, $"恢复原目录失败，已保持原有迁移状态：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
                }

                ReportProgress(progress, "cleaning", "正在清理原目标盘数据…", 0, 0, 0, 0, 98);
                string? warning = null;
                if (!TryDeleteRealDirectorySafe(currentTarget, out var cleanError))
                {
                    warning = $"数据已成功迁回 C 盘，但旧目录未能自动删除（可稍后手动清理）：{currentTarget}（{cleanError}）";
                }

                var finalStatus = Inspect(gameDirectory, localLowRoot, isGameRunning);
                ReportProgress(progress, "done", "已迁回 C 盘原位置。", finalStatus.TotalBytes, finalStatus.TotalBytes, finalStatus.FileCount, finalStatus.FileCount, 100);
                return new GameDataMigrationResult(true, "已成功将游戏数据迁回 C 盘原位置。", finalStatus, warning);
            }
            catch (OperationCanceledException)
            {
                if (createdStaging) TryDeleteRealDirectorySafe(stagingPath);
                return new GameDataMigrationResult(false, "已取消迁回，原目录与数据保持不变。", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }
            catch (Exception ex)
            {
                if (createdStaging) TryDeleteRealDirectorySafe(stagingPath);
                return new GameDataMigrationResult(false, $"迁回 C 盘失败：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }
        }, CancellationToken.None).ConfigureAwait(false);
    }

    private static GameDataMigrationResult MigrateFromLocal(
        string sourcePath,
        string finalTarget,
        string? gameDirectory,
        string? localLowRoot,
        Func<bool>? isGameRunning,
        IProgress<GameDataMigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var createdTarget = false;
        try
        {
            CopyAndVerifyDirectory(sourcePath, finalTarget, progress, cancellationToken, ref createdTarget);

            ReportProgress(progress, "switching", "正在原位切换为 NTFS 目录联接…", 0, 0, 0, 0, 94);
            var backupPath = sourcePath + ".__toys_migrating_bak_" + Guid.NewGuid().ToString("N")[..8];

            try
            {
                Directory.Move(sourcePath, backupPath);
            }
            catch (Exception ex)
            {
                TryDeleteRealDirectorySafe(finalTarget);
                return new GameDataMigrationResult(false, $"原目录存在文件占用，无法切换（请确认游戏与相关程序已关闭）：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }

            try
            {
                CreateJunction(sourcePath, finalTarget);
                if (!IsDirectoryReparsePoint(sourcePath))
                    throw new IOException("目录联接创建后校验未通过。");

                var resolved = TryResolveLinkTarget(sourcePath);
                if (string.IsNullOrWhiteSpace(resolved) || !PathsEqual(resolved, finalTarget) || !Directory.Exists(sourcePath))
                    throw new IOException($"目录联接指向与目标不一致：{resolved ?? "(null)"}");
            }
            catch (Exception ex)
            {
                ReportProgress(progress, "rolling_back", "切换失败，正在无损回滚原目录…", 0, 0, 0, 0, 96);
                RollbackLocalFromBackup(sourcePath, backupPath, finalTarget);
                return new GameDataMigrationResult(false, $"创建目录联接失败，已无损回滚：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }

            ReportProgress(progress, "cleaning", "正在清理 C 盘原数据备份…", 0, 0, 0, 0, 98);
            string? warning = null;
            if (!TryDeleteRealDirectorySafe(backupPath, out var cleanError))
            {
                warning = $"迁移已生效，但临时备份目录未能自动删除（可确认无误后手动删除）：{backupPath}（{cleanError}）";
            }

            var finalStatus = Inspect(gameDirectory, localLowRoot, isGameRunning);
            ReportProgress(progress, "done", "迁移完成。", finalStatus.TotalBytes, finalStatus.TotalBytes, finalStatus.FileCount, finalStatus.FileCount, 100);
            return new GameDataMigrationResult(true, $"已成功迁移到 {finalTarget}，并已在 C 盘原位置建立目录联接。", finalStatus, warning);
        }
        catch (OperationCanceledException)
        {
            if (createdTarget) TryDeleteRealDirectorySafe(finalTarget);
            return new GameDataMigrationResult(false, "已取消迁移，C 盘原目录未做任何改动。", Inspect(gameDirectory, localLowRoot, isGameRunning));
        }
        catch (Exception ex)
        {
            if (createdTarget) TryDeleteRealDirectorySafe(finalTarget);
            return new GameDataMigrationResult(false, $"迁移失败：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
        }
    }

    private static GameDataMigrationResult RelocateExistingMigration(
        string sourcePath,
        string oldTargetRaw,
        string finalTarget,
        string? gameDirectory,
        string? localLowRoot,
        Func<bool>? isGameRunning,
        IProgress<GameDataMigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var oldTarget = NormalizePath(oldTargetRaw);
        var sameVolume = string.Equals(
            NormalizeDriveRoot(Path.GetPathRoot(oldTarget)),
            NormalizeDriveRoot(Path.GetPathRoot(finalTarget)),
            StringComparison.OrdinalIgnoreCase);

        if (sameVolume)
        {
            try
            {
                ReportProgress(progress, "switching", "同卷移动中…", 0, 0, 0, 0, 60);
                var parent = Path.GetDirectoryName(finalTarget);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                Directory.Move(oldTarget, finalTarget);

                RemoveJunctionOnly(sourcePath);
                try
                {
                    CreateJunction(sourcePath, finalTarget);
                    if (!IsDirectoryReparsePoint(sourcePath) || !Directory.Exists(sourcePath))
                        throw new IOException("目录联接校验失败。");
                }
                catch (Exception ex)
                {
                    ReportProgress(progress, "rolling_back", "联接更新失败，正在回滚…", 0, 0, 0, 0, 90);
                    try
                    {
                        if (IsDirectoryReparsePoint(sourcePath)) RemoveJunctionOnly(sourcePath);
                        if (!Directory.Exists(oldTarget) && Directory.Exists(finalTarget)) Directory.Move(finalTarget, oldTarget);
                        CreateJunction(sourcePath, oldTarget);
                    }
                    catch { /* 尽力回滚 */ }
                    return new GameDataMigrationResult(false, $"更新目录联接失败：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
                }

                var finalStatus = Inspect(gameDirectory, localLowRoot, isGameRunning);
                ReportProgress(progress, "done", "位置更换完成。", finalStatus.TotalBytes, finalStatus.TotalBytes, finalStatus.FileCount, finalStatus.FileCount, 100);
                return new GameDataMigrationResult(true, $"已成功更换存放位置至 {finalTarget}。", finalStatus);
            }
            catch (IOException)
            {
                // 同卷直接改名失败（极少数挂载点场景），回退到复制校验流程
            }
            catch (Exception ex)
            {
                return new GameDataMigrationResult(false, $"同卷移动失败：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }
        }

        var createdTarget = false;
        try
        {
            CopyAndVerifyDirectory(oldTarget, finalTarget, progress, cancellationToken, ref createdTarget);

            ReportProgress(progress, "switching", "正在更新目录联接指向…", 0, 0, 0, 0, 95);
            RemoveJunctionOnly(sourcePath);
            try
            {
                CreateJunction(sourcePath, finalTarget);
                if (!IsDirectoryReparsePoint(sourcePath) || !Directory.Exists(sourcePath))
                    throw new IOException("目录联接校验失败。");
            }
            catch (Exception ex)
            {
                ReportProgress(progress, "rolling_back", "联接更新失败，正在恢复原指向…", 0, 0, 0, 0, 96);
                try
                {
                    if (IsDirectoryReparsePoint(sourcePath)) RemoveJunctionOnly(sourcePath);
                    CreateJunction(sourcePath, oldTarget);
                }
                catch { /* 尽力回滚 */ }
                TryDeleteRealDirectorySafe(finalTarget);
                return new GameDataMigrationResult(false, $"更新目录联接失败，已恢复原位置：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
            }

            ReportProgress(progress, "cleaning", "正在清理旧目标目录…", 0, 0, 0, 0, 98);
            string? warning = null;
            if (!TryDeleteRealDirectorySafe(oldTarget, out var cleanError))
            {
                warning = $"已切换到新位置，但旧目录未能自动删除（可稍后手动清理）：{oldTarget}（{cleanError}）";
            }

            var finalStatus = Inspect(gameDirectory, localLowRoot, isGameRunning);
            ReportProgress(progress, "done", "位置更换完成。", finalStatus.TotalBytes, finalStatus.TotalBytes, finalStatus.FileCount, finalStatus.FileCount, 100);
            return new GameDataMigrationResult(true, $"已成功更换存放位置至 {finalTarget}。", finalStatus, warning);
        }
        catch (OperationCanceledException)
        {
            if (createdTarget) TryDeleteRealDirectorySafe(finalTarget);
            return new GameDataMigrationResult(false, "已取消更换，原有位置保持不变。", Inspect(gameDirectory, localLowRoot, isGameRunning));
        }
        catch (Exception ex)
        {
            if (createdTarget) TryDeleteRealDirectorySafe(finalTarget);
            return new GameDataMigrationResult(false, $"更换存放位置失败：{ex.Message}", Inspect(gameDirectory, localLowRoot, isGameRunning));
        }
    }

    private static void CopyAndVerifyDirectory(
        string sourceRoot,
        string targetRoot,
        IProgress<GameDataMigrationProgress>? progress,
        CancellationToken cancellationToken,
        ref bool createdTarget)
    {
        var (_, totalFiles, logicalTotalBytes) = ScanDirectoryStats(sourceRoot);
        ReportProgress(progress, "copying", "正在准备复制文件…", 0, logicalTotalBytes, 0, totalFiles, 0);

        Directory.CreateDirectory(targetRoot);
        createdTarget = true;

        var copiedPrimaryById = new Dictionary<NtfsFileId, string>();
        var manifest = new List<CopiedFileRecord>(totalFiles);
        long copiedBytes = 0;
        var copiedFiles = 0;
        var buffer = new byte[CopyBufferSize];

        var pending = new Stack<string>();
        pending.Push(sourceRoot);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDir = pending.Pop();
            var relDir = Path.GetRelativePath(sourceRoot, currentDir);
            var destDir = relDir == "." ? targetRoot : Path.Combine(targetRoot, relDir);
            Directory.CreateDirectory(destDir);

            foreach (var subDir in Directory.EnumerateDirectories(currentDir))
            {
                if (IsDirectoryReparsePoint(subDir)) continue;
                pending.Push(subDir);
            }

            foreach (var srcFile in Directory.EnumerateFiles(currentDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(srcFile);
                var dstFile = Path.Combine(destDir, fileName);
                var srcInfo = new FileInfo(srcFile);
                var length = srcInfo.Length;

                var hasFileId = TryGetNtfsFileId(srcFile, out var fileId);
                if (hasFileId && copiedPrimaryById.TryGetValue(fileId, out var primaryTarget) &&
                    CreateHardLinkW(dstFile, primaryTarget, IntPtr.Zero))
                {
                    manifest.Add(new CopiedFileRecord(dstFile, length, null, true, primaryTarget));
                    copiedFiles++;
                    copiedBytes += length;
                    var pctLink = ComputeCopyPercent(copiedBytes, logicalTotalBytes, copiedFiles, totalFiles);
                    ReportProgress(progress, "copying", $"正在链接共享缓存（{copiedFiles}/{totalFiles}）…", copiedBytes, logicalTotalBytes, copiedFiles, totalFiles, pctLink);
                    continue;
                }

                string shaHex;
                using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                using (var srcStream = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CopyBufferSize))
                using (var dstStream = new FileStream(dstFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    int read;
                    while ((read = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        dstStream.Write(buffer, 0, read);
                        hasher.AppendData(buffer, 0, read);
                        copiedBytes += read;
                        var pct = ComputeCopyPercent(copiedBytes, logicalTotalBytes, copiedFiles, totalFiles);
                        ReportProgress(progress, "copying", $"正在复制文件（{copiedFiles}/{totalFiles}）…", copiedBytes, logicalTotalBytes, copiedFiles, totalFiles, pct);
                    }
                    shaHex = Convert.ToHexString(hasher.GetHashAndReset());
                }

                try
                {
                    File.SetLastWriteTimeUtc(dstFile, srcInfo.LastWriteTimeUtc);
                    var attrs = srcInfo.Attributes & ~(FileAttributes.ReparsePoint | FileAttributes.SparseFile | FileAttributes.Offline);
                    if (attrs != 0) File.SetAttributes(dstFile, attrs);
                }
                catch
                {
                    // 元数据设置失败不影响数据一致性
                }

                if (hasFileId) copiedPrimaryById[fileId] = dstFile;
                manifest.Add(new CopiedFileRecord(dstFile, length, shaHex, false, null));
                copiedFiles++;
                var pctAfter = ComputeCopyPercent(copiedBytes, logicalTotalBytes, copiedFiles, totalFiles);
                ReportProgress(progress, "copying", $"正在复制文件（{copiedFiles}/{totalFiles}）…", copiedBytes, logicalTotalBytes, copiedFiles, totalFiles, pctAfter);
            }
        }

        // 校验阶段（85% -> 93%）
        var verified = 0;
        var verifyTotal = Math.Max(1, manifest.Count);
        ReportProgress(progress, "verifying", "正在校验已复制的文件…", copiedBytes, logicalTotalBytes, copiedFiles, totalFiles, 85);

        foreach (var item in manifest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(item.TargetFile))
                throw new IOException($"校验失败，目标文件缺失：{item.TargetFile}");

            var dstInfo = new FileInfo(item.TargetFile);
            if (dstInfo.Length != item.Length)
                throw new IOException($"校验失败，文件大小不一致：{item.TargetFile}");

            if (item.IsHardLinkDuplicate && !string.IsNullOrEmpty(item.PrimaryTarget))
            {
                if (TryGetNtfsFileId(item.TargetFile, out var linkId) &&
                    TryGetNtfsFileId(item.PrimaryTarget, out var primaryId) &&
                    linkId == primaryId)
                {
                    verified++;
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(item.Sha256Hex))
            {
                using var stream = new FileStream(item.TargetFile, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize);
                var actualHex = Convert.ToHexString(SHA256.HashData(stream));
                if (!string.Equals(actualHex, item.Sha256Hex, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"校验失败，文件哈希不一致：{item.TargetFile}");
            }

            verified++;
            if (verified % 16 == 0 || verified == manifest.Count)
            {
                var pct = 85 + (int)Math.Round(8.0 * verified / verifyTotal);
                ReportProgress(progress, "verifying", $"正在校验文件（{verified}/{manifest.Count}）…", copiedBytes, logicalTotalBytes, copiedFiles, totalFiles, Math.Min(93, pct));
            }
        }
    }

    private static void RollbackLocalFromBackup(string sourcePath, string backupPath, string finalTarget)
    {
        try
        {
            if (IsDirectoryReparsePoint(sourcePath)) RemoveJunctionOnly(sourcePath);
        }
        catch { }

        try
        {
            if (!Directory.Exists(sourcePath) && Directory.Exists(backupPath))
                Directory.Move(backupPath, sourcePath);
        }
        catch { }

        TryDeleteRealDirectorySafe(finalTarget);
    }

    private static int ComputeCopyPercent(long copiedBytes, long totalBytes, int copiedFiles, int totalFiles)
    {
        if (totalBytes > 0)
            return Math.Clamp((int)Math.Round(84.0 * copiedBytes / totalBytes), 0, 84);
        if (totalFiles > 0)
            return Math.Clamp((int)Math.Round(84.0 * copiedFiles / totalFiles), 0, 84);
        return 84;
    }

    private static void ReportProgress(
        IProgress<GameDataMigrationProgress>? progress,
        string stage,
        string message,
        long copiedBytes,
        long totalBytes,
        int copiedFiles,
        int totalFiles,
        int percent)
    {
        progress?.Report(new GameDataMigrationProgress(
            stage,
            message,
            copiedBytes,
            totalBytes,
            copiedFiles,
            totalFiles,
            Math.Clamp(percent, 0, 100)));
    }

    private static bool TryResolveCompanyFolder(
        string? gameDirectory,
        string? localLowRoot,
        out string company,
        out string? product,
        out string sourcePath,
        out string? error)
    {
        company = "";
        product = null;
        sourcePath = "";
        error = null;

        var low = string.IsNullOrWhiteSpace(localLowRoot)
            ? AccountProfileService.LocalLowRoot()
            : NormalizePath(localLowRoot);
        if (string.IsNullOrWhiteSpace(low))
        {
            error = "无法定位系统 LocalLow 目录。";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(gameDirectory) &&
            Directory.Exists(gameDirectory) &&
            AccountProfileService.TryReadAppInfo(gameDirectory, out var appCompany, out var appProduct))
        {
            company = appCompany;
            product = appProduct;
            sourcePath = NormalizePath(Path.Combine(low, company));
            return true;
        }

        // 未选游戏目录或未读到 app.info 时，回退识别常见默认目录 LocalLow\feimo
        var defaultFeimo = Path.Combine(low, "feimo");
        if (Directory.Exists(defaultFeimo) || IsDirectoryReparsePoint(defaultFeimo))
        {
            company = "feimo";
            var cn = Path.Combine(defaultFeimo, "AstralParty_CN");
            product = Directory.Exists(cn) ? "AstralParty_CN" : null;
            sourcePath = NormalizePath(defaultFeimo);
            return true;
        }

        error = string.IsNullOrWhiteSpace(gameDirectory)
            ? "未检测到游戏目录，也没有找到默认的 LocalLow\\feimo 数据文件夹。请先在「设置 → 游戏目录」中添加游戏，或启动一次游戏。"
            : "没能读取当前游戏目录的 app.info，且未找到默认的 LocalLow\\feimo 数据文件夹。";
        return false;
    }

    private static IReadOnlyList<GameDataDriveOption> EnumerateDriveOptions(string company, string? excludeDriveRoot)
    {
        var list = new List<GameDataDriveOption>();
        var systemRoot = NormalizeDriveRoot(Path.GetPathRoot(Environment.SystemDirectory));

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType != DriveType.Fixed) continue;
                if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase)) continue;

                var root = NormalizeDriveRoot(drive.RootDirectory.FullName);
                if (string.IsNullOrEmpty(root)) continue;
                if (!string.IsNullOrEmpty(excludeDriveRoot) &&
                    string.Equals(root, excludeDriveRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                var shortDrive = root.TrimEnd('\\');
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? $"{shortDrive} 本地磁盘"
                    : $"{shortDrive} ({drive.VolumeLabel})";
                var isSystem = !string.IsNullOrEmpty(systemRoot) &&
                               string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);

                list.Add(new GameDataDriveOption(
                    Root: root,
                    Label: label,
                    FreeBytes: drive.AvailableFreeSpace,
                    TotalBytes: drive.TotalSize,
                    IsSystemDrive: isSystem,
                    SuggestedTarget: Path.Combine(root, "AstralPartyData", company)));
            }
            catch
            {
                // 跳过不可读磁盘
            }
        }

        return list.OrderByDescending(d => d.FreeBytes).ToList();
    }

    private static (long PhysicalBytes, int FileCount, long LogicalBytes) ScanDirectoryStats(string root)
    {
        var (physical, files, logical, _) = ScanDirectoriesStats(new[] { root });
        return (physical, files, logical);
    }

    internal static (long PhysicalBytes, int FileCount, long LogicalBytes, DateTime LatestWriteUtc) ScanDirectoriesStats(IEnumerable<string> roots)
    {
        long physicalBytes = 0;
        long logicalBytes = 0;
        var fileCount = 0;
        var latestWriteUtc = DateTime.MinValue;
        var seenIds = new HashSet<NtfsFileId>();
        var pending = new Stack<string>();

        foreach (var root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                pending.Push(root);
        }

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(current))
                {
                    if (IsDirectoryReparsePoint(subDir)) continue;
                    pending.Push(subDir);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        var length = info.Length;
                        fileCount++;
                        logicalBytes += length;
                        if (info.LastWriteTimeUtc > latestWriteUtc)
                            latestWriteUtc = info.LastWriteTimeUtc;

                        if (TryGetNtfsFileId(file, out var fileId))
                        {
                            if (seenIds.Add(fileId)) physicalBytes += length;
                        }
                        else
                        {
                            physicalBytes += length;
                        }
                    }
                    catch
                    {
                        // 单个文件权限/消失不中断统计
                    }
                }
            }
            catch
            {
                // 目录不可读时跳过
            }
        }

        return (physicalBytes, fileCount, logicalBytes, latestWriteUtc);
    }

    private static bool TryGetNtfsFileId(string filePath, out NtfsFileId id)
    {
        id = default;
        try
        {
            const uint fileReadAttributes = 0x80;
            const uint shareAll = 0x01 | 0x02 | 0x04;
            const uint openExisting = 3;

            using var handle = CreateFileW(filePath, fileReadAttributes, shareAll, IntPtr.Zero, openExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid) return false;
            if (!GetFileInformationByHandle(handle, out var info)) return false;

            id = new NtfsFileId(info.VolumeSerialNumber, info.FileIndexHigh, info.FileIndexLow);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsDirectoryReparsePoint(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return false;
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryResolveLinkTarget(string junctionPath)
    {
        try
        {
            var target = Directory.ResolveLinkTarget(junctionPath, returnFinalTarget: true);
            return target is null ? null : NormalizePath(target.FullName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>创建 NTFS 目录联接（mklink /J，无需管理员权限）。</summary>
    public static void CreateJunction(string linkPath, string targetPath)
    {
        var link = NormalizePath(linkPath);
        var target = NormalizePath(targetPath);

        if (!Directory.Exists(target))
            throw new DirectoryNotFoundException($"联接目标目录不存在：{target}");

        var parent = Path.GetDirectoryName(link);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 mklink 命令。");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"创建目录联接失败（退出码 {process.ExitCode}）：{detail.Trim()}");
        }
    }

    /// <summary>仅移除 NTFS 目录联接本身（非递归），绝不删除目标目录中的真实数据。</summary>
    public static void RemoveJunctionOnly(string junctionPath)
    {
        if (!Directory.Exists(junctionPath) && !IsDirectoryReparsePoint(junctionPath)) return;
        if (!IsDirectoryReparsePoint(junctionPath))
            throw new InvalidOperationException($"安全拦截：路径不是目录联接，拒绝执行联接移除：{junctionPath}");
        Directory.Delete(junctionPath, recursive: false);
    }

    private static bool TryDeleteRealDirectorySafe(string directoryPath) =>
        TryDeleteRealDirectorySafe(directoryPath, out _);

    private static bool TryDeleteRealDirectorySafe(string directoryPath, out string error)
    {
        error = "";
        try
        {
            if (!Directory.Exists(directoryPath)) return true;
            if (IsDirectoryReparsePoint(directoryPath))
            {
                error = "安全拦截：拒绝递归删除目录联接。";
                return false;
            }
            Directory.Delete(directoryPath, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static bool SafeCheckRunning(string? gameDirectory, Func<bool>? isGameRunning)
    {
        if (isGameRunning is not null)
        {
            try { return isGameRunning(); }
            catch { return false; }
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(gameDirectory) && Directory.Exists(gameDirectory))
            {
                var exe = SpeedhackManager.ContainsGameExe(gameDirectory);
                if (exe is not null)
                {
                    var name = Path.GetFileNameWithoutExtension(exe);
                    if (!string.IsNullOrEmpty(name) && Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
            }

            foreach (var fallbackName in new[] { "AstralParty_CN", "AstralParty" })
            {
                if (Process.GetProcessesByName(fallbackName).Length > 0)
                    return true;
            }
        }
        catch
        {
            // 进程探测异常不阻塞只读检查
        }

        return false;
    }

    private static long GetAvailableFreeBytes(string path, string? fallbackDriveRoot = null)
    {
        try
        {
            var probe = path;
            while (!string.IsNullOrEmpty(probe) && !Directory.Exists(probe))
                probe = Path.GetDirectoryName(probe);
            if (!string.IsNullOrEmpty(probe) && GetDiskFreeSpaceExW(probe, out var avail, out _, out _))
                return (long)Math.Min(avail, long.MaxValue);
        }
        catch
        {
            // 回退到 DriveInfo
        }

        try
        {
            var root = fallbackDriveRoot ?? Path.GetPathRoot(path);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady) return drive.AvailableFreeSpace;
            }
        }
        catch
        {
        }
        return 0;
    }

    private static bool IsDirectoryNonEmpty(string path)
    {
        try { return Directory.EnumerateFileSystemEntries(path).Any(); }
        catch { return true; }
    }

    private static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(full.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }
        return full.TrimEnd('\\', '/');
    }

    private static string? NormalizeDriveRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        var full = Path.GetFullPath(root.Trim());
        return full.EndsWith('\\') ? full : full + "\\";
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrInside(string candidate, string parent)
    {
        var c = NormalizePath(candidate);
        var p = NormalizePath(parent);
        if (string.Equals(c, p, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = p.EndsWith('\\') ? p : p + "\\";
        return c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double val = bytes;
        var idx = 0;
        while (val >= 1024 && idx < units.Length - 1)
        {
            val /= 1024;
            idx++;
        }
        return $"{val:0.##} {units[idx]}";
    }
}
