using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AstralParty.Toys.Services.Hub;

/// <summary>
/// 与游戏内置模组 <c>HubBridgeMod</c> 通信的本机 127.0.0.1 HTTP IPC 服务端。
/// 自动在有效游戏目录的 <c>AstralParty_ModLoader\hub-bridge\endpoint.json</c> 写入连接凭据，
/// 接收登录/建房/进房/对局结束事件，并向指定游戏实例投递自动进房命令。
/// </summary>
public sealed class HubLocalBridgeServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class QueuedBridgeCommand
    {
        public required BridgeCommandDto Command { get; init; }
        public long TargetUid { get; init; }
        public string TargetRegion { get; init; } = "";
        public DateTimeOffset EnqueuedAtUtc { get; init; }
    }

    private readonly string _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private readonly object _gate = new();
    private readonly Dictionary<string, BridgeSessionState> _sessions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenEventIds = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenEventOrder = new();
    private readonly List<QueuedBridgeCommand> _pendingCommands = [];
    private readonly List<string> _writtenEndpointPaths = [];

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private bool _disposed;

    public int Port { get; private set; }
    public string Token => _token;
    public bool IsRunning => _listener?.IsListening == true;

    public event Action<BridgeEventEnvelope>? EventReceived;
    public event Action<IReadOnlyList<BridgeSessionState>>? SessionsChanged;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || IsRunning) return;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                int port;
                using (var tcp = new TcpListener(IPAddress.Loopback, 0))
                {
                    tcp.Start();
                    port = ((IPEndPoint)tcp.LocalEndpoint).Port;
                }

                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                try
                {
                    listener.Start();
                    _listener = listener;
                    Port = port;
                    _cts = new CancellationTokenSource();
                    _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
                    return;
                }
                catch
                {
                    try { listener.Close(); }
                    catch { }
                }
            }

            throw new InvalidOperationException("无法启动 HubBridge 本地回环服务。");
        }
    }

    public void SyncGameDirectories(IEnumerable<string> gameDirectories)
    {
        if (!IsRunning && Port <= 0) return;

        var json = JsonSerializer.Serialize(new BridgeEndpointInfo
        {
            Port = Port,
            Token = _token,
            Pid = Environment.ProcessId,
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        }, JsonOptions);

        var written = new List<string>();
        foreach (var rawDir in gameDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(rawDir)) continue;
            var loaderDir = Path.Combine(rawDir, "AstralParty_ModLoader");
            if (!Directory.Exists(loaderDir)) continue;

            try
            {
                var bridgeDir = Path.Combine(loaderDir, "hub-bridge");
                Directory.CreateDirectory(bridgeDir);
                var target = Path.Combine(bridgeDir, "endpoint.json");
                var temp = target + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                File.Move(temp, target, overwrite: true);
                written.Add(target);
            }
            catch
            {
                // 单目录写失败不阻断其它有效目录。
            }
        }

        lock (_gate)
        {
            foreach (var oldPath in _writtenEndpointPaths)
            {
                if (!written.Contains(oldPath, StringComparer.OrdinalIgnoreCase))
                {
                    TryDeleteFile(oldPath);
                }
            }

            _writtenEndpointPaths.Clear();
            _writtenEndpointPaths.AddRange(written);
        }
    }

    public string EnqueueJoinRoom(
        long gameRoomId,
        string? gamePwd,
        int roomServerId,
        long targetUid = 0,
        string? targetRegion = null)
    {
        var cmdId = Guid.NewGuid().ToString("N");
        var normalizedRegion = NormalizeOptionalRegion(targetRegion);
        lock (_gate)
        {
            _pendingCommands.RemoveAll(q =>
                q.Command.Action == "join_room" &&
                q.TargetUid == targetUid &&
                string.Equals(q.TargetRegion, normalizedRegion, StringComparison.OrdinalIgnoreCase));
            _pendingCommands.Add(new QueuedBridgeCommand
            {
                Command = new BridgeCommandDto
                {
                    CommandId = cmdId,
                    Action = "join_room",
                    GameRoomId = gameRoomId,
                    GamePwd = gamePwd ?? "",
                    RoomServerId = roomServerId,
                },
                TargetUid = targetUid,
                TargetRegion = normalizedRegion,
                EnqueuedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        return cmdId;
    }

    public string EnqueueCreateRoom(
        string? roomName,
        string? gamePwd,
        int mapType = 4,
        int mapId = 0,
        long targetUid = 0,
        string? targetRegion = null)
    {
        var cmdId = Guid.NewGuid().ToString("N");
        var normalizedRegion = NormalizeOptionalRegion(targetRegion);
        lock (_gate)
        {
            _pendingCommands.RemoveAll(q =>
                q.Command.Action == "create_room" &&
                q.TargetUid == targetUid &&
                string.Equals(q.TargetRegion, normalizedRegion, StringComparison.OrdinalIgnoreCase));
            _pendingCommands.Add(new QueuedBridgeCommand
            {
                Command = new BridgeCommandDto
                {
                    CommandId = cmdId,
                    Action = "create_room",
                    RoomName = (roomName ?? "").Trim(),
                    GamePwd = (gamePwd ?? "").Trim(),
                    MapType = mapType > 0 ? mapType : 4,
                    MapId = mapId,
                },
                TargetUid = targetUid,
                TargetRegion = normalizedRegion,
                EnqueuedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        return cmdId;
    }

    public BridgeSessionState? GetSessionById(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        lock (_gate)
        {
            return _sessions.TryGetValue(sessionId, out var s) ? CloneSession(s) : null;
        }
    }

    public IReadOnlyList<BridgeSessionState> GetActiveSessions()
    {
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            var expired = _sessions.Where(kv => now - kv.Value.LastHeartbeatUtc > TimeSpan.FromSeconds(15))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in expired) _sessions.Remove(key);
            return _sessions.Values.Select(CloneSession).ToArray();
        }
    }

    internal (int StatusCode, string Body) HandleRequestInternal(
        string method,
        string path,
        string? bridgeTokenHeader,
        string body)
    {
        if (!IsAuthorized(bridgeTokenHeader))
        {
            return (401, """{"ok":false,"error":"unauthorized"}""");
        }

        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return (405, """{"ok":false,"error":"method_not_allowed"}""");
        }

        if (string.Equals(path, "/api/bridge/heartbeat", StringComparison.OrdinalIgnoreCase))
        {
            return HandleHeartbeat(body);
        }

        if (string.Equals(path, "/api/bridge/events", StringComparison.OrdinalIgnoreCase))
        {
            return HandleEvents(body);
        }

        return (404, """{"ok":false,"error":"not_found"}""");
    }

    private (int StatusCode, string Body) HandleHeartbeat(string body)
    {
        string sessionId = "";
        JsonDocument? doc = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("sessionId", out var sidEl))
                {
                    sessionId = (sidEl.GetString() ?? "").Trim();
                }
            }
        }
        catch
        {
            // 心跳缺少 JSON 体仍容错返回。
        }

        List<BridgeSessionState>? snapshot = null;
        var commands = new List<BridgeCommandDto>();
        try
        {
            var now = DateTimeOffset.UtcNow;
            lock (_gate)
            {
                BridgeSessionState? currentSession = null;
                if (sessionId.Length > 0)
                {
                    if (!_sessions.TryGetValue(sessionId, out currentSession))
                    {
                        currentSession = new BridgeSessionState { SessionId = sessionId };
                        _sessions[sessionId] = currentSession;
                    }
                    currentSession.LastHeartbeatUtc = now;
                    if (doc is not null && doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        ApplyHeartbeatToSession(currentSession, doc.RootElement);
                    }
                    snapshot = _sessions.Values.Select(CloneSession).ToList();
                }

                for (var i = _pendingCommands.Count - 1; i >= 0; i--)
                {
                    if (now - _pendingCommands[i].EnqueuedAtUtc > TimeSpan.FromMinutes(3))
                    {
                        _pendingCommands.RemoveAt(i);
                    }
                }

                for (var i = 0; i < _pendingCommands.Count;)
                {
                    var q = _pendingCommands[i];
                    if (CommandMatchesSession(q, currentSession))
                    {
                        commands.Add(q.Command);
                        _pendingCommands.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }
        finally
        {
            doc?.Dispose();
        }

        if (snapshot is not null)
        {
            SessionsChanged?.Invoke(snapshot);
        }

        var payload = JsonSerializer.Serialize(new { protocol = 1, commands }, JsonOptions);
        return (200, payload);
    }

    private static bool CommandMatchesSession(QueuedBridgeCommand queued, BridgeSessionState? session)
    {
        if (queued.TargetUid > 0)
        {
            if (session is null || !session.IsLoggedIn || session.Uid != queued.TargetUid)
                return false;
        }

        if (!string.IsNullOrEmpty(queued.TargetRegion))
        {
            if (session is null || string.IsNullOrWhiteSpace(session.Region))
                return false;
            var sessionRegion = NormalizeOptionalRegion(session.Region);
            if (!string.Equals(sessionRegion, queued.TargetRegion, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string NormalizeOptionalRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region)) return "";
        return HubSettlementService.NormalizeRegion(region);
    }

    private static void ApplyHeartbeatToSession(BridgeSessionState s, JsonElement root)
    {
        s.IsLoggedIn = ReadBool(root, "isLoggedIn", s.IsLoggedIn);
        s.Uid = ReadLong(root, "uid", s.Uid);
        s.Region = ReadString(root, "region", s.Region);
        s.Scene = ReadString(root, "scene", s.Scene);

        if (root.TryGetProperty("room", out var roomEl))
        {
            if (roomEl.ValueKind == JsonValueKind.Object)
            {
                s.GameRoomId = ReadLong(roomEl, "gameRoomId", s.GameRoomId);
                s.GamePwd = ReadString(roomEl, "gamePwd", s.GamePwd);
                s.RoomServerId = ReadInt(roomEl, "roomServerId", s.RoomServerId);
                s.MapId = ReadInt(roomEl, "mapId", s.MapId);
                s.MapType = ReadInt(roomEl, "mapType", s.MapType);
                s.MasterId = ReadLong(roomEl, "masterId", s.MasterId);
                s.IsMaster = ReadBool(roomEl, "isMaster", s.IsMaster);
                s.RoomState = ReadString(roomEl, "state", s.RoomState);
                s.PlayerCount = ReadInt(roomEl, "playerCount", s.PlayerCount);
            }
            else if (roomEl.ValueKind == JsonValueKind.Null)
            {
                s.GameRoomId = 0;
                s.GamePwd = "";
                s.RoomServerId = 0;
                s.IsMaster = false;
                s.RoomState = "";
                s.PlayerCount = 0;
            }
        }
    }

    private (int StatusCode, string Body) HandleEvents(string body)
    {
        BridgeEventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<BridgeEventEnvelope>(body, JsonOptions);
        }
        catch
        {
            return (400, """{"ok":false,"error":"invalid_json"}""");
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Type))
        {
            return (400, """{"ok":false,"error":"missing_type"}""");
        }

        List<BridgeSessionState>? snapshot = null;
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(envelope.EventId))
            {
                if (!_seenEventIds.Add(envelope.EventId))
                {
                    return (200, """{"ok":true,"duplicate":true}""");
                }
                _seenEventOrder.Enqueue(envelope.EventId);
                while (_seenEventOrder.Count > 512)
                {
                    _seenEventIds.Remove(_seenEventOrder.Dequeue());
                }
            }

            if (!string.IsNullOrWhiteSpace(envelope.SessionId))
            {
                if (!_sessions.TryGetValue(envelope.SessionId, out var session))
                {
                    session = new BridgeSessionState { SessionId = envelope.SessionId };
                    _sessions[envelope.SessionId] = session;
                }
                session.LastHeartbeatUtc = DateTimeOffset.UtcNow;
                ApplyEventToSession(session, envelope);
                snapshot = _sessions.Values.Select(CloneSession).ToList();
            }
        }

        if (snapshot is not null) SessionsChanged?.Invoke(snapshot);
        EventReceived?.Invoke(envelope);
        return (200, """{"ok":true}""");
    }

    private static void ApplyEventToSession(BridgeSessionState s, BridgeEventEnvelope env)
    {
        if (env.Data.ValueKind != JsonValueKind.Object) return;
        var data = env.Data;
        switch (env.Type)
        {
            case "login_ready":
                s.IsLoggedIn = true;
                s.Uid = ReadLong(data, "uid", s.Uid);
                s.Region = ReadString(data, "region", s.Region);
                s.Scene = ReadString(data, "scene", s.Scene);
                break;
            case "room_created":
            case "room_updated":
            case "room_entered":
            case "room_joined":
                s.GameRoomId = ReadLong(data, "gameRoomId", s.GameRoomId);
                s.GamePwd = ReadString(data, "gamePwd", s.GamePwd);
                s.RoomServerId = ReadInt(data, "roomServerId", s.RoomServerId);
                s.MapId = ReadInt(data, "mapId", s.MapId);
                s.MapType = ReadInt(data, "mapType", s.MapType);
                s.MasterId = ReadLong(data, "masterId", s.MasterId);
                s.IsMaster = ReadBool(data, "isMaster", s.IsMaster);
                s.RoomState = ReadString(data, "state", s.RoomState);
                s.PlayerCount = ReadInt(data, "playerCount", s.PlayerCount);
                break;
            case "room_left":
                s.GameRoomId = 0;
                s.GamePwd = "";
                s.RoomServerId = 0;
                s.IsMaster = false;
                s.RoomState = "";
                s.PlayerCount = 0;
                break;
        }
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => ProcessContextAsync(ctx), ct);
        }
    }

    private async Task ProcessContextAsync(HttpListenerContext ctx)
    {
        try
        {
            string body = "";
            if (ctx.Request.HasEntityBody)
            {
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var tokenHeader = ctx.Request.Headers["X-Bridge-Token"];
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var (status, json) = HandleRequestInternal(ctx.Request.HttpMethod, path, tokenHeader, body);
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch
        {
            try { ctx.Response.StatusCode = 500; }
            catch { }
        }
        finally
        {
            try { ctx.Response.Close(); }
            catch { }
        }
    }

    private bool IsAuthorized(string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided)) return false;
        var a = Encoding.UTF8.GetBytes(provided.Trim());
        var b = Encoding.UTF8.GetBytes(_token);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static BridgeSessionState CloneSession(BridgeSessionState s) => new()
    {
        SessionId = s.SessionId,
        IsLoggedIn = s.IsLoggedIn,
        Uid = s.Uid,
        Region = s.Region,
        Scene = s.Scene,
        GameRoomId = s.GameRoomId,
        GamePwd = s.GamePwd,
        RoomServerId = s.RoomServerId,
        MapId = s.MapId,
        MapType = s.MapType,
        MasterId = s.MasterId,
        IsMaster = s.IsMaster,
        RoomState = s.RoomState,
        PlayerCount = s.PlayerCount,
        LastHeartbeatUtc = s.LastHeartbeatUtc,
    };

    private static string ReadString(JsonElement obj, string name, string fallback)
        => obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? (el.GetString() ?? fallback) : fallback;

    private static int ReadInt(JsonElement obj, string name, int fallback)
        => obj.TryGetProperty(name, out var el) && el.TryGetInt32(out var v) ? v : fallback;

    private static long ReadLong(JsonElement obj, string name, long fallback)
        => obj.TryGetProperty(name, out var el) && el.TryGetInt64(out var v) ? v : fallback;

    private static bool ReadBool(JsonElement obj, string name, bool fallback)
        => obj.TryGetProperty(name, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
            ? el.GetBoolean()
            : fallback;

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts?.Cancel(); }
            catch { }
            try { _listener?.Stop(); _listener?.Close(); }
            catch { }
            foreach (var path in _writtenEndpointPaths) TryDeleteFile(path);
            _writtenEndpointPaths.Clear();
        }
    }
}
