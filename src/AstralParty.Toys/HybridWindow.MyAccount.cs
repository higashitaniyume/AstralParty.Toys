using System.Text.Json;
using AstralParty.Toys.Services.Account;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private AccountConsent MyAccountConsent => new(SpeedhackManager.ResolveProfileDirectory());
    private CancellationTokenSource? _myAccountLifetime;
    private AccountSession? _myAccountSession;
    private int _myAccountGeneration;
    private int _myAccountRequestId;
    private bool _myAccountPageVisible, _myAccountRefreshing;
    private string? _myAccountDirectory;
    private DateTimeOffset _myAccountRefreshAttempt;
    private bool _myAccountMatchesRefreshing;
    private DateTimeOffset _myAccountMatchesRefreshAttempt;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _myAccountMapIds = new(StringComparer.Ordinal);
    private AssetLocator? _myAccountAssets;
    private sealed record PendingCredentialDeletion(string Token, string Directory, string Uid);
    private PendingCredentialDeletion? _pendingCredentialDeletion;
    // Published market entry ID; do not infer this from an assembly or display name.
    private static string CredentialModMarketId => "AccountCredentialMod";

    private bool MyAccountGameRunning(string? directory)
    {
        // Unlike installation status, authentication must fail closed if process enumeration fails.
        try
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AstralParty_CN", "AstralParty" };
            if (directory != null && SpeedhackManager.ContainsGameExe(directory) is { } exe) names.Add(Path.GetFileNameWithoutExtension(exe));
            foreach (var name in names)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(name);
                try { if (processes.Length > 0) return true; }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            return false;
        }
        catch { return true; }
    }

    private void StopMyAccount(string status = "disconnected", AccountQueryException? diagnostic = null)
    {
        _myAccountGeneration++;
        _myAccountLifetime?.Cancel();
        _myAccountSession?.Stop();
        _myAccountLifetime = null;
        _myAccountSession = null;
        _myAccountDirectory = null;
        _myAccountRefreshing = false;
        _myAccountRefreshAttempt = default;
        _myAccountMatchesRefreshing = false;
        _myAccountMatchesRefreshAttempt = default;
        Post(new { type = "myAccountState", payload = new { requestId = _myAccountRequestId, status, stale = true, reason = diagnostic?.Reason, command = diagnostic?.Command, serverError = diagnostic?.ServerError } });
    }

    private async Task<bool> HandleMyAccountMessageAsync(string? type, JsonElement root)
    {
        if (type == null || !type.StartsWith("myAccount", StringComparison.Ordinal)) return false;
        // Handle all failures here; never let credential/parser exceptions reach the generic toast/logger.
        try
        {
            if (type is "myAccountConnect" or "myAccountDisconnect" or "myAccountLeave")
                _myAccountRequestId = root.TryGetProperty("requestId", out var requestId) && requestId.TryGetInt32(out var value) ? value : 0;
            switch (type)
            {
                case "myAccountEnter":
                    _myAccountPageVisible = true;
                    SendMyAccountAvailability();
                    break;
                case "myAccountLeave":
                    _pendingCredentialDeletion = null;
                    _myAccountPageVisible = false;
                    StopMyAccount();
                    break;
                case "myAccountAccept":
                    if (_myAccountPageVisible) { MyAccountConsent.Accept(); SendMyAccountAvailability(); }
                    break;
                case "myAccountDiscover":
                    if (_myAccountPageVisible) SendMyAccountAvailability();
                    break;
                case "myAccountDisconnect": StopMyAccount(); break;
                case "myAccountDeleteRequest":
                    _pendingCredentialDeletion = null;
                    if (!_myAccountPageVisible || !MyAccountConsent.Granted) break;
                    var deleteUid = root.TryGetProperty("uid", out var deleteUidElement) ? deleteUidElement.GetString() ?? "" : "";
                    var deleteDirectory = _modManager.ResolveGameDirectory();
                    if (string.IsNullOrEmpty(deleteDirectory) || !AccountCredentials.ValidUid(deleteUid) ||
                        !AccountCredentials.Discover(deleteDirectory).Contains(deleteUid)) break;
                    var pending = new PendingCredentialDeletion(Guid.NewGuid().ToString("N"), deleteDirectory, deleteUid);
                    _pendingCredentialDeletion = pending;
                    Post(new { type = "myAccountDeleteConfirm", payload = new { pending.Token, pending.Uid, path = Path.Combine(AccountCredentials.Root(deleteDirectory), deleteUid, "account.json") } });
                    break;
                case "myAccountDeleteCancel": _pendingCredentialDeletion = null; break;
                case "myAccountDelete":
                    var deletion = _pendingCredentialDeletion;
                    _pendingCredentialDeletion = null;
                    if (!_myAccountPageVisible || !MyAccountConsent.Granted || deletion == null ||
                        !root.TryGetProperty("token", out var deletionToken) || deletionToken.GetString() != deletion.Token) break;
                    if (!string.Equals(deletion.Directory, _modManager.ResolveGameDirectory(), StringComparison.OrdinalIgnoreCase))
                    {
                        Post(new { type = "myAccountDeleteResult", payload = new { deletion.Uid, success = false } });
                        break;
                    }
                    _myAccountRequestId = root.TryGetProperty("requestId", out var deleteRequest) && deleteRequest.TryGetInt32(out var deleteRequestId) ? deleteRequestId : 0;
                    StopMyAccount();
                    try
                    {
                        AccountCredentials.Delete(deletion.Directory, deletion.Uid);
                        Post(new { type = "myAccountDeleteResult", payload = new { deletion.Uid, success = true } });
                        SendMyAccountAvailability();
                    }
                    catch { Post(new { type = "myAccountDeleteResult", payload = new { deletion.Uid, success = false } }); }
                    break;
                case "myAccountConnect":
                    if (_myAccountPageVisible && MyAccountConsent.Granted)
                        await ConnectMyAccountAsync(root.TryGetProperty("uid", out var uid) ? uid.GetString() ?? "" : "");
                    break;
                case "myAccountRefresh":
                    if (_myAccountPageVisible && _myAccountSession != null && _myAccountLifetime != null)
                        await RefreshMyAccountFriendsAsync(_myAccountSession, _myAccountGeneration, _myAccountLifetime.Token);
                    break;
                case "myAccountInstallMod":
                    if (!_myAccountPageVisible || !MyAccountConsent.Granted) break;
                    Post(new { type = "myAccountMarket", payload = new { id = CredentialModMarketId } });
                    break;
                case "myAccountRecentRefresh":
                    if (_myAccountPageVisible && _myAccountSession != null && _myAccountLifetime != null)
                        await RefreshMyAccountMatchesAsync(_myAccountSession, _myAccountGeneration, _myAccountLifetime.Token);
                    break;
            }
        }
        catch { Post(new { type = "myAccountState", payload = new { requestId = _myAccountRequestId, status = "unavailable", stale = true } }); }
        return true;
    }

    private void SendMyAccountAvailability()
    {
        bool granted = MyAccountConsent.Granted;
        // Display the destination without opening credential files or enumerating accounts.
        var gameDirectory = _modManager.ResolveGameDirectory();
        var credentialRoot = string.IsNullOrWhiteSpace(gameDirectory) ? null : AccountCredentials.Root(gameDirectory);
        if (!granted)
        {
            Post(new { type = "myAccountAvailability", payload = new { consent = false, credentialRoot } });
            return; // No credential discovery or mod scan before explicit consent.
        }
        var status = _modManager.GetStatus();
        var mod = status.Mods.FirstOrDefault(m => m.Id == "AccountCredentialMod");
        var directory = status.GameDirectory;
        if (_myAccountDirectory != null && !string.Equals(directory, _myAccountDirectory, StringComparison.OrdinalIgnoreCase)) StopMyAccount("game_changed");
        Post(new
        {
            type = "myAccountAvailability",
            payload = new
            {
                consent = true, credentialRoot, installed = mod != null, enabled = mod?.Enabled == true,
                gameFound = status.GameExeFound, gameRunning = MyAccountGameRunning(directory),
                accounts = string.IsNullOrWhiteSpace(directory) ? [] : AccountCredentials.Discover(directory)
            }
        });
    }

    private async Task ConnectMyAccountAsync(string uid)
    {
        StopMyAccount();
        var directory = _modManager.ResolveGameDirectory();
        if (string.IsNullOrEmpty(directory) || !AccountCredentials.ValidUid(uid)) return;
        if (MyAccountGameRunning(directory)) { StopMyAccount("game_running"); return; }
        var lifetime = new CancellationTokenSource();
        _myAccountLifetime = lifetime;
        _myAccountDirectory = directory;
        int generation = _myAccountGeneration;
        int requestId = _myAccountRequestId;
        AccountSession? session = null;
        Post(new { type = "myAccountState", payload = new { requestId, status = "connecting", stale = true } });
        _ = MonitorMyAccountAsync(generation, directory, lifetime.Token);
        try
        {
            // Read only after consent and explicit account selection, never accept a file path from WebView.
            var credentials = AccountCredentials.Read(directory, uid);
            session = new AccountSession(Path.Combine(_appDirectory, "Protocol"), credentials);
            _myAccountSession = session;
            var player = await session.LoginAsync(credentials, uid, lifetime.Token);
            if (generation != _myAccountGeneration || lifetime.IsCancellationRequested) return;
            if (MyAccountGameRunning(directory)) { StopMyAccount("game_running"); return; }
            var display = await Task.Run(() => BuildMyAccountPlayer(player, requestId), lifetime.Token);
            if (generation != _myAccountGeneration || lifetime.IsCancellationRequested) return;
            Post(new { type = "myAccountPlayer", payload = display });
            Post(new { type = "myAccountState", payload = new { requestId, status = "connected", stale = true } });
            await RefreshMyAccountFriendsAsync(session, generation, lifetime.Token);
            await RefreshMyAccountMatchesAsync(session, generation, lifetime.Token);
            // Keep the session alive for manual refreshes; monitoring and heartbeats run separately.
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (generation == _myAccountGeneration) StopMyAccount("connection_failed", error as AccountQueryException);
        }
        finally
        {
            if (session != null) await session.DisposeAsync();
            lifetime.Dispose();
        }
    }

    private object BuildMyAccountPlayer(AccountPlayer player, int requestId)
    {
        var config = new ConfigCatalog(new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")),
            Path.Combine(_appDirectory, "GameData"), _libraryService.Settings.Language);
        var assets = _myAccountAssets ??= new AssetLocator(_assetDirectory, _materialDirectories);
        return new
        {
            requestId, player.Uid, player.Name, player.Level, capturedAt = DateTimeOffset.UtcNow,
            characters = player.Characters.Select(c => new
            {
                c.Id, c.FightCount, name = config.Character(c.Id),
                image = AssetUrl(assets.Character(c.Id, config.CharacterAssets(c.Id)))
            }).ToArray()
        };
    }

    private async Task RefreshMyAccountFriendsAsync(AccountSession session, int generation, CancellationToken ct)
    {
        if (_myAccountRefreshing || generation != _myAccountGeneration) return;
        if (DateTimeOffset.UtcNow - _myAccountRefreshAttempt < TimeSpan.FromSeconds(3)) return;
        if (MyAccountGameRunning(_myAccountDirectory)) { StopMyAccount("game_running"); return; }
        _myAccountRefreshing = true;
        _myAccountRefreshAttempt = DateTimeOffset.UtcNow;
        int requestId = _myAccountRequestId;
        Post(new { type = "myAccountState", payload = new { requestId, status = "refreshing" } });
        try
        {
            var friends = await session.FriendsAsync(ct);
            if (generation != _myAccountGeneration || ct.IsCancellationRequested) return;
            var capturedAt = DateTimeOffset.UtcNow;
            var display = friends.Select(f => new
            {
                f.Uid, f.Name, f.Note, f.Level, f.Status,
                image = AssetUrl(_myAccountAssets?.Named($"UT_AccountHeadShot_{f.HeadIcon}") ?? ""),
                offlineAt = f.OfflineTime is > 0 and <= 253402300799 ? (DateTimeOffset?)DateTimeOffset.FromUnixTimeSeconds(f.OfflineTime) : null
            }).ToArray();
            Post(new { type = "myAccountFriends", payload = new { requestId, session.Uid, friends = display, capturedAt } });
            Post(new { type = "myAccountState", payload = new { requestId, status = "connected", stale = false } });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error) { if (generation == _myAccountGeneration) StopMyAccount("refresh_failed", error as AccountQueryException); }
        finally { if (generation == _myAccountGeneration) _myAccountRefreshing = false; }
    }

    private async Task RefreshMyAccountMatchesAsync(AccountSession session, int generation, CancellationToken ct)
    {
        if (_myAccountMatchesRefreshing || generation != _myAccountGeneration || ct.IsCancellationRequested) return;
        if (DateTimeOffset.UtcNow - _myAccountMatchesRefreshAttempt < TimeSpan.FromSeconds(3)) return;
        if (MyAccountGameRunning(_myAccountDirectory)) { StopMyAccount("game_running"); return; }
        _myAccountMatchesRefreshing = true;
        _myAccountMatchesRefreshAttempt = DateTimeOffset.UtcNow;
        int requestId = _myAccountRequestId;
        Post(new { type = "myAccountRecentState", payload = new { requestId, loading = true } });
        try
        {
            var records = await session.RecentMatchesAsync(ct);
            var display = await Task.Run(() => BuildMyAccountMatches(records), ct);
            if (generation != _myAccountGeneration || ct.IsCancellationRequested) return;
            Post(new { type = "myAccountRecent", payload = new { requestId, session.Uid, matches = display, capturedAt = DateTimeOffset.UtcNow } });
            await ResolveMyAccountMatchMapsAsync(records, session.Uid, generation, requestId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch
        {
            if (generation == _myAccountGeneration)
                Post(new { type = "myAccountRecentState", payload = new { requestId, loading = false, failed = true } });
        }
        finally
        {
            if (generation == _myAccountGeneration)
            {
                _myAccountMatchesRefreshing = false;
                Post(new { type = "myAccountRecentState", payload = new { requestId, loading = false } });
            }
        }
    }

    private object[] BuildMyAccountMatches(IReadOnlyList<AccountMatch> matches)
    {
        var config = new ConfigCatalog(new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")),
            Path.Combine(_appDirectory, "GameData"), _libraryService.Settings.Language);
        return matches.Select(m => (object)new
        {
            m.Index, m.Rank, m.HeroId, m.MapType, m.ReplayId, m.Version,
            playedAt = m.Time is > 0 and <= 253402300799 ? (DateTimeOffset?)DateTimeOffset.FromUnixTimeSeconds(m.Time) : null,
            character = config.Character(m.HeroId),
            mapName = _myAccountMapIds.TryGetValue(m.ReplayId, out var mapId) ? config.Map(mapId) : null,
            mapLoading = ReplayCdn.IsValidReplayId(m.ReplayId, out _) && !_myAccountMapIds.ContainsKey(m.ReplayId),
            canDownload = ReplayCdn.IsValidReplayId(m.ReplayId, out _)
        }).ToArray();
    }

    private async Task ResolveMyAccountMatchMapsAsync(IReadOnlyList<AccountMatch> records, string uid,
        int generation, int requestId, CancellationToken ct)
    {
        using var protocol = new AccountProtocol(Path.Combine(_appDirectory, "Protocol"));
        var config = await Task.Run(() => new ConfigCatalog(new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")),
            Path.Combine(_appDirectory, "GameData"), _libraryService.Settings.Language), ct);
        await Parallel.ForEachAsync(records.Where(m => ReplayCdn.IsValidReplayId(m.ReplayId, out _)).DistinctBy(m => m.ReplayId),
            new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct }, async (record, token) =>
            {
                int mapId = _myAccountMapIds.GetValueOrDefault(record.ReplayId);
                if (mapId == 0) mapId = _libraryService.IndexedMapId(record.ReplayId);
                if (mapId == 0)
                    mapId = await ReplayMapLookup.FindMapIdAsync(record.ReplayId, payload =>
                        (int)AccountProtocol.Number(AccountProtocol.Get(protocol.Parse("RunningGameS2C", payload), "Room"), "MapId"), token);
                if (mapId > 0) _myAccountMapIds[record.ReplayId] = mapId;
                if (generation != _myAccountGeneration || token.IsCancellationRequested) return;
                Post(new { type = "myAccountRecentMap", payload = new { requestId, uid, record.ReplayId, mapName = mapId > 0 ? config.Map(mapId) : null } });
            });
    }

    private async Task MonitorMyAccountAsync(int generation, string directory, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(1000, ct);
                if (generation != _myAccountGeneration) return;
                if (MyAccountGameRunning(directory)) { StopMyAccount("game_running"); return; }
                if (!string.Equals(directory, _modManager.ResolveGameDirectory(), StringComparison.OrdinalIgnoreCase)) { StopMyAccount("game_changed"); return; }
                if (_myAccountSession is { Connected: false }) { StopMyAccount("connection_failed"); return; }
            }
        }
        catch (OperationCanceledException) { }
        catch { if (generation == _myAccountGeneration) StopMyAccount("unavailable"); }
    }
}
