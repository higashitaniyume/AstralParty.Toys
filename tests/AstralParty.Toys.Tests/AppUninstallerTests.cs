using System.Diagnostics;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public class AppUninstallerTests
{
    [Fact]
    public void UnsupportedReason_BlocksDebugFolderAndInstallerBuilds()
    {
        using var sandbox = new TestSandbox("ap-uninstall-reason");
        var single = sandbox.EnsureDirectory("single");
        var singleExe = Path.Combine(single, "AstralParty.Toys（副本）.exe");
        File.WriteAllText(singleExe, "MZ");
        var folder = sandbox.EnsureDirectory("folder");
        var folderExe = Path.Combine(folder, "AstralParty.Toys.exe");
        File.WriteAllText(folderExe, "MZ");
        File.WriteAllText(Path.Combine(folder, "AstralParty.Toys.dll"), "MZ");

        Assert.Null(AppUninstaller.UnsupportedReason(singleExe, isInstaller: false));
        Assert.NotNull(AppUninstaller.UnsupportedReason(singleExe, isInstaller: true));
        Assert.NotNull(AppUninstaller.UnsupportedReason(folderExe, isInstaller: false));
        Assert.NotNull(AppUninstaller.UnsupportedReason(Path.Combine(single, "missing.exe"), isInstaller: false));
    }

    [Fact]
    public void ExtractDirectory_OnlyAcceptsDotnetTempFolder()
    {
        var temp = @"C:\Users\x\AppData\Local\Temp";
        var exe = @"C:\Users\x\Desktop\tool.exe";

        Assert.Equal(temp + @"\.net\tool\abc", AppUninstaller.ExtractDirectory(exe, temp + @"\.net\tool\abc\", temp));
        Assert.Null(AppUninstaller.ExtractDirectory(exe, @"C:\Users\x\Desktop\", temp));
        Assert.Null(AppUninstaller.ExtractDirectory(exe, @"C:\Users\x\Documents\", temp));
        Assert.Null(AppUninstaller.ExtractDirectory(exe, temp + @"\.network\abc", temp));
    }

    [Theory]
    [InlineData("relative\tool.exe")]
    [InlineData("C:\\")]
    [InlineData("")]
    public void BuildScript_RejectsUnsafePaths(string path)
    {
        Assert.Throws<ArgumentException>(() => AppUninstaller.BuildScript(new AppUninstallPlan { ProcessId = 1, ExePath = path }));
    }

    [Fact]
    public void Script_DeletesOnlyListedItems_AndKeepsReplays_WithSpecialCharacterPaths()
    {
        using var sandbox = new TestSandbox("ap-uninstall-run");
        var root = sandbox.EnsureDirectory("工具 （副本）&100%!");
        var exe = Path.Combine(root, "AstralParty.Toys（副本）.exe");
        File.WriteAllText(exe, "MZ");
        var extract = Directory.CreateDirectory(Path.Combine(root, "extract", "sub")).Parent!.FullName;
        var profile = Directory.CreateDirectory(Path.Combine(root, "AstralPartyReplays")).FullName;
        File.WriteAllText(Path.Combine(profile, "game-profiles.json"), "{}");
        var history = Directory.CreateDirectory(Path.Combine(profile, "modloader-history", "games")).Parent!.FullName;
        var replay = Directory.CreateDirectory(Path.Combine(profile, "12345")).FullName;
        File.WriteAllText(Path.Combine(replay, "12345"), "replay");
        var emptyProfile = Directory.CreateDirectory(Path.Combine(root, "empty-after")).FullName;
        var unrelated = Path.Combine(root, "unrelated.txt");
        File.WriteAllText(unrelated, "keep");

        var items = AppUninstaller.SettingsItems(profile, keepLoaderHistory: false);
        RunToEnd(new AppUninstallPlan
        {
            ProcessId = FinishedProcessId(),
            ExePath = exe,
            Directories = items.Directories.Append(extract).ToArray(),
            Files = items.Files,
            EmptyOnlyDirectories = new[] { profile, emptyProfile }
        }, sandbox.Root);

        Assert.False(File.Exists(exe));
        Assert.False(Directory.Exists(extract));
        Assert.False(File.Exists(Path.Combine(profile, "game-profiles.json")));
        Assert.False(Directory.Exists(history));
        Assert.Contains(Path.Combine(profile, "backgrounds"), items.Directories);
        Assert.False(Directory.Exists(emptyProfile));
        // 文件夹里还有回放：回放和文件夹都必须还在
        Assert.Equal("replay", File.ReadAllText(Path.Combine(replay, "12345")));
        Assert.True(File.Exists(unrelated));
        Assert.Empty(Directory.GetFiles(sandbox.Root, "*.cmd"));   // 脚本删掉了自己
    }

    [Fact]
    public void SettingsItems_KeepsLoaderHistoryWhileALoaderRemains()
    {
        var keep = AppUninstaller.SettingsItems(@"C:\p", keepLoaderHistory: true);
        Assert.DoesNotContain(keep.Directories, d => d.EndsWith("modloader-history"));
        var remove = AppUninstaller.SettingsItems(@"C:\p", keepLoaderHistory: false);
        Assert.Contains(remove.Directories, d => d.EndsWith("modloader-history"));
    }

    [Fact]
    public void Script_WaitsForProcessToExitBeforeDeleting()
    {
        using var sandbox = new TestSandbox("ap-uninstall-wait");
        var exe = Path.Combine(sandbox.Root, "tool.exe");
        File.WriteAllText(exe, "MZ");
        using var running = Process.Start(new ProcessStartInfo("ping.exe", "-n 5 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;

        using var script = AppUninstaller.Launch(new AppUninstallPlan { ProcessId = running.Id, ExePath = exe }, sandbox.Root);
        Assert.False(script.WaitForExit(1500));
        Assert.True(File.Exists(exe));

        running.WaitForExit();
        Assert.True(script.WaitForExit(30000));
        Assert.False(File.Exists(exe));
    }

    private static int FinishedProcessId()
    {
        using var finished = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false, CreateNoWindow = true })!;
        finished.WaitForExit();
        return finished.Id;
    }

    private static void RunToEnd(AppUninstallPlan plan, string temp)
    {
        using var script = AppUninstaller.Launch(plan, temp);
        Assert.True(script.WaitForExit(30000));
    }
}
