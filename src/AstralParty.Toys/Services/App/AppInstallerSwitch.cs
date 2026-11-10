using System.IO;
using Microsoft.Win32;

namespace AstralParty.Toys.Services.App;

/// <summary>
/// 「改用安装版」之后的收尾。
/// 装好的新版在「本机应用」目录里，用户手里那份旧的 exe / 便携目录还在，
/// 再打开它会一直提示有更新。这里记一个标记：下次启动旧的那份时，
/// 告诉用户新版本装到哪了，并不再反复提示更新。
/// </summary>
public static class AppInstallerSwitch
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E9C6C61-5A60-4EF0-AD84-8F59E877A663}_is1";

    public static string MarkerPath => Path.Combine(AppSelfUpdater.UpdateTempDirectory, "switch-to-installer.txt");

    /// <summary>记下「这份旧程序已经改装成安装版」。</summary>
    public static void WriteMarker(string oldExePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
            File.WriteAllText(MarkerPath, oldExePath ?? "");
        }
        catch
        {
            // 标记写不了不影响更新本身，忽略
        }
    }

    /// <summary>读标记（不删）。没有标记时返回 null。</summary>
    public static string? ReadMarker()
    {
        try
        {
            if (!File.Exists(MarkerPath)) return null;
            var text = File.ReadAllText(MarkerPath).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>删掉标记。</summary>
    public static void ClearMarker()
    {
        try
        {
            if (File.Exists(MarkerPath)) File.Delete(MarkerPath);
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>安装版装到了哪里：先看安装器记录的目录，没有就用默认目录。</summary>
    public static string? InstalledExecutablePath()
    {
        foreach (var directory in InstalledDirectories())
        {
            var exe = Path.Combine(directory, "AstralParty.Toys.exe");
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    private static IEnumerable<string> InstalledDirectories()
    {
        string? recorded = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            recorded = key?.GetValue("InstallLocation") as string;
        }
        catch
        {
            // 读不到注册表就用默认目录
        }
        if (!string.IsNullOrWhiteSpace(recorded)) yield return recorded;
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "AstralParty.Toys");
    }
}
