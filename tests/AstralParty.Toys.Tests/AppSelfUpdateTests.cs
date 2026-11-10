using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>单文件版 / 便携版的更新：形态判断、选包、退出后替换的脚本。</summary>
public sealed class AppSelfUpdateTests
{
    // ============================== 发布形态 ==============================

    [Fact]
    public void DetectForm_TellsInstallerSingleFilePortableAndDevelopmentApart()
    {
        using var sandbox = new TestSandbox("update-form");
        var portable = sandbox.EnsureDirectory("便携 版");
        File.WriteAllText(Path.Combine(portable, "AstralParty.Toys.dll"), "dll");
        var desktop = sandbox.EnsureDirectory("Desktop");
        var extract = sandbox.EnsureDirectory(".net", "AstralParty.Toys", "abc");
        var debug = sandbox.EnsureDirectory("src", "bin", "Debug", "net8.0-windows");

        Assert.Equal(AppPackageForm.Installer, AppRuntimeDetector.DetectForm(portable, Path.Combine(portable, "AstralParty.Toys.exe"), isInstaller: true, isDebugBuild: false));
        // 单文件版：程序目录是解压目录，exe 在别处
        Assert.Equal(AppPackageForm.SingleFile, AppRuntimeDetector.DetectForm(extract, Path.Combine(desktop, "AstralParty.Toys-.exe"), false, false));
        // 不带自解压的单文件版：同一个目录，但旁边没有主程序 dll
        Assert.Equal(AppPackageForm.SingleFile, AppRuntimeDetector.DetectForm(desktop, Path.Combine(desktop, "AstralParty.Toys.exe"), false, false));
        Assert.Equal(AppPackageForm.Portable, AppRuntimeDetector.DetectForm(portable, Path.Combine(portable, "AstralParty.Toys.exe"), false, false));
        Assert.Equal(AppPackageForm.Development, AppRuntimeDetector.DetectForm(debug, Path.Combine(debug, "AstralParty.Toys.exe"), false, false));
        Assert.Equal(AppPackageForm.Development, AppRuntimeDetector.DetectForm(portable, Path.Combine(portable, "AstralParty.Toys.exe"), false, isDebugBuild: true));
    }

    // ============================== 选包 ==============================

    private static AppVersionManifest FullManifest() => new()
    {
        Version = "2.3.0",
        ReleaseTag = "v2.3.0",
        Packages = new AppPackageAssets
        {
            FrameworkDependentInstaller = Pkg("fd-installer.exe"),
            SelfContainedInstaller = Pkg("sc-installer.exe"),
            FrameworkDependentSingleFile = Pkg("fd-single-file.exe"),
            SelfContainedSingleFile = Pkg("sc-single-file.exe"),
            FrameworkDependentPortable = Pkg("fd-portable.zip"),
            SelfContainedPortable = Pkg("sc-portable.zip")
        }
    };

    private static AppAssetPackageInfo Pkg(string name) => new() { FileName = name, Url = "https://github.com/x/y/releases/download/v2.3.0/" + name };

    private static AppEnvironmentInfo Env(AppPackageForm form, AppRuntimeKind runtime = AppRuntimeKind.SelfContained) => new()
    {
        PackageForm = form,
        RuntimeKind = runtime,
        IsInstaller = form == AppPackageForm.Installer,
        ExePath = @"C:\Tools\AstralParty.Toys.exe"
    };

    [Theory]
    [InlineData(AppPackageForm.Installer, AppRuntimeKind.SelfContained, AppUpdateMode.Installer, "sc-installer.exe")]
    [InlineData(AppPackageForm.Installer, AppRuntimeKind.FrameworkDependent, AppUpdateMode.Installer, "fd-installer.exe")]
    [InlineData(AppPackageForm.SingleFile, AppRuntimeKind.SelfContained, AppUpdateMode.ReplaceExe, "sc-single-file.exe")]
    [InlineData(AppPackageForm.SingleFile, AppRuntimeKind.FrameworkDependent, AppUpdateMode.ReplaceExe, "fd-single-file.exe")]
    [InlineData(AppPackageForm.Portable, AppRuntimeKind.SelfContained, AppUpdateMode.ReplaceFolder, "sc-portable.zip")]
    [InlineData(AppPackageForm.Portable, AppRuntimeKind.FrameworkDependent, AppUpdateMode.ReplaceFolder, "fd-portable.zip")]
    public void PlanUpdate_PicksThePackageMatchingTheCurrentForm(AppPackageForm form, AppRuntimeKind runtime, AppUpdateMode mode, string file)
    {
        var plan = AppUpdateService.PlanUpdate(FullManifest(), Env(form, runtime), switchToInstaller: false, canWrite: _ => true);
        Assert.Equal(mode, plan.Mode);
        Assert.Equal(file, plan.Package?.FileName);
    }

    [Fact]
    public void PlanUpdate_SwitchToInstaller_UsesTheInstallerOfTheSameRuntime()
    {
        var plan = AppUpdateService.PlanUpdate(FullManifest(), Env(AppPackageForm.SingleFile, AppRuntimeKind.FrameworkDependent), switchToInstaller: true, canWrite: _ => true);
        Assert.Equal(AppUpdateMode.Installer, plan.Mode);
        Assert.Equal("fd-installer.exe", plan.Package?.FileName);
    }

    [Fact]
    public void PlanUpdate_FallsBackToManual_WithAReason()
    {
        // 旧的 version.json 只有安装版：单文件版不能原地替换，也不能悄悄改成安装版
        const string oldManifest = """
        { "version": "2.1.0", "releaseTag": "v2.1.0",
          "packages": { "selfContainedInstaller": { "fileName": "sc-installer.exe", "url": "https://github.com/x/y/releases/download/v2.1.0/sc-installer.exe" } } }
        """;
        var manifest = JsonSerializer.Deserialize<AppVersionManifest>(oldManifest, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var missing = AppUpdateService.PlanUpdate(manifest, Env(AppPackageForm.SingleFile), false, _ => true);
        Assert.Equal(AppUpdateMode.Manual, missing.Mode);
        Assert.Null(missing.Package);
        Assert.Contains("单文件版", missing.ManualReason);

        var readOnly = AppUpdateService.PlanUpdate(FullManifest(), Env(AppPackageForm.Portable), false, _ => false);
        Assert.Equal(AppUpdateMode.Manual, readOnly.Mode);
        Assert.Contains("写入权限", readOnly.ManualReason);

        var dev = AppUpdateService.PlanUpdate(FullManifest(), Env(AppPackageForm.Development), true, _ => true);
        Assert.Equal(AppUpdateMode.Manual, dev.Mode);
    }

    [Fact]
    public void InstallerArguments_AddDesktopShortcutOnlyWhenSwitching()
    {
        Assert.Equal("/SILENT /SP- /CLOSEAPPLICATIONS", AppUpdateService.InstallerArguments(silent: true, addDesktopShortcut: false));
        Assert.Equal("/SILENT /SP- /CLOSEAPPLICATIONS /MERGETASKS=\"desktopicon\"", AppUpdateService.InstallerArguments(true, true));
    }

    [Fact]
    public void ReleasePageUrl_PointsAtTheTag()
    {
        Assert.Equal("https://github.com/higashitaniyume/AstralParty.Toys/releases/tag/v2.3.0", AppUpdateService.ReleasePageUrl(FullManifest()));
        Assert.Equal("https://github.com/higashitaniyume/AstralParty.Toys/releases/latest", AppUpdateService.ReleasePageUrl(null));
    }

    // ============================== 便携版包 ==============================

    [Fact]
    public void ExtractPortablePackage_RequiresTheMainExeAndDll()
    {
        using var sandbox = new TestSandbox("update-portable-zip");
        var good = MakeZip(sandbox, "good.zip", ("AstralParty.Toys.exe", "exe"), ("AstralParty.Toys.dll", "dll"), ("zh/res.dll", "res"));
        var staging = AppUpdateService.ExtractPortablePackage(good, sandbox.Combine("staging-good"));
        Assert.True(File.Exists(Path.Combine(staging, "zh", "res.dll")));

        var bad = MakeZip(sandbox, "bad.zip", ("readme.txt", "not a portable build"));
        Assert.Throws<InvalidOperationException>(() => AppUpdateService.ExtractPortablePackage(bad, sandbox.Combine("staging-bad")));
        Assert.False(Directory.Exists(sandbox.Combine("staging-bad")));
    }

    [Fact]
    public void BackupOverwrittenFiles_CopiesOnlyFilesTheUpdateReplaces()
    {
        using var sandbox = new TestSandbox("update-backup");
        var app = sandbox.EnsureDirectory("Desktop");
        File.WriteAllText(Path.Combine(app, "AstralParty.Toys.dll"), "old dll");
        File.WriteAllText(Path.Combine(app, "我的笔记.txt"), "user file");
        var staging = sandbox.EnsureDirectory("staging");
        File.WriteAllText(Path.Combine(staging, "AstralParty.Toys.dll"), "new dll");
        File.WriteAllText(Path.Combine(staging, "new-only.dll"), "new");
        var backup = sandbox.Combine("backup");

        AppSelfUpdater.BackupOverwrittenFiles(staging, app, backup);

        Assert.Equal("old dll", File.ReadAllText(Path.Combine(backup, "AstralParty.Toys.dll")));
        Assert.False(File.Exists(Path.Combine(backup, "我的笔记.txt")));
        Assert.False(File.Exists(Path.Combine(backup, "new-only.dll")));

        // 新版本才有的文件记在清单里，回滚时按清单删掉
        var list = File.ReadAllLines(AppSelfUpdater.AddedFilesListPath(backup));
        Assert.Equal(new[] { Path.Combine(app, "new-only.dll") }, list);
    }

    // ============================== 脚本 ==============================

    [Theory]
    [InlineData("relative\\AstralParty.Toys.exe")]
    [InlineData("C:\\a\"b\\AstralParty.Toys.exe")]
    [InlineData("C:\\")]
    public void BuildScript_RejectsUnsafePaths(string path)
    {
        var plan = new AppReplacePlan { Mode = AppUpdateMode.ReplaceExe, CurrentExe = path, NewExe = @"C:\new.exe", ResultFile = @"C:\r.txt" };
        Assert.Throws<ArgumentException>(() => AppSelfUpdater.BuildScript(plan));
    }

    [Fact]
    public void BuildScript_RejectsUnsafeVersionAndUnknownMode()
    {
        var plan = new AppReplacePlan { Mode = AppUpdateMode.ReplaceExe, CurrentExe = @"C:\a\x.exe", NewExe = @"C:\a\n.exe", ResultFile = @"C:\a\r.txt", NewVersion = "2.3.0 & calc" };
        Assert.Throws<ArgumentException>(() => AppSelfUpdater.BuildScript(plan));
        var installer = new AppReplacePlan { Mode = AppUpdateMode.Installer, CurrentExe = @"C:\a\x.exe", ResultFile = @"C:\a\r.txt" };
        Assert.Throws<ArgumentException>(() => AppSelfUpdater.BuildScript(installer));
    }

    [Fact]
    public void Script_ReplacesSingleFileExe_AfterTheProcessExits()
    {
        using var sandbox = new TestSandbox("update-replace-exe");
        var desktop = sandbox.EnsureDirectory("桌面 50% & more");
        var exe = Path.Combine(desktop, "AstralParty.Toys-.exe");
        File.WriteAllText(exe, "old");
        var downloaded = sandbox.WriteFile("AstralPartyToysUpdate/new.exe", "new");
        var result = sandbox.Combine("result.txt");

        using var running = Process.Start(new ProcessStartInfo("ping.exe", "-n 4 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        using var script = AppSelfUpdater.Launch(new AppReplacePlan
        {
            ProcessId = running.Id, Mode = AppUpdateMode.ReplaceExe, CurrentExe = exe, NewExe = downloaded,
            ResultFile = result, NewVersion = "2.3.0", Restart = false
        }, sandbox.Root);

        Assert.False(script.WaitForExit(1500));   // 还在等进程退出
        Assert.Equal("old", File.ReadAllText(exe));
        running.WaitForExit();
        Assert.True(script.WaitForExit(30000));

        Assert.Equal("new", File.ReadAllText(exe));
        Assert.False(File.Exists(exe + ".update-old"));
        Assert.False(File.Exists(downloaded));
        Assert.Equal(new SelfUpdateOutcome("ok", "2.3.0"), AppSelfUpdater.TakeLastResult(result));
        Assert.False(File.Exists(result));   // 读一次就删
        Assert.Empty(Directory.GetFiles(sandbox.Root, "astralparty-toys-update-*.cmd"));   // 脚本删掉了自己
    }

    [Fact]
    public void Script_RestoresTheOldExe_WhenTheNewOneCannotBeCopied()
    {
        using var sandbox = new TestSandbox("update-replace-exe-fail");
        var exe = sandbox.WriteFile("Desktop/AstralParty.Toys-.exe", "old");
        var result = sandbox.Combine("result.txt");

        using var script = AppSelfUpdater.Launch(new AppReplacePlan
        {
            ProcessId = ExitedProcessId(), Mode = AppUpdateMode.ReplaceExe, CurrentExe = exe,
            NewExe = sandbox.Combine("missing.exe"), ResultFile = result, NewVersion = "2.3.0", Restart = false
        }, sandbox.Root);
        Assert.True(script.WaitForExit(30000));

        Assert.Equal("old", File.ReadAllText(exe));
        Assert.False(File.Exists(exe + ".update-old"));
        Assert.Equal(new SelfUpdateOutcome("restored", "2.3.0"), AppSelfUpdater.TakeLastResult(result));
    }

    [Fact]
    public void Script_OverlaysPortableFolder_AndKeepsUserFiles()
    {
        using var sandbox = new TestSandbox("update-replace-folder");
        var app = sandbox.EnsureDirectory("便携 50% & 版");
        File.WriteAllText(Path.Combine(app, "AstralParty.Toys.exe"), "old exe");
        File.WriteAllText(Path.Combine(app, "AstralParty.Toys.dll"), "old dll");
        File.WriteAllText(Path.Combine(app, "我的笔记.txt"), "user file");
        var staging = sandbox.EnsureDirectory("AstralPartyToysUpdate", "portable");
        File.WriteAllText(Path.Combine(staging, "AstralParty.Toys.exe"), "new exe");
        File.WriteAllText(Path.Combine(staging, "AstralParty.Toys.dll"), "new dll");
        Directory.CreateDirectory(Path.Combine(staging, "zh"));
        File.WriteAllText(Path.Combine(staging, "zh", "res.dll"), "new res");
        var zip = sandbox.WriteFile("AstralPartyToysUpdate/portable.zip", "zip");
        var backup = sandbox.Combine("AstralPartyToysUpdate", "backup");
        AppSelfUpdater.BackupOverwrittenFiles(staging, app, backup);
        var result = sandbox.Combine("result.txt");

        using var script = AppSelfUpdater.Launch(new AppReplacePlan
        {
            ProcessId = ExitedProcessId(), Mode = AppUpdateMode.ReplaceFolder, CurrentExe = Path.Combine(app, "AstralParty.Toys.exe"),
            StagingDirectory = staging, AppDirectory = app, BackupDirectory = backup, DownloadedFile = zip,
            ResultFile = result, NewVersion = "2.3.0", Restart = false
        }, sandbox.Root);
        Assert.True(script.WaitForExit(60000));

        Assert.Equal("new exe", File.ReadAllText(Path.Combine(app, "AstralParty.Toys.exe")));
        Assert.Equal("new dll", File.ReadAllText(Path.Combine(app, "AstralParty.Toys.dll")));
        Assert.Equal("new res", File.ReadAllText(Path.Combine(app, "zh", "res.dll")));
        Assert.Equal("user file", File.ReadAllText(Path.Combine(app, "我的笔记.txt")));
        Assert.False(Directory.Exists(staging));
        Assert.False(Directory.Exists(backup));
        Assert.False(File.Exists(zip));
        Assert.Equal(new SelfUpdateOutcome("ok", "2.3.0"), AppSelfUpdater.TakeLastResult(result));
    }

    /// <summary>覆盖到一半失败时要能回到更新前的样子：新引入的文件删掉，被覆盖的旧文件还原。</summary>
    [Fact]
    public void Script_RollsBackTheFolder_WhenTheOverlayFails()
    {
        using var sandbox = new TestSandbox("update-replace-folder-rollback");
        var app = sandbox.EnsureDirectory("便携 50% & 版");
        File.WriteAllText(Path.Combine(app, "AstralParty.Toys.exe"), "old exe");
        File.WriteAllText(Path.Combine(app, "AstralParty.Toys.dll"), "old dll");
        File.WriteAllText(Path.Combine(app, "我的笔记.txt"), "user file");
        var staging = sandbox.EnsureDirectory("AstralPartyToysUpdate", "portable");
        File.WriteAllText(Path.Combine(staging, "AstralParty.Toys.exe"), "new exe");
        File.WriteAllText(Path.Combine(staging, "AstralParty.Toys.dll"), "new dll");
        Directory.CreateDirectory(Path.Combine(staging, "zh"));
        File.WriteAllText(Path.Combine(staging, "zh", "res.dll"), "new res");
        // 独占锁住 staging 里的一个文件：robocopy 复制不了它，整次覆盖就算失败
        var locked = Path.Combine(staging, "locked.bin");
        File.WriteAllText(locked, "locked");
        var backup = sandbox.Combine("AstralPartyToysUpdate", "backup");
        var added = AppSelfUpdater.BackupOverwrittenFiles(staging, app, backup);
        var result = sandbox.Combine("result.txt");

        Assert.Contains(Path.Combine(app, "zh", "res.dll"), added);
        Assert.Contains(Path.Combine(app, "locked.bin"), added);
        Assert.DoesNotContain(Path.Combine(app, "AstralParty.Toys.dll"), added);

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        using (var script = AppSelfUpdater.Launch(new AppReplacePlan
        {
            ProcessId = ExitedProcessId(), Mode = AppUpdateMode.ReplaceFolder, CurrentExe = Path.Combine(app, "AstralParty.Toys.exe"),
            StagingDirectory = staging, AppDirectory = app, BackupDirectory = backup,
            ResultFile = result, NewVersion = "2.3.0", Restart = false
        }, sandbox.Root))
        {
            Assert.True(script.WaitForExit(120000));
        }

        // 回到更新前：旧文件还原，新引入的文件删掉，用户自己的文件留着
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(app, "AstralParty.Toys.exe")));
        Assert.Equal("old dll", File.ReadAllText(Path.Combine(app, "AstralParty.Toys.dll")));
        Assert.False(File.Exists(Path.Combine(app, "zh", "res.dll")));
        Assert.False(File.Exists(Path.Combine(app, "locked.bin")));
        Assert.Equal("user file", File.ReadAllText(Path.Combine(app, "我的笔记.txt")));
        // 备份和清单清掉了；解压目录里被锁住的那个文件删不掉，其余都清掉了
        Assert.False(Directory.Exists(backup));
        Assert.False(File.Exists(AppSelfUpdater.AddedFilesListPath(backup)));
        Assert.False(File.Exists(Path.Combine(staging, "AstralParty.Toys.exe")));
        Assert.True(File.Exists(Path.Combine(staging, "locked.bin")));
        Assert.Equal(new SelfUpdateOutcome("restored", "2.3.0"), AppSelfUpdater.TakeLastResult(result));
    }

    /// <summary>程序一直没退出时不动任何文件，只记下「没有开始」。</summary>
    [Fact]
    public void Script_DoesNotTouchAnything_WhenTheProgramIsStillRunning()
    {
        using var sandbox = new TestSandbox("update-replace-exe-busy");
        var exe = sandbox.WriteFile("Desktop/AstralParty.Toys-.exe", "old");
        var downloaded = sandbox.WriteFile("AstralPartyToysUpdate/new.exe", "new");
        var result = sandbox.Combine("result.txt");

        using var running = Process.Start(new ProcessStartInfo("ping.exe", "-n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        using (var script = AppSelfUpdater.Launch(new AppReplacePlan
        {
            ProcessId = running.Id, Mode = AppUpdateMode.ReplaceExe, CurrentExe = exe, NewExe = downloaded,
            ResultFile = result, NewVersion = "2.3.0", Restart = false, WaitAttempts = 2
        }, sandbox.Root))
        {
            Assert.True(script.WaitForExit(60000));
        }

        Assert.Equal("old", File.ReadAllText(exe));
        Assert.False(File.Exists(exe + ".update-old"));
        var outcome = AppSelfUpdater.TakeLastResult(result);
        Assert.NotNull(outcome);
        Assert.True(outcome!.Busy);
        Assert.Equal("2.3.0", outcome.Version);
    }

    private static int ExitedProcessId()
    {
        using var finished = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false, CreateNoWindow = true })!;
        finished.WaitForExit();
        return finished.Id;
    }

    private static string MakeZip(TestSandbox sandbox, string name, params (string Path, string Content)[] entries)
    {
        var path = sandbox.Combine(name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryPath).Open());
            writer.Write(content);
        }
        return path;
    }
}
