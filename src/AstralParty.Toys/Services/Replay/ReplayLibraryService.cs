using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AstralParty.Toys.Services.Replay;

/// <summary>
/// 回放库：游戏目录 <c>Temp\Replay</c> 只当"热位"用，长期归档放在库目录。
///
/// 依据（反编译确认）：游戏判定"本地回放已满"只看
/// <c>ListAllReplayIds().Count &gt;= MAX_BATTLE_RECORDS(=10)</c>，
/// 即 <c>Temp\Replay</c> 下有多少个「目录名 == 内部文件名」的子目录，与服务器无关。
/// 因此把回放移出该目录即可立刻腾出席位；需要时再按同样结构复制回去，游戏就能认出它。
/// </summary>
public sealed partial class ReplayLibraryService
{
    /// <summary>游戏客户端写死的本地回放席位上限（Global 配置 MAX_BATTLE_RECORDS）。</summary>
    public const int GameSlotCapacity = 10;

    /// <summary>单局回放的下载上限，与 <c>ReplayAnalyzer</c> 的文件上限保持一致（实测一局约 1 MB）。</summary>
    public const long MaxDownloadBytes = 512L * 1024 * 1024;

    private const string IndexFolderName = "_index";
    private const string OperationsLogName = "operations.log";
    private const string SettingsFileName = "replay-library.json";
    private const int CurrentSchemaVersion = 1;

    internal static readonly JsonSerializerOptions StoreJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record CacheEntry(long Size, long Ticks, ReplayMeta Meta);

    private readonly string _appDirectory;
    private readonly string _profileDirectory;
    private readonly string? _gameDirectoryOverride;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private GameProtocolContext? _protocol;
    private ConfigCatalog? _config;
    private bool _protocolUnavailable;
    private string? _supportedGameVersion;
    private bool _supportedGameVersionProbed;
    private HttpClient? _httpClient;

    public ReplayLibraryService(string appDirectory, string? gameReplayDirectory = null, string? profileDirectory = null)
    {
        _appDirectory = appDirectory;
        _profileDirectory = profileDirectory ?? SpeedhackManager.ResolveProfileDirectory();
        _gameDirectoryOverride = gameReplayDirectory;
        Settings = LoadSettings();
        LibraryRoot = ResolveLibraryRoot(Settings.LibraryRoot);
    }

    public ReplayLibrarySettings Settings { get; private set; }

    /// <summary>库根目录（永远不是游戏目录）。</summary>
    public string LibraryRoot { get; private set; }

    /// <summary>游戏的本地回放热位目录。</summary>
    public string GameDirectory
        => string.IsNullOrWhiteSpace(_gameDirectoryOverride) ? ResolveGameReplayDirectory() : _gameDirectoryOverride!;

    public string SettingsPath => Path.Combine(_profileDirectory, SettingsFileName);

    public string OperationsLogPath => Path.Combine(LibraryRoot, OperationsLogName);

    public string IndexDirectory => Path.Combine(LibraryRoot, IndexFolderName);

    /// <summary>
    /// 本工具内置协议/配表对应的游戏版本（<c>APP_VERSION-RES_VERSION</c>）。
    /// 与游戏 <c>BattleShortRecord.IsValidVersion()</c> 的比较口径一致；读不到时为 null。
    /// </summary>
    public string? SupportedGameVersion
    {
        get
        {
            if (!_supportedGameVersionProbed)
            {
                _supportedGameVersionProbed = true;
                try
                {
                    if (EnsureProtocol())
                    {
                        var path = Path.Combine(_appDirectory, "GameData", "Version.bin");
                        if (File.Exists(path))
                        {
                            var config = _protocol!.Decode("VersionConfigure", File.ReadAllBytes(path));
                            var data = ReflectionValue.Items(ReflectionValue.Get(config, "DataDict"))
                                .FirstOrDefault(item => ReflectionValue.Int(item, "Key") == 1);
                            var value = ReflectionValue.Get(data, "Value");
                            var app = ReflectionValue.Text(value, "AppVersion");
                            var res = ReflectionValue.Text(value, "ResVersion");
                            if (app.Length > 0 && res.Length > 0) _supportedGameVersion = $"{app}-{res}";
                        }
                    }
                }
                catch
                {
                    _supportedGameVersion = null;
                }
            }
            return _supportedGameVersion;
        }
    }

    /// <summary>
    /// 下载用的 HTTP 管道。只有测试会替换它（拿本地假响应跑通「下载 → 落库」全流程，
    /// 让测试套件继续离线可跑）；生产路径永远是下面那份真实 <see cref="HttpClient"/>。
    /// </summary>
    internal HttpMessageHandler? DownloadHandlerOverride { get; set; }

    private HttpClient DownloadClient => _httpClient ??= DownloadHandlerOverride is null
        ? CreateDownloadClient()
        : new HttpClient(DownloadHandlerOverride, disposeHandler: false);

    private static HttpClient CreateDownloadClient()
    {
        // 下载靠 CancellationToken 取消，所以超时交给调用方；UA 与工具其它联网处保持一致。
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstralParty.Toys/" + typeof(ReplayLibraryService).Assembly.GetName().Version);
        return client;
    }

    // ============================== 目录定位 ==============================

    /// <summary>%LocalLow%\feimo\AstralParty_CN\Temp\Replay（兼容国际服目录名）。</summary>
    public static string ResolveGameReplayDirectory()
    {
        var localLow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "feimo");
        var candidates = new[]
        {
            Path.Combine(localLow, "AstralParty_CN", "Temp", "Replay"),
            Path.Combine(localLow, "AstralParty", "_CN", "Temp", "Replay")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    /// <summary>回放库默认就在「文档\AstralPartyReplays」——和工具配置同一个文件夹。</summary>
    public static string DefaultLibraryRoot() => SpeedhackManager.ResolveDocumentsDataDirectory();

    private static string ResolveLibraryRoot(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? DefaultLibraryRoot() : configured!;

    private string GameFile(string replayId) => Path.Combine(GameDirectory, replayId, replayId);

    private string LibraryFile(string replayId) => Path.Combine(LibraryRoot, replayId, replayId);

    private string SidecarFile(string replayId) => Path.Combine(IndexDirectory, replayId + ".json");

    public bool IsSameFolder()
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(GameDirectory).TrimEnd('\\'),
                Path.GetFullPath(LibraryRoot).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // ============================== 设置 ==============================

    private ReplayLibrarySettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new ReplayLibrarySettings();
            var loaded = JsonSerializer.Deserialize<ReplayLibrarySettings>(File.ReadAllText(SettingsPath), StoreJson);
            return Normalize(loaded ?? new ReplayLibrarySettings());
        }
        catch
        {
            return new ReplayLibrarySettings();
        }
    }

    private static ReplayLibrarySettings Normalize(ReplayLibrarySettings settings)
    {
        settings.KeepInGame = Math.Clamp(settings.KeepInGame, 1, GameSlotCapacity);
        if (string.IsNullOrWhiteSpace(settings.LibraryRoot)) settings.LibraryRoot = null;
        settings.Language = AppLanguage.Normalize(settings.Language);
        settings.WorkshopUrl = WorkshopService.NormalizeBaseUrl(settings.WorkshopUrl);
        settings.WorkshopConsentVersion = Math.Max(0, settings.WorkshopConsentVersion);
        settings.GitHubMirrorType = (settings.GitHubMirrorType ?? "").Trim().ToLowerInvariant();
        if (settings.GitHubMirrorType is not (GitHubMirrorHelper.TypeDirect or GitHubMirrorHelper.TypeGhProxy or GitHubMirrorHelper.TypeGhFast or GitHubMirrorHelper.TypeCustom))
        {
            settings.GitHubMirrorType = GitHubMirrorHelper.DefaultMirrorType;
        }
        settings.CustomGitHubMirrorUrl = (settings.CustomGitHubMirrorUrl ?? "").Trim();
        return settings;
    }

    public void SaveSettings(ReplayLibrarySettings settings)
    {
        var current = Settings;
        var prevLang = current.Language;
        var normalized = Normalize(settings);
        // 同意记录没有撤销入口：普通设置保存（语言、库目录、市场地址）不能把它抹掉。
        if (normalized.WorkshopConsentVersion < current.WorkshopConsentVersion)
        {
            normalized.WorkshopConsentVersion = current.WorkshopConsentVersion;
            normalized.WorkshopConsentAt ??= current.WorkshopConsentAt;
        }

        Settings = normalized;
        Directory.CreateDirectory(_profileDirectory);
        WriteAtomic(SettingsPath, JsonSerializer.Serialize(Settings, StoreJson));
        LibraryRoot = ResolveLibraryRoot(Settings.LibraryRoot);
        if (prevLang != null && Settings.Language != prevLang && _config is not null)
        {
            _config.Reload(Settings.Language);
        }
    }

    public void EnsureLibrary()
    {
        Directory.CreateDirectory(LibraryRoot);
        Directory.CreateDirectory(IndexDirectory);
    }

    // ============================== 扫描 ==============================

    public LibrarySnapshot Snapshot()
    {
        var snapshot = new LibrarySnapshot
        {
            GameDirectory = GameDirectory,
            GameDirectoryAvailable = Directory.Exists(GameDirectory),
            LibraryRoot = LibraryRoot,
            LibraryAvailable = Directory.Exists(LibraryRoot),
            KeepInGame = Settings.KeepInGame,
            AutoMaintain = Settings.AutoMaintain,
            SameFolder = IsSameFolder(),
            GameRunning = IsGameRunning()
        };

        if (snapshot.SameFolder)
            snapshot.Warnings.Add("回放库目录与游戏回放目录被设置成了同一个路径，已停用所有归档操作。请在设置里改成别的目录。");

        var merged = new Dictionary<string, ReplayEntry>(StringComparer.Ordinal);

        foreach (var (id, file) in EnumerateReplays(GameDirectory))
        {
            var entry = new ReplayEntry
            {
                ReplayId = id,
                InGame = true,
                GamePath = file,
                SizeBytes = SafeLength(file),
                ModifiedUtc = SafeModified(file)
            };
            merged[id] = entry;
        }

        foreach (var (id, file) in EnumerateReplays(LibraryRoot))
        {
            if (!merged.TryGetValue(id, out var entry))
            {
                entry = new ReplayEntry { ReplayId = id };
                merged[id] = entry;
            }
            entry.InLibrary = true;
            entry.LibraryPath = file;
            if (!entry.InGame)
            {
                entry.SizeBytes = SafeLength(file);
                entry.ModifiedUtc = SafeModified(file);
            }
        }

        foreach (var entry in merged.Values)
        {
            try
            {
                // 库内条目优先用 sidecar + 缓存，避免每次都重读 1MB 文件。
                var source = entry.InLibrary ? entry.LibraryPath! : entry.GamePath!;
                entry.Meta = ReadMeta(entry.ReplayId, source, useSidecar: entry.InLibrary);
            }
            catch (Exception ex)
            {
                snapshot.Warnings.Add($"{entry.ReplayId} 元数据读取失败：{ex.Message}");
            }
        }

        snapshot.Entries = merged.Values
            .OrderByDescending(x => x.SortTime)
            .ThenByDescending(x => x.ReplayId, StringComparer.Ordinal)
            .ToList();
        snapshot.GameCount = snapshot.Entries.Count(x => x.InGame);
        snapshot.LibraryCount = snapshot.Entries.Count(x => x.InLibrary);
        snapshot.LibraryBytes = snapshot.Entries.Where(x => x.InLibrary).Sum(x => x.SizeBytes);
        return snapshot;
    }

    /// <summary>游戏只认「目录名 == 内部文件名」的结构，这里用同一套规则列举。</summary>
    private static IEnumerable<(string Id, string File)> EnumerateReplays(string root)
    {
        if (!Directory.Exists(root)) yield break;
        string[] directories;
        try
        {
            directories = Directory.GetDirectories(root);
        }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var directory in directories)
        {
            var id = Path.GetFileName(directory);
            if (string.IsNullOrEmpty(id)) continue;
            var file = Path.Combine(directory, id);
            if (File.Exists(file)) yield return (id, file);
        }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private static DateTime SafeModified(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.UnixEpoch; }
    }

    /// <summary>注意用游戏主程序的精确进程名判断：工具自己是 AstralParty.Toys.exe，前缀匹配会误判成"游戏在运行"。</summary>
    private static bool IsGameRunning() => SpeedhackManager.IsGameRunning();

    // ============================== 元数据 ==============================

    private ReplayMeta ReadMeta(string replayId, string filePath, bool useSidecar)
    {
        var info = new FileInfo(filePath);
        var ticks = info.LastWriteTimeUtc.Ticks;
        if (_cache.TryGetValue(filePath, out var hit) && hit.Size == info.Length && hit.Ticks == ticks)
            return hit.Meta;

        if (useSidecar && TryReadSidecar(replayId, out var sidecar) &&
            sidecar!.SizeBytes == info.Length && sidecar.ModifiedUtcTicks == ticks)
        {
            _cache[filePath] = new CacheEntry(info.Length, ticks, sidecar);
            return sidecar;
        }

        var bytes = File.ReadAllBytes(filePath);
        var meta = BuildMeta(replayId, bytes, useSidecar ? "library-scan" : "game-scan");
        meta.SizeBytes = info.Length;
        meta.ModifiedUtcTicks = ticks;
        _cache[filePath] = new CacheEntry(info.Length, ticks, meta);
        if (useSidecar)
        {
            try { WriteSidecar(meta); } catch { /* sidecar 只是缓存，写不进去不影响主流程 */ }
        }
        return meta;
    }

    private bool TryReadSidecar(string replayId, out ReplayMeta? meta)
    {
        meta = null;
        try
        {
            var path = SidecarFile(replayId);
            if (!File.Exists(path)) return false;
            var loaded = JsonSerializer.Deserialize<ReplayMeta>(File.ReadAllText(path), StoreJson);
            if (loaded is null || loaded.SchemaVersion != CurrentSchemaVersion) return false;
            meta = loaded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Read an existing map index only; never scan, import or maintain the library.</summary>
    public int IndexedMapId(string replayId) => ReplayCdn.IsValidReplayId(replayId, out _) &&
        TryReadSidecar(replayId, out var meta) && meta?.ReplayId == replayId ? Math.Max(0, meta.MapId) : 0;

    private void WriteSidecar(ReplayMeta meta)
    {
        EnsureLibrary();
        WriteAtomic(SidecarFile(meta.ReplayId), JsonSerializer.Serialize(meta, StoreJson));
    }

    private ReplayMeta BuildMeta(string replayId, byte[] bytes, string source)
    {
        var meta = new ReplayMeta
        {
            ReplayId = replayId,
            SizeBytes = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            Source = source,
            CapturedUtc = DateTime.UtcNow,
            FrameCount = CountFrames(bytes)
        };

        if (!EnsureProtocol())
        {
            meta.Error = "协议程序集不可用，无法解析结算信息。";
            return meta;
        }

        if (!_protocol!.TryParseSettlement(bytes, out var finish, out var snapshot))
        {
            meta.Error = "解析不出结算帧（cmd 1016），文件可能已损坏或来自不兼容的版本。";
            return meta;
        }

        var room = ReflectionValue.Get(snapshot, "Room");
        meta.Healthy = true;
        meta.GameVersion = ReflectionValue.Text(finish, "Version");
        meta.FinishTime = ReflectionValue.Long(finish, "FinishTime");
        meta.StartTime = ReflectionValue.Long(room, "StartTime");
        if (meta.StartTime > 0 && meta.FinishTime >= meta.StartTime)
            meta.DurationSeconds = (int)(meta.FinishTime - meta.StartTime);

        meta.MapId = ReflectionValue.Int(room, "MapId");
        meta.MapName = _config?.Map(meta.MapId) ?? $"地图 {meta.MapId}";
        var roomMapType = ReflectionValue.Int(room, "MapType");
        meta.MapType = roomMapType != 0 ? roomMapType : ReflectionValue.Int(finish, "MapType");
        meta.Difficulty = ReflectionValue.Int(room, "Difficulty");
        meta.FileReplayId = ReflectionValue.Text(finish, "ReplayId");
        meta.RoundCount = ReflectionValue.Int(room, "Round");

        foreach (var player in ReflectionValue.Items(ReflectionValue.Get(room, "Players")))
        {
            var heroId = ReflectionValue.Int(ReflectionValue.Get(player, "Hero"), "HeroId");
            meta.Players.Add(new ReplayPlayerBrief
            {
                Id = ReflectionValue.Long(player, "Id"),
                Nick = ReflectionValue.Text(player, "Nick"),
                HeroId = heroId,
                HeroName = _config?.Character(heroId) ?? $"角色 {heroId}"
            });
        }

        meta.WinnerId = ReflectionValue.Long(finish, "Winer");
        meta.WinnerName = meta.Players.FirstOrDefault(x => x.Id == meta.WinnerId)?.Nick ?? "";
        return meta;
    }

    private bool EnsureProtocol()
    {
        if (_protocol is not null) return true;
        if (_protocolUnavailable) return false;
        try
        {
            _protocol = new GameProtocolContext(Path.Combine(_appDirectory, "Protocol"));
            _config = new ConfigCatalog(_protocol, Path.Combine(_appDirectory, "GameData"), Settings.Language);
            return true;
        }
        catch
        {
            _protocolUnavailable = true;
            return false;
        }
    }

    /// <summary>只走帧头，不解析载荷；失败时返回 0。</summary>
    private static int CountFrames(byte[] bytes)
    {
        try
        {
            var count = 0;
            var offset = 0;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 6) return count;
                var payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 2, 4));
                offset += 6;
                if (payloadLength < 0 || payloadLength > bytes.Length - offset) return count;
                offset += payloadLength;
                count++;
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    // ============================== 下载（按 ID 从官方 CDN 收进库） ==============================

}
