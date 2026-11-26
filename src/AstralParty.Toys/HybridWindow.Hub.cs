using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.Account;
using AstralParty.Toys.Services.Hub;
using AstralParty.Toys.Services.Replay;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private readonly object _hubInitGate = new();
    private HubApiClient? _hubApi;
    private HubLocalBridgeServer? _hubBridge;
    private HubSettlementService? _hubSettlement;
    private AssetLocator? _hubAssets;
    private readonly ConcurrentDictionary<string, string> _hubAvatarUrlCache = new(StringComparer.Ordinal);

    private CancellationTokenSource? _hubSignInCts;
    private CancellationTokenSource? _hubStreamCts;
    private CancellationTokenSource? _hubSettlementCts;

    private HubServerConfig? _hubServerConfig;
    private HubUserProfileDto? _hubMe;
    private string? _hubActiveRoomId;
    private string? _hubActiveRoomRegion;
    private string? _hubActiveRoomTitle;
    private string? _hubActiveRoomHostUserId;
    private long _hubLastReportedGameRoomId;
    private string _hubLastReportedGamePwd = "";
    private string _hubLastAutoJoinedKey = "";
    private bool _hubAutoJoinEnabled = true;
    private string _hubLastSettlementPath = "";

    private static ToysConfigStore HubConfigStore
        => ToysConfigStore.ForRoot(SpeedhackManager.ResolveProfileDirectory());

    private HubApiClient EnsureHubApiClient()
    {
        lock (_hubInitGate)
        {
            if (_hubApi is null)
            {
                var cfg = HubConfigStore.Load().Hub;
                _hubApi = new HubApiClient(cfg.ServerUrl);
                var session = new HubAuthService(HubConfigStore).LoadStoredSession();
                if (session is not null)
                {
                    _hubApi.SetToken(session.Value.Token);
                }
            }
            return _hubApi;
        }
    }

    private HubSettlementService EnsureHubSettlement()
        => _hubSettlement ??= new HubSettlementService(_appDirectory, _libraryService);

    private AssetLocator EnsureHubAssets()
        => _hubAssets ??= new AssetLocator(_assetDirectory, _materialDirectories);

    private void EnsureHubInitialized()
    {
        try
        {
            EnsureHubApiClient();
            lock (_hubInitGate)
            {
                if (_hubBridge is null)
                {
                    _hubBridge = new HubLocalBridgeServer();
                    _hubBridge.EventReceived += OnHubBridgeEvent;
                    _hubBridge.SessionsChanged += sessions =>
                    {
                        Post(new { type = "hubBridgeSessions", payload = BuildBridgeStatusPayload(sessions) });
                        _ = TrySyncHostGameRoomFromSessionsAsync(sessions);
                    };
                    _hubBridge.Start();
                }
            }

            SyncHubBridgeEndpoints();
        }
        catch (Exception ex)
        {
            ToysLog.Warn("hub", $"初始化组队大厅本地桥接失败：{ex.Message}");
        }
    }

    private void SyncHubBridgeEndpoints()
    {
        if (_hubBridge is null || !_hubBridge.IsRunning) return;
        try
        {
            var dirs = _gameLibrary.GetProfiles().Select(p => p.Directory).ToList();
            if (_modManager.ResolveGameDirectory() is { Length: > 0 } current)
            {
                dirs.Add(current);
            }
            _hubBridge.SyncGameDirectories(dirs);
        }
        catch (Exception ex)
        {
            ToysLog.Warn("hub", $"同步 HubBridge endpoint.json 失败：{ex.Message}");
        }
    }

    private void DisposeHub()
    {
        try { _hubSignInCts?.Cancel(); _hubSignInCts?.Dispose(); }
        catch { }
        try { _hubStreamCts?.Cancel(); _hubStreamCts?.Dispose(); }
        catch { }
        try { _hubSettlementCts?.Cancel(); _hubSettlementCts?.Dispose(); }
        catch { }
        try { _hubBridge?.Dispose(); }
        catch { }
        try { _hubApi?.Dispose(); }
        catch { }
    }

    private async Task<bool> HandleHubMessageAsync(string? type, JsonElement root)
    {
        if (type is null || !type.StartsWith("hub", StringComparison.Ordinal)) return false;

        try
        {
            switch (type)
            {
                case "hubInit":
                    EnsureHubInitialized();
                    await SendHubInitStateAsync().ConfigureAwait(false);
                    break;

                case "hubSaveServerUrl":
                {
                    var rawUrl = ReadJsonString(root, "serverUrl");
                    var normalized = HubConfigSection.NormalizeServerUrl(rawUrl);
                    HubConfigStore.Update(cfg => cfg.Hub.ServerUrl = normalized);
                    EnsureHubApiClient().SetBaseUrl(normalized);
                    _hubServerConfig = null;
                    await SendHubInitStateAsync().ConfigureAwait(false);
                    Post(new { type = "toast", message = $"已切换大厅服务地址：{normalized}" });
                    break;
                }

                case "hubSetRegion":
                {
                    var region = HubSettlementService.NormalizeRegion(ReadJsonString(root, "region"));
                    HubConfigStore.Update(cfg => cfg.Hub.ActiveRegion = region);
                    await RefreshHubRoomsAsync(region, ReadJsonString(root, "query"), ReadJsonString(root, "status")).ConfigureAwait(false);
                    break;
                }

                case "hubSetAutoJoin":
                    _hubAutoJoinEnabled = !root.TryGetProperty("enabled", out var ajEl) || ajEl.ValueKind != JsonValueKind.False;
                    Post(new { type = "hubAutoJoinChanged", payload = new { enabled = _hubAutoJoinEnabled } });
                    break;

                case "hubSignIn":
                    await HandleHubSignInAsync().ConfigureAwait(false);
                    break;

                case "hubCancelSignIn":
                    _hubSignInCts?.Cancel();
                    Post(new { type = "hubAuthState", payload = BuildAuthPayload(signingIn: false, error: "已取消登录。") });
                    break;

                case "hubLogout":
                    await HandleHubLogoutAsync().ConfigureAwait(false);
                    break;

                case "hubRefreshMe":
                    await RefreshHubMeAsync().ConfigureAwait(false);
                    break;

                case "hubUpdateProfile":
                    await HandleHubUpdateProfileAsync(root).ConfigureAwait(false);
                    break;

                case "hubUpdatePrivacy":
                    await HandleHubUpdatePrivacyAsync(root).ConfigureAwait(false);
                    break;

                case "hubBindLocalAccount":
                    await HandleHubBindLocalAccountAsync(root).ConfigureAwait(false);
                    break;

                case "hubRefreshLiveAccount":
                    await HandleHubRefreshLiveAccountAsync(root).ConfigureAwait(false);
                    break;

                case "hubBindBridgeAccount":
                    await HandleHubBindBridgeAccountAsync(root).ConfigureAwait(false);
                    break;

                case "hubUnbindAccount":
                {
                    var region = HubSettlementService.NormalizeRegion(ReadJsonString(root, "region"));
                    var uid = ReadJsonString(root, "uid");
                    var me = await EnsureHubApiClient().UnbindGameAccountAsync(region, uid).ConfigureAwait(false);
                    _hubMe = me;
                    await PushHubMeAsync(me).ConfigureAwait(false);
                    Post(new { type = "toast", message = $"已解绑游戏账号 {uid}" });
                    break;
                }

                case "hubViewProfile":
                {
                    var userId = ReadJsonString(root, "userId");
                    var profile = await EnsureHubApiClient().GetProfileAsync(userId).ConfigureAwait(false);
                    await PushHubViewedProfileAsync(profile).ConfigureAwait(false);
                    break;
                }

                case "hubViewProfileByUid":
                {
                    var region = HubSettlementService.NormalizeRegion(ReadJsonString(root, "region"));
                    var uid = ReadJsonString(root, "uid");
                    var profile = await EnsureHubApiClient().GetProfileByUidAsync(region, uid).ConfigureAwait(false);
                    await PushHubViewedProfileAsync(profile).ConfigureAwait(false);
                    break;
                }

                case "hubListRooms":
                {
                    var region = HubSettlementService.NormalizeRegion(
                        ReadJsonString(root, "region", HubConfigStore.Load().Hub.ActiveRegion));
                    await RefreshHubRoomsAsync(region, ReadJsonString(root, "query"), ReadJsonString(root, "status"))
                        .ConfigureAwait(false);
                    break;
                }

                case "hubCreateRoom":
                    await HandleHubCreateRoomAsync(root).ConfigureAwait(false);
                    break;

                case "hubJoinRoom":
                    await HandleHubJoinRoomAsync(root).ConfigureAwait(false);
                    break;

                case "hubLeaveRoom":
                    await HandleHubLeaveRoomAsync().ConfigureAwait(false);
                    break;

                case "hubDisbandRoom":
                    await HandleHubDisbandRoomAsync().ConfigureAwait(false);
                    break;

                case "hubKickMember":
                    if (!string.IsNullOrEmpty(_hubActiveRoomId))
                    {
                        var targetUserId = ReadJsonString(root, "targetUserId");
                        var room = await EnsureHubApiClient().KickMemberAsync(_hubActiveRoomId, targetUserId).ConfigureAwait(false);
                        await ApplyHubRoomUpdateAsync(room).ConfigureAwait(false);
                    }
                    break;

                case "hubSwitchActiveUid":
                    if (!string.IsNullOrEmpty(_hubActiveRoomId))
                    {
                        var activeUid = ReadJsonString(root, "activeUid");
                        var room = await EnsureHubApiClient().SwitchActiveUidAsync(_hubActiveRoomId, activeUid).ConfigureAwait(false);
                        await ApplyHubRoomUpdateAsync(room).ConfigureAwait(false);
                    }
                    break;

                case "hubHostLaunchGame":
                    await HandleHubHostLaunchGameAsync(root).ConfigureAwait(false);
                    break;

                case "hubTriggerBridgeJoin":
                    HandleHubTriggerBridgeJoin(root);
                    break;

                case "hubSendMessage":
                    if (!string.IsNullOrEmpty(_hubActiveRoomId))
                    {
                        var text = ReadJsonString(root, "text");
                        var clientMsgId = Guid.NewGuid().ToString("N");
                        var evt = await EnsureHubApiClient().SendMessageAsync(_hubActiveRoomId, clientMsgId, text)
                            .ConfigureAwait(false);
                        Post(new { type = "hubRoomEvents", payload = new { roomId = _hubActiveRoomId, events = new[] { evt } } });
                    }
                    break;

                case "hubReopenRoom":
                    if (!string.IsNullOrEmpty(_hubActiveRoomId))
                    {
                        var room = await EnsureHubApiClient().ReopenRoomAsync(_hubActiveRoomId).ConfigureAwait(false);
                        await ApplyHubRoomUpdateAsync(room).ConfigureAwait(false);
                    }
                    break;

                case "hubPollSettlement":
                {
                    var replayId = ReadJsonString(root, "replayId");
                    var region = HubSettlementService.NormalizeRegion(ReadJsonString(root, "region"));
                    if (ReplayCdn.IsValidReplayId(replayId, out _))
                    {
                        StartSettlementPolling(region, replayId);
                    }
                    break;
                }

                case "hubOpenSettlementReplay":
                {
                    var path = ReadJsonString(root, "filePath");
                    if (string.IsNullOrWhiteSpace(path)) path = _hubLastSettlementPath;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        await LoadReplayAsync(path).ConfigureAwait(false);
                    }
                    else
                    {
                        Post(new { type = "toast", message = "完整回放尚未下载完成或文件不存在。" });
                    }
                    break;
                }

                case "hubInstallBridgeMod":
                    Post(new { type = "myAccountMarket", payload = new { id = "HubBridgeMod" } });
                    break;
            }
        }
        catch (HubApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearExpiredHubAuth("登录会话已失效，请重新使用 Microsoft 账号登录。");
            }
            else
            {
                Post(new { type = "hubError", payload = new { error = ex.ErrorCode, message = ex.Message } });
                Post(new { type = "toast", message = ex.Message });
            }
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消，不弹窗报错。
        }
        catch (Exception ex)
        {
            Post(new { type = "hubError", payload = new { error = "client_error", message = ex.Message } });
            Post(new { type = "toast", message = $"组队大厅操作失败：{ex.Message}" });
        }

        return true;
    }

    private async Task SendHubInitStateAsync()
    {
        var api = EnsureHubApiClient();
        var cfg = HubConfigStore.Load().Hub;
        var auth = new HubAuthService(HubConfigStore);

        try
        {
            _hubServerConfig = await api.GetConfigAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ToysLog.Warn("hub", $"获取大厅服务端配置失败：{ex.Message}");
        }

        if (api.HasToken)
        {
            try
            {
                _hubMe = await api.GetMeAsync().ConfigureAwait(false);
            }
            catch (HubApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                auth.ClearSession();
                api.SetToken("");
                _hubMe = null;
            }
            catch
            {
                // 网络暂不可达时先保留本地凭据等待用户重试。
            }
        }

        var decoratedMe = _hubMe is null ? null : await BuildDecoratedProfileAsync(_hubMe).ConfigureAwait(false);
        Post(new
        {
            type = "hubInitState",
            payload = new
            {
                serverUrl = cfg.ServerUrl,
                activeRegion = cfg.ActiveRegion,
                autoJoinEnabled = _hubAutoJoinEnabled,
                serverConfig = _hubServerConfig,
                auth = BuildAuthPayload(signingIn: false),
                me = decoratedMe,
                localAccounts = DiscoverLocalAccountsForUi(),
                bridge = BuildBridgeStatusPayload(_hubBridge?.GetActiveSessions() ?? Array.Empty<BridgeSessionState>()),
            }
        });

        await RefreshHubRoomsAsync(cfg.ActiveRegion, null, null).ConfigureAwait(false);
    }

    private async Task HandleHubSignInAsync()
    {
        var api = EnsureHubApiClient();
        _hubServerConfig ??= await api.GetConfigAsync().ConfigureAwait(false);

        _hubSignInCts?.Cancel();
        _hubSignInCts?.Dispose();
        _hubSignInCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Post(new { type = "hubAuthState", payload = BuildAuthPayload(signingIn: true) });

        var authService = new HubAuthService(HubConfigStore);
        var result = await authService.SignInWithBrowserAsync(api, _hubServerConfig, _hubSignInCts.Token)
            .ConfigureAwait(false);

        _hubMe = result.Me;
        Post(new { type = "hubAuthState", payload = BuildAuthPayload(signingIn: false) });
        await PushHubMeAsync(result.Me).ConfigureAwait(false);
        Post(new { type = "toast", message = $"已通过 Microsoft 账号登录：{result.Me.DisplayName}" });

        var activeRegion = HubConfigStore.Load().Hub.ActiveRegion;
        await RefreshHubRoomsAsync(activeRegion, null, null).ConfigureAwait(false);
    }

    private async Task HandleHubLogoutAsync()
    {
        StopRoomStream();
        await EnsureHubApiClient().LogoutAsync().ConfigureAwait(false);
        new HubAuthService(HubConfigStore).ClearSession();
        EnsureHubApiClient().SetToken("");
        _hubMe = null;
        _hubActiveRoomId = null;
        Post(new { type = "hubAuthState", payload = BuildAuthPayload(signingIn: false) });
        Post(new { type = "hubRoomLeft", payload = new { reason = "logout" } });
        Post(new { type = "toast", message = "已退出组队大厅账号" });
    }

    private async Task RefreshHubMeAsync()
    {
        var api = EnsureHubApiClient();
        if (!api.HasToken)
        {
            Post(new
            {
                type = "hubInitState",
                payload = new
                {
                    serverUrl = HubConfigStore.Load().Hub.ServerUrl,
                    activeRegion = HubConfigStore.Load().Hub.ActiveRegion,
                    autoJoinEnabled = _hubAutoJoinEnabled,
                    serverConfig = _hubServerConfig,
                    auth = BuildAuthPayload(signingIn: false),
                    me = (object?)null,
                    localAccounts = DiscoverLocalAccountsForUi(),
                    bridge = BuildBridgeStatusPayload(_hubBridge?.GetActiveSessions() ?? Array.Empty<BridgeSessionState>()),
                }
            });
            return;
        }

        _hubMe = await api.GetMeAsync().ConfigureAwait(false);
        await PushHubMeAsync(_hubMe).ConfigureAwait(false);
    }

    private async Task HandleHubUpdateProfileAsync(JsonElement root)
    {
        var primaryRegion = ReadJsonStringOrNull(root, "primaryRegion");
        var primaryUid = ReadJsonStringOrNull(root, "primaryUid");
        var me = await EnsureHubApiClient().UpdateProfileAsync(null, primaryRegion, primaryUid).ConfigureAwait(false);
        _hubMe = me;
        await PushHubMeAsync(me).ConfigureAwait(false);
        Post(new { type = "toast", message = "已切换主展示游戏账号" });
    }

    private async Task HandleHubUpdatePrivacyAsync(JsonElement root)
    {
        var privacy = JsonSerializer.Deserialize<HubPrivacySettings>(root.GetProperty("privacy").GetRawText(), WebReadOptions)
                      ?? new HubPrivacySettings();
        var me = await EnsureHubApiClient().UpdatePrivacyAsync(privacy).ConfigureAwait(false);
        _hubMe = me;
        await PushHubMeAsync(me).ConfigureAwait(false);
        Post(new { type = "toast", message = "隐私设置已保存" });
    }

    private async Task HandleHubBindLocalAccountAsync(JsonElement root)
    {
        var directory = _modManager.ResolveGameDirectory();
        if (string.IsNullOrWhiteSpace(directory))
        {
            Post(new { type = "toast", message = "未检测到有效的游戏安装目录。" });
            return;
        }

        var uid = ReadJsonString(root, "uid");
        var setPrimary = root.TryGetProperty("setPrimary", out var spEl) && spEl.ValueKind == JsonValueKind.True;
        var syncSnapshot = !root.TryGetProperty("syncSnapshot", out var ssEl) || ssEl.ValueKind != JsonValueKind.False;

        if (!AccountCredentials.ValidUid(uid))
        {
            Post(new { type = "toast", message = "请选择有效的主 UID。" });
            return;
        }

        if (!MyAccountConsent.Granted)
        {
            MyAccountConsent.Accept();
        }

        if (syncSnapshot && MyAccountGameRunning(directory))
        {
            Post(new { type = "toast", message = "检测到游戏正在运行，请退出游戏后再同步完整战绩，或使用「绑定在线角色」。" });
            return;
        }

        var sync = new HubProfileSyncService(_appDirectory);
        string region;
        HubGameSnapshot? snapshot = null;
        object[]? liveFriends = null;
        DateTimeOffset? capturedAt = null;

        if (syncSnapshot)
        {
            Post(new { type = "hubAccountLiveState", payload = new { uid, loading = true } });
            Post(new { type = "toast", message = $"正在同步 UID {uid} 的账号数据…" });
            try
            {
                var live = await sync.CaptureLiveAccountAsync(
                    directory,
                    uid,
                    _libraryService.Settings.Language,
                    p => AssetUrl(p ?? ""),
                    EnsureHubAssets(),
                    CancellationToken.None).ConfigureAwait(false);
                region = live.Region;
                snapshot = live.Snapshot;
                liveFriends = live.Friends;
                capturedAt = live.CapturedAt;
            }
            finally
            {
                Post(new { type = "hubAccountLiveState", payload = new { uid, loading = false } });
            }
        }
        else
        {
            var found = sync.DiscoverLocalAccounts(directory).FirstOrDefault(x => x.Uid == uid);
            region = found.Region is { Length: > 0 } ? found.Region : "cn";
        }

        var me = await EnsureHubApiClient().BindGameAccountAsync(region, uid, setPrimary, snapshot).ConfigureAwait(false);
        _hubMe = me;
        await PushHubMeAsync(me).ConfigureAwait(false);
        if (liveFriends is not null)
        {
            Post(new { type = "hubAccountLiveData", payload = new { uid, region, friends = liveFriends, capturedAt } });
        }
        Post(new { type = "toast", message = $"已绑定游戏账号 {uid}（{(region == "int" ? "国际服" : "国服")}）" });
    }

    private async Task HandleHubRefreshLiveAccountAsync(JsonElement root)
    {
        var directory = _modManager.ResolveGameDirectory();
        if (string.IsNullOrWhiteSpace(directory))
        {
            Post(new { type = "toast", message = "未检测到有效的游戏安装目录。" });
            return;
        }

        var uid = ReadJsonString(root, "uid");
        var syncToHub = !root.TryGetProperty("syncToHub", out var stEl) || stEl.ValueKind != JsonValueKind.False;
        if (!AccountCredentials.ValidUid(uid))
        {
            Post(new { type = "toast", message = "请选择要刷新的游戏 UID。" });
            return;
        }

        if (!AccountCredentials.Discover(directory).Contains(uid))
        {
            Post(new { type = "toast", message = $"本机未找到 UID {uid} 的登录凭据，无法直连刷新。" });
            return;
        }

        if (MyAccountGameRunning(directory))
        {
            Post(new { type = "hubAccountLiveState", payload = new { uid, loading = false, gameRunning = true } });
            Post(new { type = "toast", message = "游戏正在运行，请退出游戏后再直连刷新战绩与好友状态。" });
            return;
        }

        if (!MyAccountConsent.Granted)
        {
            MyAccountConsent.Accept();
        }

        Post(new { type = "hubAccountLiveState", payload = new { uid, loading = true } });
        try
        {
            var sync = new HubProfileSyncService(_appDirectory);
            var (region, snapshot, friends, capturedAt) = await sync.CaptureLiveAccountAsync(
                directory,
                uid,
                _libraryService.Settings.Language,
                p => AssetUrl(p ?? ""),
                EnsureHubAssets(),
                CancellationToken.None).ConfigureAwait(false);

            if (syncToHub && EnsureHubApiClient().HasToken)
            {
                var me = await EnsureHubApiClient().BindGameAccountAsync(region, uid, setPrimary: false, snapshot)
                    .ConfigureAwait(false);
                _hubMe = me;
                await PushHubMeAsync(me).ConfigureAwait(false);
            }

            Post(new { type = "hubAccountLiveData", payload = new { uid, region, friends, capturedAt } });
            Post(new { type = "toast", message = $"已刷新 {snapshot.Nick}（UID {uid}）的最新战绩与好友状态" });
        }
        finally
        {
            Post(new { type = "hubAccountLiveState", payload = new { uid, loading = false } });
        }
    }

    private async Task HandleHubBindBridgeAccountAsync(JsonElement root)
    {
        var sessions = _hubBridge?.GetActiveSessions() ?? Array.Empty<BridgeSessionState>();
        var live = sessions.FirstOrDefault(s => s.IsLoggedIn && s.Uid > 0);
        if (live is null)
        {
            Post(new { type = "toast", message = "未检测到正在登录中的游戏实例。请确保游戏已安装 HubBridgeMod 并进入主界面。" });
            return;
        }

        var region = HubSettlementService.NormalizeRegion(live.Region);
        var setPrimary = !root.TryGetProperty("setPrimary", out var spEl) || spEl.ValueKind != JsonValueKind.False;
        var me = await EnsureHubApiClient()
            .BindGameAccountAsync(region, live.Uid.ToString(), setPrimary, snapshot: null)
            .ConfigureAwait(false);
        _hubMe = me;
        await PushHubMeAsync(me).ConfigureAwait(false);
        Post(new { type = "toast", message = $"已绑定当前在线游戏角色 UID {live.Uid}（{(region == "int" ? "国际服" : "国服")}）" });
    }

    private async Task RefreshHubRoomsAsync(string region, string? query, string? status)
    {
        try
        {
            var list = await EnsureHubApiClient().ListRoomsAsync(region, query, status).ConfigureAwait(false);
            var payload = await BuildRoomListPayloadAsync(list).ConfigureAwait(false);
            Post(new { type = "hubRoomsList", payload });
        }
        catch (Exception ex)
        {
            ToysLog.Warn("hub", $"刷新组队大厅列表失败：{ex.Message}");
            Post(new { type = "hubRoomsList", payload = new { region, rooms = Array.Empty<object>(), error = ex.Message } });
        }
    }

    private async Task HandleHubCreateRoomAsync(JsonElement root)
    {
        var region = HubSettlementService.NormalizeRegion(ReadJsonString(root, "region", HubConfigStore.Load().Hub.ActiveRegion));
        var title = ReadJsonString(root, "title");
        var note = ReadJsonStringOrNull(root, "note");
        var password = ReadJsonStringOrNull(root, "password");
        var capacity = root.TryGetProperty("capacity", out var capEl) && capEl.TryGetInt32(out var c) ? Math.Clamp(c, 2, 4) : 4;
        var activeUid = ReadJsonStringOrNull(root, "activeUid");

        var room = await EnsureHubApiClient().CreateRoomAsync(region, title, note, password, capacity, activeUid)
            .ConfigureAwait(false);
        await ApplyHubRoomUpdateAsync(room).ConfigureAwait(false);
        StartRoomStream(room.RoomId, room.Cursor);
        Post(new { type = "toast", message = $"已创建组队房间「{room.Title}」" });
        await RefreshHubRoomsAsync(region, null, null).ConfigureAwait(false);
    }

    private async Task HandleHubJoinRoomAsync(JsonElement root)
    {
        var roomId = ReadJsonString(root, "roomId");
        var password = ReadJsonStringOrNull(root, "password");
        var activeUid = ReadJsonStringOrNull(root, "activeUid");

        var room = await EnsureHubApiClient().JoinRoomAsync(roomId, password, activeUid).ConfigureAwait(false);
        await ApplyHubRoomUpdateAsync(room).ConfigureAwait(false);
        StartRoomStream(room.RoomId, room.Cursor);
        Post(new { type = "toast", message = $"已加入房间「{room.Title}」" });
        await RefreshHubRoomsAsync(room.Region, null, null).ConfigureAwait(false);
    }

    private async Task HandleHubLeaveRoomAsync()
    {
        var roomId = _hubActiveRoomId;
        StopRoomStream();
        ClearLocalActiveRoomState();

        if (!string.IsNullOrEmpty(roomId))
        {
            try { await EnsureHubApiClient().LeaveRoomAsync(roomId).ConfigureAwait(false); }
            catch { }
        }

        Post(new { type = "hubRoomLeft", payload = new { reason = "user" } });
        await RefreshHubRoomsAsync(HubConfigStore.Load().Hub.ActiveRegion, null, null).ConfigureAwait(false);
    }

    private async Task HandleHubDisbandRoomAsync()
    {
        var roomId = _hubActiveRoomId;
        if (string.IsNullOrEmpty(roomId)) return;

        await EnsureHubApiClient().DisbandRoomAsync(roomId).ConfigureAwait(false);
        StopRoomStream();
        ClearLocalActiveRoomState();

        Post(new { type = "hubRoomLeft", payload = new { reason = "disbanded" } });
        Post(new { type = "toast", message = "已解散组队房间" });
        await RefreshHubRoomsAsync(HubConfigStore.Load().Hub.ActiveRegion, null, null).ConfigureAwait(false);
    }

    private void ClearLocalActiveRoomState()
    {
        _hubActiveRoomId = null;
        _hubActiveRoomRegion = null;
        _hubActiveRoomTitle = null;
        _hubActiveRoomHostUserId = null;
        _hubLastReportedGameRoomId = 0;
        _hubLastReportedGamePwd = "";
        _hubLastAutoJoinedKey = "";
    }

    private async Task HandleHubHostLaunchGameAsync(JsonElement root)
    {
        EnsureHubInitialized();

        if (!string.IsNullOrEmpty(_hubActiveRoomId))
        {
            var room = await EnsureHubApiClient().NotifyHostLaunchingAsync(_hubActiveRoomId).ConfigureAwait(false);
            await ApplyHubRoomUpdateAsync(room).ConfigureAwait(false);
        }

        var mode = ReadJsonString(root, "mode", "bypassSteam");
        var profileName = ReadJsonStringOrNull(root, "profileName");
        var gamePwd = ReadJsonString(root, "gamePwd").Trim();
        if (string.IsNullOrEmpty(gamePwd))
        {
            gamePwd = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
        }
        var mapType = root.TryGetProperty("mapType", out var mtEl) && mtEl.TryGetInt32(out var mt) && mt > 0 ? mt : 4;
        var mapId = root.TryGetProperty("mapId", out var midEl) && midEl.TryGetInt32(out var mid) && mid > 0 ? mid : 0;
        var modeLabel = mapType == 1 ? "PVP" : "PVE";
        var roomName = !string.IsNullOrWhiteSpace(_hubActiveRoomTitle)
            ? _hubActiveRoomTitle!
            : (_hubMe?.DisplayName ?? "AstralParty");

        var sessions = _hubBridge?.GetActiveSessions() ?? Array.Empty<BridgeSessionState>();
        var liveSession = sessions.FirstOrDefault(s =>
            s.IsLoggedIn &&
            (string.IsNullOrEmpty(_hubActiveRoomRegion) ||
             string.IsNullOrEmpty(s.Region) ||
             string.Equals(HubSettlementService.NormalizeRegion(s.Region), _hubActiveRoomRegion, StringComparison.OrdinalIgnoreCase)));

        _hubBridge?.EnqueueCreateRoom(roomName, gamePwd, mapType, mapId);

        if (liveSession is not null)
        {
            Post(new { type = "toast", message = $"已通知运行中的游戏自动创建 {modeLabel} 房间（密码 {gamePwd}）…" });
            return;
        }

        Dispatcher.Invoke(() =>
        {
            SyncHubBridgeEndpoints();
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                HandleAccountProfileLaunch(profileName, mode);
            }
            else
            {
                HandleLaunchGame(mode);
            }
        });
        Post(new { type = "toast", message = $"正在启动游戏，进入主界面后将自动创建 {modeLabel} 房间（密码 {gamePwd}）…" });
    }

    private void HandleHubTriggerBridgeJoin(JsonElement root)
    {
        EnsureHubInitialized();
        var gameRoomId = root.TryGetProperty("gameRoomId", out var idEl) && idEl.TryGetInt64(out var id) ? id : 0;
        var gamePwd = ReadJsonString(root, "gamePwd");
        var roomServerId = root.TryGetProperty("roomServerId", out var srvEl) && srvEl.TryGetInt32(out var s) ? s : 0;
        if (gameRoomId <= 0 || _hubBridge is null)
        {
            Post(new { type = "toast", message = "暂无可加入的游戏房间号（等待房主在游戏内开房）。" });
            return;
        }

        _hubBridge.EnqueueJoinRoom(gameRoomId, gamePwd, roomServerId);
        Post(new { type = "toast", message = $"已向运行中的游戏发送自动进房指令（房间号 {gameRoomId}）。" });
    }

    private async Task ApplyHubRoomUpdateAsync(HubRoomDetailDto room, CancellationToken ct = default)
    {
        _hubActiveRoomId = room.RoomId;
        _hubActiveRoomRegion = room.Region;
        _hubActiveRoomTitle = room.Title;
        _hubActiveRoomHostUserId = room.HostUserId;
        var payload = await BuildRoomPayloadAsync(room, ct).ConfigureAwait(false);
        Post(new { type = "hubRoomState", payload });
    }

    private async Task TrySyncHostGameRoomFromSessionsAsync(IReadOnlyList<BridgeSessionState> sessions)
    {
        var roomId = _hubActiveRoomId;
        var api = EnsureHubApiClient();
        if (string.IsNullOrEmpty(roomId) || !api.HasToken || _hubMe is null) return;
        if (!string.Equals(_hubActiveRoomHostUserId, _hubMe.UserId, StringComparison.Ordinal)) return;

        var hostSession = sessions.FirstOrDefault(s =>
            s.IsLoggedIn &&
            s.IsMaster &&
            s.GameRoomId > 0 &&
            (string.IsNullOrEmpty(_hubActiveRoomRegion) ||
             string.IsNullOrEmpty(s.Region) ||
             string.Equals(HubSettlementService.NormalizeRegion(s.Region), _hubActiveRoomRegion, StringComparison.OrdinalIgnoreCase)));
        if (hostSession is null) return;

        if (hostSession.GameRoomId == _hubLastReportedGameRoomId &&
            string.Equals(hostSession.GamePwd, _hubLastReportedGamePwd, StringComparison.Ordinal))
        {
            return;
        }

        _hubLastReportedGameRoomId = hostSession.GameRoomId;
        _hubLastReportedGamePwd = hostSession.GamePwd;
        try
        {
            var updated = await api.ReportGameRoomAsync(
                roomId,
                hostSession.GameRoomId,
                hostSession.GamePwd,
                hostSession.RoomServerId,
                hostSession.MapId,
                hostSession.MapType).ConfigureAwait(false);
            await ApplyHubRoomUpdateAsync(updated).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ToysLog.Warn("hub", $"从心跳同步房主游戏房间号失败：{ex.Message}");
        }
    }

    private void StartRoomStream(string roomId, long afterCursor)
    {
        StopRoomStream();
        var cts = new CancellationTokenSource();
        _hubStreamCts = cts;
        _ = RunRoomStreamLoopAsync(roomId, afterCursor, cts.Token);
    }

    private void StopRoomStream()
    {
        try { _hubStreamCts?.Cancel(); _hubStreamCts?.Dispose(); }
        catch { }
        _hubStreamCts = null;
    }

    private async Task RunRoomStreamLoopAsync(string roomId, long cursor, CancellationToken ct)
    {
        var api = EnsureHubApiClient();
        long currentEpoch = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var stream = await api.StreamRoomAsync(roomId, cursor, 25, ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested) break;

                if (currentEpoch != 0 && stream.Epoch != currentEpoch)
                {
                    Post(new { type = "hubRoomReset", payload = new { roomId, epoch = stream.Epoch } });
                }
                currentEpoch = stream.Epoch;
                cursor = stream.NextAfter;

                await ApplyHubRoomUpdateAsync(stream.Room, ct).ConfigureAwait(false);
                if (stream.Events.Count > 0)
                {
                    Post(new { type = "hubRoomEvents", payload = new { roomId, events = stream.Events } });
                }

                await TryAutoTriggerMemberJoinAsync(stream.Room, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (HubApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    ClearExpiredHubAuth("登录会话已过期，请重新登录。");
                }
                else
                {
                    ClearLocalActiveRoomState();
                    Post(new { type = "hubRoomLeft", payload = new { reason = ex.ErrorCode, message = ex.Message } });
                    if (ex.ErrorCode == "room_gone")
                    {
                        Post(new { type = "toast", message = "当前组队房间已被解散或关闭。" });
                    }
                    await RefreshHubRoomsAsync(HubConfigStore.Load().Hub.ActiveRegion, null, null).ConfigureAwait(false);
                }
                break;
            }
            catch
            {
                try { await Task.Delay(2500, ct).ConfigureAwait(false); }
                catch { break; }
            }
        }
    }

    private async Task TryAutoTriggerMemberJoinAsync(HubRoomDetailDto room, CancellationToken ct)
    {
        if (!_hubAutoJoinEnabled || _hubBridge is null || _hubMe is null) return;
        if (string.Equals(room.HostUserId, _hubMe.UserId, StringComparison.Ordinal)) return;
        if (room.GameRoom is null || room.GameRoom.GameRoomId <= 0) return;

        var key = $"{room.GameRoom.GameRoomId}:{room.GameRoom.PublishedAt}";
        if (string.Equals(_hubLastAutoJoinedKey, key, StringComparison.Ordinal)) return;

        var live = _hubBridge.GetActiveSessions().FirstOrDefault(s =>
            s.IsLoggedIn &&
            s.Uid > 0 &&
            string.Equals(HubSettlementService.NormalizeRegion(s.Region), room.Region, StringComparison.OrdinalIgnoreCase));
        if (live is null || live.GameRoomId == room.GameRoom.GameRoomId) return;

        _hubLastAutoJoinedKey = key;
        _hubBridge.EnqueueJoinRoom(room.GameRoom.GameRoomId, room.GameRoom.GamePwd, room.GameRoom.RoomServerId);
        try
        {
            await EnsureHubApiClient().ReportMemberStateAsync(room.RoomId, "joining", null, ct).ConfigureAwait(false);
        }
        catch { }
    }

    private void OnHubBridgeEvent(BridgeEventEnvelope env)
    {
        Post(new { type = "hubBridgeEvent", payload = env });
        _ = HandleBridgeEventAsync(env);
    }

    private async Task HandleBridgeEventAsync(BridgeEventEnvelope env)
    {
        var api = EnsureHubApiClient();
        var roomId = _hubActiveRoomId;

        switch (env.Type)
        {
            case "login_ready":
            {
                if (!string.IsNullOrEmpty(roomId) && api.HasToken && env.Data.ValueKind == JsonValueKind.Object)
                {
                    var uid = env.Data.TryGetProperty("uid", out var uidEl) && uidEl.TryGetInt64(out var u) ? u : 0;
                    var region = HubSettlementService.NormalizeRegion(
                        env.Data.TryGetProperty("region", out var regEl) ? regEl.GetString() : "");
                    if (uid > 0 && string.Equals(region, _hubActiveRoomRegion, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_hubMe is not null &&
                            _hubMe.Bindings.Any(b => b.Region == region && b.Uid == uid.ToString()))
                        {
                            try { await api.SwitchActiveUidAsync(roomId, uid.ToString()).ConfigureAwait(false); }
                            catch { }
                        }
                        try { await api.ReportMemberStateAsync(roomId, "in_lobby").ConfigureAwait(false); }
                        catch { }
                    }
                }
                break;
            }

            case "room_created":
            case "room_updated":
            {
                if (!string.IsNullOrEmpty(roomId) && api.HasToken && _hubMe is not null &&
                    string.Equals(_hubActiveRoomHostUserId, _hubMe.UserId, StringComparison.Ordinal) &&
                    env.Data.ValueKind == JsonValueKind.Object)
                {
                    var isMaster = env.Data.TryGetProperty("isMaster", out var mEl) && mEl.ValueKind == JsonValueKind.True;
                    var gameRoomId = env.Data.TryGetProperty("gameRoomId", out var gidEl) && gidEl.TryGetInt64(out var g) ? g : 0;
                    var gamePwd = env.Data.TryGetProperty("gamePwd", out var pwdEl) ? (pwdEl.GetString() ?? "") : "";
                    var srvId = env.Data.TryGetProperty("roomServerId", out var srvEl) && srvEl.TryGetInt32(out var sv) ? sv : 0;
                    var mapId = env.Data.TryGetProperty("mapId", out var midEl) && midEl.TryGetInt32(out var mid) ? mid : 0;
                    var mapType = env.Data.TryGetProperty("mapType", out var mtEl) && mtEl.TryGetInt32(out var mt) ? mt : 0;

                    if (isMaster && gameRoomId > 0 &&
                        (gameRoomId != _hubLastReportedGameRoomId || !string.Equals(gamePwd, _hubLastReportedGamePwd, StringComparison.Ordinal)))
                    {
                        _hubLastReportedGameRoomId = gameRoomId;
                        _hubLastReportedGamePwd = gamePwd;
                        try
                        {
                            var updated = await api.ReportGameRoomAsync(roomId, gameRoomId, gamePwd, srvId, mapId, mapType)
                                .ConfigureAwait(false);
                            await ApplyHubRoomUpdateAsync(updated).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            ToysLog.Warn("hub", $"同步房主游戏房间号失败：{ex.Message}");
                        }
                    }
                }
                break;
            }

            case "room_entered":
            case "room_joined":
                if (!string.IsNullOrEmpty(roomId) && api.HasToken)
                {
                    try { await api.ReportMemberStateAsync(roomId, "in_game_room").ConfigureAwait(false); }
                    catch { }
                }
                break;

            case "room_left":
                if (!string.IsNullOrEmpty(roomId) && api.HasToken)
                {
                    try { await api.ReportMemberStateAsync(roomId, "in_lobby").ConfigureAwait(false); }
                    catch { }
                }
                break;

            case "create_failed":
            {
                var reason = env.Data.ValueKind == JsonValueKind.Object
                    ? (ReadJsonStringOrNull(env.Data, "message") ?? ReadJsonStringOrNull(env.Data, "reason") ?? "自动创建游戏房间失败")
                    : "自动创建游戏房间失败";
                Post(new { type = "toast", message = reason });
                break;
            }

            case "join_failed":
            {
                var reason = env.Data.ValueKind == JsonValueKind.Object
                    ? (ReadJsonStringOrNull(env.Data, "message") ?? ReadJsonStringOrNull(env.Data, "reason") ?? "自动进房失败")
                    : "自动进房失败";
                Post(new { type = "toast", message = reason });
                if (!string.IsNullOrEmpty(roomId) && api.HasToken)
                {
                    try { await api.ReportMemberStateAsync(roomId, "join_failed", reason).ConfigureAwait(false); }
                    catch { }
                }
                break;
            }

            case "game_finished":
            {
                var summary = HubSettlementService.ParseGameFinishedEvent(env);
                if (summary is null) break;

                var assets = EnsureHubAssets();
                var stage1 = EnsureHubSettlement().BuildStage1Payload(
                    summary,
                    _libraryService.Settings.Language,
                    p => AssetUrl(p ?? ""),
                    assets,
                    roomId);
                Post(new { type = "hubSettlementStage1", payload = stage1 });

                if (!string.IsNullOrEmpty(roomId) && api.HasToken)
                {
                    try
                    {
                        var finished = await api.FinishMatchAsync(roomId, summary).ConfigureAwait(false);
                        await ApplyHubRoomUpdateAsync(finished.Room).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ToysLog.Warn("hub", $"上传统一战报到组队大厅失败：{ex.Message}");
                    }
                }

                if (ReplayCdn.IsValidReplayId(summary.ReplayId, out _))
                {
                    StartSettlementPolling(summary.Region, summary.ReplayId);
                }
                break;
            }
        }
    }

    private void StartSettlementPolling(string region, string replayId)
    {
        _hubSettlementCts?.Cancel();
        _hubSettlementCts?.Dispose();
        var cts = new CancellationTokenSource();
        _hubSettlementCts = cts;

        Post(new
        {
            type = "hubSettlementStage2Status",
            payload = new { region, replayId, status = "polling", message = "正在等待官方回放 CDN 生成完整战报…" }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                var settlement = EnsureHubSettlement();
                var assets = EnsureHubAssets();
                var lang = _libraryService.Settings.Language;
                var (ok, message, report) = await settlement.PollAndAnalyzeReplayAsync(
                    region,
                    replayId,
                    lang,
                    assets,
                    _assetDirectory,
                    _materialDirectories,
                    cts.Token).ConfigureAwait(false);

                if (cts.IsCancellationRequested) return;

                if (ok && report is not null)
                {
                    _hubLastSettlementPath = report.FilePath;
                    var stage2 = settlement.BuildStage2Payload(replayId, report, p => AssetUrl(p ?? ""));
                    Post(new
                    {
                        type = "hubSettlementStage2",
                        payload = new { region, replayId, status = "ready", message, report = stage2 }
                    });
                    await SendReplayLibraryAsync(runMaintain: false).ConfigureAwait(false);
                }
                else
                {
                    Post(new
                    {
                        type = "hubSettlementStage2Status",
                        payload = new { region, replayId, status = "failed", message }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception ex)
            {
                Post(new
                {
                    type = "hubSettlementStage2Status",
                    payload = new { region, replayId, status = "failed", message = $"回放拉取或解析异常：{ex.Message}" }
                });
            }
        }, cts.Token);
    }

    private void ClearExpiredHubAuth(string toastMessage)
    {
        StopRoomStream();
        new HubAuthService(HubConfigStore).ClearSession();
        EnsureHubApiClient().SetToken("");
        _hubMe = null;
        _hubActiveRoomId = null;
        Post(new { type = "hubAuthState", payload = BuildAuthPayload(signingIn: false, error: toastMessage) });
        Post(new { type = "toast", message = toastMessage });
    }

    private object BuildAuthPayload(bool signingIn, string? error = null)
    {
        var cfg = HubConfigStore.Load().Hub;
        var hasToken = EnsureHubApiClient().HasToken;
        return new
        {
            signedIn = hasToken && _hubMe is not null,
            hasStoredToken = hasToken,
            signingIn,
            userId = _hubMe?.UserId ?? cfg.UserId,
            displayName = _hubMe?.DisplayName ?? cfg.DisplayName,
            expiresAt = cfg.TokenExpiresAt,
            error = error ?? "",
        };
    }

    private async Task<string> ResolveHubAvatarUrlAsync(string? serverPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverPath) || !serverPath.StartsWith("/api/hub/avatars/", StringComparison.Ordinal))
        {
            return "";
        }

        var api = EnsureHubApiClient();
        var cacheKey = $"{api.BaseUrl}|{serverPath}";
        if (_hubAvatarUrlCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))[..16].ToLowerInvariant();
        var dir = ToysPaths.HubAvatarCacheDirectory;
        try
        {
            Directory.CreateDirectory(dir);
            foreach (var ext in new[] { ".jpg", ".png", ".webp" })
            {
                var existing = Path.Combine(dir, $"av_{hash}{ext}");
                if (File.Exists(existing) && new FileInfo(existing).Length > 0)
                {
                    var url = $"https://hubavatars.aspatoys.hiynet.localhost/av_{hash}{ext}";
                    _hubAvatarUrlCache[cacheKey] = url;
                    return url;
                }
            }

            var downloaded = await api.DownloadAvatarAsync(serverPath, ct).ConfigureAwait(false);
            if (downloaded is null) return "";

            var fileExt = downloaded.Value.ContentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg",
            };
            var fileName = $"av_{hash}{fileExt}";
            var target = Path.Combine(dir, fileName);
            var tmp = target + ".tmp";
            await File.WriteAllBytesAsync(tmp, downloaded.Value.Bytes, ct).ConfigureAwait(false);
            File.Move(tmp, target, overwrite: true);

            var localUrl = $"https://hubavatars.aspatoys.hiynet.localhost/{fileName}";
            _hubAvatarUrlCache[cacheKey] = localUrl;
            return localUrl;
        }
        catch
        {
            return "";
        }
    }

    private async Task<object> BuildDecoratedProfileAsync(HubUserProfileDto profile, CancellationToken ct = default)
    {
        var avatar = await ResolveHubAvatarUrlAsync(profile.AvatarUrl, ct).ConfigureAwait(false);
        return new HubProfileSyncService(_appDirectory).DecorateProfileForUi(
            profile,
            _libraryService.Settings.Language,
            p => AssetUrl(p ?? ""),
            EnsureHubAssets(),
            hubAvatarUrlMapper: _ => avatar,
            indexedMapIdLookup: id => _libraryService.IndexedMapId(id));
    }

    private async Task<object> BuildRoomPayloadAsync(HubRoomDetailDto room, CancellationToken ct = default)
    {
        var avatarTasks = room.Members
            .Select(m => m.AvatarUrl)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(u => u, u => ResolveHubAvatarUrlAsync(u, ct), StringComparer.Ordinal);
        if (avatarTasks.Count > 0)
        {
            await Task.WhenAll(avatarTasks.Values).ConfigureAwait(false);
        }

        return new
        {
            room.RoomId,
            room.Region,
            room.Title,
            room.Note,
            room.HasPassword,
            room.Capacity,
            room.OnlineCount,
            room.Status,
            room.HostUserId,
            room.Epoch,
            room.Cursor,
            room.CreatedAt,
            room.LastActivityAt,
            room.GameRoom,
            room.LatestMatch,
            room.ServerTime,
            members = room.Members.Select(m => new
            {
                m.UserId,
                m.DisplayName,
                avatarUrl = !string.IsNullOrWhiteSpace(m.AvatarUrl) && avatarTasks.TryGetValue(m.AvatarUrl, out var t) ? t.Result : "",
                m.ActiveUid,
                m.ActiveNick,
                m.ActiveHeadIcon,
                m.ActiveLevel,
                m.IsHost,
                m.Online,
                m.Self,
                m.GameState,
                m.JoinError,
                m.JoinedAt,
                m.LastSeenAt,
            }).ToArray(),
        };
    }

    private async Task<object> BuildRoomListPayloadAsync(HubRoomListResponse list, CancellationToken ct = default)
    {
        var avatarTasks = list.Rooms
            .Select(r => r.HostAvatarUrl)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(u => u, u => ResolveHubAvatarUrlAsync(u, ct), StringComparer.Ordinal);
        if (avatarTasks.Count > 0)
        {
            await Task.WhenAll(avatarTasks.Values).ConfigureAwait(false);
        }

        return new
        {
            list.Region,
            rooms = list.Rooms.Select(r => new
            {
                r.RoomId,
                r.Region,
                r.Title,
                r.Note,
                r.HasPassword,
                r.Capacity,
                r.MemberCount,
                r.OnlineCount,
                r.Status,
                r.HostUserId,
                r.HostDisplayName,
                hostAvatarUrl = !string.IsNullOrWhiteSpace(r.HostAvatarUrl) && avatarTasks.TryGetValue(r.HostAvatarUrl, out var t) ? t.Result : "",
                r.HostActiveUid,
                r.CreatedAt,
                r.LastActivityAt,
            }).ToArray(),
        };
    }

    private async Task PushHubMeAsync(HubUserProfileDto profile, CancellationToken ct = default)
    {
        var me = await BuildDecoratedProfileAsync(profile, ct).ConfigureAwait(false);
        Post(new
        {
            type = "hubMe",
            payload = new
            {
                me,
                localAccounts = DiscoverLocalAccountsForUi(),
            }
        });
    }

    private async Task PushHubViewedProfileAsync(HubUserProfileDto profile, CancellationToken ct = default)
    {
        var decorated = await BuildDecoratedProfileAsync(profile, ct).ConfigureAwait(false);
        Post(new
        {
            type = "hubViewedProfile",
            payload = decorated
        });
    }

    private object[] DiscoverLocalAccountsForUi()
    {
        var dir = _modManager.ResolveGameDirectory();
        if (string.IsNullOrWhiteSpace(dir)) return [];

        if (MyAccountConsent.Granted)
        {
            return new HubProfileSyncService(_appDirectory)
                .DiscoverLocalAccounts(dir)
                .Select(x => (object)new { uid = x.Uid, region = x.Region })
                .ToArray();
        }

        return AccountCredentials.Discover(dir)
            .Select(uid => (object)new { uid, region = "" })
            .ToArray();
    }

    private object BuildBridgeStatusPayload(IReadOnlyList<BridgeSessionState> sessions)
    {
        var gameDir = _modManager.ResolveGameDirectory();
        var status = _modManager.GetStatus();
        var mod = status.Mods.FirstOrDefault(m => m.Id == "HubBridgeMod");
        return new
        {
            running = _hubBridge?.IsRunning == true,
            port = _hubBridge?.Port ?? 0,
            gameFound = status.GameExeFound,
            gameDirectory = gameDir ?? "",
            modInstalled = mod is not null,
            modEnabled = mod?.Enabled == true,
            sessions = sessions.Select(s => new
            {
                s.SessionId,
                s.IsLoggedIn,
                uid = s.Uid > 0 ? s.Uid.ToString() : "",
                region = HubSettlementService.NormalizeRegion(s.Region),
                s.Scene,
                s.GameRoomId,
                s.GamePwd,
                s.RoomServerId,
                s.MapId,
                s.MapType,
                s.IsMaster,
                s.RoomState,
                s.PlayerCount,
            }).ToArray(),
        };
    }

    private static string ReadJsonString(JsonElement root, string prop, string fallback = "")
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
            ? (el.GetString() ?? fallback)
            : fallback;

    private static string? ReadJsonStringOrNull(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
