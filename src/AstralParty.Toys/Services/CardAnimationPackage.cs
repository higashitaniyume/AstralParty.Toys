using System.Globalization;
using System.IO;

namespace AstralParty.Toys.Services;

public sealed record CardAnimationPackage(string Binding, int Fps, int FrameCount, int Width, int Height)
{
    public static CardAnimationPackage Read(string manifest)
    {
        RejectLink(manifest);
        RejectLink(Path.GetDirectoryName(manifest)!);
        if (new FileInfo(manifest).Length > 8192) throw new InvalidDataException("动态卡面清单过大。");
        var text = File.ReadAllText(manifest).TrimStart('\ufeff');
        if (text.Length > 4096) throw new InvalidDataException("动态卡面清单过大。");
        var binding = Path.GetFileNameWithoutExtension(manifest);
        CardAnimationConverter.ValidateBinding(binding);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Trim().Split('=');
            if (parts.Length != 2) throw new InvalidDataException("动态卡面清单格式错误。");
            var key = parts[0].Trim(); var number = parts[1].Trim();
            if (key is not ("version" or "fps" or "frameCount" or "width" or "height") || number.Length == 0 || number.Any(c => c < '0' || c > '9')
                || !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || !values.TryAdd(key, value))
                throw new InvalidDataException("动态卡面清单格式错误。");
        }
        if (values.Count != 5 || values.GetValueOrDefault("version") != 1) throw new InvalidDataException("不支持的动态卡面版本。");
        var fps = values.GetValueOrDefault("fps"); var count = values.GetValueOrDefault("frameCount");
        var width = values.GetValueOrDefault("width"); var height = values.GetValueOrDefault("height");
        if (fps is < 1 or > 30 || count is < 1 or > 300 || width is < 1 or > 560 || height is < 1 or > 560 || (long)width * height * count > CardAnimationConverter.MaxPixels)
            throw new InvalidDataException("动态卡面超出帧率、帧数或尺寸限制。");
        var directory = Path.Combine(Path.GetDirectoryName(manifest)!, binding + ".frames");
        RejectLink(directory);
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(directory, i.ToString("D6", CultureInfo.InvariantCulture) + ".png");
            RejectLink(path);
            if (new FileInfo(path).Length > 8L * 1024 * 1024 || CardAnimationConverter.ReadPngSize(path) != (width, height)) throw new InvalidDataException("动态卡面帧缺失、损坏或尺寸不一致。");
        }
        return new(binding, fps, count, width, height);
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("动态卡面不支持符号链接或目录联接。");
    }

    public static void Remove(string directory, string binding)
    {
        CardAnimationConverter.ValidateBinding(binding);
        var manifest = Path.GetFullPath(Path.Combine(directory, binding + ".cardanim"));
        var frames = Path.GetFullPath(Path.Combine(directory, binding + ".frames"));
        // 两个目标都由经过校验的绑定名构造，仅清理同一方案的对应动画。
        if (File.Exists(manifest)) File.Delete(manifest);
        if (Directory.Exists(frames)) Directory.Delete(frames, true);
    }
}
