using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AstralParty.Toys.Services;

/// <summary>将视频/GIF 转成 CardSkinMod 可直接读取的有界 PNG 序列。</summary>
public sealed record CardAnimationCrop(int X, int Y, int Width, int Height, int SourceWidth, int SourceHeight)
{
    public void Validate()
    {
        if (SourceWidth is < 1 or > 1600 || SourceHeight is < 1 or > 1600 || Width < 1 || Height < 1 || X < 0 || Y < 0
            || Width > SourceWidth - X || Height > SourceHeight - Y || Math.Abs((long)Width * 400 - (long)Height * 404) > 404)
            throw new ArgumentException("动态卡面裁剪区域无效，请重新选择。");
    }
}

public static class CardAnimationConverter
{
    public const int OutputWidth = 404;
    public const int OutputHeight = 400;
    public const string PreviewScale = "scale=w='min(1600,iw)':h='min(1600,ih)':force_original_aspect_ratio=decrease:flags=lanczos,setsar=1";
    public const int Fps = 15;
    public const int MaxFrames = 300;
    public const long MaxPixels = 60_000_000;
    public static bool IsMedia(string path) => new[] { ".gif", ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string? FindFfmpeg()
    {
        var candidates = new List<string>();
        var configured = Environment.GetEnvironmentVariable("ASTRAL_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"));
        candidates.Add(Path.Combine(SpeedhackManager.ResolveProfileDirectory(), "tools", "ffmpeg.exe"));
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(dir)) candidates.Add(Path.Combine(dir.Trim('"'), "ffmpeg.exe"));
        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task<string> EnsureFfmpegAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (FindFfmpeg() is { } existing) return existing;
        if (!Environment.Is64BitOperatingSystem) throw new InvalidOperationException("请将适合本机的 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH 后重试。");
        progress?.Report("首次使用：正在下载 FFmpeg 转换工具…");
        var tools = Path.Combine(SpeedhackManager.ResolveProfileDirectory(), "tools");
        Directory.CreateDirectory(tools);
        var temporary = Path.Combine(tools, Guid.NewGuid().ToString("N") + ".zip.partial");
        var target = Path.Combine(tools, "ffmpeg.exe");
        var executableTemp = target + ".partial";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            using var response = await client.GetAsync("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920]; long size = 0; int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += count;
                    if (size > 256L * 1024 * 1024) throw new InvalidOperationException("转换工具下载超过 256 MB。");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
            }
            using var archive = ZipFile.OpenRead(temporary);
            var entry = archive.Entries.Single(e => e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
            if (entry.Length > 256L * 1024 * 1024) throw new InvalidOperationException("转换工具体积不符合预期。");
            entry.ExtractToFile(executableTemp, true);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(executableTemp, target, true);
            File.WriteAllText(Path.Combine(tools, "ffmpeg-source.txt"), "FFmpeg GPL build by BtbN\nhttps://github.com/BtbN/FFmpeg-Builds\nhttps://ffmpeg.org/legal.html\n");
            return target;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。" + ex.Message, ex);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(executableTemp)) File.Delete(executableTemp);
        }
    }

    public static async Task ExtractPreviewAsync(string executable, string source, string output, CancellationToken cancellationToken)
    {
        if (!IsMedia(source) || !File.Exists(source)) throw new ArgumentException("请选择视频或 GIF 文件。");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", Path.GetFullPath(source), "-map", "0:v:0", "-an", "-vf", PreviewScale, "-frames:v", "1", output }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 FFmpeg。");
        using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var error = await errors;
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("无法读取视频首帧：" + error[..Math.Min(error.Length, 1500)]);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            try { await errors; } catch (OperationCanceledException) { }
            throw;
        }
    }

    public static void ValidateBinding(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name != Path.GetFileName(name)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            throw new ArgumentException("卡面绑定名称不正确。");
    }

    public static async Task ConvertAsync(string executable, string source, string profileDirectory, string binding,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default, CardAnimationCrop? crop = null)
    {
        crop?.Validate();
        ValidateBinding(binding);
        if (!IsMedia(source) || !File.Exists(source)) throw new ArgumentException("请选择视频或 GIF 文件。");
        Directory.CreateDirectory(profileDirectory);
        var staging = Path.Combine(profileDirectory, ".animation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var frames = Path.Combine(staging, binding + ".frames");
            Directory.CreateDirectory(frames);
            var cropFilter = crop == null
                ? "crop=w='min(iw,ih*404/400)':h='min(ih,iw*400/404)':exact=1"
                : $"crop={crop.Width}:{crop.Height}:{crop.X}:{crop.Y}:exact=1";
            var filter = $"fps=15,{PreviewScale},{cropFilter},scale={OutputWidth}:{OutputHeight}:flags=lanczos,setsar=1";
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", Path.GetFullPath(source), "-map", "0:v:0", "-an", "-vf", filter, "-frames:v", "301", "-start_number", "0", "-progress", "pipe:1", Path.Combine(frames, "%06d.png") }) start.ArgumentList.Add(arg);
            progress?.Report("正在转换动态卡面（15 帧/秒，最多 20 秒）…");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 FFmpeg。");
            using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            var updates = ReadProgressAsync(process, progress, cancellationToken);
            string error;
            try
            {
                await process.WaitForExitAsync(cancellationToken);
                await updates;
                error = await errors;
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync();
                try { await Task.WhenAll(errors, updates); } catch (OperationCanceledException) { }
                throw;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidOperationException("视频转换失败：" + error[..Math.Min(error.Length, 1500)]);
            var count = Directory.GetFiles(frames, "*.png").Length;
            if (count is < 1 or > MaxFrames) throw new InvalidOperationException("动态卡面最多 20 秒（300 帧），请先剪辑视频或 GIF 后重试。");
            var (width, height) = ReadPngSize(Path.Combine(frames, "000000.png"));
            if (width != OutputWidth || height != OutputHeight || (long)width * height * count > MaxPixels) throw new InvalidOperationException("动态卡面尺寸或总像素超过限制。");
            for (var i = 0; i < count; i++)
                if (ReadPngSize(Path.Combine(frames, i.ToString("D6") + ".png")) != (width, height)) throw new InvalidOperationException("视频转换产生不一致的帧尺寸。");
            var manifest = Path.Combine(staging, binding + ".cardanim");
            File.WriteAllText(manifest, $"version=1\nfps={Fps}\nframeCount={count}\nwidth={width}\nheight={height}\n", new UTF8Encoding(false));
            cancellationToken.ThrowIfCancellationRequested();
            Install(staging, profileDirectory, binding);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    private static async Task ReadProgressAsync(Process process, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            if (line.StartsWith("frame=", StringComparison.Ordinal) && int.TryParse(line[6..], out var frame)) progress?.Report($"正在转换：{Math.Min(frame, MaxFrames)} / {MaxFrames} 帧上限");
    }

    public static (int Width, int Height) ReadPngSize(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[33];
        stream.ReadExactly(header);
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || BinaryPrimitives.ReadInt32BigEndian(header[8..12]) != 13 || !header[12..16].SequenceEqual("IHDR"u8)) throw new InvalidDataException("无效的 PNG 帧。");
        return (BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
    }

    private static void Install(string staging, string target, string binding)
    {
        var manifest = Path.Combine(target, binding + ".cardanim");
        var frames = Path.Combine(target, binding + ".frames");
        var backup = Path.Combine(staging, ".backup");
        Directory.CreateDirectory(backup);
        var oldManifest = Path.Combine(backup, "old.cardanim");
        var oldFrames = Path.Combine(backup, "old.frames");
        var movedFrames = false; var movedManifest = false;
        try
        {
            if (File.Exists(manifest)) File.Move(manifest, oldManifest);
            if (Directory.Exists(frames)) Directory.Move(frames, oldFrames);
            Directory.Move(Path.Combine(staging, binding + ".frames"), frames); movedFrames = true;
            File.Move(Path.Combine(staging, binding + ".cardanim"), manifest); movedManifest = true;
        }
        catch
        {
            if (movedManifest) File.Delete(manifest);
            if (movedFrames) Directory.Delete(frames, true);
            if (Directory.Exists(oldFrames)) Directory.Move(oldFrames, frames);
            if (File.Exists(oldManifest)) File.Move(oldManifest, manifest);
            throw;
        }
    }
}
