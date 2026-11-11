using System.Security.Cryptography;

namespace AstralParty.Toys.Services;

public sealed record DataRootMoveResult(bool Ok, string Message);

/// <summary>
/// 把数据文件夹整个搬到另一个位置（设置页点「更改」时用）。
///
/// 只接受空目标目录，逐项搬完后如果中途失败就把已经搬走的搬回来——不会留下"一半在旧位置、
/// 一半在新位置"的状态。同一卷走改名，跨卷走复制 → 校验 → 删除源。
/// </summary>
public static class DataRootMover
{
    public static DataRootMoveResult Move(string source, string target)
    {
        string from, to;
        try
        {
            from = Path.GetFullPath(source).TrimEnd('\\');
            to = Path.GetFullPath(target).TrimEnd('\\');
        }
        catch (Exception ex)
        {
            return new DataRootMoveResult(false, "路径无效：" + ex.Message);
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return new DataRootMoveResult(true, "");
        if (IsInside(to, from) || IsInside(from, to))
            return new DataRootMoveResult(false, "两个文件夹不能互相包含。");

        try
        {
            Directory.CreateDirectory(to);
        }
        catch (Exception ex)
        {
            return new DataRootMoveResult(false, "无法创建目标文件夹：" + ex.Message);
        }

        if (!ToysPaths.TryEnsureWritable(to))
            return new DataRootMoveResult(false, "目标文件夹不可写。");
        if (Directory.EnumerateFileSystemEntries(to).Any())
            return new DataRootMoveResult(false, "目标文件夹不是空的，请换一个空文件夹。");
        if (!Directory.Exists(from))
            return new DataRootMoveResult(true, "");

        var moved = new List<(string From, string To)>();
        string[] entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(from).ToArray();
        }
        catch (Exception ex)
        {
            return new DataRootMoveResult(false, "读取数据文件夹失败：" + ex.Message);
        }

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith(".astral-write-probe", StringComparison.OrdinalIgnoreCase)) continue;

            var destination = Path.Combine(to, name);
            if (!TryMoveEntry(entry, destination, out var error))
            {
                Rollback(moved);
                return new DataRootMoveResult(false, $"{name}：{error}");
            }
            moved.Add((entry, destination));
        }

        return new DataRootMoveResult(true, "");
    }

    private static void Rollback(List<(string From, string To)> moved)
    {
        for (var index = moved.Count - 1; index >= 0; index--)
        {
            var (from, to) = moved[index];
            try
            {
                if (!TryMoveEntry(to, from, out var error))
                    ToysLog.Error("config", $"回滚 {to} → {from} 失败：{error}");
            }
            catch (Exception ex)
            {
                ToysLog.Error("config", $"回滚 {to} → {from} 失败", ex);
            }
        }
    }

    private static bool TryMoveEntry(string source, string target, out string error)
    {
        error = "";
        var directory = Directory.Exists(source);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (directory) Directory.Move(source, target);
            else File.Move(source, target);
            return true;
        }
        catch (IOException)
        {
            // 多半跨卷，走复制校验
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }

        try
        {
            if (directory) CopyDirectory(source, target);
            else File.Copy(source, target, overwrite: false);

            if (!SameContent(source, target, directory))
            {
                error = "复制后校验不一致，源数据已保留";
                return false;
            }

            if (directory) Directory.Delete(source, recursive: true);
            else File.Delete(source);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        foreach (var child in Directory.EnumerateDirectories(source))
            CopyDirectory(child, Path.Combine(target, Path.GetFileName(child)));
    }

    private static bool SameContent(string source, string target, bool directory)
    {
        try
        {
            if (!directory) return SameFile(source, target);

            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToArray();
            if (files.Length != Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).Count())
                return false;

            foreach (var file in files)
            {
                var counterpart = Path.Combine(target, Path.GetRelativePath(source, file));
                if (!File.Exists(counterpart) || !SameFile(file, counterpart)) return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool SameFile(string left, string right)
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

    private static bool IsInside(string child, string parent) =>
        child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
