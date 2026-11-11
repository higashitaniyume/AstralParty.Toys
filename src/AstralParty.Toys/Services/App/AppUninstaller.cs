using System.Diagnostics;
using System.IO;
using System.Text;

namespace AstralParty.Toys.Services.App;

/// <summary>退出后要清理的东西。路径全部由程序算出来，不接受界面传入。</summary>
public sealed class AppUninstallPlan
{
    public int ProcessId { get; init; }
    public string ExePath { get; init; } = "";
    /// <summary>整个删除的目录（单文件解压目录、本机缓存等，都是程序自己的）。</summary>
    public IReadOnlyList<string> Directories { get; init; } = Array.Empty<string>();
    /// <summary>逐个删除的文件。</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    /// <summary>只有已经变空才删除的目录（设置文件夹可能同时放着回放，不能整个删）。</summary>
    public IReadOnlyList<string> EmptyOnlyDirectories { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 卸载程序本身。运行中的 exe 删不掉自己，所以写一个临时脚本：等本进程退出，再删 exe 和选中的数据，最后删脚本。
/// </summary>
public static class AppUninstaller
{
    /// <summary>
    /// 只有单文件版才能这样卸载。返回不能卸载的原因，能卸载时返回 null。
    /// exe 旁边有同名 dll 说明是 Debug 或文件夹版：exe 只是其中一个文件，删掉它只会弄坏程序。
    /// </summary>
    public static string? UnsupportedReason(string? exePath, bool isInstaller)
    {
        if (isInstaller) return "这个版本是用安装程序装的，请在 Windows「设置 → 应用」里卸载。";
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return "找不到程序文件的位置。";
        var directory = Path.GetDirectoryName(exePath)!;
        var dll = Path.Combine(directory, Path.GetFileNameWithoutExtension(exePath) + ".dll");
        if (File.Exists(dll) || File.Exists(Path.Combine(directory, "AstralParty.Toys.dll")))
            return "这不是单文件版（调试版或文件夹版），不能在程序里卸载，直接删除程序文件夹就行。";
        return null;
    }

    /// <summary>单文件版运行时解压出来的目录。只认临时目录下 .net 里的路径，别的一律不算。</summary>
    public static string? ExtractDirectory(string exePath, string baseDirectory, string tempDirectory)
    {
        var extract = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        var exeDirectory = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(exePath))!);
        var dotnetRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(tempDirectory, ".net")));
        if (string.Equals(extract, exeDirectory, StringComparison.OrdinalIgnoreCase)) return null;
        return extract.StartsWith(dotnetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? extract : null;
    }

    /// <summary>
    /// 设置文件夹里属于工具的东西。回放和回放索引可能在同一个文件夹，所以只列名字明确的，绝不整个删。
    /// 还有游戏留着加载器时要保留 modloader-history：没有它，以后重装工具会认不出这份加载器。
    /// </summary>
    public static (IReadOnlyList<string> Files, IReadOnlyList<string> Directories) SettingsItems(string profileDirectory, bool keepLoaderHistory)
    {
        var files = new[]
            {
                ToysPaths.ConfigFileName,
                ToysPaths.ConfigFileName + ".bak",
                ToysPaths.ConfigFileName + ".tmp"
            }
            .Select(name => Path.Combine(profileDirectory, name)).ToList();

        // 回放固定在 replays\ 里，绝不在这个清单里：卸载工具不能带走玩家的回放。
        var directories = new List<string>
        {
            Path.Combine(profileDirectory, ToysPaths.SpeedhackFolderName),
            Path.Combine(profileDirectory, ToysPaths.ToolsFolderName),
            Path.Combine(profileDirectory, ToysPaths.BackgroundsFolderName),
            Path.Combine(profileDirectory, ToysPaths.StateFolderName),
            Path.Combine(profileDirectory, ToysPaths.LogsFolderName),
            Path.Combine(profileDirectory, ToysPaths.CacheFolderName)
        };
        if (!keepLoaderHistory) directories.Add(Path.Combine(profileDirectory, ToysPaths.InstallHistoryFolderName));
        return (files, directories);
    }

    public static string BuildScript(AppUninstallPlan plan)
    {
        foreach (var path in new[] { plan.ExePath }.Concat(plan.Directories).Concat(plan.Files).Concat(plan.EmptyOnlyDirectories))
            RequireSafePath(path);

        var script = new StringBuilder();
        void Line(string text) => script.Append(text).Append("\r\n");   // 批处理的标签在只有 LF 的文件里会失灵

        // 系统命令都写完整路径：装了 Git 的电脑，PATH 里可能先找到 Git 自带的 find，那是另一个程序（测试里踩过）
        static string Sys(string name) => "\"%SystemRoot%\\System32\\" + name + "\"";
        var sleep = Sys("PING.EXE") + " -n 2 127.0.0.1 >nul";

        Line("@echo off");
        Line(Sys("chcp.com") + " 65001 >nul");
        Line("set /a tries=0");
        Line(":wait");
        Line($"{Sys("tasklist.exe")} /fi \"PID eq {plan.ProcessId}\" /nh | {Sys("find.exe")} \" {plan.ProcessId} \" >nul");
        Line("if errorlevel 1 goto gone");
        Line("set /a tries+=1");
        Line("if %tries% geq 60 goto gone");
        Line(sleep);
        Line("goto wait");
        Line(":gone");
        Line("set /a tries=0");
        Line(":delexe");
        Line($"del /f /q {Quote(plan.ExePath)} >nul 2>&1");
        Line($"if not exist {Quote(plan.ExePath)} goto exedone");
        Line("set /a tries+=1");
        Line("if %tries% geq 15 goto exedone");
        Line(sleep);
        Line("goto delexe");
        Line(":exedone");
        foreach (var file in plan.Files) Line($"del /f /q {Quote(file)} >nul 2>&1");
        foreach (var directory in plan.Directories) Line($"rd /s /q {Quote(directory)} >nul 2>&1");
        foreach (var directory in plan.EmptyOnlyDirectories) Line($"rd {Quote(directory)} >nul 2>&1");   // 没有 /s：里面还有东西就删不掉
        Line("(goto) 2>nul & del /f /q \"%~f0\"");
        return script.ToString();
    }

    /// <summary>写好脚本并启动（它会等本进程退出才动手）。启动失败会抛异常，调用方不要退出程序。</summary>
    public static Process Launch(AppUninstallPlan plan, string? tempDirectory = null)
    {
        var temp = tempDirectory ?? Path.GetTempPath();
        var scriptPath = Path.Combine(temp, $"astralparty-toys-uninstall-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(scriptPath, BuildScript(plan), new UTF8Encoding(false));
        var start = new ProcessStartInfo("cmd.exe", $"/d /c \"\"{scriptPath}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = temp   // 工作目录不能留在要删的文件夹里，否则会占用它
        };
        return Process.Start(start) ?? throw new InvalidOperationException("没能启动清理脚本。");
    }

    private static string Quote(string path) => "\"" + path.Replace("%", "%%") + "\"";

    private static void RequireSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Contains('"')
            || string.IsNullOrEmpty(Path.GetFileName(Path.TrimEndingDirectorySeparator(path))))
            throw new ArgumentException($"不安全的卸载路径：{path}");
    }
}
