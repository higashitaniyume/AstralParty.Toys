using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Modding;
using AstralParty.Toys.Services.Speedhack;

namespace AstralParty.Toys.Services.Skins;

/// <summary>「自定义 → 主页背景」页面显示用的状态。只放真实读到的东西。</summary>
public sealed class HomeBackgroundStatus
{
    public string GameDirectory { get; set; } = "";
    public bool LoaderInstalled { get; set; }
    public bool ModInstalled { get; set; }
    public bool ModEnabled { get; set; }
    public string InstalledVersion { get; set; } = "";
    public string BundledVersion { get; set; } = "";
    /// <summary>off（没装或停用）/ none（启用了但没有背景文件）/ image / video。和模组自己的判断顺序一致：有视频先用视频。</summary>
    public string Mode { get; set; } = "off";
    public bool ConfigEnabled { get; set; } = true;
    public bool HideThemeVideo { get; set; } = true;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    /// <summary>当前背景图的缩略图（data:image/jpeg;base64,…），没有图片时为空。</summary>
    public string ImagePreview { get; set; } = "";
    public string VideoFile { get; set; } = "";
    public long VideoSize { get; set; }
    /// <summary>从文件里认出的视频编码（H.264 / VP8 / VP9…），认不出为空。</summary>
    public string VideoCodec { get; set; } = "";
    /// <summary>当前视频游戏放不了时的说明（例如 VP9），否则为空。</summary>
    public string VideoProblem { get; set; } = "";
    public bool GameRunning { get; set; }
    public string ModDirectory { get; set; } = "";
}

/// <summary>
/// 把 TenkinMori 的「自定义主页背景」模组（HomeBackgroundMod，MIT，随工具箱分发）接进工具箱。
/// <para>
/// 模组怎么读背景（读自 1.1.0 源码）：配置 <c>enable / hideThemeVideo / video / image</c>；
/// 先找视频（<c>mods\HomeBackgroundMod\{video}</c>、<c>assets\{video}</c>、<c>bg.mp4</c>），没有再用图片；
/// 图片只认 8-bit 非隔行 PNG，铺满屏幕（会拉伸）。配置变了会重新加载图片，但视频每次启动游戏只试一次。
/// </para>
/// <para>
/// 随包的是 1.1.0.1 修改版：视频准备好才换上，10 秒没准备好就退回图片（原版会一直闪）。
/// 详见 <c>Resources\ThirdPartyMods\HomeBackgroundMod\README.md</c>。
/// </para>
/// </summary>
public sealed class HomeBackgroundService
{
    public const string ModId = "HomeBackgroundMod";
    public const string ModFileName = ModId + ".dll";
    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"];
    public static readonly string[] VideoExtensions = [".mp4", ".webm"];

    private const string ResourcePrefix = "AstralParty.Toys.ThirdParty.HomeBackgroundMod.";
    private const string ManagedImagePrefix = "bg-toys-";
    private const int MaxWidth = 2560, MaxHeight = 1440;

    private readonly ModManager _mods;

    public HomeBackgroundService(ModManager mods) => _mods = mods;

    // ------------------------------------------------------------ 随包文件

    private static byte[]? ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name);
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string BundledVersion
    {
        get
        {
            try
            {
                var json = ReadResource(ModId + ".json");
                if (json == null) return "";
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            }
            catch { return ""; }
        }
    }

    // ------------------------------------------------------------ 位置

    private static string LoaderRoot(string game) => Path.Combine(game, ModManager.LoaderFolderName);

    private static bool LoaderInstalled(string game) =>
        Directory.Exists(LoaderRoot(game)) && File.Exists(Path.Combine(game, "version.dll"));

    /// <summary>模组文件夹：已经停用（在 disabled_mods）就在那里，否则在 mods。</summary>
    public static string ModDirectory(string game)
    {
        var disabled = Path.Combine(LoaderRoot(game), ModManager.DisabledModsFolderName, ModId);
        if (File.Exists(Path.Combine(disabled, ModFileName))) return disabled;
        return Path.Combine(LoaderRoot(game), ModManager.ModsFolderName, ModId);
    }

    private static bool IsEnabledLocation(string modDir) =>
        string.Equals(Path.GetFileName(Path.GetDirectoryName(modDir)), ModManager.ModsFolderName, StringComparison.OrdinalIgnoreCase);

    private static string ReadVersion(string modDir)
    {
        try
        {
            var path = Path.Combine(modDir, ModId + ".json");
            if (!File.Exists(path)) return "";
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
        }
        catch { return ""; }
    }

    // ------------------------------------------------------------ 状态

    public HomeBackgroundStatus GetStatus(string game)
    {
        var status = new HomeBackgroundStatus
        {
            GameDirectory = game,
            LoaderInstalled = LoaderInstalled(game),
            BundledVersion = BundledVersion,
            GameRunning = SpeedhackManager.IsGameRunning()
        };
        var modDir = ModDirectory(game);
        status.ModDirectory = modDir;
        status.ModInstalled = File.Exists(Path.Combine(modDir, ModFileName));
        if (!status.ModInstalled) return status;

        status.ModEnabled = IsEnabledLocation(modDir);
        status.InstalledVersion = ReadVersion(modDir);
        var cfg = ReadConfig(game);
        status.ConfigEnabled = cfg.Enable;
        status.HideThemeVideo = cfg.HideThemeVideo;

        var video = ResolveVideo(modDir, cfg.Video);
        var image = ResolveImage(modDir, cfg.Image);
        if (video != null)
        {
            status.VideoFile = Path.GetFileName(video);
            try { status.VideoSize = new FileInfo(video).Length; } catch { }
            status.VideoCodec = DetectVideoCodec(video) ?? "";
            if (status.VideoCodec.Length > 0 && !IsSupportedCodec(status.VideoCodec, Path.GetExtension(video).ToLowerInvariant()))
                status.VideoProblem = UnsupportedCodecMessage(status.VideoCodec);
        }
        if (image != null)
        {
            try
            {
                var (w, h, preview) = ReadPreview(image);
                status.ImageWidth = w;
                status.ImageHeight = h;
                status.ImagePreview = preview;
            }
            catch { }
        }
        status.Mode = !status.ModEnabled || !cfg.Enable ? "off" : video != null ? "video" : image != null ? "image" : "none";
        return status;
    }

    // 和模组的 ResolveVideoPath 一样的三个位置
    private static string? ResolveVideo(string modDir, string video)
    {
        var rel = string.IsNullOrEmpty(video) ? "bg.mp4" : video.Replace('/', Path.DirectorySeparatorChar);
        foreach (var p in new[] { Path.Combine(modDir, rel), Path.Combine(modDir, "assets", rel), Path.Combine(modDir, "bg.mp4") })
            if (File.Exists(p)) return p;
        return null;
    }

    private static string? ResolveImage(string modDir, string image)
    {
        var rel = string.IsNullOrEmpty(image) ? "assets/bg.png" : image;
        rel = rel.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.IsPathRooted(rel) ? rel : Path.Combine(modDir, rel);
        return File.Exists(path) ? path : null;
    }

    // ------------------------------------------------------------ 配置（走 ModManager，和「模组」页的配置编辑同一套读写）

    internal sealed record Config(bool Enable, bool HideThemeVideo, string Video, string Image);

    /// <summary>
    /// 加载器（SDK 3.8.5 的 ModConfig）实际读的那份：<c>configs\{ModId}.json</c> 存在就读它，
    /// 否则才读 <c>mods\{ModId}\config.json</c>。工具箱读写都落在 configs（见 ModManager 的配置路径），
    /// 所以从市场装的模组（没带 config.json、第一次运行才在 configs 里建）也能正常读到。
    /// </summary>
    internal static string ConfigPath(string game) =>
        Path.Combine(LoaderRoot(game), "configs", ModId + ".json");

    internal Config ReadConfig(string game)
    {
        bool enable = true, hide = true;
        string video = "bg.mp4", image = "assets/bg.png";
        var external = ConfigPath(game);
        if (File.Exists(external))
        {
            // 加载器读的是这份，就以它为准
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(external), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var r = doc.RootElement;
                if (r.TryGetProperty("enable", out var e) && e.ValueKind is JsonValueKind.True or JsonValueKind.False) enable = e.GetBoolean();
                if (r.TryGetProperty("hideThemeVideo", out var h) && h.ValueKind is JsonValueKind.True or JsonValueKind.False) hide = h.GetBoolean();
                if (r.TryGetProperty("video", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } vs) video = vs;
                if (r.TryGetProperty("image", out var i) && i.ValueKind == JsonValueKind.String && i.GetString() is { Length: > 0 } iss) image = iss;
                return new Config(enable, hide, video, image);
            }
            catch { /* 坏了就退回下面的读法 */ }
        }
        foreach (var f in _mods.ReadModConfigFields(game, ModFileName))
        {
            switch (f.Name)
            {
                case "enable": enable = f.BoolValue; break;
                case "hideThemeVideo": hide = f.BoolValue; break;
                case "video": if (!string.IsNullOrEmpty(f.StringValue)) video = f.StringValue; break;
                case "image": if (!string.IsNullOrEmpty(f.StringValue)) image = f.StringValue; break;
            }
        }
        return new Config(enable, hide, video, image);
    }

    /// <summary>
    /// 四个字段一起写（缺字段会被当成默认值）。走 ModManager 的配置写入，落点是
    /// <c>configs\{ModId}.json</c> —— 加载器只优先读它，写到 mods 那份游戏里不会生效。
    /// </summary>
    private void SaveConfig(string game, Config cfg)
    {
        _mods.SaveModConfigFields(game, ModFileName,
        [
            new ModConfigField { Name = "enable", Kind = "bool", BoolValue = cfg.Enable },
            new ModConfigField { Name = "hideThemeVideo", Kind = "bool", BoolValue = cfg.HideThemeVideo },
            new ModConfigField { Name = "video", Kind = "string", StringValue = cfg.Video },
            new ModConfigField { Name = "image", Kind = "string", StringValue = cfg.Image },
        ]);
    }

    // ------------------------------------------------------------ 安装 / 启用

    /// <summary>
    /// 确保模组已装好并启用：没装就用随包的 1.1.0.1 装上；装的是旧版就升级；已经是更新的版本（比如市场装的）不降级。
    /// 停用的会搬回 mods。
    /// </summary>
    public string EnsureInstalledAndEnabled(string game)
    {
        if (!LoaderInstalled(game)) throw new InvalidOperationException("还没有安装加载器：先到「模组」页安装 CesiumLoader");
        var modDir = ModDirectory(game);
        var installed = ReadVersion(modDir);
        var bundled = BundledVersion;
        var needWrite = !File.Exists(Path.Combine(modDir, ModFileName)) || IsOlder(installed, bundled);
        if (needWrite)
        {
            if (SpeedhackManager.IsGameRunning() && File.Exists(Path.Combine(modDir, ModFileName)) && IsEnabledLocation(modDir))
                throw new InvalidOperationException("游戏正在运行，模组文件被占用：先关闭游戏再设置");
            Directory.CreateDirectory(modDir);
            foreach (var name in new[] { ModFileName, ModId + ".json", "icon.png", "LICENSE" })
            {
                var bytes = ReadResource(name) ?? throw new InvalidOperationException("工具箱缺少随包文件：" + name);
                if (name == ModId + ".json")
                    bytes = System.Text.Encoding.UTF8.GetBytes(WithEnabled(bytes, IsEnabledLocation(modDir)));
                WriteAtomic(Path.Combine(modDir, name), bytes);
            }
        }
        if (!IsEnabledLocation(modDir))
        {
            _mods.ToggleMod(game, ModFileName, true);
            modDir = ModDirectory(game);
        }
        return modDir;
    }

    private static string WithEnabled(byte[] json, bool enabled)
    {
        var node = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        node["enabled"] = enabled;
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    internal static bool IsOlder(string installed, string bundled)
    {
        if (!Version.TryParse(bundled, out var b)) return false;
        return !Version.TryParse(installed, out var i) || i < b;
    }

    // ------------------------------------------------------------ 换图片 / 视频 / 选项 / 恢复默认

    /// <summary>
    /// 换成图片：转成模组能读的 PNG（16:9 居中裁切、最大 2560×1440、8-bit RGBA 非隔行），每次换一个新文件名，
    /// 这样配置一变模组就会立刻重新加载（游戏开着也能换）。会删掉模组文件夹里的视频，否则模组会先用视频。
    /// </summary>
    public void SetImage(string game, string sourcePath)
    {
        var png = ConvertToModPng(File.ReadAllBytes(sourcePath), out _, out _);
        var modDir = EnsureInstalledAndEnabled(game);
        var cfg = ReadConfig(game);
        var video = ResolveVideo(modDir, cfg.Video);
        if (video != null)
        {
            try { File.Delete(video); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException("游戏正在播放现在的视频背景，删不掉它：先关闭游戏再换成图片", ex);
            }
        }

        var assets = Path.Combine(modDir, "assets");
        Directory.CreateDirectory(assets);
        var name = $"{ManagedImagePrefix}{DateTime.Now:yyyyMMdd-HHmmss-fff}.png";
        WriteAtomic(Path.Combine(assets, name), png);
        SaveConfig(game, cfg with { Enable = true, Image = "assets/" + name });
        CleanupManagedImages(assets, keep: name);
    }

    /// <summary>
    /// 换成视频：复制成模组文件夹里的 bg.mp4（或 bg.webm）。模组每次启动游戏只试一次视频，所以要重启游戏。
    /// 游戏自带的播放器（Unity VideoPlayer，Windows）只认 H.264 的 mp4 和 VP8 的 webm：
    /// VP9 会在游戏日志里报 <c>Unsupported video codec 'VP9'</c>，模组却已经把主题背景藏了、盖上一层空白，
    /// 主页就会一闪一闪。所以认得出是不支持的编码就直接拒绝。
    /// </summary>
    public void SetVideo(string game, string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!VideoExtensions.Contains(ext)) throw new InvalidOperationException("只支持 mp4 或 webm 视频");
        var codec = DetectVideoCodec(sourcePath);
        if (codec != null && !IsSupportedCodec(codec, ext))
            throw new InvalidOperationException(UnsupportedCodecMessage(codec));
        var modDir = EnsureInstalledAndEnabled(game);
        var cfg = ReadConfig(game);
        var target = Path.Combine(modDir, "bg" + ext);
        try
        {
            // 先删另一种扩展名的旧视频，免得模组按 bg.mp4 兜底找到旧的
            foreach (var other in VideoExtensions.Where(e => e != ext))
            {
                var old = Path.Combine(modDir, "bg" + other);
                if (File.Exists(old)) File.Delete(old);
            }
            var temp = target + ".toys-tmp";
            File.Copy(sourcePath, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("游戏正在播放现在的视频背景，文件被占用：先关闭游戏再换", ex);
        }
        SaveConfig(game, cfg with { Enable = true, Video = "bg" + ext });
    }

    public void SetHideThemeVideo(string game, bool hide)
    {
        EnsureInstalledAndEnabled(game);
        SaveConfig(game, ReadConfig(game) with { HideThemeVideo = hide });
    }

    /// <summary>恢复游戏默认：停用模组（搬进 disabled_mods，背景文件留着，下次设置时还在）。游戏开着时 dll 被占用，要先关游戏。</summary>
    public void RestoreDefault(string game)
    {
        var modDir = ModDirectory(game);
        if (!File.Exists(Path.Combine(modDir, ModFileName)) || !IsEnabledLocation(modDir)) return;
        try
        {
            _mods.ToggleMod(game, ModFileName, false);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("游戏正在运行，模组文件被占用：先关闭游戏再恢复默认", ex);
        }
    }

    private static void CleanupManagedImages(string assets, string keep)
    {
        foreach (var f in Directory.EnumerateFiles(assets, ManagedImagePrefix + "*.png"))
        {
            if (string.Equals(Path.GetFileName(f), keep, StringComparison.OrdinalIgnoreCase)) continue;
            try { File.Delete(f); } catch { /* 游戏正在读的那张删不掉，下次再清 */ }
        }
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temp = path + ".toys-tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    // ------------------------------------------------------------ 视频编码

    internal static bool IsSupportedCodec(string codec, string ext) =>
        ext == ".webm" ? codec == "VP8" : codec == "H.264";

    internal static string UnsupportedCodecMessage(string codec) =>
        $"这个视频是 {codec} 编码，游戏里的播放器放不了。请转成 H.264 编码的 mp4（最常见；HandBrake、格式工厂等都能转）或 VP8 编码的 webm 再选";

    /// <summary>
    /// 认视频编码（只看容器里写的编码标识，不解码）：webm/mkv 看 CodecID（V_VP8 / V_VP9 / V_AV1…），
    /// mp4/mov 看每条轨道 stsd 里的样本类型（avc1 / hvc1 / av01 / vp09…）。认不出返回 null。
    /// </summary>
    internal static string? DetectVideoCodec(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var head = new byte[Math.Min(fs.Length, 2 * 1024 * 1024)];
            fs.ReadExactly(head);
            // Matroska / WebM：开头是 EBML 头 1A 45 DF A3，轨道信息一般在文件前部
            if (head.Length >= 4 && head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3)
            {
                var text = System.Text.Encoding.ASCII.GetString(head);
                foreach (var (id, name) in new[] { ("V_VP8", "VP8"), ("V_VP9", "VP9"), ("V_AV1", "AV1"), ("V_MPEG4/ISO/AVC", "H.264"), ("V_MPEGH/ISO/HEVC", "H.265 (HEVC)") })
                    if (text.Contains(id, StringComparison.Ordinal)) return name;
                return null;
            }

            // MP4 / MOV：moov 可能在文件末尾，整份按块扫 "stsd"，样本类型在它后面第 12 个字节起
            var map = new Dictionary<string, string>
            {
                ["avc1"] = "H.264", ["avc3"] = "H.264", ["hvc1"] = "H.265 (HEVC)", ["hev1"] = "H.265 (HEVC)",
                ["av01"] = "AV1", ["vp09"] = "VP9", ["vp08"] = "VP8", ["mp4v"] = "MPEG-4 Part 2",
            };
            const int chunk = 1 << 20;
            var buf = new byte[chunk + 32];
            fs.Position = 0;
            long offset = 0;
            int carry = 0;
            while (true)
            {
                int read = fs.Read(buf, carry, chunk);
                if (read <= 0) break;
                int len = carry + read;
                for (int i = 0; i + 20 <= len; i++)
                {
                    if (buf[i] != 's' || buf[i + 1] != 't' || buf[i + 2] != 's' || buf[i + 3] != 'd') continue;
                    var type = System.Text.Encoding.ASCII.GetString(buf, i + 16, 4);
                    if (map.TryGetValue(type, out var name)) return name;
                }
                // 留 20 字节接到下一块前面，免得 "stsd" 正好跨块
                carry = Math.Min(20, len);
                Array.Copy(buf, len - carry, buf, 0, carry);
                offset += read;
            }
        }
        catch { }
        return null;
    }

    // ------------------------------------------------------------ 图片转换

    /// <summary>
    /// 任意常见图片 → 模组能读的 PNG：按 EXIF 方向摆正 → 16:9 居中裁切（模组会把图拉伸铺满屏幕）→
    /// 超过 2560×1440 就缩小 → 8-bit RGBA、不隔行。
    /// </summary>
    internal static byte[] ConvertToModPng(byte[] source, out int width, out int height)
    {
        BitmapSource bmp;
        using (var ms = new MemoryStream(source))
        {
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            bmp = ApplyOrientation(frame, ReadOrientation(frame));
        }

        // 16:9 居中裁切
        int sw = bmp.PixelWidth, sh = bmp.PixelHeight;
        if (sw <= 0 || sh <= 0) throw new InvalidOperationException("图片尺寸无效");
        int cw = sw, ch = (int)Math.Round(sw * 9.0 / 16.0);
        if (ch > sh) { ch = sh; cw = (int)Math.Round(sh * 16.0 / 9.0); }
        cw = Math.Clamp(cw, 1, sw);
        ch = Math.Clamp(ch, 1, sh);
        BitmapSource result = new CroppedBitmap(bmp, new Int32Rect((sw - cw) / 2, (sh - ch) / 2, cw, ch));

        if (cw > MaxWidth || ch > MaxHeight)
        {
            var scale = Math.Min((double)MaxWidth / cw, (double)MaxHeight / ch);
            result = new TransformedBitmap(result, new ScaleTransform(scale, scale));
        }
        result = new FormatConvertedBitmap(result, PixelFormats.Bgra32, null, 0);
        result.Freeze();

        width = result.PixelWidth;
        height = result.PixelHeight;
        var encoder = new PngBitmapEncoder { Interlace = PngInterlaceOption.Off };
        encoder.Frames.Add(BitmapFrame.Create(result));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static int ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata meta && meta.GetQuery("System.Photo.Orientation") is ushort o) return o;
        }
        catch { }
        return 1;
    }

    private static BitmapSource ApplyOrientation(BitmapSource bmp, int orientation)
    {
        Transform? t = orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
            6 => new RotateTransform(90),
            7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
            8 => new RotateTransform(270),
            _ => null
        };
        return t == null ? bmp : new TransformedBitmap(bmp, t);
    }

    /// <summary>页面上的缩略图：最长边 480 的 JPEG（data URL）。</summary>
    private static (int W, int H, string DataUrl) ReadPreview(string path)
    {
        using var fs = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames[0];
        int w = frame.PixelWidth, h = frame.PixelHeight;
        var scale = Math.Min(1.0, 480.0 / Math.Max(w, h));
        BitmapSource thumb = scale < 1 ? new TransformedBitmap(frame, new ScaleTransform(scale, scale)) : frame;
        var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
        encoder.Frames.Add(BitmapFrame.Create(thumb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return (w, h, "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray()));
    }
}
