using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 游戏档案（多位置管理）：档案 CRUD、当前游戏指针、去重、迁移、区服识别与 Unity 结构校验。
/// 全在临时沙箱里造假游戏目录，不碰真实游戏。
/// </summary>
public sealed class GameLibraryTests
{
    /// <summary>在沙箱里造一个标准 Unity 结构的假游戏目录，返回目录路径。</summary>
    private static string MakeFakeGame(TestSandbox sandbox, string folderName, string exeName)
    {
        var dir = sandbox.EnsureDirectory(folderName);
        File.WriteAllText(Path.Combine(dir, exeName), "MZ");
        Directory.CreateDirectory(Path.Combine(dir, Path.GetFileNameWithoutExtension(exeName) + "_Data"));
        File.WriteAllText(Path.Combine(dir, "UnityPlayer.dll"), "unity");
        return dir;
    }

    [Fact]
    public void Load_Empty_WhenNothingSaved()
    {
        using var sandbox = new TestSandbox("ap-gamelib-empty");
        var service = new GameLibraryService(sandbox.Root);

        Assert.Empty(service.GetProfiles());
        Assert.Null(service.GetActive());
        Assert.Null(service.GetActiveDirectory());
    }

    [Fact]
    public void AddDirectory_AddsActivatesAndPersists()
    {
        using var sandbox = new TestSandbox("ap-gamelib-add");
        var game = MakeFakeGame(sandbox, "GameCN", "AstralParty_CN.exe");
        var service = new GameLibraryService(sandbox.Root);

        var view = service.AddDirectory(game, activate: true);

        Assert.True(view.Active);
        Assert.Equal("cn", view.Edition);
        Assert.True(view.Valid, "标准 Unity 结构应校验通过");
        Assert.Equal(game, service.GetActiveDirectory());

        // 新实例应能从磁盘读回
        var reloaded = new GameLibraryService(sandbox.Root);
        Assert.Single(reloaded.GetProfiles());
        Assert.Equal(game, reloaded.GetActiveDirectory());
    }

    [Fact]
    public void AddDirectory_DedupesBySamePath()
    {
        using var sandbox = new TestSandbox("ap-gamelib-dedupe");
        var game = MakeFakeGame(sandbox, "Game", "AstralParty.exe");
        var service = new GameLibraryService(sandbox.Root);

        service.AddDirectory(game);
        service.AddDirectory(game.ToUpperInvariant()); // 大小写 / 规范化后同一路径

        Assert.Single(service.GetProfiles());
    }

    [Fact]
    public void Remove_ReassignsActiveToRemaining()
    {
        using var sandbox = new TestSandbox("ap-gamelib-remove");
        var g1 = MakeFakeGame(sandbox, "G1", "AstralParty_CN.exe");
        var g2 = MakeFakeGame(sandbox, "G2", "AstralParty.exe");
        var service = new GameLibraryService(sandbox.Root);

        var v1 = service.AddDirectory(g1, activate: true);
        service.AddDirectory(g2, activate: false);

        service.Remove(v1.Id);

        var profiles = service.GetProfiles();
        Assert.Single(profiles);
        Assert.True(profiles[0].Active, "移除当前档案后应自动把另一个设为当前");
    }

    [Fact]
    public void SetActive_SwitchesPointer()
    {
        using var sandbox = new TestSandbox("ap-gamelib-active");
        var g1 = MakeFakeGame(sandbox, "G1", "AstralParty_CN.exe");
        var g2 = MakeFakeGame(sandbox, "G2", "AstralParty.exe");
        var service = new GameLibraryService(sandbox.Root);
        service.AddDirectory(g1, activate: true);
        var v2 = service.AddDirectory(g2, activate: false);

        service.SetActive(v2.Id);

        Assert.Equal(g2, service.GetActiveDirectory());
    }

    [Fact]
    public void UpdateProfile_ChangesLabelAndEdition()
    {
        using var sandbox = new TestSandbox("ap-gamelib-update");
        var game = MakeFakeGame(sandbox, "Game", "AstralParty.exe");
        var service = new GameLibraryService(sandbox.Root);
        var view = service.AddDirectory(game);

        service.UpdateProfile(view.Id, "我的国际服", "global");

        var updated = service.GetProfiles().Single();
        Assert.Equal("我的国际服", updated.Label);
        Assert.Equal("global", updated.Edition);
        Assert.Equal("国际服", updated.EditionLabel);
    }

    [Fact]
    public void SeedFromLegacyIfEmpty_ImportsLegacyDirectory()
    {
        using var sandbox = new TestSandbox("ap-gamelib-seed");
        var game = MakeFakeGame(sandbox, "Legacy", "AstralParty_CN.exe");
        var service = new GameLibraryService(sandbox.Root);

        service.SeedFromLegacyIfEmpty(new[] { game });

        var profiles = service.GetProfiles();
        Assert.Single(profiles);
        Assert.True(profiles[0].Active);
        Assert.Equal(game, service.GetActiveDirectory());
    }

    [Fact]
    public void SeedFromLegacyIfEmpty_NoOpWhenProfilesExist()
    {
        using var sandbox = new TestSandbox("ap-gamelib-seed-noop");
        var existing = MakeFakeGame(sandbox, "Existing", "AstralParty.exe");
        var legacy = MakeFakeGame(sandbox, "Legacy", "AstralParty_CN.exe");
        var service = new GameLibraryService(sandbox.Root);
        service.AddDirectory(existing);

        service.SeedFromLegacyIfEmpty(new[] { legacy });

        Assert.Single(service.GetProfiles()); // 不再播种
    }

    [Theory]
    [InlineData("AstralParty_CN.exe", "cn")]
    [InlineData("AstralParty.exe", "global")]
    public void DetectEdition_FromExeName(string exeName, string expected)
    {
        using var sandbox = new TestSandbox("ap-gamelib-edition");
        var dir = MakeFakeGame(sandbox, "G", exeName);
        Assert.Equal(expected, GameLibraryService.DetectEdition(dir, exeName));
    }

    [Fact]
    public void DetectEdition_TapTapFromPath()
    {
        Assert.Equal("taptap", GameLibraryService.DetectEdition(@"C:\TapTap\AstralParty", "AstralParty_CN.exe"));
    }

    [Fact]
    public void ValidateUnityStructure_RejectsMissingPieces()
    {
        using var sandbox = new TestSandbox("ap-gamelib-unity");
        // 只有 exe，没有 _Data / UnityPlayer.dll
        var bare = sandbox.EnsureDirectory("Bare");
        File.WriteAllText(Path.Combine(bare, "AstralParty.exe"), "MZ");
        Assert.False(GameLibraryService.ValidateUnityStructure(bare));

        var full = MakeFakeGame(sandbox, "Full", "AstralParty.exe");
        Assert.True(GameLibraryService.ValidateUnityStructure(full));
    }

    [Fact]
    public void DetectExe_AcceptsAnyAstralPartyPrefixedExe()
    {
        using var sandbox = new TestSandbox("ap-gamelib-exe-pattern");
        // 手动选的目录里 exe 叫别的名字（如 TapTap 端 / 其它渠道），只要是 AstralParty*.exe 就该认
        var dir = sandbox.EnsureDirectory("Weird");
        File.WriteAllText(Path.Combine(dir, "AstralParty_TapTap.exe"), "MZ");

        Assert.Equal("AstralParty_TapTap.exe", GameLibraryService.DetectExe(dir));
        Assert.NotNull(SpeedhackManager.ContainsGameExe(dir)); // 安装/状态用的检测也认，不再硬编码那两个名字
    }

    [Fact]
    public void DetectExe_RejectsUnrelatedExe()
    {
        using var sandbox = new TestSandbox("ap-gamelib-exe-unrelated");
        var dir = sandbox.EnsureDirectory("Other");
        File.WriteAllText(Path.Combine(dir, "SomeGame.exe"), "MZ");

        Assert.Null(GameLibraryService.DetectExe(dir));
        Assert.Null(SpeedhackManager.ContainsGameExe(dir));
    }

    [Fact]
    public void GetActiveDirectory_NullWhenDirectoryMissing()
    {
        using var sandbox = new TestSandbox("ap-gamelib-missing");
        var game = MakeFakeGame(sandbox, "Gone", "AstralParty.exe");
        var service = new GameLibraryService(sandbox.Root);
        service.AddDirectory(game, activate: true);

        Directory.Delete(game, recursive: true);

        Assert.Null(service.GetActiveDirectory());
        // 档案仍在列表里，但标记为失效
        var view = service.GetProfiles().Single();
        Assert.False(view.DirectoryExists);
    }
}
