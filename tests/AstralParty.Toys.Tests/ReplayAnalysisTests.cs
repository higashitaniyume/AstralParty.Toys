using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 解析管线：合成回放 → 游戏自己的结算解析器 → <see cref="ReplayAnalyzer"/> 报告。
/// 覆盖帧拆分、锚点、回合/轮次统计、玩家摘要、时间线事件与筹码流转。
/// </summary>
public sealed class ReplayAnalysisTests
{
    private const string ReplayId = "1234567890123456";

    [Fact]
    public void SyntheticReplay_IsAcceptedByGameSettlementParser()
    {
        var bytes = ReplayFixture.Create();
        var protocol = new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol"));

        Assert.True(protocol.TryParseSettlement(bytes, out var finish, out var snapshot),
            "游戏自己的结算解析器读不出合成回放，合成夹具或协议已经变了");
        Assert.NotNull(finish);
        Assert.NotNull(snapshot);
    }

    [Fact]
    public void Analyze_MapsEverySummaryField()
    {
        using var sandbox = new TestSandbox("ap-analysis-summary");
        var (analyzer, options, report) = ReportFactory.Create(sandbox, new ReplayFixtureOptions
        {
            ReplayId = ReplayId,
            RoundCount = 3
        });

        // 帧结构与协议分布
        Assert.Equal(1 + options.RoundCount * 3 + 1, report.FrameCount);
        Assert.Equal(1003, report.Frames[0].CmdId);
        Assert.Equal(1016, report.Frames[^1].CmdId);
        Assert.Equal(5, report.CommandTypeCount);
        Assert.Equal(64, report.Sha256.Length);
        Assert.Equal(ReplayId, report.FileName);
        Assert.NotEqual("—", report.FileSizeText);

        // 对局信息
        Assert.Equal(options.RoundCount, report.RoundCount);
        Assert.Equal(options.RoundCount - 1, report.TurnCount);
        Assert.Equal(options.MapId, report.MapId);
        Assert.Equal(analyzer.Config.Map(options.MapId), report.MapName);
        Assert.NotEqual("未知地图", report.MapName);
        Assert.Equal(options.MapType, report.MapType);
        Assert.Equal(options.Difficulty, report.Difficulty);
        Assert.Equal(options.PlayerDeaths, report.PlayerDeaths);
        Assert.Equal(options.GameProgress, report.GameProgress);
        Assert.Equal(options.GameMaxProgress, report.GameMaxProgress);
        Assert.Equal(options.RoomId, report.RoomId);
        Assert.Equal(options.RoomServerId, report.RoomServerId);
        Assert.NotEqual("—", report.StartTimeText);
        Assert.NotEqual("—", report.FinishTimeText);
        Assert.Equal("01:00:00", report.DurationText);

        // 结算信息
        Assert.Equal(ReplayId, report.ReplayId);
        Assert.Equal(options.GameVersion, report.GameVersion);
        Assert.Equal("胜利", report.ResultText);
        Assert.Contains(options.WinnerId.ToString(), report.WinnerText);
        Assert.Contains("×5", report.AwardsText);
        Assert.Contains(analyzer.Config.Item(options.ItemId), report.AwardsText);
        Assert.Equal(analyzer.Config.Monster(options.MonsterId), report.BossName);
        Assert.NotEqual("—", report.BossName);

        // 玩家摘要
        Assert.Equal(options.PlayerIds.Length, report.Players.Count);
        for (var index = 0; index < options.PlayerIds.Length; index++)
        {
            var player = report.Players[index];
            Assert.Equal(options.PlayerIds[index], player.Id);
            Assert.Equal(options.Nicks[index], player.Nickname);
            Assert.Equal(index, player.Slot);
            Assert.Equal(30 + index, player.AccountLevel);
            Assert.Equal(options.HeroIds[index], player.HeroId);
            Assert.Equal(analyzer.Config.Character(options.HeroIds[index]), player.HeroName);
        }
        Assert.Equal("存活至结算", report.Players[0].FinalStatus);
        Assert.Equal("结算时生命值为 0", report.Players[1].FinalStatus);
        Assert.Equal(20, report.Players[0].MaxHp);
        Assert.Equal(103, report.Players[0].Gold);          // 100 + 0*10 + 末回合 3
        Assert.Equal(5, report.Players[0].Attack);
        Assert.Equal(3, report.Players[0].Defense);
        Assert.Equal(2, report.Players[0].BoughtRelics);    // 0 + 3 - 1
    }

    [Fact]
    public void Analyze_BuildsTimelineEventsAndRelicRecords()
    {
        using var sandbox = new TestSandbox("ap-analysis-timeline");
        var (analyzer, options, report) = ReportFactory.Create(sandbox, new ReplayFixtureOptions
        {
            ReplayId = ReplayId,
            RoundCount = 3
        });

        // 每个回合一个行动快照，最后一个快照不产生「回合开始」事件
        var turns = report.Events.Where(item => item.Type == "回合").ToList();
        Assert.Equal(options.RoundCount - 1, turns.Count);
        Assert.All(turns, item => Assert.Equal("行动回合开始", item.Title));

        // 中间那一回合走的是「请求刷新」分支，不记入筹码选择
        var refresh = Assert.Single(report.Events, item => item.Title == "请求刷新筹码");
        Assert.Equal(2, refresh.Round);
        Assert.Equal("筹码", refresh.Type);

        var kills = report.Events.Where(item => item.Type == "击败").ToList();
        Assert.Equal(options.RoundCount, kills.Count);
        Assert.All(kills, item => Assert.Equal($"击败 {analyzer.Config.Monster(options.MonsterId)}", item.Title));
        Assert.Equal(options.Nicks[0], kills[0].PlayerName);
        Assert.Equal(options.Nicks[1], kills[1].PlayerName);
        Assert.Equal(options.Nicks[2], kills[2].PlayerName);

        var finish = Assert.Single(report.Events, item => item.Type == "结算");
        Assert.Equal("对局结束 · 胜利", finish.Title);
        Assert.Contains(report.WinnerText, finish.Description);

        // 第 1、3 回合各记录一次「选择」，筹码名与品质来自配表
        Assert.Equal(2, report.Relics.Count);
        Assert.All(report.Relics, record => Assert.Equal("选择", record.Kind));
        Assert.Equal(options.RelicIds[0], report.Relics[0].RelicId);
        Assert.Equal(analyzer.Config.Relic(options.RelicIds[0]), report.Relics[0].RelicName);
        Assert.Equal(analyzer.Config.RelicQuality(options.RelicIds[0]), report.Relics[0].Quality);
        Assert.Equal(1, report.Relics[0].Round);
        Assert.Equal(options.RelicIds[2], report.Relics[1].RelicId);
        Assert.Equal(3, report.Relics[1].Round);

        // 选择过的筹码会回填到玩家摘要
        var first = report.Players.Single(player => player.Id == options.PlayerIds[0]);
        Assert.Equal(1, first.SelectedRelicCount);
        Assert.Contains(analyzer.Config.Relic(options.RelicIds[0]), first.SelectedRelicsText);
        var third = report.Players.Single(player => player.Id == options.PlayerIds[2]);
        Assert.Equal(1, third.SelectedRelicCount);

        // 统计条
        Assert.NotEmpty(report.CommandStats);
        Assert.NotEmpty(report.RelicQualityStats);
    }

    [Fact]
    public void Analyze_RejectsCorruptedFiles()
    {
        using var sandbox = new TestSandbox("ap-analysis-corrupt");
        var analyzer = TestPaths.CreateAnalyzer();

        var empty = sandbox.WriteFile(@"empty\empty", []);
        Assert.Throws<InvalidDataException>(() => analyzer.Analyze(empty));

        // 帧头声称 3 字节载荷，实际只剩 1 字节
        var truncated = ReplayFixture.FrameStream([(1016, [1, 2, 3])])[..^2];
        var truncatedPath = sandbox.WriteFile(@"truncated\truncated", truncated);
        Assert.Throws<InvalidDataException>(() => analyzer.Analyze(truncatedPath));

        var random = sandbox.WriteFile(@"random\random", ReplayFixture.CreateUnparseable());
        Assert.Throws<InvalidDataException>(() => analyzer.Analyze(random));

        Assert.Throws<FileNotFoundException>(() => analyzer.Analyze(Path.Combine(sandbox.Root, "missing", "missing")));
    }

    [RealReplayFact]
    public void Analyze_RealReplay_ProducesConsistentReport()
    {
        var path = RealReplay.FindFile()!;
        var analyzer = TestPaths.CreateAnalyzer();

        var report = analyzer.Analyze(path);

        Assert.True(report.FrameCount > 0);
        Assert.Equal(1003, report.Frames[0].CmdId);
        Assert.Equal(1016, report.Frames[^1].CmdId);
        Assert.True(report.RoundCount > 0, $"{path} 解析不出回合数");
        Assert.True(report.TurnCount > 0, $"{path} 解析不出行动轮次");
        Assert.True(report.MapId > 0);
        Assert.NotEqual("未知地图", report.MapName);
        Assert.NotEqual("—", report.GameVersion);
        Assert.NotEmpty(report.Players);
        Assert.Equal(64, report.Sha256.Length);
        Assert.All(report.Relics, record => Assert.True(record.RelicId > 0, "筹码记录缺少 ID"));
    }

    [RealReplayFact]
    public void Analyze_RealReplay_IsAcceptedByGameSettlementParser()
    {
        var bytes = File.ReadAllBytes(RealReplay.FindFile()!);
        var protocol = new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol"));

        Assert.True(protocol.TryParseSettlement(bytes, out var finish, out _), "游戏自己的结算解析器读不出这局回放");
        Assert.NotNull(finish);
    }
}
