namespace AstralParty.Toys.Services;

/// <summary>
/// 工具箱所有用户数据的唯一根目录。
///
/// 默认位置是「文档\AstralParty.Toys」，用户可以在设置里改成任意文件夹；实际选中的位置记在
/// <c>%LOCALAPPDATA%\AstralParty.Toys\data-root.txt</c>——配置文件本身就住在这个根目录里，
/// 启动时必须先在固定位置知道根在哪，所以只能留这么一个引导指针。
///
/// 只有 WebView2 用户数据目录和多开实例留在 %LOCALAPPDATA%：前者带锁文件、后者的路径要和游戏进程
/// 保持稳定，都不能跟着数据根走。
/// </summary>
public static class ToysPaths
{
    /// <summary>默认数据文件夹（位于用户「文档」下）。</summary>
    public const string DefaultFolderName = "AstralParty.Toys";

    /// <summary>统一配置文件名。</summary>
    public const string ConfigFileName = "AstralParty.Toys.Config.json";

    /// <summary>记录数据根位置的引导指针文件名。</summary>
    public const string PointerFileName = "data-root.txt";

    /// <summary>旧版数据根目录名（文档\AstralPartyReplays），只用于一次性迁移。</summary>
    public const string LegacyFolderName = "AstralPartyReplays";

    public const string ReplaysFolderName = "replays";
    public const string CacheFolderName = "cache";
    public const string LogsFolderName = "logs";
    public const string InstallHistoryFolderName = "install-history";
    public const string StateFolderName = "state";
    public const string BackgroundsFolderName = "backgrounds";
    public const string ToolsFolderName = "tools";
    public const string SpeedhackFolderName = "speedhack";

    private static readonly object Gate = new();
    private static string? _root;
    private static string _note = "";

    // ============================== 固定位置（不跟随数据根） ==============================

    /// <summary>%LOCALAPPDATA%\AstralParty.Toys。</summary>
    public static string LocalDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstralParty.Toys");

    /// <summary>数据根引导指针。</summary>
    public static string PointerFile => Path.Combine(LocalDataRoot, PointerFileName);

    /// <summary>WebView2 用户数据目录（必须留在本地，含锁文件）。</summary>
    public static string WebView2Directory => Path.Combine(LocalDataRoot, "WebView2");

    /// <summary>多开实例目录（必须留在本地）。</summary>
    public static string InstancesDirectory => Path.Combine(LocalDataRoot, "instances");

    /// <summary>%LOCALAPPDATA% 里旧版的封面缓存目录，仅用于迁移。</summary>
    public static string LegacyWorkshopCacheDirectory => Path.Combine(LocalDataRoot, "workshop-covers");

    /// <summary>文档目录下的默认数据根。</summary>
    public static string DefaultRoot
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
                documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(documents, DefaultFolderName);
        }
    }

    /// <summary>文档目录下的旧数据根，仅用于迁移。</summary>
    public static string LegacyDocumentsRoot
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
                documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(documents, LegacyFolderName);
        }
    }

    /// <summary>%APPDATA%\AstralParty.Toys（旧配置位置，仅用于迁移）。</summary>
    public static string LegacyRoamingRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AstralParty.Toys");

    // ============================== 数据根 ==============================

    /// <summary>当前数据根。首次访问时解析引导指针。</summary>
    public static string Root
    {
        get
        {
            lock (Gate)
            {
                return _root ??= ResolveRoot();
            }
        }
    }

    /// <summary>数据根位置的人话说明（正常情况为空）。</summary>
    public static string RootNote
    {
        get
        {
            lock (Gate)
            {
                _ = Root;
                return _note;
            }
        }
    }

    /// <summary>显式指定数据根（测试或启动时已解析过）。</summary>
    public static string Use(string root)
    {
        lock (Gate)
        {
            _root = Path.GetFullPath(root);
            _note = "";
            return _root;
        }
    }

    /// <summary>
    /// 解析数据根：优先读引导指针；指针不可用或目录不可写时回退到默认「文档」目录，
    /// 再不行就退到 %LOCALAPPDATA%\AstralParty.Toys\data。
    /// </summary>
    public static string ResolveRoot(string? localDataRoot = null, string? defaultRoot = null)
    {
        var pointer = Path.Combine(localDataRoot ?? LocalDataRoot, PointerFileName);
        var fallback = defaultRoot ?? DefaultRoot;

        var recorded = ReadPointer(pointer);
        if (!string.IsNullOrWhiteSpace(recorded))
        {
            if (TryEnsureWritable(recorded))
            {
                lock (Gate) _note = "";
                return Path.GetFullPath(recorded);
            }
            lock (Gate) _note = $"数据文件夹不可写（{recorded}），暂时改用 {fallback}。";
        }

        if (TryEnsureWritable(fallback))
        {
            WritePointer(pointer, fallback);
            return Path.GetFullPath(fallback);
        }

        var local = Path.Combine(localDataRoot ?? LocalDataRoot, "data");
        TryEnsureWritable(local);
        lock (Gate) _note = $"文档目录不可写，数据暂时保存在 {local}。";
        return Path.GetFullPath(local);
    }

    private static string? ReadPointer(string pointer)
    {
        try
        {
            if (!File.Exists(pointer)) return null;
            var text = File.ReadAllText(pointer).Trim();
            return text.Length == 0 ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static void WritePointer(string pointer, string root)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pointer)!);
            File.WriteAllText(pointer, Path.GetFullPath(root));
        }
        catch
        {
            // 指不进去就每次重新解析，不影响使用
        }
    }

    /// <summary>把数据根改成 <paramref name="root"/> 并更新引导指针（下次启动生效）。</summary>
    public static void RememberRoot(string root, string? localDataRoot = null)
    {
        var full = Path.GetFullPath(root);
        Directory.CreateDirectory(full);
        WritePointer(Path.Combine(localDataRoot ?? LocalDataRoot, PointerFileName), full);
    }

    /// <summary>
    /// 切换数据根：内存里立刻换成新根（各服务下次访问即生效），同时写引导指针。
    /// 前提是数据已经搬过去了，见 <see cref="DataRootMover"/>。
    /// </summary>
    public static void SwitchRoot(string root, string? localDataRoot = null)
    {
        Use(root);
        RememberRoot(root, localDataRoot);
    }

    /// <summary>真的往目录里写一个临时文件来探测可写性（目录不存在会先创建）。</summary>
    public static bool TryEnsureWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".astral-write-probe-{Guid.NewGuid():N}.tmp");
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ============================== 子目录 ==============================

    public static string ConfigFile => Path.Combine(Root, ConfigFileName);

    /// <summary>回放归档目录（固定，不再单独配置）。</summary>
    public static string ReplaysDirectory => Path.Combine(Root, ReplaysFolderName);

    /// <summary>回放索引目录。</summary>
    public static string ReplayIndexDirectory => Path.Combine(ReplaysDirectory, "_index");

    /// <summary>可随时删除的缓存根。</summary>
    public static string CacheDirectory => Path.Combine(Root, CacheFolderName);

    /// <summary>市场封面 / 贴纸缓存（也是虚拟域名 workshop.* 的根）。</summary>
    public static string WorkshopCacheDirectory => Path.Combine(CacheDirectory, "workshop");

    public static string LogsDirectory => Path.Combine(Root, LogsFolderName);

    /// <summary>加载器 / 模组安装历史。</summary>
    public static string InstallHistoryDirectory => Path.Combine(Root, InstallHistoryFolderName);

    /// <summary>高频写入的状态文件（游戏目录、加载器状态、变速器状态）。</summary>
    public static string StateDirectory => Path.Combine(Root, StateFolderName);

    public static string BackgroundsDirectory => Path.Combine(Root, BackgroundsFolderName);

    public static string ToolsDirectory => Path.Combine(Root, ToolsFolderName);

    public static string SpeedhackDirectory => Path.Combine(Root, SpeedhackFolderName);

    /// <summary>可编辑的变速器主配置（加载器格式，安装时原样拷进游戏目录）。</summary>
    public static string SpeedhackConfigPath => Path.Combine(SpeedhackDirectory, "speedhack_config.json");

    /// <summary>建立运行所需的空目录。缓存目录按需创建，日志目录由日志组件创建。</summary>
    public static void EnsureDirectories()
    {
        foreach (var directory in new[]
                 {
                     Root, ReplaysDirectory, ReplayIndexDirectory, CacheDirectory, WorkshopCacheDirectory,
                     LogsDirectory, StateDirectory, BackgroundsDirectory, ToolsDirectory, SpeedhackDirectory
                 })
        {
            try { Directory.CreateDirectory(directory); }
            catch { /* 个别子目录建不出来不影响启动，用到时再报 */ }
        }
    }
}
