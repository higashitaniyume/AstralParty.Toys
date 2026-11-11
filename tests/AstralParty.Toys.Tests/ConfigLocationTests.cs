using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.App;
using AstralParty.Toys.Services.Replay;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 数据文件夹规则：所有东西统一放在用户选的根目录（默认「文档\AstralParty.Toys」），
/// 位置记在 %LOCALAPPDATA% 的引导指针里；旧布局（文档\AstralPartyReplays + %APPDATA% 的零散 json）
/// 会被一次性迁移进来。用临时目录扮演「文档」「%LOCALAPPDATA%」「%APPDATA%」，覆盖各个分支。
/// </summary>
public sealed class ConfigLocationTests
{
    private static string LocalAppData(TestSandbox sandbox) => sandbox.Combine("LocalAppData");

    private static string DefaultRoot(TestSandbox sandbox) =>
        sandbox.Combine("Documents", ToysPaths.DefaultFolderName);

    private static string LegacyRoot(TestSandbox sandbox) =>
        sandbox.Combine("Documents", ToysPaths.LegacyFolderName);

    private static string PointerFile(TestSandbox sandbox) =>
        Path.Combine(LocalAppData(sandbox), ToysPaths.PointerFileName);

    // ============================== 根目录解析 ==============================

    [Fact]
    public void FreshInstall_UsesDocumentsFolderAndWritesPointer()
    {
        using var sandbox = new TestSandbox("ap-root-fresh");
        var documents = DefaultRoot(sandbox);

        var resolved = ToysPaths.ResolveRoot(LocalAppData(sandbox), documents);

        Assert.Equal(documents, resolved);
        Assert.True(Directory.Exists(documents), "数据文件夹没有被创建");
        Assert.Equal(documents, File.ReadAllText(PointerFile(sandbox)).Trim());
    }

    [Fact]
    public void UnwritableDocuments_FallsBackToLocalData()
    {
        using var sandbox = new TestSandbox("ap-root-fallback");
        // 把「文档目录」挂在一个文件下面：CreateDirectory 必然失败
        var blocker = sandbox.WriteFile("blocker.txt", "x");
        var broken = Path.Combine(blocker, ToysPaths.DefaultFolderName);

        var resolved = ToysPaths.ResolveRoot(LocalAppData(sandbox), broken);

        Assert.Equal(Path.Combine(LocalAppData(sandbox), "data"), resolved);
    }

    [Fact]
    public void RecordedPointer_WinsOverDefault()
    {
        using var sandbox = new TestSandbox("ap-root-pointer");
        var chosen = sandbox.EnsureDirectory("D", "ToysData");
        Directory.CreateDirectory(LocalAppData(sandbox));
        File.WriteAllText(PointerFile(sandbox), chosen);

        Assert.Equal(chosen, ToysPaths.ResolveRoot(LocalAppData(sandbox), DefaultRoot(sandbox)));
    }

    [Fact]
    public void SwitchRoot_UpdatesInMemoryRootAndPointer()
    {
        using var sandbox = new TestSandbox("ap-root-switch");
        var original = ToysPaths.Root;
        var target = sandbox.EnsureDirectory("new-root");
        try
        {
            ToysPaths.SwitchRoot(target, sandbox.Combine("LocalAppData"));

            Assert.Equal(target, ToysPaths.Root);
            Assert.Equal(target, File.ReadAllText(PointerFile(sandbox)).Trim());
        }
        finally
        {
            ToysPaths.Use(original);
        }
    }

    [Fact]
    public void Services_WithoutOverride_FollowTheCurrentDataRoot()
    {
        var root = ToysPaths.Root;

        Assert.Equal(root, new SpeedhackManager(TestPaths.AppDirectory).ProfileDirectory);
        Assert.Equal(root, new ModManager(TestPaths.AppDirectory).ProfileDirectory);
        Assert.Equal(root, new GameLibraryService().ProfileDirectory);
        Assert.Equal(root, new ReplayLibraryService(TestPaths.AppDirectory).DataRoot);
    }

    // ============================== 旧布局迁移 ==============================

    [Fact]
    public void Migration_RelocatesLegacyRootAndReorganizesEverything()
    {
        using var sandbox = new TestSandbox("ap-migrate-layout");
        var root = DefaultRoot(sandbox);
        var legacy = sandbox.EnsureDirectory("Documents", ToysPaths.LegacyFolderName);

        // 旧根：回放、索引、安装历史、状态文件、旧设置
        sandbox.WriteFile(@"Documents\AstralPartyReplays\12345\12345", "replay");
        sandbox.WriteFile(@"Documents\AstralPartyReplays\_index\12345.json", "{}");
        sandbox.WriteFile(@"Documents\AstralPartyReplays\modloader-history\games\a.json", "{}");
        sandbox.WriteFile(@"Documents\AstralPartyReplays\speedhack-state.json", """{"gameDirectory":"D:\\Game"}""");
        var oldSettings = sandbox.WriteFile(@"Documents\AstralPartyReplays\replay-library.json",
            """{"autoMaintain":true,"keepInGame":6,"language":"en","workshopUrl":"https://mirror.example.com"}""");
        sandbox.WriteFile(@"Documents\AstralPartyReplays\ui-theme.txt", "light");

        var store = ToysConfigStore.ForRoot(root);
        var report = ToysMigration.Run(root, store, legacy, sandbox.Combine("Roaming"), sandbox.Combine("LocalCache"));

        Assert.Empty(report.Problems);
        Assert.True(File.Exists(Path.Combine(root, "replays", "12345", "12345")), "回放没有移进 replays");
        Assert.True(File.Exists(Path.Combine(root, "replays", "_index", "12345.json")), "索引没有移进 replays");
        Assert.True(File.Exists(Path.Combine(root, "install-history", "games", "a.json")), "安装历史没有改名");
        Assert.True(File.Exists(Path.Combine(root, "state", "speedhack-state.json")), "状态文件没有移进 state");
        Assert.False(Directory.Exists(legacy), "旧数据目录应当已经搬空");
        Assert.False(File.Exists(oldSettings), "旧设置文件在合并后应当删除");

        var config = store.Load();
        Assert.True(config.Replay.AutoMaintain);
        Assert.Equal(6, config.Replay.KeepInGame);
        Assert.Equal("en", config.App.Language);
        Assert.Equal("light", config.App.Theme);
        Assert.Equal("https://mirror.example.com", config.Workshop.Url);
        Assert.NotNull(config.Migration.CompletedUtc);
    }

    [Fact]
    public void Migration_RelocatesLegacyRootWhenNewRootIsAnEmptyFolder()
    {
        using var sandbox = new TestSandbox("ap-migrate-empty-root");
        var root = sandbox.EnsureDirectory("Documents", ToysPaths.DefaultFolderName);
        var legacy = sandbox.EnsureDirectory("Documents", ToysPaths.LegacyFolderName);

        sandbox.WriteFile(@"Documents\AstralPartyReplays\12345\12345", "replay");

        var report = ToysMigration.Run(root, ToysConfigStore.ForRoot(root), legacy,
            sandbox.Combine("Roaming"), sandbox.Combine("LocalCache"));

        Assert.Empty(report.Problems);
        Assert.True(File.Exists(Path.Combine(root, "replays", "12345", "12345")), "空目录挡路时也应整体搬过来");
        Assert.False(Directory.Exists(legacy), "旧数据目录应当已经搬空");
    }

    [Fact]
    public void Migration_DoesNotOverwriteExistingState()
    {
        using var sandbox = new TestSandbox("ap-migrate-keep");
        var root = DefaultRoot(sandbox);
        var legacy = sandbox.EnsureDirectory("Documents", ToysPaths.LegacyFolderName);

        sandbox.WriteFile(@"Documents\AstralPartyReplays\speedhack-state.json", """{"gameDirectory":"D:\\Game"}""");
        sandbox.WriteFile(@"Documents\AstralParty.Toys\state\speedhack-state.json", """{"gameDirectory":"E:\\Mine"}""");

        ToysMigration.Run(root, ToysConfigStore.ForRoot(root), legacy,
            sandbox.Combine("Roaming"), sandbox.Combine("LocalCache"));

        var text = File.ReadAllText(Path.Combine(root, "state", "speedhack-state.json"));
        Assert.Contains("E:\\\\Mine", text);
    }

    [Fact]
    public void Migration_ImportsLegacyRoamingConfig_ThenDeletesSource()
    {
        using var sandbox = new TestSandbox("ap-migrate-roaming");
        var root = DefaultRoot(sandbox);
        var roaming = sandbox.EnsureDirectory("Roaming");

        var config = sandbox.WriteFile(@"Roaming\speedhack\speedhack_config.json", """{"console":true}""");
        var state = sandbox.WriteFile(@"Roaming\speedhack-state.json", """{"gameDirectory":"D:\\Game"}""");

        ToysMigration.Run(root, ToysConfigStore.ForRoot(root), sandbox.Combine("Documents", "Nope"), roaming,
            sandbox.Combine("LocalCache"));

        Assert.True(File.Exists(Path.Combine(root, "speedhack", "speedhack_config.json")), "旧主配置没有迁入");
        Assert.True(File.Exists(Path.Combine(root, "state", "speedhack-state.json")), "旧状态没有迁入");
        Assert.False(File.Exists(config), "迁移成功后应当删除 %APPDATA% 里的旧文件");
        Assert.False(File.Exists(state));
    }

    [Fact]
    public void Migration_MovesWorkshopCacheIntoCacheFolder()
    {
        using var sandbox = new TestSandbox("ap-migrate-cache");
        var root = DefaultRoot(sandbox);
        sandbox.WriteFile(@"LocalCache\workshop-covers\covers\a.webp", "img");
        sandbox.WriteFile(@"LocalCache\workshop-covers\stickers\catalog.json", "{}");

        ToysMigration.Run(root, ToysConfigStore.ForRoot(root), sandbox.Combine("Documents", "Nope"),
            sandbox.Combine("Roaming"), sandbox.Combine("LocalCache", "workshop-covers"));

        Assert.True(File.Exists(Path.Combine(root, "cache", "workshop", "covers", "a.webp")));
        Assert.True(File.Exists(Path.Combine(root, "cache", "workshop", "stickers", "catalog.json")));
        Assert.False(Directory.Exists(sandbox.Combine("LocalCache", "workshop-covers")), "旧缓存目录应当被搬空");
    }

    // ============================== 统一配置 ==============================

    [Fact]
    public void ConfigStore_MergesSectionsWithoutLosingEachOther()
    {
        using var sandbox = new TestSandbox("ap-config-merge");
        var path = sandbox.Combine(ToysPaths.ConfigFileName);
        var theme = new ToysConfigStore(path);
        var replay = new ToysConfigStore(path);

        theme.Update(config => config.App.Theme = "light");
        replay.Update(config =>
        {
            config.App.Language = "ja";
            config.Replay.KeepInGame = 4;
        });

        var loaded = new ToysConfigStore(path).Load();
        Assert.Equal("light", loaded.App.Theme);
        Assert.Equal("ja", loaded.App.Language);
        Assert.Equal(4, loaded.Replay.KeepInGame);
    }

    [Fact]
    public void ConfigStore_FallsBackToDefaultsWhenFileIsBroken()
    {
        using var sandbox = new TestSandbox("ap-config-broken");
        var path = sandbox.Combine(ToysPaths.ConfigFileName);
        File.WriteAllText(path, "{ this is not json");

        var loaded = new ToysConfigStore(path).Load();

        Assert.Equal(AppLanguage.SimplifiedChinese, loaded.App.Language);
        Assert.Equal(ReplayServiceLimits.GameSlotCapacity, loaded.Replay.KeepInGame);
    }

    [Fact]
    public void LibrarySettings_LiveInUnifiedConfig()
    {
        using var sandbox = new TestSandbox("ap-config-library");
        var root = sandbox.EnsureDirectory("data");
        var service = new ReplayLibraryService(TestPaths.AppDirectory, sandbox.Combine("game"), root);

        Assert.Equal(Path.Combine(root, ToysPaths.ConfigFileName), service.SettingsPath);
        Assert.Equal(Path.Combine(root, "replays"), service.LibraryRoot);
    }

    [Fact]
    public void DefaultLibraryRoot_IsReplaysUnderDataRoot()
    {
        Assert.Equal(
            Path.Combine(SpeedhackManager.ResolveProfileDirectory(), ToysPaths.ReplaysFolderName),
            ReplayLibraryService.DefaultLibraryRoot());
    }

    // ============================== 换根 ==============================

    [Fact]
    public void SettingsPage_ExposesDataFolderAndLogsControls()
    {
        var html = Web("view-settings.html");
        var classic = Web("js/settings.js");
        var v2 = Web("js/v2/settings.js");

        // 设置页：数据文件夹 + 日志两块
        Assert.Contains("id=\"libraryRootInput\"", html);
        Assert.Contains("data-i18n=\"settings.data_root\"", html);
        Assert.Contains("id=\"openLogsFolderBtn\"", html);
        Assert.Contains("id=\"logLevelSelect\"", html);
        Assert.Contains("id=\"logsPathHint\"", html);

        // 页面发出的消息，宿主都要接
        Assert.Contains("type: 'openDataRoot'", classic);
        Assert.Contains("type: 'openLogsFolder'", classic);
        Assert.Contains("type: 'setLoggingLevel'", classic);
        Assert.Contains("type: 'openDataRoot'", v2);
        Assert.Contains("type: 'openLogsFolder'", v2);
        Assert.Contains("type: 'setLoggingLevel'", v2);
    }

    [Fact]
    public void EveryNewSettingsTranslationKey_ExistsInAllFourLanguages()
    {
        var dictionaries = new[] { "zh-Hans", "zh-Hant", "en", "ja" }
            .Select(lang => Web($"js/i18n/dict-{lang}.js"))
            .ToArray();

        foreach (var key in new[]
                 {
                     "settings.group_data", "settings.data_root", "settings.data_root_note",
                     "settings.data_root_hint", "settings.group_logs", "settings.logs_note",
                     "settings.log_level", "settings.log_level_desc", "settings.open_logs"
                 })
        {
            foreach (var dictionary in dictionaries)
            {
                Assert.True(dictionary.Contains($"'{key}'", StringComparison.Ordinal),
                    $"翻译键 {key} 在某个语言里缺失");
            }
        }
    }

    [Fact]
    public void DataRootMover_MovesEverythingIntoEmptyTarget()
    {
        using var sandbox = new TestSandbox("ap-root-move");
        var source = sandbox.EnsureDirectory("old");
        sandbox.WriteFile(@"old\AstralParty.Toys.Config.json", "{}");
        sandbox.WriteFile(@"old\replays\1\1", "replay");
        var target = sandbox.EnsureDirectory("new");

        var result = DataRootMover.Move(source, target);

        Assert.True(result.Ok, result.Message);
        Assert.True(File.Exists(Path.Combine(target, "AstralParty.Toys.Config.json")));
        Assert.True(File.Exists(Path.Combine(target, "replays", "1", "1")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(source));
    }

    [Fact]
    public void DataRootMover_RefusesNonEmptyTarget()
    {
        using var sandbox = new TestSandbox("ap-root-move-busy");
        var source = sandbox.EnsureDirectory("old");
        var target = sandbox.EnsureDirectory("new");
        sandbox.WriteFile(@"new\keep.txt", "x");

        var result = DataRootMover.Move(source, target);

        Assert.False(result.Ok);
    }

    /// <summary>读内嵌的 WebUI 文件（页面与脚本都在程序集里）。</summary>
    private static string Web(string path)
    {
        using var stream = EmbeddedWebStore.Open(path);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
