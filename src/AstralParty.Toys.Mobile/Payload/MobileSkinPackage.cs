using System.IO.Compression;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Mobile.Payload;

/// <summary>方案名 + 要落到 <c>skins/&lt;方案&gt;/</c> 下的文件（路径相对方案目录）。</summary>
public sealed record MobileSkinEntry(string RelativePath, byte[] Bytes);

/// <summary>
/// 卡面方案包（ZIP）的解析：识别桌面版导入器支持的三种形态，但**目标布局改为设备侧**
/// （<c>skins/&lt;方案&gt;/</c>，相对路径不含 <c>AstralParty_ModLoader</c>）。
///
/// 形态：
/// <list type="bullet">
/// <item>A <c>AstralParty_ModLoader/skins/&lt;方案&gt;/…</c>（官方默认卡面包就是这个形态）</item>
/// <item>B 单一顶层目录 <c>&lt;方案&gt;/…</c></item>
/// <item>C 根目录平铺，以 ZIP 文件名当方案名</item>
/// </list>
/// 上限：包 256 MB / 条目 64 MB / 解压总量 512 MB / 条目数 8192。设备侧要整包读进内存再逐个写，
/// 所以必须有界；卡面方案正常在几十 MB 以内，超出这个量级说明包不对。
/// </summary>
public sealed class MobileSkinPackage
{
    public const long MaxPackageBytes = 256L * 1024 * 1024;
    public const long MaxEntryBytes = 64L * 1024 * 1024;
    public const long MaxTotalBytes = 512L * 1024 * 1024;
    public const int MaxEntries = 8192;

    /// <summary>
    /// 只带这些文件过去：卡面图片，加上方案自身的元数据（<c>skin.json</c>）与卡牌动画清单（<c>*.cardanim</c>）。
    /// 说明文档、缩略图目录之类不进设备。
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".webp",
    };

    private static bool IsCarried(string relativePath)
    {
        var name = relativePath[(relativePath.LastIndexOf('/') + 1)..];
        return ImageExtensions.Contains(Path.GetExtension(name))
               || name.Equals("skin.json", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".cardanim", StringComparison.OrdinalIgnoreCase);
    }

    private MobileSkinPackage(string profileName, IReadOnlyList<MobileSkinEntry> entries, int skipped)
    {
        ProfileName = profileName;
        Entries = entries;
        Skipped = skipped;
    }

    /// <summary>方案名（目录名），已按文件系统安全字符清洗。</summary>
    public string ProfileName { get; }

    public IReadOnlyList<MobileSkinEntry> Entries { get; }

    /// <summary>因为不是图片而被跳过的条目数（在日志里说明，不静默丢）。</summary>
    public int Skipped { get; }

    public static MobileSkinPackage Read(string zipPath)
    {
        var info = new FileInfo(zipPath);
        if (!info.Exists)
            throw new FileNotFoundException($"找不到卡面方案包：{zipPath}");
        if (info.Length > MaxPackageBytes)
            throw new InvalidDataException($"卡面方案包超过 {MaxPackageBytes / 1024 / 1024} MB");

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidDataException($"条目数超过 {MaxEntries}");

        var files = new List<(string Name, byte[] Bytes)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.EndsWith('/'))
                continue;   // 目录条目
            ValidateEntryName(name);
            if (!seen.Add(name))
                throw new InvalidDataException($"包内有重复条目：{name}");
            if (entry.Length > MaxEntryBytes)
                throw new InvalidDataException($"条目超过 {MaxEntryBytes / 1024 / 1024} MB：{name}");

            total += entry.Length;
            if (total > MaxTotalBytes)
                throw new InvalidDataException($"解压总量超过 {MaxTotalBytes / 1024 / 1024} MB");

            using var stream = entry.Open();
            using var buffer = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));
            stream.CopyTo(buffer);
            files.Add((name, buffer.ToArray()));
        }

        if (files.Count == 0)
            throw new InvalidDataException("包是空的");

        var (profile, mapped, skipped) = Map(files.Select(f => f.Name).ToList(), zipPath);
        if (profile.Length == 0)
            throw new InvalidDataException("从包里认不出方案名（顶层目录名不可用）");

        var entries = new List<MobileSkinEntry>();
        for (var i = 0; i < files.Count; i++)
        {
            var relative = mapped[i];
            if (relative is null)
                continue;
            entries.Add(new MobileSkinEntry(relative, files[i].Bytes));
        }

        if (entries.Count == 0)
            throw new InvalidDataException(
                "包里没有卡面文件（支持 png / jpg / jpeg / bmp / tga / webp，外加 skin.json 与 *.cardanim）");

        return new MobileSkinPackage(profile, entries, skipped);
    }

    /// <summary>
    /// 决定方案名与每个条目的方案内相对路径；返回 <c>null</c> 路径表示该条目不带过去。
    /// 与桌面版一致的三形态判定，只是不做落盘。
    /// </summary>
    private static (string Profile, string?[] Paths, int Skipped) Map(IReadOnlyList<string> names, string zipPath)
    {
        const string loaderPrefix = "AstralParty_ModLoader/skins/";

        // 形态 A：带完整加载器路径
        var shapeA = names.Any(n => n.StartsWith(loaderPrefix, StringComparison.OrdinalIgnoreCase));
        string profile;
        string?[] paths;

        if (shapeA)
        {
            profile = "";
            paths = new string?[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (!name.StartsWith(loaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    paths[i] = null;
                    continue;
                }

                var rest = name[loaderPrefix.Length..];
                var slash = rest.IndexOf('/');
                if (slash <= 0)
                {
                    paths[i] = null;
                    continue;
                }

                var candidate = rest[..slash];
                if (profile.Length == 0)
                    profile = candidate;
                // 一个包里混了多个方案时只取第一个（与桌面版 FirstOrDefault 的口径一致），其余的丢弃并计数
                paths[i] = string.Equals(candidate, profile, StringComparison.OrdinalIgnoreCase)
                    ? rest[(slash + 1)..]
                    : null;
            }
        }
        else
        {
            var topDirectories = names
                .Select(n => n.Split('/')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var nested = names.Any(n => n.Contains('/'));

            if (topDirectories.Count == 1 && !string.IsNullOrEmpty(topDirectories[0]) && nested)
            {
                // 形态 B：单一顶层目录
                profile = topDirectories[0];
                paths = names
                    .Select(n => n.Length > profile.Length + 1 ? n[(profile.Length + 1)..] : null)
                    .ToArray();
            }
            else
            {
                // 形态 C：平铺，方案名取 ZIP 文件名
                profile = Sanitize(Path.GetFileNameWithoutExtension(zipPath));
                if (profile.Length == 0)
                    profile = "custom_skin_" + DateTime.Now.ToString("yyyyMMdd");
                paths = names.ToArray();
            }
        }

        var skipped = 0;
        for (var i = 0; i < paths.Length; i++)
        {
            if (paths[i] is null)
            {
                skipped++;
                continue;
            }

            // 方案目录下的子目录结构照搬（有些方案按分类分子目录），只过滤不该带的文件
            if (!IsCarried(paths[i]!))
            {
                paths[i] = null;
                skipped++;
            }
        }

        return (Sanitize(profile), paths, skipped);
    }

    /// <summary>目录名清洗：去掉路径分隔符与非法字符，并拦掉 <c>.</c> / <c>..</c>。</summary>
    internal static string Sanitize(string name)
    {
        var cleaned = new string(name
            .Select(ch => ch is '/' or '\\' || Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned is "." or ".." ? "" : cleaned;
    }

    private static void ValidateEntryName(string name)
    {
        if (name.Length == 0)
            throw new InvalidDataException("包内有空条目名");
        if (name.StartsWith('/') || name.Contains(':'))
            throw new InvalidDataException($"包内有绝对路径条目：{name}");
        foreach (var part in name.Split('/'))
        {
            if (part.Length == 0)
                throw new InvalidDataException($"包内条目路径不合法：{name}");
            if (part is "." or "..")
                throw new InvalidDataException($"包内条目含相对路径：{name}");
        }
    }
}
