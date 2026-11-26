using System.Runtime.InteropServices;
using AstralParty.Toys.Services.Game;
using AstralParty.Toys.Tests.Support;
using Microsoft.Win32.SafeHandles;

namespace AstralParty.Toys.Tests;

public sealed class GameDataMigrationTests
{
    private sealed class Harness : IDisposable
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out ByHandleFileInformation lpFileInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public FileTime CreationTime;
            public FileTime LastAccessTime;
            public FileTime LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        public Harness(string name)
        {
            Sandbox = new TestSandbox(name);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            Sandbox.WriteFile("game/AstralParty_CN_Data/app.info", "feimo\nAstralParty_CN");
            LocalLow = Sandbox.EnsureDirectory("low");
            CompanySource = Path.Combine(LocalLow, "feimo");
            Directory.CreateDirectory(Path.Combine(CompanySource, "AstralParty_CN", "Temp", "Replay"));
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public string LocalLow { get; }
        public string CompanySource { get; }

        public string WriteSourceFile(string relativeInsideCompany, string content)
        {
            var path = Path.Combine(CompanySource, relativeInsideCompany);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public string WriteSourceBytes(string relativeInsideCompany, byte[] content)
        {
            var path = Path.Combine(CompanySource, relativeInsideCompany);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
            return path;
        }

        public bool TryHardLink(string existingFile, string newLink)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newLink)!);
            return CreateHardLinkW(newLink, existingFile, IntPtr.Zero);
        }

        public static (uint Vol, uint High, uint Low)? GetFileId(string filePath)
        {
            using var handle = CreateFileW(filePath, 0x80, 0x07, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) return null;
            return GetFileInformationByHandle(handle, out var info)
                ? (info.VolumeSerialNumber, info.FileIndexHigh, info.FileIndexLow)
                : null;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(CompanySource) &&
                    (File.GetAttributes(CompanySource) & FileAttributes.ReparsePoint) != 0)
                {
                    GameDataMigrationService.RemoveJunctionOnly(CompanySource);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
            Sandbox.Dispose();
        }
    }

    [Fact]
    public void Inspect_UnmigratedDirectory_ReportsSizeFilesAndNotMigrated()
    {
        using var h = new Harness("ap-mig-inspect");
        h.WriteSourceFile("AstralParty_CN/SaveFile.es3", "hello");
        h.WriteSourceBytes("AstralParty_CN/Temp/Replay/r1", new byte[1024]);

        var status = GameDataMigrationService.Inspect(h.GameDirectory, h.LocalLow, () => false);

        Assert.True(status.Available);
        Assert.Equal("feimo", status.Company);
        Assert.Equal("AstralParty_CN", status.Product);
        Assert.Equal(h.CompanySource, status.SourcePath);
        Assert.True(status.SourceExists);
        Assert.False(status.IsMigrated);
        Assert.Null(status.TargetPath);
        Assert.Equal(2, status.FileCount);
        Assert.Equal(5 + 1024, status.TotalBytes);
    }

    [Fact]
    public async Task Migrate_CreatesJunction_PreservesContentAndHardLinks_AndCleansBackup()
    {
        using var h = new Harness("ap-mig-ok");
        var cacheBytes = new byte[4096];
        Random.Shared.NextBytes(cacheBytes);
        var mainCache = h.WriteSourceBytes("AstralParty_CN/download/bundle.bin", cacheBytes);
        var profileCache = Path.Combine(h.CompanySource, "AstralParty_CN_profiles", "P2", "download", "bundle.bin");
        Assert.True(h.TryHardLink(mainCache, profileCache));
        h.WriteSourceFile("AstralParty_CN/SaveFile.es3", "save-data");

        var targetPick = h.Sandbox.EnsureDirectory("target-drive");
        var result = await GameDataMigrationService.MigrateAsync(
            h.GameDirectory,
            targetPick,
            h.LocalLow,
            () => false,
            allowSameVolumeForTest: true,
            allowNonNtfsForTest: true,
            safetyMarginBytes: 0);

        Assert.True(result.Ok, result.Message);
        Assert.NotNull(result.Status);
        Assert.True(result.Status!.IsMigrated);
        Assert.True(result.Status.TargetExists);

        var expectedTarget = Path.Combine(targetPick, "feimo");
        Assert.Equal(expectedTarget, result.Status.TargetPath);
        Assert.True((File.GetAttributes(h.CompanySource) & FileAttributes.ReparsePoint) != 0);

        // 校验内容通过 C 盘原入口仍可读，且硬盘占用没有把硬链接重复算两遍
        Assert.Equal("save-data", File.ReadAllText(Path.Combine(h.CompanySource, "AstralParty_CN", "SaveFile.es3")));
        Assert.Equal(cacheBytes, File.ReadAllBytes(Path.Combine(h.CompanySource, "AstralParty_CN_profiles", "P2", "download", "bundle.bin")));
        Assert.Equal(3, result.Status.FileCount);
        Assert.Equal(cacheBytes.Length + "save-data".Length, result.Status.TotalBytes);

        // 校验迁移后两份缓存仍然保持硬链接关系
        var id1 = Harness.GetFileId(Path.Combine(expectedTarget, "AstralParty_CN", "download", "bundle.bin"));
        var id2 = Harness.GetFileId(Path.Combine(expectedTarget, "AstralParty_CN_profiles", "P2", "download", "bundle.bin"));
        Assert.NotNull(id1);
        Assert.Equal(id1, id2);

        // 校验 C 盘临时备份目录已清理
        var leftovers = Directory.GetDirectories(h.LocalLow, "feimo.__toys_migrating_bak_*");
        Assert.Empty(leftovers);
    }

    [Fact]
    public async Task Migrate_WhenGameRunning_ReturnsFailureWithoutModifyingFiles()
    {
        using var h = new Harness("ap-mig-running");
        h.WriteSourceFile("AstralParty_CN/SaveFile.es3", "keep-me");
        var targetPick = h.Sandbox.EnsureDirectory("target-drive");

        var result = await GameDataMigrationService.MigrateAsync(
            h.GameDirectory,
            targetPick,
            h.LocalLow,
            () => true,
            allowSameVolumeForTest: true,
            allowNonNtfsForTest: true,
            safetyMarginBytes: 0);

        Assert.False(result.Ok);
        Assert.False((File.GetAttributes(h.CompanySource) & FileAttributes.ReparsePoint) != 0);
        Assert.False(Directory.Exists(Path.Combine(targetPick, "feimo")));
        Assert.Equal("keep-me", File.ReadAllText(Path.Combine(h.CompanySource, "AstralParty_CN", "SaveFile.es3")));
    }

    [Fact]
    public async Task Migrate_WhenCancelled_RollsBackAndLeavesSourceUntouched()
    {
        using var h = new Harness("ap-mig-cancel");
        h.WriteSourceBytes("AstralParty_CN/large.bin", new byte[512 * 1024]);
        var targetPick = h.Sandbox.EnsureDirectory("target-drive");

        using var cts = new CancellationTokenSource();
        var progress = new Progress<GameDataMigrationProgress>(_ => cts.Cancel());
        cts.Cancel();

        var result = await GameDataMigrationService.MigrateAsync(
            h.GameDirectory,
            targetPick,
            h.LocalLow,
            () => false,
            progress,
            cts.Token,
            allowSameVolumeForTest: true,
            allowNonNtfsForTest: true,
            safetyMarginBytes: 0);

        Assert.False(result.Ok);
        Assert.False((File.GetAttributes(h.CompanySource) & FileAttributes.ReparsePoint) != 0);
        Assert.False(Directory.Exists(Path.Combine(targetPick, "feimo")));
        Assert.True(File.Exists(Path.Combine(h.CompanySource, "AstralParty_CN", "large.bin")));
    }

    [Fact]
    public async Task Migrate_AlreadyMigrated_SwitchesTargetAndUpdatesJunction()
    {
        using var h = new Harness("ap-mig-relocate");
        h.WriteSourceFile("AstralParty_CN/SaveFile.es3", "relocate-data");
        var firstPick = h.Sandbox.EnsureDirectory("drive-d");
        var secondPick = h.Sandbox.EnsureDirectory("drive-e");

        var first = await GameDataMigrationService.MigrateAsync(
            h.GameDirectory,
            firstPick,
            h.LocalLow,
            () => false,
            allowSameVolumeForTest: true,
            allowNonNtfsForTest: true,
            safetyMarginBytes: 0);
        Assert.True(first.Ok, first.Message);

        var second = await GameDataMigrationService.MigrateAsync(
            h.GameDirectory,
            secondPick,
            h.LocalLow,
            () => false,
            allowSameVolumeForTest: true,
            allowNonNtfsForTest: true,
            safetyMarginBytes: 0);
        Assert.True(second.Ok, second.Message);

        var expectedSecond = Path.Combine(secondPick, "feimo");
        Assert.True(second.Status!.IsMigrated);
        Assert.Equal(expectedSecond, second.Status.TargetPath);
        Assert.False(Directory.Exists(Path.Combine(firstPick, "feimo")));
        Assert.Equal("relocate-data", File.ReadAllText(Path.Combine(h.CompanySource, "AstralParty_CN", "SaveFile.es3")));
    }

    [Fact]
    public async Task RestoreToLocal_RestoresRegularDirectoryAndRemovesTarget()
    {
        using var h = new Harness("ap-mig-restore");
        h.WriteSourceFile("AstralParty_CN/SaveFile.es3", "restore-data");
        var targetPick = h.Sandbox.EnsureDirectory("drive-d");

        var migrated = await GameDataMigrationService.MigrateAsync(
            h.GameDirectory,
            targetPick,
            h.LocalLow,
            () => false,
            allowSameVolumeForTest: true,
            allowNonNtfsForTest: true,
            safetyMarginBytes: 0);
        Assert.True(migrated.Ok, migrated.Message);

        var restored = await GameDataMigrationService.RestoreToLocalAsync(
            h.GameDirectory,
            h.LocalLow,
            () => false,
            safetyMarginBytes: 0);

        Assert.True(restored.Ok, restored.Message);
        Assert.NotNull(restored.Status);
        Assert.False(restored.Status!.IsMigrated);
        Assert.True(restored.Status.SourceExists);
        Assert.False((File.GetAttributes(h.CompanySource) & FileAttributes.ReparsePoint) != 0);
        Assert.False(Directory.Exists(Path.Combine(targetPick, "feimo")));
        Assert.Equal("restore-data", File.ReadAllText(Path.Combine(h.CompanySource, "AstralParty_CN", "SaveFile.es3")));
    }

    [Fact]
    public void Inspect_BrokenJunction_ReportsTargetMissing()
    {
        using var h = new Harness("ap-mig-broken");
        Directory.Delete(h.CompanySource, recursive: true);
        var target = h.Sandbox.EnsureDirectory("vanished-target", "feimo");
        GameDataMigrationService.CreateJunction(h.CompanySource, target);
        Directory.Delete(target, recursive: true);

        var status = GameDataMigrationService.Inspect(h.GameDirectory, h.LocalLow, () => false);

        Assert.True(status.Available);
        Assert.True(status.IsMigrated);
        Assert.False(status.TargetExists);
    }
}
