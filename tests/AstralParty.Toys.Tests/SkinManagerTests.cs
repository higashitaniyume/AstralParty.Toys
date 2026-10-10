using System.IO.Compression;
using System.Text;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class SkinManagerTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SkinManager _skinManager;

    public SkinManagerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "AstralParty_SkinManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _skinManager = new SkinManager();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in temp dir
        }
    }

    [Fact]
    public void GetActiveSkin_DefaultsToDefault_WhenCfgMissing()
    {
        var active = _skinManager.GetActiveSkin(_testRoot);
        Assert.Equal("default", active);
    }

    [Fact]
    public void SetActiveSkin_CreatesOrUpdatesModCfg()
    {
        _skinManager.SetActiveSkin(_testRoot, "my_custom_skin");
        var active = _skinManager.GetActiveSkin(_testRoot);
        Assert.Equal("my_custom_skin", active);

        var cfgPath = _skinManager.GetModConfigPath(_testRoot);
        Assert.True(File.Exists(cfgPath));
        var content = File.ReadAllText(cfgPath);
        Assert.Contains("my_custom_skin", content);

        // Update again
        _skinManager.SetActiveSkin(_testRoot, "another_skin");
        Assert.Equal("another_skin", _skinManager.GetActiveSkin(_testRoot));
    }

    [Fact]
    public void GameDefault_DisablesExternalSkinsAndPreservesOtherSettings()
    {
        var path = _skinManager.GetModConfigPath(_testRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"Enabled\":true,\"ActiveSkin\":\"custom\",\"ForceFullCard\":true}");
        _skinManager.SetActiveSkin(_testRoot, SkinManager.GameDefault);
        Assert.Equal(SkinManager.GameDefault, _skinManager.GetActiveSkin(_testRoot));
        using (var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.False(config.RootElement.GetProperty("Enabled").GetBoolean());
            Assert.True(config.RootElement.GetProperty("ForceFullCard").GetBoolean());
            Assert.Equal("custom", config.RootElement.GetProperty("ActiveSkin").GetString());
        }
        Assert.Contains(_skinManager.GetProfiles(_testRoot), p => p.Name == SkinManager.GameDefault && p.IsActive);
        _skinManager.SetActiveSkin(_testRoot, "default");
        Assert.Equal("default", _skinManager.GetActiveSkin(_testRoot));
    }

    [Fact]
    public void DuplicateProfile_CopiesFiles_AndGeneratesMetadata()
    {
        var skinsDir = _skinManager.GetSkinsDirectory(_testRoot);
        var defaultDir = Path.Combine(skinsDir, "default");
        Directory.CreateDirectory(defaultDir);

        File.WriteAllText(Path.Combine(defaultDir, "UT_HandCard_101.png"), "fake image 1");
        File.WriteAllText(Path.Combine(defaultDir, "UT_Destiny_202.png"), "fake image 2");

        var created = _skinManager.DuplicateProfile(
            _testRoot,
            "default",
            "summer_2026",
            "夏日主题",
            "AuthorTest",
            "夏日泳装卡面替换方案"
        );

        Assert.Equal("summer_2026", created);
        var targetDir = Path.Combine(skinsDir, "summer_2026");
        Assert.True(Directory.Exists(targetDir));
        Assert.True(File.Exists(Path.Combine(targetDir, "UT_HandCard_10001.png")));
        Assert.True(File.Exists(Path.Combine(targetDir, "UT_Destiny_40001.png")));

        var profiles = _skinManager.GetProfiles(_testRoot);
        var targetProfile = profiles.FirstOrDefault(p => p.Name == "summer_2026");
        Assert.NotNull(targetProfile);
        Assert.Equal("夏日主题", targetProfile!.DisplayName);
        Assert.Equal("AuthorTest", targetProfile.Author);
        Assert.Equal(185, targetProfile.CardCount);
        Assert.False(File.Exists(Path.Combine(targetDir, "UT_HandCard_101.png")));
    }

    [Fact]
    public void DeleteProfile_ThrowsForDefault_AndRemovesCustom()
    {
        var skinsDir = _skinManager.GetSkinsDirectory(_testRoot);
        var defaultDir = Path.Combine(skinsDir, "default");
        var customDir = Path.Combine(skinsDir, "temp_skin");
        Directory.CreateDirectory(defaultDir);
        Directory.CreateDirectory(customDir);

        Assert.Throws<InvalidOperationException>(() => _skinManager.DeleteProfile(_testRoot, "default"));

        _skinManager.SetActiveSkin(_testRoot, "temp_skin");
        Assert.Equal("temp_skin", _skinManager.GetActiveSkin(_testRoot));

        _skinManager.DeleteProfile(_testRoot, "temp_skin");
        Assert.False(Directory.Exists(customDir));
        // Deleting active skin automatically falls back to default
        Assert.Equal("default", _skinManager.GetActiveSkin(_testRoot));
    }

    [Fact]
    public void ImportSkinZip_FullLoaderPath_ExtractsProperly()
    {
        var zipPath = Path.Combine(_testRoot, "package_full.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("AstralParty_ModLoader/skins/cyberpunk/UT_HandCard_1.png");
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write("png data");
            }

            var metaEntry = archive.CreateEntry("AstralParty_ModLoader/skins/cyberpunk/skin.json");
            using (var metaWriter = new StreamWriter(metaEntry.Open()))
            {
                metaWriter.Write("{\"name\":\"赛博朋克\",\"author\":\"Modder\",\"description\":\"科幻卡面\"}");
            }
        }

        var imported = _skinManager.ImportSkinZip(_testRoot, zipPath);
        Assert.Equal("cyberpunk", imported);

        var skinsDir = _skinManager.GetSkinsDirectory(_testRoot);
        var destFile = Path.Combine(skinsDir, "cyberpunk", "UT_HandCard_1.png");
        Assert.True(File.Exists(destFile));

        var profiles = _skinManager.GetProfiles(_testRoot);
        var p = profiles.FirstOrDefault(x => x.Name == "cyberpunk");
        Assert.NotNull(p);
        Assert.Equal("赛博朋克", p!.DisplayName);
        Assert.Equal(1, p.CardCount);
    }

    [Fact]
    public void ImportSkinZip_FlatFiles_UsesZipNameAsProfile()
    {
        var zipPath = Path.Combine(_testRoot, "flat_pack.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry1 = archive.CreateEntry("UT_HandCard_5.png");
            using (var writer = new StreamWriter(entry1.Open())) writer.Write("5");
            var entry2 = archive.CreateEntry("UT_Destiny_6.jpg");
            using (var writer = new StreamWriter(entry2.Open())) writer.Write("6");
        }

        var imported = _skinManager.ImportSkinZip(_testRoot, zipPath);
        Assert.Equal("flat_pack", imported);

        var skinsDir = _skinManager.GetSkinsDirectory(_testRoot);
        Assert.True(File.Exists(Path.Combine(skinsDir, "flat_pack", "UT_HandCard_5.png")));
        Assert.True(File.Exists(Path.Combine(skinsDir, "flat_pack", "UT_Destiny_6.jpg")));
    }

    [Fact]
    public void ImportSkinZip_RejectsPathTraversal()
    {
        var zipPath = Path.Combine(_testRoot, "malicious.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("evil");
        }

        Assert.Throws<InvalidOperationException>(() => _skinManager.ImportSkinZip(_testRoot, zipPath));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("custom")]
    public void ReplaceCardImage_ListRefreshKeepsNewContentVersion(string skinName)
    {
        const string fileName = "UT_HandCard_10001.png";
        var directory = Path.Combine(_skinManager.GetSkinsDirectory(_testRoot), skinName);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, fileName);
        var source = Path.Combine(_testRoot, "replacement.png");
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(target, "old-image");
        File.SetLastWriteTimeUtc(target, timestamp);
        var before = Assert.Single(_skinManager.GetCards(_testRoot, skinName));

        // 同样大小、同样修改时间也必须识别出图片内容已经改变。
        File.WriteAllText(source, "new-image");
        File.SetLastWriteTimeUtc(source, timestamp);
        var updated = _skinManager.ReplaceCardImage(_testRoot, skinName, fileName, source);
        var refreshed = Assert.Single(_skinManager.GetCards(_testRoot, skinName));

        Assert.Equal("new-image", File.ReadAllText(target));
        Assert.NotEqual(before.PreviewUrl, updated.PreviewUrl);
        Assert.Equal(updated.PreviewUrl, refreshed.PreviewUrl);
        Assert.Equal(refreshed.PreviewUrl, Assert.Single(_skinManager.GetCards(_testRoot, skinName)).PreviewUrl);
    }

    [Fact]
    public void RevertCardImage_ListRefreshUsesCurrentDefaultContentVersion()
    {
        const string fileName = "UT_HandCard_10001.png";
        var skins = _skinManager.GetSkinsDirectory(_testRoot);
        Directory.CreateDirectory(Path.Combine(skins, "default"));
        Directory.CreateDirectory(Path.Combine(skins, "custom"));
        File.WriteAllText(Path.Combine(skins, "default", fileName), "default-image");
        File.WriteAllText(Path.Combine(skins, "custom", fileName), "custom-image");
        var before = Assert.Single(_skinManager.GetCards(_testRoot, "custom"));
        var reverted = _skinManager.RevertCardImage(_testRoot, "custom", fileName);
        var refreshed = Assert.Single(_skinManager.GetCards(_testRoot, "custom"));
        Assert.NotEqual(before.PreviewUrl, reverted.PreviewUrl);
        Assert.Equal(reverted.PreviewUrl, refreshed.PreviewUrl);
        Assert.True(refreshed.IsCustomized);
        Assert.NotEqual(refreshed.PreviewUrl, refreshed.FallbackUrl);
        Assert.NotEqual("default-image", File.ReadAllText(Path.Combine(skins, "custom", fileName)));
    }

    [Fact]
    public void ExportZip_RoundTripsMetadataCustomImagesAndDefaultFallbacks()
    {
        var skins = _skinManager.GetSkinsDirectory(_testRoot);
        Directory.CreateDirectory(Path.Combine(skins, "default"));
        Directory.CreateDirectory(Path.Combine(skins, "custom"));
        File.WriteAllText(Path.Combine(skins, "default", "UT_HandCard_10001.png"), "default hand");
        File.WriteAllText(Path.Combine(skins, "default", "UT_Destiny_40001.png"), "default destiny");
        File.WriteAllText(Path.Combine(skins, "custom", "UT_HandCard_10001.png"), "custom hand");
        var metadata = "{\"name\":\"My scheme\",\"author\":\"Player\"}";
        File.WriteAllText(Path.Combine(skins, "custom", "skin.json"), metadata);
        var zip = Path.Combine(_testRoot, "export.zip");
        _skinManager.ExportSkinZip(_testRoot, "custom", zip);
        var secondGame = Path.Combine(_testRoot, "second-game");
        Assert.Equal("custom", _skinManager.ImportSkinZip(secondGame, zip));
        var imported = Path.Combine(_skinManager.GetSkinsDirectory(secondGame), "custom");
        Assert.Equal("custom hand", File.ReadAllText(Path.Combine(imported, "UT_HandCard_10001.png")));
        Assert.Equal("default destiny", File.ReadAllText(Path.Combine(imported, "UT_Destiny_40001.png")));
        Assert.Equal(metadata, File.ReadAllText(Path.Combine(imported, "skin.json")));
        Assert.Throws<InvalidOperationException>(() => _skinManager.ExportSkinZip(_testRoot, SkinManager.GameDefault, zip));
        Assert.Empty(Directory.GetFiles(_testRoot, "*.partial"));
    }

    [Fact]
    public void EmbeddedTemplate_RestoresDefaultAndCreatesPristineSchemeOffline()
    {
        var baseline = Path.Combine(_testRoot, "baseline");
        Assert.Equal(186, DefaultSkinPackage.Install(baseline));
        var expected = File.ReadAllBytes(Path.Combine(baseline, "UT_HandCard_10001.png"));
        _skinManager.RestoreDefaultProfile(_testRoot);
        var defaultDir = Path.Combine(_skinManager.GetSkinsDirectory(_testRoot), "default");
        File.WriteAllText(Path.Combine(defaultDir, "UT_HandCard_10001.png"), "user modification");
        var created = _skinManager.DuplicateProfile(_testRoot, "default", "fresh", "Fresh", "", "");
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_skinManager.GetSkinsDirectory(_testRoot), created, "UT_HandCard_10001.png")));
        _skinManager.RestoreDefaultProfile(_testRoot);
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(defaultDir, "UT_HandCard_10001.png")));
        File.WriteAllText(Path.Combine(defaultDir, "UT_HandCard_10001.png"), "modified again");
        _skinManager.RevertCardImage(_testRoot, "default", "UT_HandCard_10001.png");
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(defaultDir, "UT_HandCard_10001.png")));
    }

    [Fact]
    public void GetCards_CorrectlyCategorizesAndExtractsCardId()
    {
        var skinsDir = _skinManager.GetSkinsDirectory(_testRoot);
        var testSkinDir = Path.Combine(skinsDir, "test_skin");
        Directory.CreateDirectory(testSkinDir);

        File.WriteAllText(Path.Combine(testSkinDir, "UT_HandCard_105.png"), "1");
        File.WriteAllText(Path.Combine(testSkinDir, "UT_Destiny_205_sfw.png"), "2");
        File.WriteAllText(Path.Combine(testSkinDir, "UT_Item_AltArt_305.jpg"), "3");
        File.WriteAllText(Path.Combine(testSkinDir, "UT_Event_405.jpeg"), "4");
        File.WriteAllText(Path.Combine(testSkinDir, "UT_MapEvent_505.png"), "5");

        var cards = _skinManager.GetCards(_testRoot, "test_skin");
        Assert.Equal(5, cards.Count);

        var handCard = cards.FirstOrDefault(c => c.FileName == "UT_HandCard_105.png");
        Assert.NotNull(handCard);
        Assert.Equal(105, handCard!.CardId);
        Assert.Equal("手牌卡", handCard.Category);
        Assert.False(handCard.IsSfw);

        var destinyCard = cards.FirstOrDefault(c => c.FileName == "UT_Destiny_205_sfw.png");
        Assert.NotNull(destinyCard);
        Assert.Equal(205, destinyCard!.CardId);
        Assert.Equal("命运卡", destinyCard.Category);
        Assert.True(destinyCard.IsSfw);

        var altArtCard = cards.FirstOrDefault(c => c.FileName == "UT_Item_AltArt_305.jpg");
        Assert.NotNull(altArtCard);
        Assert.Equal(305, altArtCard!.CardId);
        Assert.Equal("异画道具", altArtCard.Category);

        var eventCard = cards.FirstOrDefault(c => c.FileName == "UT_Event_405.jpeg");
        Assert.NotNull(eventCard);
        Assert.Equal(405, eventCard!.CardId);
        Assert.Equal("事件卡", eventCard.Category);

        var mapEventCard = cards.FirstOrDefault(c => c.FileName == "UT_MapEvent_505.png");
        Assert.NotNull(mapEventCard);
        Assert.Equal(505, mapEventCard!.CardId);
        Assert.Equal("地图事件", mapEventCard.Category);
    }
}
