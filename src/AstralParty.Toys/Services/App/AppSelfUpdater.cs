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
    /// <summary>等程序退出的次数上限（每次约 1 秒）。超时后不动任何文件。</summary>
    public int WaitAttempts { get; init; } = 60;
}

/// <summary>上一次原地更新的结果。</summary>
public sealed record SelfUpdateOutcome(string Status, string Version)
{
    /// <summary>替换成功。</summary>
    public bool Ok => Status == AppSelfUpdater.ResultOk;
    /// <summary>程序没有退出，替换根本没有开始，文件没动过。</summary>
    public bool Busy => Status == AppSelfUpdater.ResultBusy;
    /// <summary>替换失败，已还原成原来的版本。</summary>
    public bool Restored => Status == AppSelfUpdater.ResultRestored;
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
    /// <summary>程序一直没退出，替换没有开始，文件原封不动。</summary>
    public const string ResultBusy = "busy";

    public static string UpdateTempDirectory => Path.Combine(Path.GetTempPath(), "AstralPartyToysUpdate");

    public static string DefaultResultFile => Path.Combine(UpdateTempDirectory, "self-update-result.txt");

    /// <summary>
    /// 便携版：新版本带来的、旧版本里没有的文件清单（完整路径，一行一个）。
    /// 放在备份目录外面，免得回滚时被 robocopy 拷回程序目录。
    /// </summary>
    public static string AddedFilesListPath(string backupDirectory) => backupDirectory + "-added-files.txt";

    /// <summary>
    /// 便携版：程序还开着时，先把会被新版覆盖的旧文件复制一份（运行中的 dll 可以读）。
    /// 只备份重名的文件，不整个复制程序文件夹：用户可能把便携版解压在桌面这种放着别的东西的地方。
    /// 同时把新版本新增的文件记下来，回滚时才能一并删掉，回到更新前的样子。
    /// </summary>
    public static IReadOnlyList<string> BackupOverwrittenFiles(string stagingDirectory, string appDirectory, string backupDirectory)
    {
        if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, recursive: true);
        Directory.CreateDirectory(backupDirectory);
        var added = new List<string>();
        foreach (var source in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(stagingDirectory, source);
            var existing = Path.Combine(appDirectory, relative);
            if (!File.Exists(existing))
            {
                // 新版本才有的文件：旧目录里没有，回滚时要删掉
                added.Add(existing);
                continue;
            }
            var target = Path.Combine(backupDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(existing, target, overwrite: true);
        }

        var listPath = AddedFilesListPath(backupDirectory);
        File.WriteAllLines(listPath, added, new UTF8Encoding(false));
        return added;
    }

    public static string BuildScript(AppReplacePlan plan)
    {
        var paths = new List<string> { plan.CurrentExe, plan.ResultFile };
        if (plan.Mode == AppUpdateMode.ReplaceExe) paths.Add(plan.NewExe);
        else if (plan.Mode == AppUpdateMode.ReplaceFolder) paths.AddRange(new[] { plan.StagingDirectory, plan.AppDirectory, plan.BackupDirectory, AddedFilesListPath(plan.BackupDirectory) }.Where(p => !string.IsNullOrEmpty(p)));
        else throw new ArgumentException($"不支持的更新方式：{plan.Mode}");
        if (!string.IsNullOrEmpty(plan.DownloadedFile)) paths.Add(plan.DownloadedFile);
        foreach (var path in paths) RequireSafePath(path);
        if (plan.NewVersion.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-' or '+')))
            throw new ArgumentException($"版本号里有不安全的字符：{plan.NewVersion}");

        var script = new StringBuilder();
        void Line(string text) => script.Append(text).Append("\r\n");   // 批处理的标签在只有 LF 的文件里会失灵

        static string Sys(string name) => "\"" + "%SystemRoot%" + "\\System32\\" + name + "\"";
        var sleep = Sys("PING.EXE") + " -n 2 127.0.0.1 >nul";
        var start = plan.Restart ? $"start \"\" /D {Quote(Path.GetDirectoryName(plan.CurrentExe)!)} {Quote(plan.CurrentExe)}" : "rem";
        void Result(string value) => Line($"> {Quote(plan.ResultFile)} echo {value} {plan.NewVersion}");
        // 等待进程退出：最多等 60 秒；超时后如果进程还在，就不动任何文件（改了多半会被占用挡下来）
        string Alive() => $"{Sys("tasklist.exe")} /fi \"PID eq {plan.ProcessId}\" /nh | {Sys("find.exe")} \" {plan.ProcessId} \" >nul";

        Line("@echo off");
        Line(Sys("chcp.com") + " 65001 >nul");
        Line("set /a tries=0");
        Line(":wait");
        Line(Alive());
        Line("if errorlevel 1 goto gone");
        Line("set /a tries+=1");
        Line($"if %tries% geq {Math.Max(1, plan.WaitAttempts)} goto busy");
        Line(sleep);
        Line("goto wait");
        Line(":busy");
        // 程序一直没退出：不动文件，也不删下载好的包，用户可以直接再试一次
        Line(Alive());
        Line("if errorlevel 1 goto gone");
        Result(ResultBusy);
        Line("goto end");
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
            const string quiet = "/R:5 /W:1 /NFL /NDL /NJH /NJS /NP >nul";
            // 只覆盖和新增，不删程序文件夹里多出来的东西（可能是用户自己放的）
            Line($"{robocopy} {Quote(plan.StagingDirectory)} {Quote(plan.AppDirectory)} /E /IS /IT {quiet}");
            Line("if errorlevel 8 goto restore");   // robocopy 返回 8 及以上才是失败
            Line($"rd /s /q {Quote(plan.StagingDirectory)} >nul 2>&1");
            Line($"rd /s /q {Quote(plan.BackupDirectory)} >nul 2>&1");
            Line($"del /f /q {Quote(AddedFilesListPath(plan.BackupDirectory))} >nul 2>&1");
            Line("goto ok");
            Line(":restore");
            // 回滚：先删掉这次新引入的文件，再把被覆盖的旧文件拷回去，目录就回到更新前的样子
            Line($"for /f \"usebackq delims=\" %%f in ({Quote(AddedFilesListPath(plan.BackupDirectory))}) do del /f /q \"%%f\" >nul 2>&1");
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
        Line(CleanupTemporary(plan));
        Line(start);   // 失败也把（旧版）程序打开，别让用户什么都没有
        Line(":end");
        Line("(goto) 2>nul & del /f /q \"%~f0\"");
        return script.ToString();
    }

    /// <summary>清理更新用到的临时产物：解压目录、备份目录、新增文件清单、下载的包。</summary>
    private static string CleanupTemporary(AppReplacePlan plan)
    {
        var lines = new List<string>();
        if (plan.Mode == AppUpdateMode.ReplaceFolder)
        {
            if (!string.IsNullOrEmpty(plan.StagingDirectory)) lines.Add($"rd /s /q {Quote(plan.StagingDirectory)} >nul 2>&1");
            if (!string.IsNullOrEmpty(plan.BackupDirectory))
            {
                lines.Add($"rd /s /q {Quote(plan.BackupDirectory)} >nul 2>&1");
                lines.Add($"del /f /q {Quote(AddedFilesListPath(plan.BackupDirectory))} >nul 2>&1");
            }
        }
        if (plan.Mode == AppUpdateMode.ReplaceExe && !string.IsNullOrEmpty(plan.NewExe))
            lines.Add($"del /f /q {Quote(plan.NewExe)} >nul 2>&1");
        if (!string.IsNullOrEmpty(plan.DownloadedFile)) lines.Add($"del /f /q {Quote(plan.DownloadedFile)} >nul 2>&1");
        return string.Join(" & ", lines);
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
    /// </summary>
    public static SelfUpdateOutcome? TakeLastResult(string? resultFile = null)
    {
        var file = resultFile ?? DefaultResultFile;
        try
        {
            if (!File.Exists(file)) return null;
            var parts = File.ReadAllText(file).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            File.Delete(file);
            if (parts.Length == 0) return null;
            return new SelfUpdateOutcome(parts[0], parts.Length > 1 ? parts[1].Trim() : "");
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
