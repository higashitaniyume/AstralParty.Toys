using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace AstralParty.Toys.Mobile.Payload;

/// <summary>载荷里一个要上设备的文件（相对载荷根）。来自目录时 <see cref="Bytes"/> 为空，按需从磁盘读。</summary>
public sealed record MobilePayloadFile(string RelativePath, long Length, byte[]? Bytes = null);

/// <summary>
/// 移动端载荷：给设备装加载器时要投放的那份文件。
///
/// **只有一个来源**：内嵌的 <c>cesium-loader-android-managed.zip</c>（安卓仓库每次发版产出的托管载荷包，
/// 内含引导 + SDK + API 桥 + <c>loader.json</c>），和桌面那包走同一条路：一个 zip、逐文件校验、整包解包。
/// 版本从包内 <c>loader.json</c> 读，不在代码里写死，所以不会出现「包里换了、Toys 还是旧的」这种漂移。
///
/// 也支持直接给一个目录（第三方自备载荷），布局按目录里是什么就是什么。
/// </summary>
public sealed class MobilePayload
{
    /// <summary>新名（改名后）。</summary>
    public const string BootstrapFile = "CesiumLoader.Mobile.Bootstrap.dll";

    /// <summary>旧名。改名要加载器产物一起换，所以读取端两个名字都认。</summary>
    public const string LegacyBootstrapFile = "CesiumLoader.Android.Bootstrap.dll";

    /// <summary>按「新名优先、旧名兜底」的顺序列出可能的引导文件名。</summary>
    public static readonly string[] BootstrapFileCandidates = [BootstrapFile, LegacyBootstrapFile];

    public const string SdkRelative = "sdk/CesiumLoader.SDK.dll";

    public const string ManifestFile = "loader.json";

    /// <summary>
    /// 载荷里的相对路径 → 设备加载器目录下的相对路径。两端布局不同：
    /// 安卓把引导放在 <c>bootstrap/</c>（与安卓版 App 的安装结果一致），iOS 平铺在根。
    /// 托管包里本来就是 <c>bootstrap/xxx</c>，所以这里对两种来源形状都做归一化，映射只此一处。
    /// </summary>
    public static string DeviceRelativePath(string payloadRelative, bool android)
    {
        var normalized = payloadRelative.Replace('\\', '/');
        var name = normalized.Contains('/') ? normalized[(normalized.LastIndexOf('/') + 1)..] : normalized;
        if (!BootstrapFileCandidates.Contains(name, StringComparer.Ordinal))
            return normalized;

        return android ? "bootstrap/" + name : name;
    }

    /// <summary>
    /// 只有带这个符号的 SDK 才会在「原生后端不可用」时退回 Unity 后端。
    /// 桌面构建的 SDK 少了这条兜底，手机上热键模组会每帧抛 <c>DllNotFoundException(user32.dll)</c> 把游戏卡死。
    /// </summary>
    // 标记是方法名本身：命名空间在 #Strings 堆里是独立条目，拼全名反而搜不到（参考实现同样只搜方法名）。
    public const string MobileCapableMarker = "MarkNativeUnavailable";

    /// <summary>确认要装原样文件时的放行开关（与 iOS 侧同名）。</summary>
    public const string AllowDesktopSdkEnv = "CESIUM_ALLOW_DESKTOP_SDK";

    private static readonly HashSet<string> GlobalSkipSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdb", ".lib", ".exp", ".iobj", ".ipdb",
    };

    private static readonly HashSet<string> RootOnlySkipSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".xml",
    };

    private static bool ShouldSkipEntry(string relative)
    {
        var ext = Path.GetExtension(relative);
        if (GlobalSkipSuffixes.Contains(ext))
            return true;
        return !relative.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) &&
               RootOnlySkipSuffixes.Contains(ext);
    }

    private MobilePayload(string root, IReadOnlyList<MobilePayloadFile> files, int skipped, string version, bool archive)
    {
        Root = root;
        Files = files;
        Skipped = skipped;
        Version = version;
        IsArchive = archive;
    }

    /// <summary>目录载荷是目录路径，包载荷是 zip 文件路径。</summary>
    public string Root { get; }

    public bool IsArchive { get; }

    public IReadOnlyList<MobilePayloadFile> Files { get; }

    /// <summary>被跳过的非运行时文件数（调试符号 / 说明 / 许可证）。</summary>
    public int Skipped { get; }

    /// <summary>载荷版本：包内 <c>loader.json</c> 的 <c>version</c>，没有就退回引导程序集版本。</summary>
    public string Version { get; }

    /// <summary>这份载荷实际使用的引导文件名（新名或旧名）。</summary>
    public string Bootstrap { get; init; } = BootstrapFile;

    /// <summary>取文件内容：包载荷直接用内存里的字节，目录载荷读磁盘。</summary>
    public byte[] BytesOf(MobilePayloadFile file) =>
        file.Bytes ?? File.ReadAllBytes(Path.Combine(Root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>SDK 的程序集版本（读不到留空）。</summary>
    public string SdkVersion
    {
        get
        {
            var sdk = Files.FirstOrDefault(f =>
                string.Equals(f.RelativePath, SdkRelative, StringComparison.OrdinalIgnoreCase));
            return sdk is null ? "" : AssemblyVersionOf(BytesOf(sdk));
        }
    }

    /// <summary>读一份载荷：给 <c>.zip</c> 走包路径，给目录走目录路径。</summary>
    public static MobilePayload Read(string path)
    {
        var full = Path.GetFullPath(path);
        return File.Exists(full) ? ReadArchive(full) : ReadDirectory(full);
    }

    private static MobilePayload ReadArchive(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var files = new List<MobilePayloadFile>();
        var skipped = 0;

        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (relative.EndsWith('/') || relative.Length == 0)
                continue;   // 目录条目

            // 包根可能带一层目录（托管包是平的，但第三方包不一定）
            if (ShouldSkipEntry(relative))
            {
                skipped++;
                continue;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            files.Add(new MobilePayloadFile(relative, entry.Length, buffer.ToArray()));
        }

        return Build(zipPath, files, skipped, archive: true);
    }

    private static MobilePayload ReadDirectory(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"载荷不存在：{root}");

        var files = new List<MobilePayloadFile>();
        var skipped = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (ShouldSkipEntry(relative))
            {
                skipped++;
                continue;
            }

            files.Add(new MobilePayloadFile(relative, new FileInfo(path).Length));
        }

        return Build(root, files, skipped, archive: false);
    }

    private static MobilePayload Build(string root, List<MobilePayloadFile> files, int skipped, bool archive)
    {
        if (files.Count == 0)
            throw new InvalidDataException("载荷是空的");

        var bootstrap = files.Select(f => f.RelativePath)
            .FirstOrDefault(path => BootstrapFileCandidates.Contains(
                path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path, StringComparer.Ordinal));
        if (bootstrap is null)
            throw new InvalidDataException(
                $"载荷里缺少引导 DLL（{string.Join(" 或 ", BootstrapFileCandidates)}）；" +
                "注入的 cctor 会去 CesiumLoader/<引导名> 加载它");

        var ordered = files.OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList();
        var payload = new MobilePayload(root, ordered, skipped, "", archive) { Bootstrap = bootstrap };

        var version = payload.ReadVersion();
        if (version.Length == 0)
            version = payload.SdkVersion;
        return new MobilePayload(root, ordered, skipped, version, archive) { Bootstrap = bootstrap };
    }

    private string ReadVersion()
    {
        var manifest = Files.FirstOrDefault(f =>
            string.Equals(f.RelativePath, ManifestFile, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
            return "";
        try
        {
            using var document = JsonDocument.Parse(BytesOf(manifest));
            return document.RootElement.TryGetProperty("version", out var version) &&
                   version.ValueKind == JsonValueKind.String
                ? version.GetString() ?? ""
                : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string AssemblyVersionOf(byte[] bytes)
    {
        var temp = Path.Combine(Path.GetTempPath(), "cesium-payload-" + Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            File.WriteAllBytes(temp, bytes);
            return AssemblyName.GetAssemblyName(temp).Version?.ToString() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
        finally
        {
            try { File.Delete(temp); }
            catch (IOException) { /* 临时文件清理尽力而为 */ }
        }
    }

    /// <summary>
    /// 挡住跑不起来的 SDK。没有 SDK 时不表态（只投引导也能让加载器自己起来）。
    /// </summary>
    public void EnsureSdkUsable(bool allowDesktopSdk)
    {
        if (allowDesktopSdk)
            return;

        var sdk = Files.FirstOrDefault(f =>
            string.Equals(f.RelativePath, SdkRelative, StringComparison.OrdinalIgnoreCase));
        if (sdk is null)
            return;

        if (ContainsMarker(BytesOf(sdk), MobileCapableMarker))
            return;

        throw new InvalidDataException(
            $"{SdkRelative} 缺少「原生后端不可用就退回 Unity」的兜底（不含 MobilePlatform.{MobileCapableMarker}）——" +
            "手机上热键模组会每帧抛 DllNotFoundException(user32.dll)，游戏会卡在大厅或对局里。" +
            $"请换成新版加载器的移动端构建 SDK；确认要装原样文件时设 {AllowDesktopSdkEnv}=1。");
    }

    private static bool ContainsMarker(byte[] data, string marker)
    {
        var needle = System.Text.Encoding.ASCII.GetBytes(marker);
        return data.AsSpan().IndexOf(needle) >= 0;
    }
}
