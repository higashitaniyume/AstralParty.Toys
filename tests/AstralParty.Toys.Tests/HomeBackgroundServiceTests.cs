using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Modding;
using AstralParty.Toys.Services.Skins;
using AstralParty.Toys.Tests.Support;
using AstralParty.Toys.Tests.ThirdParty;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class HomeBackgroundServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "toys-homebg-" + Guid.NewGuid().ToString("N"));
    private readonly string _game;
    private readonly HomeBackgroundService _service;

    public HomeBackgroundServiceTests()
    {
        _game = Path.Combine(_root, "game");
        Directory.CreateDirectory(Path.Combine(_game, ModManager.LoaderFolderName, ModManager.ModsFolderName));
        File.WriteAllBytes(Path.Combine(_game, "version.dll"), [0]);
        Directory.CreateDirectory(Path.Combine(_root, "app"));
        _service = new HomeBackgroundService(new ModManager(Path.Combine(_root, "app")));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    private string ModsDir => Path.Combine(_game, ModManager.LoaderFolderName, ModManager.ModsFolderName, HomeBackgroundService.ModId);
    private string DisabledDir => Path.Combine(_game, ModManager.LoaderFolderName, ModManager.DisabledModsFolderName, HomeBackgroundService.ModId);
    /// <summary>玩家配置（4.0.0 起唯一位置）：模组目录里的 config.json。</summary>
    private string ConfigFile => ModConfigLocator.Canonical(_game, HomeBackgroundService.ModId);

    // ---------------------------------------------------------------- 造测试图片

    private static byte[] Encode(BitmapEncoder encoder, int w, int h, PixelFormat format, BitmapMetadata? meta = null)
    {
        int bpp = (format.BitsPerPixel + 7) / 8, stride = w * bpp;
        var pixels = new byte[stride * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 31);
        var bmp = BitmapSource.Create(w, h, 96, 96, format, null, pixels, stride);
        encoder.Frames.Add(BitmapFrame.Create(bmp, null, meta, null));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private string WriteImage(string name, byte[] bytes)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static (int W, int H) DecodeWithMod(byte[] png)
    {
        Assert.True(HomeBackgroundModPngDecoder.TryDecode(png, out var w, out var h, out var rgba), "模组的 PNG 解码器读不了工具箱转出来的图");
        Assert.Equal(w * h * 4, rgba.Length);
        return (w, h);
    }

    // ---------------------------------------------------------------- 图片转换

    [Theory]
    [InlineData(1920, 1080, 1920, 1080)]   // 正好 16:9
    [InlineData(1000, 1000, 1000, 562)]    // 方图：上下裁掉（562.5 按银行家舍入取偶数）
    [InlineData(1080, 1920, 1080, 608)]    // 竖图（手机截图）
    [InlineData(5120, 2160, 2560, 1440)]   // 超宽 + 超大：先裁 16:9 再缩小
    public void Convert_CropsTo16x9_AndFitsLimit(int w, int h, int ew, int eh)
    {
        var jpg = Encode(new JpegBitmapEncoder(), w, h, PixelFormats.Bgr24);
        var png = HomeBackgroundService.ConvertToModPng(jpg, out var ow, out var oh);
        Assert.Equal(ew, ow);
        Assert.Equal(eh, oh);
        Assert.Equal((ow, oh), DecodeWithMod(png));
    }

    [Fact]
    public void Convert_16BitAndInterlacedPng_BecomeReadableByMod()
    {
        // 模组只认 8-bit 非隔行；这两种原图直接给模组都会失败
        var deep = Encode(new PngBitmapEncoder(), 320, 180, PixelFormats.Rgba64);
        Assert.False(HomeBackgroundModPngDecoder.TryDecode(deep, out _, out _, out _));
        DecodeWithMod(HomeBackgroundService.ConvertToModPng(deep, out _, out _));

        var interlaced = Encode(new PngBitmapEncoder { Interlace = PngInterlaceOption.On }, 320, 180, PixelFormats.Bgra32);
        Assert.False(HomeBackgroundModPngDecoder.TryDecode(interlaced, out _, out _, out _));
        DecodeWithMod(HomeBackgroundService.ConvertToModPng(interlaced, out _, out _));
    }

    [Fact]
    public void Convert_RespectsExifOrientation()
    {
        // 手机竖着拍：像素是横的 1600×900，EXIF 写着「顺时针转 90°」，摆正后是竖图 900×1600 → 裁成 900×506
        var meta = new BitmapMetadata("jpg");
        meta.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
        var jpg = Encode(new JpegBitmapEncoder(), 1600, 900, PixelFormats.Bgr24, meta);
        HomeBackgroundService.ConvertToModPng(jpg, out var w, out var h);
        Assert.Equal((900, 506), (w, h));
    }

    // ---------------------------------------------------------------- 安装 / 升级 / 不降级

    [Theory]
    [InlineData("1.1.0", true)]    // 市场装的原版 → 升级成修好闪烁的 1.1.0.1
    [InlineData("1.0.1", true)]
    [InlineData("", true)]
    [InlineData("1.1.0.1", false)]
    [InlineData("1.1.1", false)]   // 原作者以后的新版不降级
    public void BundledModifiedVersion_UpgradesMarket110ButNotNewer(string installed, bool expected)
    {
        Assert.Equal("1.1.0.1", HomeBackgroundService.BundledVersion);
        Assert.Equal(expected, HomeBackgroundService.IsOlder(installed, HomeBackgroundService.BundledVersion));
    }

    [Fact]
    public void FirstImage_InstallsBundledModAndLicense()
    {
        _service.SetImage(_game, WriteImage("a.jpg", Encode(new JpegBitmapEncoder(), 800, 450, PixelFormats.Bgr24)));

        Assert.True(File.Exists(Path.Combine(ModsDir, "HomeBackgroundMod.dll")));
        Assert.True(File.Exists(Path.Combine(ModsDir, "LICENSE")));
        Assert.Equal(
            "8f7781adb8d485e0924ab6573df4021b27950feb228968dce299e218c0600d5b",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(ModsDir, "HomeBackgroundMod.dll")))).ToLowerInvariant());
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(ModsDir, "HomeBackgroundMod.json")));
        Assert.Equal("1.1.0.1", json.RootElement.GetProperty("version").GetString());
        Assert.True(json.RootElement.GetProperty("enabled").GetBoolean());

        var s = _service.GetStatus(_game);
        Assert.Equal("image", s.Mode);
        Assert.Equal((800, 450), (s.ImageWidth, s.ImageHeight));
        Assert.StartsWith("data:image/jpeg;base64,", s.ImagePreview);
    }

    [Fact]
    public void DisabledOldVersion_IsUpgradedAndEnabled_NewerIsNotDowngraded()
    {
        Directory.CreateDirectory(DisabledDir);
        File.WriteAllBytes(Path.Combine(DisabledDir, "HomeBackgroundMod.dll"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(DisabledDir, "HomeBackgroundMod.json"), "{\"id\":\"HomeBackgroundMod\",\"version\":\"1.0.0\",\"enabled\":false}");

        _service.SetHideThemeVideo(_game, false);
        Assert.False(Directory.Exists(DisabledDir));
        Assert.Equal(17920, new FileInfo(Path.Combine(ModsDir, "HomeBackgroundMod.dll")).Length);
        Assert.False(_service.GetStatus(_game).HideThemeVideo);

        // 用户从市场装了更新的版本：工具箱不覆盖
        File.WriteAllBytes(Path.Combine(ModsDir, "HomeBackgroundMod.dll"), [9]);
        File.WriteAllText(Path.Combine(ModsDir, "HomeBackgroundMod.json"), "{\"id\":\"HomeBackgroundMod\",\"version\":\"9.0.0\",\"enabled\":true}");
        _service.SetHideThemeVideo(_game, true);
        Assert.Equal(1, new FileInfo(Path.Combine(ModsDir, "HomeBackgroundMod.dll")).Length);
    }

    [Fact]
    public void WithoutLoader_ReportsAndRefuses()
    {
        File.Delete(Path.Combine(_game, "version.dll"));
        Assert.False(_service.GetStatus(_game).LoaderInstalled);
        var ex = Assert.Throws<InvalidOperationException>(() => _service.SetHideThemeVideo(_game, true));
        Assert.Contains("加载器", ex.Message);
    }

    // ---------------------------------------------------------------- 图片 / 视频 / 恢复默认

    [Fact]
    public void ImageVideoImage_SwitchesFilesAndConfig()
    {
        var img = WriteImage("a.png", Encode(new PngBitmapEncoder(), 640, 360, PixelFormats.Bgra32));
        _service.SetImage(_game, img);
        var first = Directory.GetFiles(Path.Combine(ModsDir, "assets"), "bg-toys-*.png").Single();

        // 再换一张：新文件名（让模组的「配置变了」生效），旧的清掉
        System.Threading.Thread.Sleep(5);
        _service.SetImage(_game, img);
        var second = Directory.GetFiles(Path.Combine(ModsDir, "assets"), "bg-toys-*.png").Single();
        Assert.NotEqual(first, second);
        Assert.Equal("assets/" + Path.GetFileName(second), _service.ReadConfig(_game).Image);

        var video = WriteImage("clip.mp4", [0, 0, 0, 24, 102, 116, 121, 112]);
        _service.SetVideo(_game, video);
        Assert.True(File.Exists(Path.Combine(ModsDir, "bg.mp4")));
        Assert.Equal("video", _service.GetStatus(_game).Mode);
        Assert.Equal("bg.mp4", _service.ReadConfig(_game).Video);

        // 换回图片：模组总是先找视频，所以要把那份视频删掉；用户的原视频不动
        _service.SetImage(_game, img);
        Assert.False(File.Exists(Path.Combine(ModsDir, "bg.mp4")));
        Assert.True(File.Exists(video));
        Assert.Equal("image", _service.GetStatus(_game).Mode);
    }

    // ---------------------------------------------------------------- 视频编码（游戏的播放器只认 H.264 mp4 / VP8 webm）

    private static byte[] FakeWebm(string codecId) =>
        [0x1A, 0x45, 0xDF, 0xA3, .. System.Text.Encoding.ASCII.GetBytes("....webm....\x86" + codecId + "...."), .. new byte[64]];

    /// <summary>最小的 mp4 片段：ftyp + 一个 stsd（版本/标志、条目数 1、条目大小、样本类型）。</summary>
    private static byte[] FakeMp4(string fourcc) =>
        [0, 0, 0, 16, .. "ftypisom"u8.ToArray(), 0, 0, 0, 0,
         0, 0, 0, 40, .. "stsd"u8.ToArray(), 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 24, .. System.Text.Encoding.ASCII.GetBytes(fourcc), .. new byte[16]];

    [Theory]
    [InlineData("clip.webm", "V_VP9", "VP9")]
    [InlineData("clip.webm", "V_VP8", "VP8")]
    [InlineData("clip.webm", "V_AV1", "AV1")]
    public void DetectCodec_Webm(string name, string codecId, string expected)
    {
        Assert.Equal(expected, HomeBackgroundService.DetectVideoCodec(WriteImage(name, FakeWebm(codecId))));
    }

    [Theory]
    [InlineData("avc1", "H.264")]
    [InlineData("hvc1", "H.265 (HEVC)")]
    [InlineData("av01", "AV1")]
    public void DetectCodec_Mp4(string fourcc, string expected)
    {
        Assert.Equal(expected, HomeBackgroundService.DetectVideoCodec(WriteImage("clip.mp4", FakeMp4(fourcc))));
    }

    [Fact]
    public void SetVideo_RejectsCodecsTheGameCannotPlay_AndLeavesNothingBehind()
    {
        var vp9 = WriteImage("vp9.webm", FakeWebm("V_VP9"));
        var ex = Assert.Throws<InvalidOperationException>(() => _service.SetVideo(_game, vp9));
        Assert.Contains("VP9", ex.Message);
        Assert.Contains("H.264", ex.Message);
        Assert.False(File.Exists(Path.Combine(ModsDir, "bg.webm")));

        Assert.Throws<InvalidOperationException>(() => _service.SetVideo(_game, WriteImage("h265.mp4", FakeMp4("hvc1"))));
        Assert.False(File.Exists(Path.Combine(ModsDir, "bg.mp4")));

        _service.SetVideo(_game, WriteImage("vp8.webm", FakeWebm("V_VP8")));
        Assert.True(File.Exists(Path.Combine(ModsDir, "bg.webm")));
        _service.SetVideo(_game, WriteImage("h264.mp4", FakeMp4("avc1")));
        Assert.True(File.Exists(Path.Combine(ModsDir, "bg.mp4")));
        Assert.False(File.Exists(Path.Combine(ModsDir, "bg.webm"))); // 换成 mp4 时旧的 webm 删掉
        Assert.Equal("", _service.GetStatus(_game).VideoProblem);
    }

    [Fact]
    public void Status_FlagsExistingUnplayableVideo()
    {
        // 修复前放进去的 VP9 视频：页面要说清楚为什么主页会闪
        _service.SetHideThemeVideo(_game, true);
        File.WriteAllBytes(Path.Combine(ModsDir, "bg.webm"), FakeWebm("V_VP9"));
        var cfg = ConfigFile;
        File.WriteAllText(cfg, File.ReadAllText(cfg).Replace("\"bg.mp4\"", "\"bg.webm\""));

        var s = _service.GetStatus(_game);
        Assert.Equal("video", s.Mode);
        Assert.Equal("VP9", s.VideoCodec);
        Assert.Contains("VP9", s.VideoProblem);
    }

    [Fact]
    public void Config_AlwaysWritesAllFourFields()
    {
        _service.SetHideThemeVideo(_game, false);
        var cfgPath = ConfigFile;
        using var doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
        foreach (var key in new[] { "enable", "hideThemeVideo", "video", "image" })
            Assert.True(doc.RootElement.TryGetProperty(key, out _), "缺少配置字段 " + key);
    }

    /// <summary>
    /// 4.0.0 起模组只读模组目录里的 config.json：旧版集中目录里的文件不再被读，
    /// 工具箱也不再改写它（加载器启动时会把整个 configs 改名 configs.bak）。
    /// </summary>
    [Fact]
    public void LegacyConfigsFolder_IsIgnoredAndNotRewritten()
    {
        var legacy = Path.Combine(_game, ModManager.LoaderFolderName, "configs", HomeBackgroundService.ModId + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "{\"enable\":false,\"hideThemeVideo\":true,\"video\":\"bg.mp4\",\"image\":\"assets/x.png\"}");
        var before = File.ReadAllText(legacy);

        _service.SetHideThemeVideo(_game, false);

        var current = ConfigFile;
        Assert.True(File.Exists(current), "配置应写在模组目录里");
        Assert.Contains("\"hideThemeVideo\": false", File.ReadAllText(current));

        // 旧文件原样不动，也不再影响读到的配置
        Assert.Equal(before, File.ReadAllText(legacy));
        var cfg = _service.ReadConfig(_game);
        Assert.False(cfg.HideThemeVideo);
        Assert.True(cfg.Enable);
    }

    [Fact]
    public void RestoreDefault_DisablesButKeepsFiles()
    {
        _service.SetImage(_game, WriteImage("a.jpg", Encode(new JpegBitmapEncoder(), 640, 360, PixelFormats.Bgr24)));
        _service.RestoreDefault(_game);

        Assert.True(File.Exists(Path.Combine(DisabledDir, "HomeBackgroundMod.dll")));
        var s = _service.GetStatus(_game);
        Assert.Equal("off", s.Mode);
        Assert.False(s.ModEnabled);
        Assert.False(string.IsNullOrEmpty(s.ImagePreview)); // 之前的图还在，页面上灰着显示

        // 再选图片又启用回来
        _service.SetImage(_game, WriteImage("b.jpg", Encode(new JpegBitmapEncoder(), 640, 360, PixelFormats.Bgr24)));
        Assert.Equal("image", _service.GetStatus(_game).Mode);
    }

    // ---------------------------------------------------------------- 分发器

    [Fact]
    public void EveryHomeBgMessage_IsRoutedByDispatcher()
    {
        var handler = File.ReadAllText(Path.Combine(TestPaths.AppSourceDirectory, "HybridWindow.HomeBackground.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(TestPaths.AppSourceDirectory, "HybridWindow.Dispatcher.cs"));
        var handled = Regex.Matches(handler, "case \"(homeBg\\w+)\":").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(handled);
        var missing = handled.Where(t => !dispatcher.Contains($"case \"{t}\":")).ToList();
        Assert.True(missing.Count == 0, "分发器没有登记：" + string.Join(", ", missing));
    }
}
