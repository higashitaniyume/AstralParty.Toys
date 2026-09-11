using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 配置目录规则：所有配置统一放「文档\AstralPartyReplays」，首次运行从 %AppData% 迁移旧配置，
/// 文档目录不可写时才回退。用临时目录扮演「文档」与「%AppData%」，覆盖全部分支。
/// </summary>
public sealed class ConfigLocationTests
{
    private static string DocumentsDataDirectory(TestSandbox sandbox) =>
        sandbox.Combine("Documents", SpeedhackManager.DataFolderName);

    private static string LegacyAppDataDirectory(TestSandbox sandbox) =>
        sandbox.Combine("AppData", "AstralParty.Toys");

    /// <summary>文档目录里的相对路径（沙箱根 = 假「文档」的上级）。</summary>
    private static string DocumentsRelative(TestSandbox sandbox, string relativePath) =>
        Path.Combine("Documents", SpeedhackManager.DataFolderName, relativePath);

    [Fact]
    public void FreshInstall_PutsConfigInDocuments()
    {
        using var sandbox = new TestSandbox("ap-config-fresh");
        var documents = DocumentsDataDirectory(sandbox);
        var appData = LegacyAppDataDirectory(sandbox);
        Directory.CreateDirectory(appData);

        var resolved = SpeedhackManager.ResolveProfileDirectory(appData, documents);

        Assert.Equal(documents, resolved);
        Assert.True(Directory.Exists(documents), "文档数据目录没有被创建");
    }

    [Fact]
    public void UnwritableDocuments_FallsBackToAppData()
    {
        using var sandbox = new TestSandbox("ap-config-fallback");
        var appData = LegacyAppDataDirectory(sandbox);
        Directory.CreateDirectory(appData);

        // 把「文档目录」挂在一个文件下面：CreateDirectory 必然失败，用来模拟不可写
        var blocker = sandbox.WriteFile("blocker.txt", "x");
        var brokenDocuments = Path.Combine(blocker, SpeedhackManager.DataFolderName);

        var resolved = SpeedhackManager.ResolveProfileDirectory(appData, brokenDocuments);

        Assert.Equal(appData, resolved);
    }

    [Fact]
    public void LegacyAppDataConfig_IsMigrated_WithoutDeletingOriginals()
    {
        using var sandbox = new TestSandbox("ap-config-migrate");
        var documents = DocumentsDataDirectory(sandbox);
        var appData = LegacyAppDataDirectory(sandbox);

        var configPath = sandbox.WriteFile(@"AppData\AstralParty.Toys\speedhack\speedhack_config.json", """{"console":true}""");
        sandbox.WriteFile(@"AppData\AstralParty.Toys\speedhack-state.json", """{"gameDirectory":"D:\\Game"}""");
        sandbox.WriteFile(@"AppData\AstralParty.Toys\replay-library.json", """{"autoMaintain":true}""");

        var resolved = SpeedhackManager.ResolveProfileDirectory(appData, documents);

        Assert.Equal(documents, resolved);
        Assert.True(File.Exists(Path.Combine(documents, "speedhack", "speedhack_config.json")), "主配置没有迁移过来");
        Assert.True(File.Exists(Path.Combine(documents, "speedhack-state.json")), "游戏目录记忆没有迁移过来");
        Assert.True(File.Exists(Path.Combine(documents, "replay-library.json")), "回放库设置没有迁移过来");
        Assert.Contains("D:\\\\Game", File.ReadAllText(Path.Combine(documents, "speedhack-state.json")));
        Assert.True(File.Exists(configPath), "迁移不应删除 AppData 里的原文件");
    }

    [Fact]
    public void Migration_DoesNotOverwriteExistingDocumentsConfig()
    {
        using var sandbox = new TestSandbox("ap-config-migrate-keep");
        var documents = DocumentsDataDirectory(sandbox);
        var appData = LegacyAppDataDirectory(sandbox);

        sandbox.WriteFile(@"AppData\AstralParty.Toys\speedhack-state.json", """{"gameDirectory":"D:\\Game"}""");
        sandbox.WriteFile(DocumentsRelative(sandbox, "speedhack-state.json"), """{"gameDirectory":"E:\\Mine"}""");

        var resolved = SpeedhackManager.ResolveProfileDirectory(appData, documents);

        Assert.Equal(documents, resolved);
        var text = File.ReadAllText(Path.Combine(documents, "speedhack-state.json"));
        Assert.True(text.Contains("E:\\\\Mine"), $"文档目录里已有的配置被覆盖了：{text}");
    }

    [Fact]
    public void DefaultLibraryRoot_EqualsConfigDirectory()
    {
        Assert.Equal(SpeedhackManager.ResolveDocumentsDataDirectory(), ReplayLibraryService.DefaultLibraryRoot());
    }

    [Fact]
    public void LibrarySettings_LiveInConfigDirectory()
    {
        using var sandbox = new TestSandbox("ap-config-library-settings");
        var documents = DocumentsDataDirectory(sandbox);
        var service = new ReplayLibraryService(TestPaths.AppDirectory, sandbox.Combine("game"), documents);

        Assert.Equal(Path.Combine(documents, "replay-library.json"), service.SettingsPath);
    }

    [Fact]
    public void ConfigFolder_IsNotMistakenForReplayEntries()
    {
        using var sandbox = new TestSandbox("ap-config-not-replays");
        var documents = DocumentsDataDirectory(sandbox);
        // 配置就落在回放库根目录里：speedhack\ 与那几个 json 不能被当成回放条目
        sandbox.WriteFile(DocumentsRelative(sandbox, Path.Combine("speedhack", "speedhack_config.json")), "{}");
        sandbox.WriteFile(DocumentsRelative(sandbox, "speedhack-state.json"), "{}");
        sandbox.WriteFile(DocumentsRelative(sandbox, "replay-library.json"), "{}");

        var service = new ReplayLibraryService(TestPaths.AppDirectory, sandbox.Combine("game"), documents);
        service.SaveSettings(new ReplayLibrarySettings { LibraryRoot = documents, KeepInGame = 10 });
        var snapshot = service.Snapshot();

        Assert.Equal(0, snapshot.LibraryCount);
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void ManagerStatus_ReportsDocumentsLocation()
    {
        using var sandbox = new TestSandbox("ap-config-status");
        var documents = DocumentsDataDirectory(sandbox);
        var appData = LegacyAppDataDirectory(sandbox);

        var manager = new SpeedhackManager(TestPaths.AppDirectory, profileDirectory: null,
            appDataDirectory: appData, documentsDirectory: documents);
        var status = manager.GetStatus();

        Assert.Equal(documents, status.ProfileDirectory);
        Assert.True(status.ProfileLocationDocuments, "状态未标记为文档目录");
        Assert.Contains("文档", status.ProfileLocationNote);
        Assert.True(status.ProfileConfigPath.StartsWith(documents, StringComparison.OrdinalIgnoreCase),
            $"主配置路径不在文档目录下：{status.ProfileConfigPath}");
    }

    [Fact]
    public void ManagerStatus_ReportsFallbackWhenDocumentsBlocked()
    {
        using var sandbox = new TestSandbox("ap-config-status-fallback");
        var appData = LegacyAppDataDirectory(sandbox);
        var blocker = sandbox.WriteFile("blocker.txt", "x");
        var brokenDocuments = Path.Combine(blocker, SpeedhackManager.DataFolderName);

        var manager = new SpeedhackManager(TestPaths.AppDirectory, profileDirectory: null,
            appDataDirectory: appData, documentsDirectory: brokenDocuments);
        var status = manager.GetStatus();

        Assert.False(status.ProfileLocationDocuments, "回退场景不应标记为文档目录");
        Assert.Equal(appData, status.ProfileDirectory);
    }
}
