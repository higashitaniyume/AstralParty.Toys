using System;
using System.IO;
using System.Linq;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class AudioSkinSchemeTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), "toys-audio-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AudioSkinSchemeManager _manager = new();

    public AudioSkinSchemeTests()
    {
        Directory.CreateDirectory(_gameDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_gameDir))
        {
            try { Directory.Delete(_gameDir, true); } catch { }
        }
    }

    [Fact]
    public void GetProfiles_Initial_CreatesDefaultScheme()
    {
        var profiles = _manager.GetProfiles(_gameDir);
        Assert.Single(profiles);
        var def = profiles[0];
        Assert.Equal("default", def.Id);
        Assert.True(def.IsActive);
        Assert.True(def.IsDefault);
    }

    [Fact]
    public void CreateScheme_And_SetActiveScheme_Works()
    {
        var scheme = _manager.CreateScheme(_gameDir, "arcade", "街机音效", "Tester", "测试方案");
        Assert.Equal("arcade", scheme.Id);
        Assert.Equal("街机音效", scheme.Name);

        _manager.SetActiveScheme(_gameDir, "arcade");
        Assert.Equal("arcade", _manager.GetActiveSchemeId(_gameDir));

        var profiles = _manager.GetProfiles(_gameDir);
        Assert.Equal(2, profiles.Count);

        var arcade = profiles.First(p => p.Id == "arcade");
        Assert.True(arcade.IsActive);
        Assert.False(arcade.IsDefault);
    }

    [Fact]
    public void UpdateMetadata_UpdatesValues()
    {
        _manager.CreateScheme(_gameDir, "custom", "原名称");
        _manager.UpdateMetadata(_gameDir, "custom", "新名称", "新作者", "新描述");

        var loaded = _manager.LoadScheme(_gameDir, "custom");
        Assert.Equal("新名称", loaded.Name);
        Assert.Equal("新作者", loaded.Author);
        Assert.Equal("新描述", loaded.Description);
    }

    [Fact]
    public void CustomAudio_Save_Revert_Mute_Redirect_Work()
    {
        _manager.CreateScheme(_gameDir, "rules_test", "规则测试");

        // 1. Save custom audio
        byte[] fakeWav = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00 };
        _manager.SaveCustomAudio(_gameDir, "rules_test", 1001, "UI_Common_Click01", fakeWav, "click_custom.wav", 0.9f);

        var scheme = _manager.LoadScheme(_gameDir, "rules_test");
        Assert.Single(scheme.Rules);
        Assert.Equal("replace", scheme.Rules[0].Action);
        Assert.Equal("click_custom.wav", scheme.Rules[0].File);
        Assert.Equal(0.9f, scheme.Rules[0].Volume);

        // 2. Set Mute
        _manager.SetMute(_gameDir, "rules_test", 1002, "UI_Common_ChooseError", true);
        scheme = _manager.LoadScheme(_gameDir, "rules_test");
        Assert.Equal(2, scheme.Rules.Count);
        Assert.Contains(scheme.Rules, r => r.ConfigId == 1002 && r.Action == "mute");

        // 3. Set Redirect
        _manager.SetRedirect(_gameDir, "rules_test", 1003, "UI_Common_DrumPassingDown", 1004);
        scheme = _manager.LoadScheme(_gameDir, "rules_test");
        Assert.Equal(3, scheme.Rules.Count);
        var r3 = scheme.Rules.First(r => r.ConfigId == 1003);
        Assert.Equal("redirect", r3.Action);
        Assert.Equal(1004, r3.RedirectId);

        // 4. Revert
        _manager.RevertAudio(_gameDir, "rules_test", 1001, "UI_Common_Click01");
        scheme = _manager.LoadScheme(_gameDir, "rules_test");
        Assert.Equal(2, scheme.Rules.Count);
        Assert.DoesNotContain(scheme.Rules, r => r.ConfigId == 1001);
    }

    [Fact]
    public void ExportAndImportZip_PreservesRulesAndFiles()
    {
        _manager.CreateScheme(_gameDir, "zip_pack", "ZIP打包测试");
        byte[] fakeWav = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        _manager.SaveCustomAudio(_gameDir, "zip_pack", 1, "UI_Common_Click01", fakeWav, "click.wav", 0.8f);

        string zipPath = Path.Combine(_gameDir, "pack.zip");
        _manager.ExportZip(_gameDir, "zip_pack", zipPath);
        Assert.True(File.Exists(zipPath));

        _manager.ImportZip(_gameDir, zipPath);
        var imported = _manager.LoadScheme(_gameDir, "pack");
        Assert.NotNull(imported);
        Assert.Single(imported.Rules);
        Assert.Equal(1, imported.Rules[0].ConfigId);
        Assert.Equal("click.wav", imported.Rules[0].File);
    }

    [Fact]
    public void DuplicateScheme_CopiesRulesAndFiles()
    {
        _manager.CreateScheme(_gameDir, "src_scheme", "源方案");
        byte[] fakeWav = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        _manager.SaveCustomAudio(_gameDir, "src_scheme", 8, "UI_Common_ThrowDice", fakeWav, "dice.wav", 1.0f);

        _manager.DuplicateScheme(_gameDir, "src_scheme", "copied_scheme", "副本方案");

        var copied = _manager.LoadScheme(_gameDir, "copied_scheme");
        Assert.Equal("copied_scheme", copied.Id);
        Assert.Equal("副本方案", copied.Name);
        Assert.Single(copied.Rules);
        Assert.Equal(8, copied.Rules[0].ConfigId);
        Assert.Equal("dice.wav", copied.Rules[0].File);
    }
}
