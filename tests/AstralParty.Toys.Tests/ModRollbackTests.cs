using System.Text;
using AstralParty.Toys.Services.Modding;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>无损回滚：套用方案 / 方案定义改动之后，能把文件按原始字节与位置完全还原。</summary>
public sealed class ModRollbackTests
{
    private static void AssertSnapshotsEqual(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (path, hash) in expected)
        {
            Assert.True(actual.TryGetValue(path, out var got), $"缺少文件：{path}");
            Assert.Equal(hash, got);
        }
    }

    private static string FirstId(ModProfileHarness harness, string kind)
        => harness.Service.Journal.ListEntries().First(e => e.Kind == kind).Id;

    [Fact]
    public void Apply_ThenRollback_RestoresEveryByteAndLocation()
    {
        using var harness = new ModProfileHarness("ap-rollback-apply");
        harness.AddMod("ModA", enabled: true, config: "{\r\n  // A\r\n  \"x\": 1\r\n}\r\n");
        harness.AddMod("ModB", enabled: false, config: "{\"y\": 2}");
        harness.Service.Capture("PVE", "", includeConfigs: true, includeLoader: true);

        // 先打乱当前状态：这个状态就是"回滚要回到的样子"
        harness.Manager.ToggleMod(harness.GameDirectory, "ModA.dll", enabled: false);
        harness.Manager.ToggleMod(harness.GameDirectory, "ModB.dll", enabled: true);
        File.WriteAllText(harness.ModConfigPath("ModA", enabled: false), "{\"x\": 7}");
        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        config.SpeedhackBaseSpeed = 4.0;
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        var before = harness.Snapshot();
        var apply = harness.Service.Apply("PVE");
        Assert.True(apply.Ok, apply.Message + " || " + string.Join(" | ", apply.Failed) + " || " + string.Join(" | ", apply.Skipped));
        var applied = harness.Snapshot();
        Assert.True(
            applied.Count != before.Count ||
            applied.Any(pair => !before.TryGetValue(pair.Key, out var hash) || hash != pair.Value),
            "套用应当改变了磁盘状态");

        var entryId = FirstId(harness, "modProfileApply");
        var rollback = harness.Service.Journal.Rollback(entryId, force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        AssertSnapshotsEqual(before, harness.Snapshot());
    }

    [Fact]
    public void Rollback_DeletesConfigThatDidNotExistBefore()
    {
        using var harness = new ModProfileHarness("ap-rollback-created-config");
        harness.AddMod("ModC", enabled: true, config: "{\"c\": 1}");
        harness.Service.Capture("P", "", includeConfigs: true, includeLoader: false);

        var configPath = harness.ModConfigPath("ModC");
        File.Delete(configPath);            // 套用前这份文件不存在
        var apply = harness.Service.Apply("P");
        Assert.True(apply.Ok, apply.Message + " || " + string.Join(" | ", apply.Failed) + " || " + string.Join(" | ", apply.Skipped));
        Assert.True(File.Exists(configPath), "套用会按方案把配置写回来");

        var rollback = harness.Service.Journal.Rollback(FirstId(harness, "modProfileApply"), force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        Assert.False(File.Exists(configPath), "回滚要删掉操作前不存在的那份配置");
    }

    [Fact]
    public void Rollback_RequiresForceWhenFilesChangedAfterwards()
    {
        using var harness = new ModProfileHarness("ap-rollback-conflict");
        harness.AddMod("ModD", enabled: true, config: "{\"d\": 1}");
        var configPath = harness.ModConfigPath("ModD");
        File.WriteAllText(configPath, "{\"d\": 1}");   // 玩家配置在 configs 下, 方案会捕获这份原文
        harness.Service.Capture("P", "", includeConfigs: true, includeLoader: false);
        harness.Service.Apply("P");

        File.WriteAllText(configPath, "{\"d\": 2}");   // 操作之后又被外部改过

        var blocked = harness.Service.Journal.Rollback(FirstId(harness, "modProfileApply"), force: false);
        Assert.False(blocked.Ok);
        Assert.True(blocked.NeedsForce);
        Assert.NotEmpty(blocked.Conflicts);
        Assert.Equal("{\"d\": 2}", File.ReadAllText(configPath));   // 没确认就不动

        var forced = harness.Service.Journal.Rollback(FirstId(harness, "modProfileApply"), force: true);
        Assert.True(forced.Ok, string.Join(" / ", forced.Failed));
        Assert.Equal("{\"d\": 1}", File.ReadAllText(configPath));
    }

    [Fact]
    public void Rollback_RestoresDeletedProfileDefinition()
    {
        using var harness = new ModProfileHarness("ap-rollback-delete");
        harness.AddMod("ModA", enabled: true, config: "{\"a\": 1}");
        harness.Service.Capture("P", "备注", includeConfigs: true, includeLoader: true);
        harness.Service.Delete("P");
        Assert.Empty(harness.Service.List());

        var rollback = harness.Service.Journal.Rollback(FirstId(harness, "modProfileDelete"), force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        var profile = Assert.Single(harness.Service.List());
        Assert.Equal("P", profile.Name);
        Assert.Equal("备注", profile.Note);
        Assert.Equal("P", harness.Service.Load().Active);
    }

    [Fact]
    public void Rollback_PreservesBomInLoaderConfig()
    {
        using var harness = new ModProfileHarness("ap-rollback-bom");
        harness.AddMod("ModA", enabled: true);
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(ModProfileHarness.LoaderJson)).ToArray();
        File.WriteAllBytes(harness.LoaderConfigPath, withBom);

        harness.Service.Capture("P", "", includeConfigs: false, includeLoader: true);
        harness.Service.Apply("P");
        Assert.False(File.ReadAllBytes(harness.LoaderConfigPath).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
            "加载器设置保存会重写成无 BOM（因此才需要回滚）");

        var rollback = harness.Service.Journal.Rollback(FirstId(harness, "modProfileApply"), force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        Assert.Equal(withBom, File.ReadAllBytes(harness.LoaderConfigPath));
    }

    [Fact]
    public void Rollback_RejectsInvalidOrUnknownId()
    {
        using var harness = new ModProfileHarness("ap-rollback-invalid");

        var invalid = harness.Service.Journal.Rollback("../../evil", force: false);
        Assert.False(invalid.Ok);
        Assert.Contains("编号", invalid.Message);

        var missing = harness.Service.Journal.Rollback("20260101-000000-000-test-abcdef", force: false);
        Assert.False(missing.Ok);
        Assert.Contains("找不到", missing.Message);
    }

    [Fact]
    public void Rollback_ReportsMissingModAsFailure()
    {
        using var harness = new ModProfileHarness("ap-rollback-missing-mod");
        harness.AddMod("ModA", enabled: true, config: "{\"a\": 1}");
        harness.Service.Capture("P", "", includeConfigs: true, includeLoader: false);
        harness.Service.Apply("P");
        harness.Manager.DeleteMod(harness.GameDirectory, "ModA.dll");

        var rollback = harness.Service.Journal.Rollback(FirstId(harness, "modProfileApply"), force: true);

        Assert.False(rollback.Ok);
        Assert.Contains(rollback.Failed, line => line.Contains("ModA"));
    }

    [Fact]
    public void Rollback_IsRejectedWhenAlreadyRolledBack()
    {
        using var harness = new ModProfileHarness("ap-rollback-twice");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("P", "", includeConfigs: false, includeLoader: false);
        harness.Service.Apply("P");
        var id = FirstId(harness, "modProfileApply");

        Assert.True(harness.Service.Journal.Rollback(id, force: false).Ok);
        var second = harness.Service.Journal.Rollback(id, force: false);

        Assert.False(second.Ok);
        Assert.Contains("已经回滚", second.Message);
    }

    [Fact]
    public void Rollback_Capture_RemovesOnlyThatProfile()
    {
        using var harness = new ModProfileHarness("ap-rollback-capture-targeted");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("方案A", "", includeConfigs: false, includeLoader: false);
        harness.Service.Capture("方案B", "", includeConfigs: false, includeLoader: false);

        var captureA = harness.Service.Journal.ListEntries()
            .First(e => e.Kind == "modProfileCapture" && e.Title.Contains("方案A"));
        var rollback = harness.Service.Journal.Rollback(captureA.Id, force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        Assert.Null(harness.Service.Find("方案A"));
        Assert.NotNull(harness.Service.Find("方案B"));
        // 撤销 A 时当前方案已经是 B：不把 B 一起清掉
        Assert.Equal("方案B", harness.Service.Load().Active);
    }

    [Fact]
    public void Rollback_Update_RestoresPreviousDefinitionAndKeepsOthers()
    {
        using var harness = new ModProfileHarness("ap-rollback-update-targeted");
        harness.AddMod("ModA", enabled: true);
        harness.Service.Capture("方案A", "旧备注", includeConfigs: false, includeLoader: false);
        harness.Service.Capture("方案B", "", includeConfigs: false, includeLoader: false);

        harness.AddMod("ModNew", enabled: false);
        harness.Service.Update("方案A", includeConfigs: false, includeLoader: false);
        Assert.Contains(harness.Service.Load().Profiles.Single(p => p.Name == "方案A").Mods, m => m.Name == "ModNew");

        var update = harness.Service.Journal.ListEntries().First(e => e.Kind == "modProfileUpdate");
        var rollback = harness.Service.Journal.Rollback(update.Id, force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        var a = harness.Service.Load().Profiles.Single(p => p.Name == "方案A");
        Assert.DoesNotContain(a.Mods, m => m.Name == "ModNew");
        Assert.Equal("旧备注", a.Note);
        Assert.NotNull(harness.Service.Find("方案B"));
    }

    [Fact]
    public void Rollback_Apply_KeepsProfilesCreatedAfterwards()
    {
        using var harness = new ModProfileHarness("ap-rollback-apply-targeted");
        harness.AddMod("ModA", enabled: true, config: "{\"a\": 1}");
        harness.Service.Capture("PVE", "", includeConfigs: true, includeLoader: false);
        harness.Service.Apply("PVE");

        harness.AddMod("ModB", enabled: true);
        harness.Service.Capture("之后新建", "", includeConfigs: false, includeLoader: false);   // 当前方案变成它

        var apply = harness.Service.Journal.ListEntries().First(e => e.Kind == "modProfileApply");
        var forced = harness.Service.Journal.Rollback(apply.Id, force: true);

        Assert.True(forced.Ok, string.Join(" / ", forced.Failed));
        Assert.NotNull(harness.Service.Find("之后新建"));       // 新方案不能被回滚顺手删掉
        Assert.NotNull(harness.Service.Find("PVE"));
    }

    [Fact]
    public void Rollback_Apply_KeepsAccountProfileSwitchedAfterwards()
    {
        using var harness = new ModProfileHarness("ap-rollback-account");
        harness.AddMod("ModA", enabled: true, config: "{\"a\": 1}");
        harness.Service.Capture("PVE", "", includeConfigs: true, includeLoader: true);

        // 套用前把倍速改成 3x：回滚要回到这个值
        var before = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        before.SpeedhackBaseSpeed = 3.0;
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, before);

        Assert.True(harness.Service.Apply("PVE").Ok);

        // 套用之后切换多开档案：回滚方案不该把账号一起退回去
        Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, "P3"));

        var apply = harness.Service.Journal.ListEntries().First(e => e.Kind == "modProfileApply");
        var rollback = harness.Service.Journal.Rollback(apply.Id, force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        var after = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        Assert.Equal(3.0, after.SpeedhackBaseSpeed);   // 加载器设置回到套用前
        Assert.Equal("P3", after.Profile);             // 多开档案保持套用后的选择
    }

    [Fact]
    public void Rollback_AccountProfileSwitch_RestoresPreviousSelection()
    {
        using var harness = new ModProfileHarness("ap-rollback-account-switch");
        Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, "P2"));
        var journal = harness.Service.Journal;

        using (var op = journal.Begin("accountProfileSet", "切换多开档案「P3」", harness.GameDirectory,
                   includeMods: false, includeLoader: true, includeProfiles: false))
        {
            Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, "P3"));
            op.Commit();
        }

        var entry = journal.ListEntries().First(e => e.Kind == "accountProfileSet");
        var rollback = journal.Rollback(entry.Id, force: false);

        Assert.True(rollback.Ok, string.Join(" / ", rollback.Failed));
        Assert.Equal("P2", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    [Fact]
    public void Retention_PrunesOldEntries()
    {
        using var harness = new ModProfileHarness("ap-rollback-retention");
        harness.AddMod("ModA", enabled: true);
        var journal = harness.Service.Journal;

        for (var i = 0; i < ModRollbackJournal.MaxEntries + 5; i++)
        {
            using var op = journal.Begin("testOp", $"测试操作 {i}", harness.GameDirectory,
                includeMods: false, includeLoader: true, includeProfiles: false);
            op.Commit();
        }

        Assert.True(journal.ListEntries().Count <= ModRollbackJournal.MaxEntries);
        Assert.True(Directory.GetFiles(journal.Root, "*.json").Length <= ModRollbackJournal.MaxEntries);
    }
}

