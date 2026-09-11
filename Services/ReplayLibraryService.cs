using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AstralParty.Toys.Services;

/// <summary>
/// 回放库：游戏目录 <c>Temp\Replay</c> 只当"热位"用，长期归档放在库目录。
///
/// 依据（反编译确认）：游戏判定"本地回放已满"只看
/// <c>ListAllReplayIds().Count &gt;= MAX_BATTLE_RECORDS(=10)</c>，
/// 即 <c>Temp\Replay</c> 下有多少个「目录名 == 内部文件名」的子目录，与服务器无关。
/// 因此把回放移出该目录即可立刻腾出席位；需要时再按同样结构复制回去，游戏就能认出它。
/// </summary>
public sealed class ReplayLibraryService
{
    /// <summary>游戏客户端写死的本地回放席位上限（Global 配置 MAX_BATTLE_RECORDS）。</summary>
    public const int GameSlotCapacity = 10;

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
        return settings;
    }

    public void SaveSettings(ReplayLibrarySettings settings)
    {
        Settings = Normalize(settings);
        Directory.CreateDirectory(_profileDirectory);
        WriteAtomic(SettingsPath, JsonSerializer.Serialize(Settings, StoreJson));
        LibraryRoot = ResolveLibraryRoot(Settings.LibraryRoot);
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
            _config = new ConfigCatalog(_protocol, Path.Combine(_appDirectory, "GameData"));
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

    // ============================== 归档 / 放回 ==============================

    /// <summary>把游戏目录里的回放移动到库（腾出席位）。</summary>
    public ReplayOperationResult Archive(IReadOnlyCollection<string> replayIds)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;

        foreach (var id in replayIds)
        {
            var source = GameFile(id);
            if (!File.Exists(source))
            {
                result.Skipped++;
                result.Messages.Add($"{id}：游戏目录里没有这个回放，已跳过。");
                continue;
            }

            var target = LibraryFile(id);
            try
            {
                if (File.Exists(target))
                {
                    if (FilesIdentical(source, target))
                    {
                        DeleteReplayFolder(Path.GetDirectoryName(source)!, out _);
                        result.Applied++;
                        result.Messages.Add($"{id}：库里已有同一份回放，已直接腾出游戏席位。");
                        continue;
                    }
                    result.Skipped++;
                    result.Messages.Add($"{id}：库里存在同 ID 但内容不同的回放，为避免覆盖已跳过（请手动处理）。");
                    continue;
                }

                var bytes = File.ReadAllBytes(source);
                CopyVerified(source, target);
                var meta = BuildMeta(id, bytes, "game-archive");
                meta.SizeBytes = new FileInfo(target).Length;
                meta.ModifiedUtcTicks = File.GetLastWriteTimeUtc(target).Ticks;
                WriteSidecar(meta);
                _cache[target] = new CacheEntry(meta.SizeBytes, meta.ModifiedUtcTicks, meta);

                if (!DeleteReplayFolder(Path.GetDirectoryName(source)!, out var error))
                {
                    result.Failed++;
                    result.Messages.Add($"{id}：已复制进库，但删除游戏目录副本失败（{error}）。库里有备份，可手动删除。");
                    continue;
                }

                result.Applied++;
                result.Messages.Add($"{id}：已归档到回放库。");
                Log("archive", id, $"{source} -> {target}");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"{id}：归档失败（{ex.Message}）。");
            }
        }

        return result;
    }

    /// <summary>
    /// 把库里的回放复制回游戏目录（库里永远保留副本）。
    /// allowUnparseable：工具解析不出结算帧时也照样放回——需要玩家确认过才传 true。
    /// </summary>
    public ReplayOperationResult Restore(IReadOnlyCollection<string> replayIds, bool allowUnparseable = false)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;

        foreach (var id in replayIds)
        {
            var source = LibraryFile(id);
            if (!File.Exists(source))
            {
                result.Skipped++;
                result.Messages.Add($"{id}：回放库里没有这个回放，已跳过。");
                continue;
            }

            var target = GameFile(id);
            try
            {
                // 游戏列本地回放时解析不出结算帧就会把整个目录删掉，
                // 所以解析不了的先拦一次问清楚，而不是直接放回去。
                var meta = ReadMeta(id, source, useSidecar: true);
                if (!meta.Healthy && !allowUnparseable)
                {
                    result.Ok = false;
                    result.Skipped++;
                    result.NeedsConfirmation = true;
                    result.PendingIds.Add(id);
                    result.Messages.Add($"{id}：本工具解析不出它的结算帧（工具自带的协议版本可能比游戏旧）。" +
                                        "如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。");
                    continue;
                }

                if (File.Exists(target))
                {
                    if (FilesIdentical(source, target))
                    {
                        result.Skipped++;
                        result.Messages.Add($"{id}：游戏目录里已经有这一局了。");
                        continue;
                    }
                    result.Skipped++;
                    result.Messages.Add($"{id}：游戏目录里已有同 ID 但内容不同的文件，为避免覆盖已跳过。");
                    continue;
                }

                CopyVerified(source, target);
                result.Applied++;
                result.Messages.Add($"{id}：已放回游戏目录，游戏内「本地回放」可直接观看。");
                Log("restore", id, $"{source} -> {target}");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"{id}：放回失败（{ex.Message}）。");
            }
        }

        if (result.Applied > 0 && CountGameSlots() > GameSlotCapacity)
        {
            result.Messages.Add($"注意：现在游戏目录里有 {CountGameSlots()} 局，超过游戏上限 {GameSlotCapacity}，" +
                                "继续保存新回放会失败。播放不受影响，建议归档几局。");
        }

        return result;
    }

    private int CountGameSlots() => EnumerateReplays(GameDirectory).Count();

    /// <summary>归档游戏目录里最旧的若干局，只保留最新 keepCount 个。</summary>
    public ReplayOperationResult Maintain(int? keepCount = null)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;

        var keep = Math.Clamp(keepCount ?? Settings.KeepInGame, 1, GameSlotCapacity);
        var snapshot = Snapshot();
        var excess = snapshot.Entries.Where(x => x.InGame).Skip(keep).ToList();
        if (excess.Count == 0)
        {
            result.Messages.Add($"游戏目录里只有 {snapshot.GameCount} 局，未超过保留名额 {keep}，无需整理。");
            return result;
        }

        var archived = Archive(excess.Select(x => x.ReplayId).ToList());
        result.Applied = archived.Applied;
        result.Skipped = archived.Skipped;
        result.Failed = archived.Failed;
        result.Ok = archived.Ok;
        result.Messages.AddRange(archived.Messages);
        result.Messages.Insert(0, $"整理完成：游戏目录保留最新 {keep} 局，归档 {archived.Applied} 局。");
        Log("maintain", string.Join(',', excess.Select(x => x.ReplayId)), $"keep={keep}");
        return result;
    }

    /// <summary>把一个外部回放文件导入库（文件名不必是回放 ID）。</summary>
    public ReplayOperationResult Import(string filePath)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;
        if (!File.Exists(filePath)) return ReplayOperationResult.Fail($"文件不存在：{filePath}");

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var meta = BuildMeta(Path.GetFileNameWithoutExtension(filePath), bytes, "manual-import");
            if (!meta.Healthy)
                return ReplayOperationResult.Fail("这个文件不是能解出结算帧的回放，已拒绝导入。");

            var id = meta.FileReplayId is { Length: > 0 } fileReplayId
                ? fileReplayId
                : Path.GetFileName(filePath);
            if (id.Length == 0) return ReplayOperationResult.Fail("无法确定回放 ID。");

            var target = LibraryFile(id);
            if (File.Exists(target) && FilesIdentical(filePath, target))
            {
                result.Skipped++;
                result.Messages.Add($"{id}：库里已经有这一局了。");
                return result;
            }

            CopyVerified(filePath, target);
            meta.ReplayId = id;
            meta.SizeBytes = new FileInfo(target).Length;
            meta.ModifiedUtcTicks = File.GetLastWriteTimeUtc(target).Ticks;
            WriteSidecar(meta);
            _cache[target] = new CacheEntry(meta.SizeBytes, meta.ModifiedUtcTicks, meta);
            result.Applied++;
            result.Messages.Add($"已导入 {id} 到回放库。");
            Log("import", id, filePath);
        }
        catch (Exception ex)
        {
            return ReplayOperationResult.Fail($"导入失败：{ex.Message}");
        }

        return result;
    }

    // ============================== 删除 ==============================

    public ReplayOperationResult Delete(IReadOnlyCollection<string> replayIds, ReplayDeleteTarget target)
    {
        var result = new ReplayOperationResult();
        var touchedLibrary = false;

        foreach (var id in replayIds)
        {
            if (target is ReplayDeleteTarget.Game or ReplayDeleteTarget.Both)
            {
                var folder = Path.GetDirectoryName(GameFile(id))!;
                if (Directory.Exists(folder))
                {
                    var outcome = RecycleBinHelper.Delete(folder, out var error);
                    if (outcome != DeleteOutcome.Failed)
                    {
                        result.Applied++;
                        result.Messages.Add($"{id}：已从游戏目录删除{RecycleBinHelper.Describe(outcome, toRecycleBin: true)}。");
                        Log("delete-game", id, folder);
                    }
                    else
                    {
                        result.Failed++;
                        result.Messages.Add($"{id}：删除游戏目录副本失败（{error}）。");
                    }
                }
            }

            if (target is ReplayDeleteTarget.Library or ReplayDeleteTarget.Both)
            {
                var folder = Path.GetDirectoryName(LibraryFile(id))!;
                if (Directory.Exists(folder))
                {
                    var outcome = RecycleBinHelper.Delete(folder, out var error);
                    if (outcome != DeleteOutcome.Failed)
                    {
                        result.Applied++;
                        result.Messages.Add($"{id}：已从回放库删除{RecycleBinHelper.Describe(outcome, toRecycleBin: true)}。");
                        touchedLibrary = true;
                        Log("delete-library", id, folder);
                    }
                    else
                    {
                        result.Failed++;
                        result.Messages.Add($"{id}：删除库内副本失败（{error}）。");
                    }
                }
                try
                {
                    var sidecar = SidecarFile(id);
                    if (File.Exists(sidecar)) File.Delete(sidecar);
                }
                catch { /* 索引残留无害 */ }
            }
        }

        if (!touchedLibrary && result.Applied == 0 && result.Failed == 0)
            result.Messages.Add("没有可删除的回放。");
        return result;
    }

    // ============================== 工具方法 ==============================

    private ReplayOperationResult? Guard(ReplayOperationResult result)
    {
        if (IsSameFolder())
            return ReplayOperationResult.Fail("回放库目录与游戏回放目录不能是同一个路径，操作已拒绝。请先在设置里修改库目录。");
        return null;
    }

    private static bool FilesIdentical(string left, string right)
    {
        try
        {
            if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(left)))
                == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(right)));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>复制到目标（先写 .part、校验哈希、再改名），保证库/游戏目录里不会出现半截文件。</summary>
    private static void CopyVerified(string sourceFile, string targetFile)
    {
        var directory = Path.GetDirectoryName(targetFile)!;
        Directory.CreateDirectory(directory);
        var expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile)));
        var temp = targetFile + ".part";
        File.Copy(sourceFile, temp, overwrite: true);
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temp)));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(temp); } catch { }
            throw new IOException("复制后校验失败，目标文件已清理，源文件保持不动。");
        }
        File.Move(temp, targetFile, overwrite: true);
    }

    private static bool DeleteReplayFolder(string folder, out string error)
    {
        error = "";
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void Log(string action, string replayIds, string detail)
    {
        try
        {
            EnsureLibrary();
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{action}\t{replayIds}\t{detail}{Environment.NewLine}";
            File.AppendAllText(OperationsLogPath, line, Encoding.UTF8);
        }
        catch { /* 日志失败不影响操作结果 */ }
    }

    /// <summary>临时文件 + 原子替换（MoveFileEx 覆盖），避免断电/崩溃留下半个 JSON。</summary>
    internal static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}
