using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace AstralParty.Toys.LinuxHost;

// All game writes use directory descriptors and single-component names. A check of a
// string path followed by File.Move/WriteAllBytes would re-follow substituted symlinks.
internal sealed class LinuxLoaderFiles : IDisposable
{
    private const int AtCwd = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const int AtRemoveDir = 0x200;
    private const int AtEmptyPath = 0x1000;
    private const int OWriteOnly = 1;
    private const int OCreate = 0x40;
    private const int OExclusive = 0x80;
    private const int ONonBlock = 0x800;
    private const int ODirectory = 0x10000;
    private const int ONoFollow = 0x20000;
    private const int OCloseOnExec = 0x80000;
    private const int DirectoryFlags = ODirectory | ONoFollow | OCloseOnExec;
    private const int Missing = 2;
    private const int Exists = 17;
    private const int LockExclusiveNonBlocking = 2 | 4;
    private const uint RenameNoReplace = 1;
    private const uint StatModeInodeSize = 0x303;
    private const int FileTypeMask = 0xf000;
    private const int RegularFile = 0x8000;
    private const int DirectoryMode = 0x1ed; // 0755, subject to umask
    private const int PrivateDirectoryMode = 0x1c0; // 0700
    private const int DefaultFileMode = 0x1a4; // 0644, subject to umask
    private const int PermissionMask = 0x1ff;
    internal const int MaxFileBytes = 64 * 1024 * 1024;
    private const int MaxMetadataBytes = 1024 * 1024;

    private readonly SafeFileHandle root;

    internal LinuxLoaderFiles(string gameDirectory)
    {
        if (!Path.IsPathFullyQualified(gameDirectory) || Path.GetFullPath(gameDirectory) == "/")
            throw new InvalidOperationException("请选择有效的游戏目录。");

        using var systemRoot = Open(AtCwd, "/", DirectoryFlags)!;
        root = OpenDirectory(systemRoot, Path.GetFullPath(gameDirectory).Trim('/'))
            ?? throw new DirectoryNotFoundException($"找不到游戏目录：{gameDirectory}");
        if (Flock(Fd(root), LockExclusiveNonBlocking) != 0)
        {
            var error = LastError("无法锁定游戏目录（可能已有加载器操作在进行）", gameDirectory);
            root.Dispose();
            throw error;
        }
    }

    public void Dispose() => root.Dispose();

    internal bool DirectoryExists(string relative)
    {
        using var directory = OpenDirectory(root, relative);
        return directory is not null;
    }

    internal bool IsRegularFile(string relative)
    {
        using var parent = OpenParent(relative, out var name);
        return parent is not null && Stat(Fd(parent), name, AtSymlinkNoFollow) is { } file &&
            (file.Mode & FileTypeMask) == RegularFile;
    }


    internal FileSnapshot? Inspect(string relative, bool readMetadata = false)
    {
        using var parent = OpenParent(relative, out var name);
        return parent is null ? null : Inspect(parent, name, relative, readMetadata);
    }

    internal void Commit(IReadOnlyList<FileChange> changes, IReadOnlyList<string> requiredDirectories)
    {
        if (changes.Count == 0 && requiredDirectories.All(DirectoryExists)) return;

        var workName = ".astralparty-toys-loader-" + Guid.NewGuid().ToString("N");
        if (MkdirAt(Fd(root), workName, PrivateDirectoryMode) != 0)
            throw LastError("无法创建加载器事务目录", workName);
        using var work = Open(Fd(root), workName, DirectoryFlags)!;
        var created = new List<string>();
        var steps = new List<AppliedChange>(changes.Count);
        var retainBackup = false;

        try
        {
            // Stage every replacement before touching any installed file.
            foreach (var change in changes)
            {
                var step = new AppliedChange(change, steps.Count.ToString(CultureInfo.InvariantCulture));
                steps.Add(step);
                if (change.Write is null) continue;
                using var staged = Open(Fd(work), step.NewName,
                    OWriteOnly | OCreate | OExclusive | ONoFollow | OCloseOnExec, DefaultFileMode)!;
                using (var output = new FileStream(staged, FileAccess.Write))
                {
                    change.Write(output);
                    if (change.Before is { } before && Fchmod(Fd(staged), before.Identity.Mode & PermissionMask) != 0)
                        throw LastError("无法保留文件权限", change.Path);
                }
                step.NewIdentity = Stat(Fd(work), step.NewName, AtSymlinkNoFollow)!.Value.Identity;
            }

            foreach (var relative in requiredDirectories)
                OpenDirectory(root, relative, created)!.Dispose();

            foreach (var step in steps)
            {
                var change = step.Change;
                using var parent = OpenParent(change.Path, out var name, change.Write is null ? null : created)
                    ?? throw new IOException($"操作期间目录已变更：{change.Path}");
                if (change.Before is { } before)
                {
                    var now = Stat(Fd(parent), name, AtSymlinkNoFollow)?.Identity;
                    if (now != before.Identity) throw Changed(change.Path);
                    Rename(parent, name, work, step.OldName, change.Path);
                    step.OldMoved = true;
                    // Verify the actual file moved, not just the directory entry seen during preflight.
                    var moved = Inspect(work, step.OldName, change.Path, readMetadata: false);
                    if (moved?.Identity != before.Identity || moved?.Sha256 != before.Sha256) throw Changed(change.Path);
                }
                if (change.Write is not null)
                {
                    RejectCaseAlias(parent, name, change.Path);
                    Rename(work, step.NewName, parent, name, change.Path);
                    step.NewInstalled = true;
                }
            }
        }
        catch (Exception failure)
        {
            List<Exception> errors = [failure];
            for (var i = steps.Count - 1; i >= 0; i--)
            {
                var step = steps[i];
                if (!step.OldMoved && !step.NewInstalled) continue;
                try
                {
                    using var parent = OpenParent(step.Change.Path, out var name)
                        ?? throw Changed(step.Change.Path);
                    if (step.NewInstalled)
                    {
                        if (Stat(Fd(parent), name, AtSymlinkNoFollow)?.Identity != step.NewIdentity)
                            throw Changed(step.Change.Path);
                        Rename(parent, name, work, step.NewName, step.Change.Path);
                    }
                    if (step.OldMoved) Rename(work, step.OldName, parent, name, step.Change.Path);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            PruneEmptyDirectories(created);
            if (errors.Count > 1)
            {
                retainBackup = true;
                throw new IOException($"加载器操作失败且无法完整还原。未覆盖冲突文件，事务备份保留于游戏目录的 {workName}。",
                    new AggregateException(errors));
            }
            throw;
        }
        finally
        {
            if (!retainBackup)
            {
                foreach (var step in steps)
                {
                    UnlinkAt(Fd(work), step.OldName, 0);
                    UnlinkAt(Fd(work), step.NewName, 0);
                }
                UnlinkAt(Fd(root), workName, AtRemoveDir);
            }
        }
    }

    internal void PruneEmptyDirectories(IEnumerable<string> paths)
    {
        foreach (var relative in paths.Distinct(StringComparer.Ordinal).OrderByDescending(p => p.Length))
        {
            try
            {
                using var parent = OpenParent(relative, out var name);
                if (parent is not null) UnlinkAt(Fd(parent), name, AtRemoveDir);
            }
            catch (IOException)
            {
                // No recursive removal. A busy, foreign or replaced directory is left alone.
            }
        }
    }

    private SafeFileHandle? OpenParent(string relative, out string name, List<string>? created = null)
    {
        var slash = relative.LastIndexOf('/');
        name = relative[(slash + 1)..];
        return OpenDirectory(root, slash < 0 ? "" : relative[..slash], created);
    }

    private static SafeFileHandle? OpenDirectory(SafeFileHandle scope, string relative, List<string>? created = null)
    {
        var current = Open(Fd(scope), ".", DirectoryFlags)!;
        try
        {
            var offset = 0;
            while (offset < relative.Length)
            {
                var end = relative.IndexOf('/', offset);
                if (end < 0) end = relative.Length;
                var name = relative[offset..end];
                var next = Open(Fd(current), name, DirectoryFlags, optional: true);
                if (next is null)
                {
                    RejectCaseAlias(current, name, relative[..end]);
                    if (created is null) return null;
                    if (MkdirAt(Fd(current), name, DirectoryMode) == 0) created.Add(relative[..end]);
                    else if (Marshal.GetLastPInvokeError() != Exists) throw LastError("无法创建目录", relative[..end]);
                    next = Open(Fd(current), name, DirectoryFlags)!;
                }
                current.Dispose();
                current = next;
                offset = end + 1;
            }
            var result = current;
            current = null!;
            return result;
        }
        finally
        {
            current?.Dispose();
        }
    }

    private static FileSnapshot? Inspect(SafeFileHandle parent, string name, string display, bool readMetadata)
    {
        var before = Stat(Fd(parent), name, AtSymlinkNoFollow);
        if (before is not { } expected)
        {
            RejectCaseAlias(parent, name, display);
            return null;
        }
        if ((expected.Mode & FileTypeMask) != RegularFile)
            throw new IOException($"拒绝操作符号链接或非普通文件：{display}");
        var limit = readMetadata ? MaxMetadataBytes : MaxFileBytes;
        if (expected.Size > (ulong)limit) throw new InvalidDataException($"文件超过大小限制：{display}");
        using var handle = Open(Fd(parent), name, ONonBlock | ONoFollow | OCloseOnExec)!;
        var opened = Stat(Fd(handle), "", AtEmptyPath)!.Value;
        if (opened.Identity != expected.Identity || opened.Size != expected.Size) throw Changed(display);
        using var input = new FileStream(handle, FileAccess.Read);
        if (!readMetadata) return new FileSnapshot(expected.Identity, Convert.ToHexString(SHA256.HashData(input)), null);
        var bytes = new byte[(int)expected.Size];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw Changed(display);
        return new FileSnapshot(expected.Identity, Convert.ToHexString(SHA256.HashData(bytes)), bytes);
    }

    private static void RejectCaseAlias(SafeFileHandle parent, string name, string display)
    {
        // Linux paths are case-sensitive; the game runs through a Windows path layer.
        foreach (var path in Directory.EnumerateFileSystemEntries($"/proc/self/fd/{Fd(parent)}"))
        {
            var existing = Path.GetFileName(path);
            if (existing != name && string.Equals(existing, name, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"存在仅大小写不同的游戏文件，未改动：{display}");
        }
    }

    private static void Rename(SafeFileHandle from, string source, SafeFileHandle to, string target, string display)
    {
        if (RenameAt2(Fd(from), source, Fd(to), target, RenameNoReplace) != 0)
            throw LastError("无法原子移动加载器文件", display);
    }

    private static SafeFileHandle? Open(int dir, string name, int flags, int mode = 0, bool optional = false)
    {
        var fd = OpenAt(dir, name, flags, mode);
        if (fd >= 0) return new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        if (optional && Marshal.GetLastPInvokeError() == Missing) return null;
        throw LastError("无法安全打开路径（不允许符号链接）", name);
    }

    private static StatInfo? Stat(int dir, string name, int flags)
    {
        if (StatAt(dir, name, flags, StatModeInodeSize, out var status) == 0) return status;
        if (Marshal.GetLastPInvokeError() == Missing) return null;
        throw LastError("无法检查加载器文件", name);
    }

    private static int Fd(SafeFileHandle handle) => handle.DangerousGetHandle().ToInt32();
    private static IOException Changed(string path) => new($"操作期间文件已变更，已停止：{path}");
    private static IOException LastError(string action, string path) =>
        new($"{action}：{path}（{new Win32Exception(Marshal.GetLastPInvokeError()).Message}）");

    internal sealed record FileChange(string Path, FileSnapshot? Before, Action<Stream>? Write);
    internal sealed record FileSnapshot(FileIdentity Identity, string Sha256, byte[]? Bytes);
    internal readonly record struct FileIdentity(uint Major, uint Minor, ulong Inode, ushort Mode);

    private sealed class AppliedChange(FileChange change, string id)
    {
        internal FileChange Change { get; } = change;
        internal string NewName { get; } = id + ".new";
        internal string OldName { get; } = id + ".old";
        internal FileIdentity? NewIdentity { get; set; }
        internal bool OldMoved { get; set; }
        internal bool NewInstalled { get; set; }
    }

    // Linux statx ABI uses fixed-width fields on all architectures.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatInfo
    {
        [FieldOffset(28)] public ushort Mode;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(40)] public ulong Size;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
        public readonly FileIdentity Identity => new(DeviceMajor, DeviceMinor, Inode, Mode);
    }

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(int dir, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int flags, int mode);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int StatAt(int dir, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int flags, uint mask, out StatInfo info);

    [DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
    private static extern int MkdirAt(int dir, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int mode);

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameAt2(int from, [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        int to, [MarshalAs(UnmanagedType.LPUTF8Str)] string target, uint flags);

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAt(int dir, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int flags);

    [DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static extern int Fchmod(int fd, int mode);

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int fd, int operation);
}
