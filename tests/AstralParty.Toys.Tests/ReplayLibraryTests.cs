using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 回放库：扫描 / 归档 / 放回 / 自动整理 / 删除。
/// 全程只在临时沙箱里读写；回放数据由 <see cref="ReplayFixture"/> 合成，所以不依赖真实录像。
/// </summary>
public sealed class ReplayLibraryTests
{
    [Fact]
    public void Snapshot_MarksSyntheticReplaysAsHealthy()
    {
        using var harness = new LibraryHarness("ap-library-scan");

        var snapshot = harness.Service.Snapshot();

        Assert.Equal(3, snapshot.GameCount);
        Assert.Equal(0, snapshot.LibraryCount);
        Assert.False(snapshot.SameFolder);
        foreach (var entry in snapshot.Entries)
        {
            Assert.True(entry.Meta is { Healthy: true },
                $"{entry.ReplayId} 没能被游戏自己的结算解析器读出来：{entry.Meta?.Error}");
            Assert.False(string.IsNullOrEmpty(entry.Meta!.MapName));
            Assert.NotEqual("未知地图", entry.Meta.MapName);
            Assert.NotEmpty(entry.Meta.Players);
            Assert.True(entry.Meta.FrameCount > 0, $"{entry.ReplayId} 帧数为 0");
            Assert.True(entry.Meta.FinishTime > 0, $"{entry.ReplayId} 结算时间为 0");
            Assert.Equal(64, entry.Meta.Sha256.Length);
        }
        Assert.Equal(3, snapshot.Entries.Select(entry => entry.ReplayId).Distinct().Count());
    }

    [Fact]
    public void Snapshot_OrdersNewestFirst()
    {
        using var harness = new LibraryHarness("ap-library-order");

        var ids = harness.Service.Snapshot().Entries.Select(entry => entry.ReplayId).ToList();

        Assert.Equal([LibraryHarness.NewestId, LibraryHarness.MiddleId, LibraryHarness.OldestId], ids);
    }

    [Fact]
    public void Archive_MovesReplayIntoLibrary()
    {
        using var harness = new LibraryHarness("ap-library-archive");

        var result = harness.Service.Archive([LibraryHarness.OldestId]);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(1, result.Applied);
        Assert.False(Directory.Exists(Path.Combine(harness.GameDirectory, LibraryHarness.OldestId)), "归档后游戏目录里还留着目录");
        Assert.True(File.Exists(Path.Combine(harness.LibraryDirectory, LibraryHarness.OldestId, LibraryHarness.OldestId)), "归档后库里没有文件");
        Assert.True(File.Exists(Path.Combine(harness.Service.IndexDirectory, LibraryHarness.OldestId + ".json")), "归档后没有写元数据 sidecar");
        Assert.True(File.Exists(harness.Service.OperationsLogPath), "归档后没有写操作日志");

        var snapshot = harness.Service.Snapshot();
        Assert.Equal(2, snapshot.GameCount);
        Assert.Equal(1, snapshot.LibraryCount);
        var entry = Assert.Single(snapshot.Entries, item => item.ReplayId == LibraryHarness.OldestId);
        Assert.True(entry.InLibrary && !entry.InGame, "归档后条目来源标记错误");
        Assert.True(entry.Meta is { Healthy: true }, "sidecar 复读后元数据不可用");
    }

    [Fact]
    public void Restore_CopiesBack_AndKeepsLibraryCopy()
    {
        using var harness = new LibraryHarness("ap-library-restore");
        harness.Service.Archive([LibraryHarness.OldestId]);

        var result = harness.Service.Restore([LibraryHarness.OldestId]);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(1, result.Applied);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, LibraryHarness.OldestId, LibraryHarness.OldestId)), "放回后游戏目录里没有文件");

        var snapshot = harness.Service.Snapshot();
        Assert.Equal(3, snapshot.GameCount);
        Assert.Equal(1, snapshot.LibraryCount);
        Assert.True(snapshot.Entries.Single(item => item.ReplayId == LibraryHarness.OldestId).InGame, "放回后条目没有标记为游戏内");

        // 重复放回应当被跳过而不是覆盖
        var again = harness.Service.Restore([LibraryHarness.OldestId]);
        Assert.Equal(0, again.Applied);
        Assert.Equal(1, again.Skipped);
    }

    [Fact]
    public void Maintain_KeepsNewestTwo_AndArchivesTheRest()
    {
        using var harness = new LibraryHarness("ap-library-maintain");
        harness.Service.Archive([LibraryHarness.OldestId]);
        harness.Service.Restore([LibraryHarness.OldestId]);
        harness.Service.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = harness.LibraryDirectory,
            AutoMaintain = true,
            KeepInGame = 2
        });

        var result = harness.Service.Maintain();

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        var snapshot = harness.Service.Snapshot();
        Assert.Equal(2, snapshot.GameCount);
        var expectedKeep = new[] { LibraryHarness.NewestId, LibraryHarness.MiddleId }
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var actualKeep = snapshot.Entries.Where(item => item.InGame).Select(item => item.ReplayId)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedKeep, actualKeep);
        Assert.True(snapshot.Entries.Single(item => item.ReplayId == LibraryHarness.OldestId).InLibrary, "被整理掉的那一局没有进库");
    }

    [Fact]
    public void UnparseableReplay_IsArchivedButGuardedOnRestore()
    {
        using var harness = new LibraryHarness("ap-library-broken");
        var brokenId = harness.SeedUnparseableReplay();

        var archived = harness.Service.Archive([brokenId]);
        Assert.True(archived.Ok, string.Join(" | ", archived.Messages));
        Assert.Equal(1, archived.Applied);

        var entry = harness.Service.Snapshot().Entries.Single(item => item.ReplayId == brokenId);
        Assert.True(entry.InLibrary);
        Assert.True(entry.Meta is { Healthy: false }, "损坏文件没有被标记为不可解析");

        // 放回一个解析不出结算帧的回放必须先拦一次并索要确认：游戏读到坏目录会整个删掉
        var blocked = harness.Service.Restore([brokenId]);
        Assert.False(blocked.Ok);
        Assert.Equal(0, blocked.Applied);
        Assert.True(blocked.NeedsConfirmation, "拦截时没有给出确认信息");
        Assert.Contains(brokenId, blocked.PendingIds);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, brokenId, brokenId)), "被拦截时不该写入游戏目录");

        var forced = harness.Service.Restore([brokenId], allowUnparseable: true);
        Assert.True(forced.Ok, string.Join(" | ", forced.Messages));
        Assert.Equal(1, forced.Applied);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, brokenId, brokenId)), "确认后游戏目录里没有文件");
    }

    [Fact]
    public void LibraryRootEqualToGameRoot_RejectsEverything()
    {
        using var harness = new LibraryHarness("ap-library-same-folder");
        harness.Service.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = harness.GameDirectory,
            KeepInGame = ReplayLibraryService.GameSlotCapacity
        });

        Assert.True(harness.Service.Snapshot().SameFolder, "同一个路径没有被识别出来");
        Assert.False(harness.Service.Archive([LibraryHarness.NewestId]).Ok, "同一个路径时归档应当被拒绝");
    }

    [Fact]
    public void Delete_RemovesLibraryCopyAndSidecar()
    {
        using var harness = new LibraryHarness("ap-library-delete");
        harness.Service.Archive([LibraryHarness.OldestId]);
        var before = harness.Service.Snapshot().LibraryCount;

        var result = harness.Service.Delete([LibraryHarness.OldestId], ReplayDeleteTarget.Library);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(1, result.Applied);
        Assert.Equal(before - 1, harness.Service.Snapshot().LibraryCount);
        Assert.False(File.Exists(Path.Combine(harness.Service.IndexDirectory, LibraryHarness.OldestId + ".json")), "删除后 sidecar 没有清理");
    }

    /// <summary>沙箱化的回放库环境：3 局合成回放 + 独立的库目录与配置目录。</summary>
    private sealed class LibraryHarness : IDisposable
    {
        public const string NewestId = "1800000000000001";
        public const string MiddleId = "1800000000000002";
        public const string OldestId = "1800000000000003";

        private const long BaseTime = 1_760_000_000;

        private static readonly (string ReplayId, DateTime ModifiedUtc, long StartTime, long FinishTime)[] Seeds =
        [
            (NewestId, new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc), BaseTime + 172_800, BaseTime + 176_400),
            (MiddleId, new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc), BaseTime + 86_400, BaseTime + 90_000),
            (OldestId, new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), BaseTime, BaseTime + 3_600)
        ];

        public LibraryHarness(string sandboxName)
        {
            Sandbox = new TestSandbox(sandboxName);
            GameDirectory = Sandbox.EnsureDirectory("game");
            LibraryDirectory = Sandbox.EnsureDirectory("library");
            ProfileDirectory = Sandbox.EnsureDirectory("profile");
            Service = new ReplayLibraryService(TestPaths.AppDirectory, GameDirectory, ProfileDirectory);

            foreach (var seed in Seeds) SeedReplay(seed);
            Service.SaveSettings(new ReplayLibrarySettings
            {
                LibraryRoot = LibraryDirectory,
                AutoMaintain = false,
                KeepInGame = ReplayLibraryService.GameSlotCapacity
            });
            Service.EnsureLibrary();
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public string LibraryDirectory { get; }
        public string ProfileDirectory { get; }
        public ReplayLibraryService Service { get; }

        public string SeedUnparseableReplay(string replayId = "0000000000000000000000000000dead")
        {
            WriteReplayFile(replayId, ReplayFixture.CreateUnparseable(), DateTime.UtcNow);
            return replayId;
        }

        private void SeedReplay((string ReplayId, DateTime ModifiedUtc, long StartTime, long FinishTime) seed)
        {
            var bytes = ReplayFixture.Create(new ReplayFixtureOptions
            {
                ReplayId = seed.ReplayId,
                StartTime = seed.StartTime,
                FinishTime = seed.FinishTime
            });
            WriteReplayFile(seed.ReplayId, bytes, seed.ModifiedUtc);
        }

        private void WriteReplayFile(string replayId, byte[] bytes, DateTime modifiedUtc)
        {
            var directory = Path.Combine(GameDirectory, replayId);
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, replayId);
            File.WriteAllBytes(file, bytes);
            // 排序依据（文件时间 / 结算时间）保持一致，测试才是确定的
            File.SetLastWriteTimeUtc(file, modifiedUtc);
        }

        public void Dispose() => Sandbox.Dispose();
    }
}
