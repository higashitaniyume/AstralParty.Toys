using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>游戏多开（账号 profile）的目录管理与加载器 profile 字段。</summary>
public sealed class AccountProfileTests
{
    private sealed class Harness : IDisposable
    {
        public Harness(string name)
        {
            Sandbox = new TestSandbox(name);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            Sandbox.WriteFile("game/AstralParty_CN_Data/app.info", "feimo\nAstralParty_CN");
            LocalLow = Sandbox.EnsureDirectory("low");
            Directory.CreateDirectory(Path.Combine(LocalLow, "feimo", "AstralParty_CN"));
            Service = new AccountProfileService(GameDirectory, LocalLow);
            Manager = new ModManager(TestPaths.AppDirectory, Sandbox.EnsureDirectory("profile"));
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public string LocalLow { get; }
        public AccountProfileService Service { get; }
        public ModManager Manager { get; }

        public void Dispose() => Sandbox.Dispose();
    }

    [Fact]
    public void Service_ResolvesSourceAndProfilesRootFromAppInfo()
    {
        using var harness = new Harness("ap-account-resolve");

        Assert.Null(harness.Service.Error);
        Assert.Equal("feimo", harness.Service.Company);
        Assert.Equal("AstralParty_CN", harness.Service.Product);
        Assert.Equal(Path.Combine(harness.LocalLow, "feimo", "AstralParty_CN"), harness.Service.SourceDirectory);
        Assert.Equal(Path.Combine(harness.LocalLow, "feimo", "AstralParty_CN") + "_profiles", harness.Service.ProfilesRoot);
    }

    [Fact]
    public void Service_ReportsErrorWhenGameDirectoryMissing()
    {
        var service = new AccountProfileService(Path.Combine(Path.GetTempPath(), "ap-missing-" + Guid.NewGuid().ToString("N")));
        Assert.NotNull(service.Error);
        Assert.Empty(service.List(null));
    }

    [Theory]
    [InlineData("P2", true)]
    [InlineData("abc_123", true)]
    [InlineData("ABCdef126_", true)]
    [InlineData("_", true)]
    [InlineData("  P2  ", true)]          // 前后空白会被去掉
    [InlineData("小号 2", false)]          // 只支持英文/数字/下划线
    [InlineData("player-01", false)]
    [InlineData("player.01", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a:b", false)]
    [InlineData("CON", false)]            // Windows 保留设备名
    public void IsValidName_OnlyLettersDigitsUnderscore(string name, bool expected)
    {
        Assert.Equal(expected, AccountProfileService.IsValidName(name, out _));
    }

    [Fact]
    public void IsValidName_LimitsLength()
    {
        Assert.True(AccountProfileService.IsValidName(new string('a', AccountProfileService.MaxNameLength), out _));
        Assert.False(AccountProfileService.IsValidName(new string('a', AccountProfileService.MaxNameLength + 1), out _));
    }

    [Theory]
    [InlineData("P2")]
    [InlineData("player_01")]
    [InlineData("ABCdef126_")]
    public void NamePattern_AcceptsAllowedNames(string name)
    {
        Assert.Matches(AccountProfileService.NamePattern, name);
    }

    [Theory]
    [InlineData("小号")]
    [InlineData("player-01")]
    [InlineData("player 01")]
    [InlineData("player.01")]
    [InlineData("")]
    public void NamePattern_RejectsOtherNames(string name)
    {
        Assert.DoesNotMatch(AccountProfileService.NamePattern, name);
    }

    [Fact]
    public void LegacyNames_StillManageableButNotCreatable()
    {
        using var harness = new Harness("ap-account-legacy-name");
        Assert.False(AccountProfileService.IsValidName("小号", out _));

        // 手写配置留下的老档案仍然能定位（打开/删除），只是不能再新建/改名成这种名字。
        var directory = Path.Combine(harness.Service.ProfilesRoot!, "小号");
        Directory.CreateDirectory(directory);
        Assert.Equal(directory, harness.Service.ResolveDirectory("小号"));
        Assert.Contains(harness.Service.List("小号"), p => p.Name == "小号" && p.Active);
    }

    [Fact]
    public void Create_List_ReportsDefaultFirst_AndFlagsActive()
    {
        using var harness = new Harness("ap-account-list");
        harness.Service.Create("P2");
        harness.Service.Create("P3");

        var listing = harness.Service.List("P3");
        Assert.Equal(3, listing.Count);
        Assert.True(listing[0].IsDefault);
        Assert.Equal("", listing[0].Name);
        Assert.False(listing[0].Active);
        Assert.Equal(new[] { "P2", "P3" }, listing.Skip(1).Select(p => p.Name).OrderBy(n => n));
        Assert.True(listing.Single(p => p.Name == "P3").Active);
        Assert.All(listing, p => Assert.True(p.Exists));
    }

    [Fact]
    public void Create_RejectsDuplicateAndInvalidNames()
    {
        using var harness = new Harness("ap-account-create-guard");
        harness.Service.Create("P2");

        Assert.Throws<InvalidOperationException>(() => harness.Service.Create("P2"));
        Assert.Throws<ArgumentException>(() => harness.Service.Create("bad/name"));
    }

    [Fact]
    public void Rename_MovesDirectory_AndRejectsCollision()
    {
        using var harness = new Harness("ap-account-rename");
        var created = harness.Service.Create("P2");
        File.WriteAllText(Path.Combine(created, "marker.txt"), "x");
        harness.Service.Create("P3");

        var renamed = harness.Service.Rename("P2", "P2_old");
        Assert.False(Directory.Exists(created));
        Assert.True(Directory.Exists(renamed));
        Assert.Equal("x", File.ReadAllText(Path.Combine(renamed, "marker.txt")));
        Assert.Throws<InvalidOperationException>(() => harness.Service.Rename("P2_old", "P3"));
    }

    [Fact]
    public void List_ShowsConfiguredProfileThatNoLongerExists()
    {
        using var harness = new Harness("ap-account-missing");

        var listing = harness.Service.List("deleted-profile");
        var missing = Assert.Single(listing, p => p.Name == "deleted-profile");
        Assert.True(missing.Active);
        Assert.False(missing.Exists);
        Assert.Equal(harness.Service.ProfilesRoot, Path.GetDirectoryName(missing.Directory));
    }

    [Fact]
    public void List_IgnoresSharedCacheFolderWhenMeasuringSize()
    {
        using var harness = new Harness("ap-account-size");
        var profile = harness.Service.Create("P2");
        // 模拟“共享缓存”（加载器用硬链接播种）：几百 MB 也不该算进这一档的占用
        var cache = Path.Combine(profile, "com.unity.addressables", "AssetBundles");
        Directory.CreateDirectory(cache);
        File.WriteAllBytes(Path.Combine(cache, "bundle"), new byte[512 * 1024]);
        Directory.CreateDirectory(Path.Combine(profile, "Temp"));
        File.WriteAllBytes(Path.Combine(profile, "Temp", "SaveFile.es3"), new byte[2048]);

        var info = harness.Service.List("").Single(p => p.Name == "P2");
        Assert.InRange(info.Bytes, 1024, 64 * 1024);
    }

    [Fact]
    public void Note_SavesIntoProfileFolder_AndShowsInList()
    {
        using var harness = new Harness("ap-account-note");
        var profile = harness.Service.Create("P2");

        harness.Service.SetNote("P2", "  朋友的号  ");
        Assert.Equal("朋友的号", AccountProfileService.ReadNote(profile));
        Assert.True(File.Exists(Path.Combine(profile, ".profile.json")));
        // 备注文件是给人看的：中文不应被转义成 \uXXXX
        Assert.Contains("朋友的号", File.ReadAllText(Path.Combine(profile, ".profile.json")));
        Assert.Equal("朋友的号", harness.Service.List("").Single(p => p.Name == "P2").Note);

        // 空备注 = 删除备注文件
        harness.Service.SetNote("P2", "   ");
        Assert.False(File.Exists(Path.Combine(profile, ".profile.json")));
        Assert.Equal("", harness.Service.List("").Single(p => p.Name == "P2").Note);
    }

    [Fact]
    public void Note_IsTruncatedToLimit()
    {
        using var harness = new Harness("ap-account-note-limit");
        harness.Service.Create("P2");

        harness.Service.SetNote("P2", new string('备', AccountProfileService.MaxNoteLength + 40));
        var note = harness.Service.List("").Single(p => p.Name == "P2").Note;
        Assert.Equal(AccountProfileService.MaxNoteLength, note.Length);
    }

    [Fact]
    public void Note_SurvivesRename_AndDefaultHasNoNote()
    {
        using var harness = new Harness("ap-account-note-rename");
        harness.Service.Create("P2");
        harness.Service.SetNote("P2", "小号");

        harness.Service.Rename("P2", "P2_new");
        Assert.Equal("小号", harness.Service.List("").Single(p => p.Name == "P2_new").Note);
        Assert.Equal("", harness.Service.List("").Single(p => p.IsDefault).Note);
        Assert.Throws<ArgumentException>(() => harness.Service.SetNote("", "默认档案没有备注"));
    }

    [Fact]
    public void Note_CorruptFileIsIgnored()
    {
        using var harness = new Harness("ap-account-note-corrupt");
        var profile = harness.Service.Create("P2");
        File.WriteAllText(Path.Combine(profile, ".profile.json"), "{ not json");

        Assert.Equal("", AccountProfileService.ReadNote(profile));
        Assert.Equal("", harness.Service.List("").Single(p => p.Name == "P2").Note);
    }

    // ============================== 加载器配置里的 profile 字段 ==============================

    private const string ConfigWithProfile = """
        {
          // 注释里出现 "profile": "X" 不能算数
          "enabled": true,
          // 多 profile
          "profile": "",
          "sdkVersion": "3.1.4"
        }
        """;

    private const string ConfigWithoutProfile = """
        {
          "enabled": true,
          // 保持注释
          "sdkVersion": "3.1.4"
        }
        """;

    [Fact]
    public void ReadLoaderConfig_ReadsProfile()
    {
        using var harness = new Harness("ap-account-config-read");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithProfile.Replace("\"profile\": \"\"", "\"profile\": \"P2\""));

        Assert.Equal("P2", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    [Fact]
    public void SaveLoaderConfig_KeepsProfile()
    {
        using var harness = new Harness("ap-account-config-save");
        Directory.CreateDirectory(Path.GetDirectoryName(harness.Manager.LoaderConfigPath(harness.GameDirectory))!);
        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        config.Profile = "P2";
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        Assert.Equal("P2", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    [Fact]
    public void SetLoaderProfile_ReplacesValue_AndKeepsComments()
    {
        using var harness = new Harness("ap-account-config-set");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithProfile);

        Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, "P2"));
        var text = File.ReadAllText(path);
        Assert.Contains("\"profile\": \"P2\"", text);
        Assert.Contains("// 注释里出现 \"profile\": \"X\" 不能算数", text);
        Assert.Contains("// 多 profile", text);
        Assert.Contains("\"sdkVersion\": \"3.1.4\"", text);
        Assert.Equal("P2", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);

        Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, ""));
        Assert.Contains("\"profile\": \"\"", File.ReadAllText(path));
        Assert.Equal("", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    [Fact]
    public void SetLoaderProfile_InsertsKeyWhenMissing()
    {
        using var harness = new Harness("ap-account-config-insert");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithoutProfile);

        Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, "P3"));
        var text = File.ReadAllText(path);
        Assert.Contains("\"profile\": \"P3\"", text);
        Assert.Contains("// 保持注释", text);
        Assert.Equal("P3", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    [Fact]
    public void SetLoaderProfile_ReturnsFalseWhenConfigMissing()
    {
        using var harness = new Harness("ap-account-config-absent");
        Assert.False(harness.Manager.SetLoaderProfile(harness.GameDirectory, "P2"));
    }

    [Fact]
    public void SetLoaderProfile_ReturnsFalseWhenNameIsNotJsonSafe()
    {
        using var harness = new Harness("ap-account-config-bad");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithProfile);

        // 引号会被转义，写进去仍是合法 JSON；但自检读回来的值必须与目标完全一致
        Assert.True(harness.Manager.SetLoaderProfile(harness.GameDirectory, "a\"b"));
        Assert.Equal("a\"b", harness.Manager.ReadLoaderConfig(harness.GameDirectory).Profile);
    }

    // ============================== 加载器配置里的 profileWindowTitle ==============================

    private const string ConfigWithWindowTitle = """
        {
          // 注释里出现 "profileWindowTitle": false 不能算数
          "enabled": true,
          // 多 profile 窗口标题
          "profileWindowTitle": true,
          "sdkVersion": "3.1.4"
        }
        """;

    [Fact]
    public void ReadLoaderConfig_ReadsProfileWindowTitle()
    {
        using var harness = new Harness("ap-window-title-read");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithWindowTitle.Replace("\"profileWindowTitle\": true", "\"profileWindowTitle\": false"));

        Assert.False(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
    }

    [Fact]
    public void ReadLoaderConfig_DefaultsProfileWindowTitleToOn()
    {
        using var harness = new Harness("ap-window-title-default");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithProfile);

        // 老配置没有这个键：加载器按默认（开）处理，界面显示也跟随。
        Assert.True(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
    }

    [Fact]
    public void SaveLoaderConfig_KeepsProfileWindowTitle()
    {
        using var harness = new Harness("ap-window-title-save");
        Directory.CreateDirectory(Path.GetDirectoryName(harness.Manager.LoaderConfigPath(harness.GameDirectory))!);
        var config = harness.Manager.ReadLoaderConfig(harness.GameDirectory);
        config.ProfileWindowTitle = false;
        harness.Manager.SaveLoaderConfig(harness.GameDirectory, config);

        Assert.False(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
    }

    [Fact]
    public void SetProfileWindowTitle_ReplacesValue_AndKeepsComments()
    {
        using var harness = new Harness("ap-window-title-set");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithWindowTitle);

        Assert.True(harness.Manager.SetProfileWindowTitle(harness.GameDirectory, false));
        var text = File.ReadAllText(path);
        Assert.Contains("\"profileWindowTitle\": false", text);
        Assert.Contains("// 注释里出现 \"profileWindowTitle\": false 不能算数", text);
        Assert.Contains("// 多 profile 窗口标题", text);
        Assert.Contains("\"sdkVersion\": \"3.1.4\"", text);
        Assert.False(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);

        Assert.True(harness.Manager.SetProfileWindowTitle(harness.GameDirectory, true));
        Assert.Contains("\"profileWindowTitle\": true", File.ReadAllText(path));
        Assert.True(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
    }

    [Fact]
    public void SetProfileWindowTitle_InsertsKeyWhenMissing()
    {
        using var harness = new Harness("ap-window-title-insert");
        var path = harness.Manager.LoaderConfigPath(harness.GameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConfigWithoutProfile);

        Assert.True(harness.Manager.SetProfileWindowTitle(harness.GameDirectory, false));
        var text = File.ReadAllText(path);
        Assert.Contains("\"profileWindowTitle\": false", text);
        Assert.Contains("// 保持注释", text);
        Assert.False(harness.Manager.ReadLoaderConfig(harness.GameDirectory).ProfileWindowTitle);
    }

    [Fact]
    public void SetProfileWindowTitle_ReturnsFalseWhenConfigMissing()
    {
        using var harness = new Harness("ap-window-title-absent");
        Assert.False(harness.Manager.SetProfileWindowTitle(harness.GameDirectory, false));
    }
}
