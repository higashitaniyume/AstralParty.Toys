using System.Text;

namespace AstralParty.Toys.Services.Common;

/// <summary>Read-only access to installed files. The Linux host does not follow links in the inspected tree.</summary>
internal static class InspectionFiles
{
    private const int MetadataLimit = 1024 * 1024;

    internal static bool DirectoryExists(string path) => OperatingSystem.IsLinux()
        ? HasUnlinkedPath(path, directory: true)
        : Directory.Exists(path);

    internal static bool FileExists(string path) => OperatingSystem.IsLinux()
        ? HasUnlinkedPath(path, directory: false)
        : File.Exists(path);

    internal static bool IsSymbolicLink(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static Stream? OpenRead(string path, long maxBytes = long.MaxValue)
    {
        if (!FileExists(path)) return null;
        if (OperatingSystem.IsLinux())
        {
            // FIFOs, sockets and device nodes have no file contents on the filesystem. Do not open them.
            var length = new FileInfo(path).Length;
            if (length <= 0 || length > maxBytes) return null;
        }
        return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    internal static string? ReadText(string path, int maxBytes = MetadataLimit)
    {
        if (!OperatingSystem.IsLinux()) return File.Exists(path) ? File.ReadAllText(path) : null;
        var bytes = ReadBytes(path, maxBytes);
        if (bytes is null) return null;
        var utf8 = bytes.AsSpan();
        return Encoding.UTF8.GetString(utf8.StartsWith("\uFEFF"u8) ? utf8[3..] : utf8);
    }

    internal static byte[]? ReadBytes(string path, int maxBytes = MetadataLimit)
    {
        if (!OperatingSystem.IsLinux()) return File.Exists(path) ? File.ReadAllBytes(path) : null;
        using var source = OpenRead(path, maxBytes);
        if (source is null) return null;
        var length = source.Length;
        if (length > maxBytes) return null;
        var bytes = new byte[(int)length];
        source.ReadExactly(bytes);
        return bytes;
    }

    private static bool HasUnlinkedPath(string path, bool directory)
    {
        if (!Path.IsPathFullyQualified(path)) return false;
        try
        {
            // Check the ancestors first. File.GetAttributes alone only protects the last path component.
            var parent = Path.GetDirectoryName(path);
            if (parent is not null && !HasUnlinkedPath(parent, directory: true)) return false;
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) == 0 &&
                ((attributes & FileAttributes.Directory) != 0) == directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
