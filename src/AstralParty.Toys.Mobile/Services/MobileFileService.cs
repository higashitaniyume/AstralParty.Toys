using System.Diagnostics;
using System.Text;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Services;

/// <summary>
/// 设备容器内文件管理（参考 iOS 伴生工具 files.py）：
/// 以当前游戏容器根为沙盒边界（iOS 为 <c>Documents/</c>，安卓为 <c>files/</c>），
/// 提供列目录、文本预览、新建文件夹、重命名、删除、上传、下载与系统默认程序打开。
/// </summary>
public sealed class MobileFileService(IDeviceChannel channel, MobileOperationLog? log = null)
{
    public const int DefaultPreviewLimitBytes = 256 * 1024;

    private static readonly string OpenStagingRoot =
        Path.Combine(Path.GetTempPath(), "astralparty-device-open");

    public static string NormalizeRelativePath(string? rawPath)
    {
        var normalized = (rawPath ?? "").Trim().Replace('\\', '/');
        if (normalized.Contains('\0') || normalized.Contains(':'))
            throw new InvalidOperationException("非法的设备路径");

        var segments = new List<string>();
        foreach (var part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;
            if (part == "..")
                throw new InvalidOperationException("路径里不能包含 ..");
            segments.Add(part);
        }

        return string.Join("/", segments);
    }

    public static string ValidateSingleName(string? rawName)
    {
        var name = (rawName ?? "").Trim();
        if (string.IsNullOrEmpty(name) || name is "." or ".." ||
            name.Contains('/') || name.Contains('\\') || name.Contains('\0') || name.Contains(':'))
        {
            throw new InvalidOperationException($"非法的名称：{rawName}");
        }

        return name;
    }

    public async Task<MobileFileListResult> ListAsync(
        MobileTarget target, string? rawPath, bool showHidden = false,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null)
    {
        var relPath = NormalizeRelativePath(rawPath);
        progress?.Invoke($"正在读取目录 {(string.IsNullOrEmpty(relPath) ? "/" : relPath)}…", 15, true);

        return await WithContainerAsync(
            target,
            cancellationToken,
            async (fileSystem, root) =>
            {
                progress?.Invoke("正在列出目录内容…", 50, true);
                var rawEntries = await fileSystem.ListDetailedAsync(
                    ResolveDevicePath(root, relPath), showHidden, cancellationToken);
                progress?.Invoke($"已读取 {rawEntries.Count} 项，正在整理…", 88, false);
                var prefix = string.IsNullOrEmpty(relPath) ? "" : relPath + "/";
                var items = rawEntries
                    .Select(e => new MobileFileEntry
                    {
                        Name = e.Name,
                        Path = prefix + e.Name,
                        IsDir = e.IsDirectory,
                        Size = e.Size,
                        Modified = e.Modified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                    })
                    .OrderByDescending(e => e.IsDir)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new MobileFileListResult
                {
                    TargetId = target.Id,
                    Kind = target.Kind,
                    RootLabel = root.TrimEnd('/'),
                    Path = relPath,
                    Entries = items,
                };
            },
            () => new MobileFileListResult
            {
                TargetId = target.Id,
                Kind = target.Kind,
                Path = relPath,
                Error = "找不到游戏容器（先做一次「检测」或绑定游戏包名）",
            });
    }

    public async Task<MobileFilePreviewResult> ReadPreviewAsync(
        MobileTarget target, string rawPath, int maxBytes = DefaultPreviewLimitBytes,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null)
    {
        var relPath = NormalizeRelativePath(rawPath);
        if (string.IsNullOrEmpty(relPath))
            throw new InvalidOperationException("不能直接预览根目录");

        progress?.Invoke($"正在读取文件 {Path.GetFileName(relPath)}…", 20, true);
        return await WithContainerAsync(
            target,
            cancellationToken,
            async (fileSystem, root) =>
            {
                var fileName = Path.GetFileName(relPath);
                var devicePath = ResolveDevicePath(root, relPath);
                var stat = await fileSystem.StatAsync(devicePath, cancellationToken);
                if (stat is null && !await fileSystem.ExistsAsync(devicePath, cancellationToken))
                    return PreviewError(target.Id, relPath, fileName, "文件不存在");
                if (stat?.IsDirectory == true)
                    return PreviewError(target.Id, relPath, fileName, "这是一个目录");

                var size = stat?.Size ?? 0;
                if (size > maxBytes)
                {
                    return new MobileFilePreviewResult
                    {
                        TargetId = target.Id,
                        Path = relPath,
                        Name = fileName,
                        Size = size,
                        Truncated = true,
                    };
                }

                progress?.Invoke($"正在读取预览内容：{fileName}", 65, false);
                var bytes = await fileSystem.ReadAllBytesAsync(devicePath, cancellationToken);
                var isBinary = Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8192)) >= 0;
                return new MobileFilePreviewResult
                {
                    TargetId = target.Id,
                    Path = relPath,
                    Name = fileName,
                    Size = bytes.Length,
                    Binary = isBinary,
                    Text = isBinary ? null : Encoding.UTF8.GetString(bytes),
                };
            },
            () => throw new InvalidOperationException("找不到游戏容器"));
    }

    public async Task<MobileOperationResult> CreateDirectoryAsync(
        MobileTarget target, string? baseRawPath, string name, CancellationToken cancellationToken = default)
    {
        var basePath = NormalizeRelativePath(baseRawPath);
        var cleanName = ValidateSingleName(name);
        var relTarget = JoinRelative(basePath, cleanName);

        return await RunContainerOpAsync(target, cancellationToken, async (fileSystem, root) =>
        {
            var deviceTarget = ResolveDevicePath(root, relTarget);
            if (await fileSystem.ExistsAsync(deviceTarget, cancellationToken))
                return MobileOperationResult.Fail($"「{cleanName}」已存在");
            if (await CheckLoaderBlockedAsync(fileSystem, root, relTarget, cancellationToken) is { } blocked)
                return MobileOperationResult.Fail(blocked);

            await fileSystem.MakeDirectoriesAsync(deviceTarget, cancellationToken);
            return new MobileOperationResult { Ok = true, Message = $"已新建文件夹：{cleanName}", Detail = relTarget };
        });
    }

    public async Task<MobileOperationResult> RenameAsync(
        MobileTarget target, string rawPath, string newName, CancellationToken cancellationToken = default)
    {
        var relPath = NormalizeRelativePath(rawPath);
        if (string.IsNullOrEmpty(relPath))
            return MobileOperationResult.Fail("不能重命名根目录");

        var cleanName = ValidateSingleName(newName);
        var lastSlash = relPath.LastIndexOf('/');
        var parentRel = lastSlash >= 0 ? relPath[..lastSlash] : "";
        var nextRel = JoinRelative(parentRel, cleanName);
        if (string.Equals(relPath, nextRel, StringComparison.Ordinal))
            return new MobileOperationResult { Ok = true, Message = "名称未改变", Detail = relPath };

        return await RunContainerOpAsync(target, cancellationToken, async (fileSystem, root) =>
        {
            if (await CheckLoaderBlockedAsync(fileSystem, root, relPath, cancellationToken) is { } blockedFrom)
                return MobileOperationResult.Fail(blockedFrom);
            if (await CheckLoaderBlockedAsync(fileSystem, root, nextRel, cancellationToken) is { } blockedTo)
                return MobileOperationResult.Fail(blockedTo);

            var fromDevice = ResolveDevicePath(root, relPath);
            var toDevice = ResolveDevicePath(root, nextRel);
            if (!await fileSystem.ExistsAsync(fromDevice, cancellationToken))
                return MobileOperationResult.Fail($"路径不存在：{relPath}");
            if (await fileSystem.ExistsAsync(toDevice, cancellationToken))
                return MobileOperationResult.Fail($"目标名称已存在：{cleanName}");

            await fileSystem.RenameAsync(fromDevice, toDevice, cancellationToken);
            return new MobileOperationResult { Ok = true, Message = $"已重命名为 {cleanName}", Detail = nextRel };
        });
    }

    public async Task<MobileOperationResult> DeleteAsync(
        MobileTarget target, string rawPath, CancellationToken cancellationToken = default)
    {
        var relPath = NormalizeRelativePath(rawPath);
        if (string.IsNullOrEmpty(relPath))
            return MobileOperationResult.Fail("不能删除容器根目录");

        return await RunContainerOpAsync(target, cancellationToken, async (fileSystem, root) =>
        {
            if (await CheckLoaderBlockedAsync(fileSystem, root, relPath, cancellationToken) is { } blocked)
                return MobileOperationResult.Fail(blocked);

            var devicePath = ResolveDevicePath(root, relPath);
            if (!await fileSystem.ExistsAsync(devicePath, cancellationToken))
                return MobileOperationResult.Fail($"路径不存在：{relPath}");

            await fileSystem.DeleteAsync(devicePath, cancellationToken);
            return await fileSystem.ExistsAsync(devicePath, cancellationToken)
                ? MobileOperationResult.Fail($"删除后仍然存在：{relPath}")
                : new MobileOperationResult { Ok = true, Message = $"已删除：{relPath}", Detail = relPath };
        });
    }

    public async Task<MobileOperationResult> UploadFilesAsync(
        MobileTarget target, string? baseRawPath, IReadOnlyList<string> localFiles,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null)
    {
        if (localFiles.Count == 0)
            return MobileOperationResult.Fail("没有选择任何文件");

        var basePath = NormalizeRelativePath(baseRawPath);
        progress?.Invoke("正在连接设备容器…", 5, true);
        return await RunContainerOpAsync(target, cancellationToken, async (fileSystem, root) =>
        {
            var uploaded = 0;
            var total = localFiles.Count;
            for (var index = 0; index < total; index++)
            {
                var local = localFiles[index];
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(local))
                    return MobileOperationResult.Fail($"本地文件不存在：{local}");

                var fileName = ValidateSingleName(Path.GetFileName(local));
                var relTarget = JoinRelative(basePath, fileName);
                var deviceTarget = ResolveDevicePath(root, relTarget);
                if (!await fileSystem.ExistsAsync(deviceTarget, cancellationToken) &&
                    await CheckLoaderBlockedAsync(fileSystem, root, relTarget, cancellationToken) is { } blocked)
                    return MobileOperationResult.Fail(blocked);

                var bytes = await File.ReadAllBytesAsync(local, cancellationToken);
                progress?.Invoke($"正在上传 ({index + 1}/{total})：{fileName} ({bytes.Length:N0} B)",
                    10 + (int)(85.0 * index / total), false);
                await fileSystem.WriteAllBytesAsync(deviceTarget, bytes, cancellationToken);
                uploaded++;
                progress?.Invoke($"已上传 ({uploaded}/{total})：{fileName}",
                    10 + (int)(88.0 * uploaded / total), false);
            }

            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已上传 {uploaded} 个文件",
                Detail = string.IsNullOrEmpty(basePath) ? root.TrimEnd('/') : basePath,
            };
        });
    }

    public Task<MobileOperationResult> DownloadFileAsync(
        MobileTarget target, string rawPath, string localDirectory,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null) =>
        SaveSingleFileCoreAsync(target, rawPath,
            fileName => UniqueLocalPath(localDirectory, fileName),
            "暂不支持按单文件保存目录", cancellationToken, progress);

    public Task<MobileOperationResult> DownloadToFileAsync(
        MobileTarget target, string rawPath, string destinationFilePath,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null) =>
        SaveSingleFileCoreAsync(target, rawPath,
            _ => destinationFilePath,
            "所选路径是目录，不能按单文件保存", cancellationToken, progress);

    public async Task<MobileOperationResult> DownloadDirectoryAsync(
        MobileTarget target, string rawPath, string localDirectory,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null)
    {
        var relPath = NormalizeRelativePath(rawPath);
        if (string.IsNullOrEmpty(relPath))
            return MobileOperationResult.Fail("请选择要下载的目录");

        progress?.Invoke($"正在扫描目录 {relPath}…", 10, true);
        return await RunContainerOpAsync(target, cancellationToken, async (fileSystem, root) =>
        {
            var devicePath = ResolveDevicePath(root, relPath);
            var stat = await fileSystem.StatAsync(devicePath, cancellationToken);
            if (stat is not null && !stat.IsDirectory)
                return await SaveOpenedFileAsync(fileSystem, root, relPath,
                    fileName => UniqueLocalPath(localDirectory, fileName),
                    "所选路径是目录", cancellationToken, progress);

            Directory.CreateDirectory(localDirectory);
            var destRoot = UniqueLocalDirectoryPath(localDirectory, Path.GetFileName(relPath));
            Directory.CreateDirectory(destRoot);

            var filesToPull = new List<(string RemotePath, string LocalPath)>();
            await CollectRemoteFilesAsync(fileSystem, devicePath, destRoot, filesToPull, cancellationToken, progress);
            var total = filesToPull.Count;
            if (total == 0)
            {
                progress?.Invoke("目录为空，已创建空目录", 100, false);
                return new MobileOperationResult
                {
                    Ok = true,
                    Message = $"已创建目录 {destRoot}",
                    Detail = "目录为空（0 个文件）",
                };
            }

            for (var index = 0; index < total; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (remoteFile, localFile) = filesToPull[index];
                progress?.Invoke($"正在下载 ({index + 1}/{total})：{Path.GetFileName(remoteFile)}",
                    20 + (int)(78.0 * index / total), false);
                var bytes = await fileSystem.ReadAllBytesAsync(remoteFile, cancellationToken);
                var parentDir = Path.GetDirectoryName(localFile);
                if (!string.IsNullOrEmpty(parentDir))
                    Directory.CreateDirectory(parentDir);
                await File.WriteAllBytesAsync(localFile, bytes, cancellationToken);
            }

            progress?.Invoke($"目录下载完成（共 {total} 个文件）", 100, false);
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已下载目录到 {destRoot}",
                Detail = $"共 {total} 个文件",
            };
        });
    }

    public async Task<MobileOperationResult> OpenLocallyAsync(
        MobileTarget target, string rawPath,
        CancellationToken cancellationToken = default, Action<string, int, bool>? progress = null)
    {
        var relPath = NormalizeRelativePath(rawPath);
        if (string.IsNullOrEmpty(relPath))
            return MobileOperationResult.Fail("请选择一个文件");

        progress?.Invoke($"正在拉取文件到临时目录：{Path.GetFileName(relPath)}", 25, true);
        return await RunContainerOpAsync(target, cancellationToken, async (fileSystem, root) =>
        {
            var devicePath = ResolveDevicePath(root, relPath);
            var stat = await fileSystem.StatAsync(devicePath, cancellationToken);
            if (stat?.IsDirectory == true)
                return MobileOperationResult.Fail("这是一个目录");

            var fileName = Path.GetFileName(relPath);
            progress?.Invoke($"正在下载临时副本：{fileName}", 55, stat is null);
            var bytes = await fileSystem.ReadAllBytesAsync(devicePath, cancellationToken);
            progress?.Invoke($"正在用默认程序打开 {fileName}…", 90, false);
            Directory.CreateDirectory(OpenStagingRoot);
            var local = UniqueLocalPath(OpenStagingRoot, fileName);
            await File.WriteAllBytesAsync(local, bytes, cancellationToken);

            Process.Start(new ProcessStartInfo { FileName = local, UseShellExecute = true });
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已打开 {Path.GetFileName(local)}",
                Detail = local,
            };
        });
    }

    private async Task<T> WithContainerAsync<T>(
        MobileTarget target,
        CancellationToken cancellationToken,
        Func<IDeviceFileSystem, string, Task<T>> action,
        Func<T> onMissing)
    {
        var opened = await MobileDeviceAccess.TryOpenRootAsync(channel, target, log, cancellationToken);
        if (opened is null)
            return onMissing();

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
            return await action(fileSystem, root);
    }

    private Task<MobileOperationResult> RunContainerOpAsync(
        MobileTarget target,
        CancellationToken cancellationToken,
        Func<IDeviceFileSystem, string, Task<MobileOperationResult>> action) =>
        WithContainerAsync(target, cancellationToken, action, () => MobileOperationResult.Fail("找不到游戏容器"));

    private Task<string?> CheckLoaderBlockedAsync(
        IDeviceFileSystem fileSystem, string root, string relPath, CancellationToken cancellationToken) =>
        IsInsideLoader(relPath)
            ? MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, root + MobilePaths.LoaderRelative, cancellationToken)
            : Task.FromResult<string?>(null);

    private async Task<MobileOperationResult> SaveSingleFileCoreAsync(
        MobileTarget target, string rawPath, Func<string, string> resolveDestination,
        string dirError, CancellationToken cancellationToken, Action<string, int, bool>? progress)
    {
        var relPath = NormalizeRelativePath(rawPath);
        if (string.IsNullOrEmpty(relPath))
            return MobileOperationResult.Fail("请选择一个文件下载");

        progress?.Invoke($"正在连接设备读取 {Path.GetFileName(relPath)}…", 15, true);
        return await RunContainerOpAsync(target, cancellationToken,
            (fs, root) => SaveOpenedFileAsync(fs, root, relPath, resolveDestination, dirError, cancellationToken, progress));
    }

    private static async Task<MobileOperationResult> SaveOpenedFileAsync(
        IDeviceFileSystem fileSystem, string root, string relPath,
        Func<string, string> resolveDestination, string dirError,
        CancellationToken cancellationToken, Action<string, int, bool>? progress)
    {
        var fileName = Path.GetFileName(relPath);
        var devicePath = ResolveDevicePath(root, relPath);
        var stat = await fileSystem.StatAsync(devicePath, cancellationToken);
        if (stat?.IsDirectory == true)
            return MobileOperationResult.Fail(dirError);

        progress?.Invoke($"正在从设备下载：{fileName}", 45, stat is null);
        var bytes = await fileSystem.ReadAllBytesAsync(devicePath, cancellationToken);
        var destination = resolveDestination(fileName);
        progress?.Invoke($"正在写入本地文件：{Path.GetFileName(destination)}", 88, false);
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
        progress?.Invoke($"下载完成：{Path.GetFileName(destination)}", 100, false);
        return new MobileOperationResult
        {
            Ok = true,
            Message = $"已保存到 {destination}",
            Detail = $"{bytes.Length:N0} B",
        };
    }

    private static async Task CollectRemoteFilesAsync(
        IDeviceFileSystem fileSystem,
        string remoteDir,
        string localDir,
        List<(string RemotePath, string LocalPath)> files,
        CancellationToken cancellationToken,
        Action<string, int, bool>? progress)
    {
        var entries = await fileSystem.ListDetailedAsync(remoteDir, includeHidden: true, cancellationToken);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remoteChild = $"{remoteDir.TrimEnd('/')}/{entry.Name}";
            var localChild = Path.Combine(localDir, entry.Name);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(localChild);
                await CollectRemoteFilesAsync(fileSystem, remoteChild, localChild, files, cancellationToken, progress);
            }
            else
            {
                files.Add((remoteChild, localChild));
                if (files.Count % 25 == 0)
                    progress?.Invoke($"正在统计待下载文件（已扫描 {files.Count} 个）…", 15, true);
            }
        }
    }

    private static MobileFilePreviewResult PreviewError(string targetId, string relPath, string name, string error) =>
        new() { TargetId = targetId, Path = relPath, Name = name, Error = error };

    private static string JoinRelative(string basePath, string name) =>
        string.IsNullOrEmpty(basePath) ? name : $"{basePath}/{name}";

    private static string ResolveDevicePath(string root, string relPath)
    {
        var cleanRoot = root.TrimEnd('/');
        return string.IsNullOrEmpty(relPath) ? cleanRoot : $"{cleanRoot}/{relPath}";
    }

    private static bool IsInsideLoader(string relPath) =>
        relPath.Equals(MobilePaths.LoaderRelative, StringComparison.OrdinalIgnoreCase) ||
        relPath.StartsWith(MobilePaths.LoaderRelative + "/", StringComparison.OrdinalIgnoreCase);

    private static string UniqueLocalPath(string directory, string fileName) =>
        FindUniquePath(fileName, candidate => File.Exists(Path.Combine(directory, candidate))
            ? null
            : Path.Combine(directory, candidate),
            (stem, ext, i) => Path.Combine(directory, $"{stem} ({i}){ext}"),
            (stem, ext) => Path.Combine(directory, $"{stem}-{Guid.NewGuid():N}{ext}"));

    private static string UniqueLocalDirectoryPath(string directory, string dirName) =>
        FindUniquePath(dirName,
            candidate => Directory.Exists(Path.Combine(directory, candidate)) || File.Exists(Path.Combine(directory, candidate))
                ? null
                : Path.Combine(directory, candidate),
            (stem, ext, i) => Path.Combine(directory, $"{stem}{ext} ({i})"),
            (stem, ext) => Path.Combine(directory, $"{stem}{ext}-{Guid.NewGuid():N}"));

    private static string FindUniquePath(
        string name,
        Func<string, string?> checkExact,
        Func<string, string, int, string> formatNth,
        Func<string, string, string> formatFallback)
    {
        if (checkExact(name) is { } exact)
            return exact;

        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 1; i < 1000; i++)
        {
            var next = formatNth(stem, ext, i);
            if (!File.Exists(next) && !Directory.Exists(next))
                return next;
        }

        return formatFallback(stem, ext);
    }
}
