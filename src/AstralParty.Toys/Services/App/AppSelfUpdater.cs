using System.Diagnostics;
using System.IO;
using System.Text;

namespace AstralParty.Toys.Services.App;

/// <summary>退出后替换程序要用的路径。全部由程序算出来，不接受界面传入。</summary>
public sealed class AppReplacePlan
{
    public int ProcessId { get; init; }
    public AppUpdateMode Mode { get; init; }
    /// <summary>正在运行的 exe：单文件版要替换的就是它；两种都在完成后重新打开它。</summary>
    public string CurrentExe { get; init; } = "";
    /// <summary>单文件版：下载好、已校验的新 exe。</summary>
    public string NewExe { get; init; } = "";
    /// <summary>便携版：新版本解压出来的文件夹。</summary>
    public string StagingDirectory { get; init; } = "";
    /// <summary>便携版：程序文件夹。</summary>
    public string AppDirectory { get; init; } = "";
    /// <summary>便携版：退出前备份的、会被覆盖的旧文件。</summary>
    public string BackupDirectory { get; init; } = "";
    /// <summary>下载的原始文件（exe 或 zip），成功后删掉。</summary>
    public string DownloadedFile { get; init; } = "";
    /// <summary>写结果的文件：新版本启动时读它，告诉用户成功还是已还原。</summary>
    public string ResultFile { get; init; } = "";
    public string NewVersion { get; init; } = "";
    /// <summary>完成后重新打开程序（测试里关掉，测试用的「exe」只是文本文件）。</summary>
    public bool Restart { get; init; } = true;
}

/// <summary>
/// 单文件版和便携版的原地更新。运行中的程序换不掉自己，所以写一个临时脚本：
/// 等本进程退出，再换文件；任何一步失败就还原旧文件。最后重新打开程序、删掉脚本。
/// 写法和 <see cref="AppUninstaller"/> 一样（等待方式、系统命令写完整路径）。
/// </summary>
public static class AppSelfUpdater
{
    public const string ResultOk = "ok";
    public const string ResultRestored = "restored";

    public static string UpdateTempDirectory => Path.Combine(Path.GetTempPath(), "AstralPartyToysUpdate");

    public static string DefaultResultFile => Path.Combine(UpdateTempDirectory, "self-update-result.txt");

    /// <summary>
    /// 便携版：程序还开着时，先把会被新版覆盖的旧文件复制一份（运行中的 dll 可以读）。
    /// 只备份重名的文件，不整个复制程序文件夹：用户可能把便携版解压在桌面这种放着别的东西的地方。
    /// </summary>
    public static void BackupOverwrittenFiles(string stagingDirectory, string appDirectory, string backupDirectory)
    {
        if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, recursive: true);
        Directory.CreateDirectory(backupDirectory);
        foreach (var source in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(stagingDirectory, source);
            var existing = Path.Combine(appDirectory, relative);
            if (!File.Exists(existing)) continue;
            var target = Path.Combine(backupDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(existing, target, overwrite: true);
        }
    }

    public static string BuildScript(AppReplacePlan plan)
    {
        var paths = new List<string> { plan.CurrentExe, plan.ResultFile };
        if (plan.Mode == AppUpdateMode.ReplaceExe) paths.Add(plan.NewExe);
        else if (plan.Mode == AppUpdateMode.ReplaceFolder) paths.AddRange(new[] { plan.StagingDirectory, plan.AppDirectory, plan.BackupDirectory });
        else throw new ArgumentException($"不支持的更新方式：{plan.Mode}");
        if (!string.IsNullOrEmpty(plan.DownloadedFile)) paths.Add(plan.DownloadedFile);
        foreach (var path in paths) RequireSafePath(path);
        if (plan.NewVersion.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-' or '+')))
            throw new ArgumentException($"版本号里有不安全的字符：{plan.NewVersion}");

        var script = new StringBuilder();
        void Line(string text) => script.Append(text).Append("\r\n");   // 批处理的标签在只有 LF 的文件里会失灵

        static string Sys(string name) => "\"%SystemRoot%\\System32\\" + name + "\"";
        var sleep = Sys("PING.EXE") + " -n 2 127.0.0.1 >nul";
        var start = plan.Restart ? $"start \"\" /D {Quote(Path.GetDirectoryName(plan.CurrentExe)!)} {Quote(plan.CurrentExe)}" : "rem";
        void Result(string value) => Line($"> {Quote(plan.ResultFile)} echo {value} {plan.NewVersion}");

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

        if (plan.Mode == AppUpdateMode.ReplaceExe)
        {
            var old = plan.CurrentExe + ".update-old";
            Line($"del /f /q {Quote(old)} >nul 2>&1");
            Line("set /a tries=0");
            // 先改名而不是直接覆盖：改名成功说明文件没被占用，失败了也能原样改回来
            Line(":rename");
            Line($"move /y {Quote(plan.CurrentExe)} {Quote(old)} >nul 2>&1");
            Line($"if not exist {Quote(plan.CurrentExe)} goto renamed");
            Line("set /a tries+=1");
            Line("if %tries% geq 15 goto failed");
            Line(sleep);
            Line("goto rename");
            Line(":renamed");
            Line($"copy /y /b {Quote(plan.NewExe)} {Quote(plan.CurrentExe)} >nul 2>&1");
            Line("if errorlevel 1 goto restore");
            Line($"if not exist {Quote(plan.CurrentExe)} goto restore");
            Line($"del /f /q {Quote(old)} >nul 2>&1");
            Line($"del /f /q {Quote(plan.NewExe)} >nul 2>&1");
            Line("goto ok");
            Line(":restore");
            Line($"del /f /q {Quote(plan.CurrentExe)} >nul 2>&1");
            Line($"move /y {Quote(old)} {Quote(plan.CurrentExe)} >nul 2>&1");
            Line("goto failed");
        }
        else
        {
            var robocopy = Sys("Robocopy.exe");
            const string quiet = "/R:10 /W:1 /NFL /NDL /NJH /NJS /NP >nul";
            // 只覆盖和新增，不删程序文件夹里多出来的东西（可能是用户自己放的）
            Line($"{robocopy} {Quote(plan.StagingDirectory)} {Quote(plan.AppDirectory)} /E /IS /IT {quiet}");
            Line("if errorlevel 8 goto restore");   // robocopy 返回 8 及以上才是失败
            Line($"rd /s /q {Quote(plan.StagingDirectory)} >nul 2>&1");
            Line($"rd /s /q {Quote(plan.BackupDirectory)} >nul 2>&1");
            Line("goto ok");
            Line(":restore");
            Line($"{robocopy} {Quote(plan.BackupDirectory)} {Quote(plan.AppDirectory)} /E /IS /IT {quiet}");
            Line("goto failed");
        }

        Line(":ok");
        if (!string.IsNullOrEmpty(plan.DownloadedFile)) Line($"del /f /q {Quote(plan.DownloadedFile)} >nul 2>&1");
        Result(ResultOk);
        Line(start);
        Line("goto end");
        Line(":failed");
        Result(ResultRestored);
        Line(start);   // 失败也把（旧版）程序打开，别让用户什么都没有
        Line(":end");
        Line("(goto) 2>nul & del /f /q \"%~f0\"");
        return script.ToString();
    }

    /// <summary>写好脚本并启动（它会等本进程退出才动手）。启动失败会抛异常，调用方不要退出程序。</summary>
    public static Process Launch(AppReplacePlan plan, string? tempDirectory = null)
    {
        var temp = tempDirectory ?? Path.GetTempPath();
        var scriptPath = Path.Combine(temp, $"astralparty-toys-update-{Guid.NewGuid():N}.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(plan.ResultFile)!);
        if (File.Exists(plan.ResultFile)) File.Delete(plan.ResultFile);
        File.WriteAllText(scriptPath, BuildScript(plan), new UTF8Encoding(false));
        var startInfo = new ProcessStartInfo("cmd.exe", $"/d /c \"\"{scriptPath}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = temp   // 工作目录不能留在要替换的文件夹里
        };
        return Process.Start(startInfo) ?? throw new InvalidOperationException("没能启动更新脚本。");
    }

    /// <summary>
    /// 读出并删掉上一次原地更新的结果。没有结果时返回 null。
    /// 返回 (成功没有, 目标版本)。
    /// </summary>
    public static (bool Ok, string Version)? TakeLastResult(string? resultFile = null)
    {
        var file = resultFile ?? DefaultResultFile;
        try
        {
            if (!File.Exists(file)) return null;
            var parts = File.ReadAllText(file).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            File.Delete(file);
            if (parts.Length == 0) return null;
            return (parts[0] == ResultOk, parts.Length > 1 ? parts[1].Trim() : "");
        }
        catch
        {
            return null;
        }
    }

    private static string Quote(string path) => "\"" + path.Replace("%", "%%") + "\"";

    private static void RequireSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Contains('"')
            || string.IsNullOrEmpty(Path.GetFileName(Path.TrimEndingDirectorySeparator(path))))
            throw new ArgumentException($"不安全的更新路径：{path}");
    }
}
