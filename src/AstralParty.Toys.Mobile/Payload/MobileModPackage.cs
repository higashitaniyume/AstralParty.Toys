using System.IO.Compression;
using System.Text.Json;

namespace AstralParty.Toys.Mobile.Payload;

/// <summary>包里一个要落到设备上的文件（路径相对模组目录）。</summary>
public sealed record MobilePackageEntry(string RelativePath, byte[] Bytes);

/// <summary>
/// 本地模组包（ZIP）的解析与预检。口径对齐桌面版 <c>ModManager.ModImport</c>：
/// 包 64 MB、条目 64 MB、解压总量 128 MB、条目数 8192；拒绝绝对路径 / <c>..</c> / 冒号 / 重复条目 / 非法文件名。
/// 结构要求：包内是一个 <c>&lt;ModId&gt;/</c> 目录（或平铺），里面要有 <c>&lt;ModId&gt;.dll</c> 与 <c>&lt;ModId&gt;.json</c> sidecar。
/// </summary>
public sealed class MobileModPackage
{
    public const long MaxPackageBytes = 64L * 1024 * 1024;
    public const long MaxEntryBytes = 64L * 1024 * 1024;
    public const long MaxTotalBytes = 128L * 1024 * 1024;
    public const int MaxEntries = 8192;

    private MobileModPackage(string modId, string name, string version, IReadOnlyList<string> platforms,
        IReadOnlyList<MobilePackageEntry> entries)
    {
        ModId = modId;
        Name = name;
        Version = version;
        Platforms = platforms;
        Entries = entries;
    }

    public string ModId { get; }

    public string Name { get; }

    public string Version { get; }

    /// <summary>sidecar 里声明的平台；空表示没声明（不设门槛）。</summary>
    public IReadOnlyList<string> Platforms { get; }

    public IReadOnlyList<MobilePackageEntry> Entries { get; }

    public static MobileModPackage Read(string zipPath)
    {
        var info = new FileInfo(zipPath);
        if (!info.Exists)
            throw new FileNotFoundException($"找不到模组包：{zipPath}");
        if (info.Length > MaxPackageBytes)
            throw new InvalidDataException($"模组包超过 {MaxPackageBytes / 1024 / 1024} MB");

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

        // 统一剥掉共同的顶层目录，让相对路径以模组目录为基准
        var prefix = CommonPrefix(files.Select(f => f.Name));
        var stripped = files
            .Select(f => (Name: f.Name[prefix.Length..], f.Bytes))
            .Where(f => f.Name.Length > 0)
            .ToList();

        var sidecars = stripped.Where(f => f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                                            && !f.Name.Contains('/')
                                            && !f.Name.StartsWith('_')).ToList();
        if (sidecars.Count == 0)
            throw new InvalidDataException("包内没有 sidecar（<ModId>.json）");

        var sidecarName = Path.GetFileNameWithoutExtension(sidecars[0].Name);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(sidecars[0].Bytes);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("sidecar 不是合法 JSON：" + ex.Message, ex);
        }

        using (document)
        {
            var data = document.RootElement;
            var declaredId = GetString(data, "id");
            var modId = string.IsNullOrWhiteSpace(declaredId) ? sidecarName : declaredId!;
            if (!string.Equals(modId, sidecarName, StringComparison.Ordinal))
                throw new InvalidDataException($"sidecar 里的 id（{modId}）与文件名（{sidecarName}.json）不一致");

            if (!stripped.Any(f => string.Equals(f.Name, modId + ".dll", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"包内缺少 {modId}.dll");

            var platforms = new List<string>();
            if (data.TryGetProperty("platforms", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                platforms.AddRange(array.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? ""));
            }

            return new MobileModPackage(modId, GetString(data, "name") ?? modId,
                GetString(data, "version") ?? "", platforms,
                stripped.Select(f => new MobilePackageEntry(f.Name, f.Bytes)).ToList());
        }
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
            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException($"包内条目含非法文件名：{name}");
        }
    }

    private static string CommonPrefix(IEnumerable<string> names)
    {
        var list = names.Select(n => n.Split('/')).ToList();
        if (list.All(parts => parts.Length < 2))
            return "";   // 平铺包：没有公共目录

        var first = list[0][0];
        return list.All(parts => parts.Length >= 2 && string.Equals(parts[0], first, StringComparison.Ordinal))
            ? first + "/"
            : "";
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
