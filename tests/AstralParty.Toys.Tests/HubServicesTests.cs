using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.Hub;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class HubServicesTests
{
    [Fact]
    public void HubConfig_NormalizeServerUrl_AllowsHttpsAndLoopbackOnly()
    {
        Assert.Equal(HubConfigSection.DefaultServerUrl, HubConfigSection.NormalizeServerUrl(""));
        Assert.Equal("https://connect.hiynet.com", HubConfigSection.NormalizeServerUrl("connect.hiynet.com/"));
        Assert.Equal("http://127.0.0.1:56700", HubConfigSection.NormalizeServerUrl("http://127.0.0.1:56700/"));
        Assert.Equal("http://localhost:56700", HubConfigSection.NormalizeServerUrl("localhost:56700"));
        Assert.Equal(HubConfigSection.DefaultServerUrl, HubConfigSection.NormalizeServerUrl("http://example.com:56700"));
    }

    [Fact]
    public void HubAuthService_PkceAndDpapiSessionRoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ap-hub-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = ToysConfigStore.ForRoot(tempDir);
            var auth = new HubAuthService(store);

            var verifier = HubAuthService.GenerateCodeVerifier();
            Assert.InRange(verifier.Length, 43, 128);

            var expectedChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Assert.Equal(expectedChallenge, HubAuthService.ComputeCodeChallenge(verifier));

            var url = HubAuthService.BuildAuthorizeUrl("cid-123", "common", "http://127.0.0.1:43210/callback", "st", expectedChallenge);
            Assert.Contains("client_id=cid-123", url);
            Assert.Contains("code_challenge_method=S256", url);
            Assert.Contains("scope=openid%20profile%20User.Read", url);
            Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A43210%2Fcallback", url);

            auth.SaveSession("hub-tok-secret", 1900000000000L, new HubUserProfileDto
            {
                UserId = "u_01",
                DisplayName = "测试玩家",
            });

            var loaded = auth.LoadStoredSession();
            Assert.NotNull(loaded);
            Assert.Equal("hub-tok-secret", loaded!.Value.Token);
            Assert.Equal("u_01", loaded.Value.UserId);
            Assert.Equal("测试玩家", loaded.Value.DisplayName);

            auth.ClearSession();
            Assert.Null(auth.LoadStoredSession());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void HubLocalBridgeServer_HandlesAuthCommandsAndDedup()
    {
        var tempGameDir = Path.Combine(Path.GetTempPath(), "ap-hub-bridge-" + Guid.NewGuid().ToString("N"));
        var loaderDir = Path.Combine(tempGameDir, "AstralParty_ModLoader");
        Directory.CreateDirectory(loaderDir);

        using var bridge = new HubLocalBridgeServer();
        try
        {
            bridge.Start();
            bridge.SyncGameDirectories([tempGameDir]);

            var endpointFile = Path.Combine(loaderDir, "hub-bridge", "endpoint.json");
            Assert.True(File.Exists(endpointFile));
            using var endpointDoc = JsonDocument.Parse(File.ReadAllText(endpointFile));
            Assert.Equal(bridge.Port, endpointDoc.RootElement.GetProperty("port").GetInt32());
            Assert.Equal(bridge.Token, endpointDoc.RootElement.GetProperty("token").GetString());

            var unauth = bridge.HandleRequestInternal("POST", "/api/bridge/heartbeat", "wrong-token", "{}");
            Assert.Equal(401, unauth.StatusCode);

            var cmdId = bridge.EnqueueJoinRoom(998877, "4321", 3);
            var hb = bridge.HandleRequestInternal("POST", "/api/bridge/heartbeat", bridge.Token, """{"sessionId":"s1"}""");
            Assert.Equal(200, hb.StatusCode);
            using var hbDoc = JsonDocument.Parse(hb.Body);
            var cmds = hbDoc.RootElement.GetProperty("commands");
            Assert.Equal(1, cmds.GetArrayLength());
            Assert.Equal(cmdId, cmds[0].GetProperty("commandId").GetString());
            Assert.Equal("join_room", cmds[0].GetProperty("action").GetString());
            Assert.Equal(998877L, cmds[0].GetProperty("gameRoomId").GetInt64());

            var loginEvt = """{"eventId":"e1","sessionId":"s1","type":"login_ready","atMs":1000,"data":{"uid":100020,"region":"china","scene":"Home"}}""";
            Assert.Equal(200, bridge.HandleRequestInternal("POST", "/api/bridge/events", bridge.Token, loginEvt).StatusCode);

            var roomEvt = """{"eventId":"e2","sessionId":"s1","type":"room_created","atMs":1001,"data":{"gameRoomId":555666,"gamePwd":"8888","roomServerId":2,"mapId":12,"mapType":4,"masterId":100020,"isMaster":true,"playerCount":1,"state":"Wait"}}""";
            Assert.Equal(200, bridge.HandleRequestInternal("POST", "/api/bridge/events", bridge.Token, roomEvt).StatusCode);
            var dup = bridge.HandleRequestInternal("POST", "/api/bridge/events", bridge.Token, roomEvt);
            Assert.Contains("duplicate", dup.Body);

            var sessions = bridge.GetActiveSessions();
            var session = Assert.Single(sessions);
            Assert.True(session.IsLoggedIn);
            Assert.Equal(100020L, session.Uid);
            Assert.Equal(555666L, session.GameRoomId);
            Assert.Equal("8888", session.GamePwd);
            Assert.True(session.IsMaster);

            bridge.Dispose();
            Assert.False(File.Exists(endpointFile));
        }
        finally
        {
            if (Directory.Exists(tempGameDir)) Directory.Delete(tempGameDir, recursive: true);
        }
    }

    [Fact]
    public void HubLocalBridgeServer_EnqueuesCreateRoomAndSyncsHeartbeatState()
    {
        using var bridge = new HubLocalBridgeServer();
        bridge.Start();

        var createId = bridge.EnqueueCreateRoom("测试开黑房", "7391");
        var pvpId = bridge.EnqueueCreateRoom("测试PVP房", "2468", mapType: 1, mapId: 5, targetUid: 1744079, targetRegion: "cn");
        var hbBody = """
        {
          "protocol": 1,
          "sessionId": "s2",
          "isLoggedIn": true,
          "uid": 1744079,
          "region": "cn",
          "scene": "Home",
          "room": {
            "gameRoomId": 8848123,
            "gamePwd": "7391",
            "roomServerId": 1,
            "mapId": 5,
            "mapType": 4,
            "masterId": 1744079,
            "isMaster": true,
            "state": "Wait",
            "playerCount": 1
          }
        }
        """;

        var hb = bridge.HandleRequestInternal("POST", "/api/bridge/heartbeat", bridge.Token, hbBody);
        Assert.Equal(200, hb.StatusCode);

        using var doc = JsonDocument.Parse(hb.Body);
        var cmds = doc.RootElement.GetProperty("commands");
        Assert.Equal(2, cmds.GetArrayLength());
        Assert.Equal(createId, cmds[0].GetProperty("commandId").GetString());
        Assert.Equal("create_room", cmds[0].GetProperty("action").GetString());
        Assert.Equal("测试开黑房", cmds[0].GetProperty("roomName").GetString());
        Assert.Equal("7391", cmds[0].GetProperty("gamePwd").GetString());
        Assert.Equal(4, cmds[0].GetProperty("mapType").GetInt32());

        Assert.Equal(pvpId, cmds[1].GetProperty("commandId").GetString());
        Assert.Equal("create_room", cmds[1].GetProperty("action").GetString());
        Assert.Equal(1, cmds[1].GetProperty("mapType").GetInt32());
        Assert.Equal(5, cmds[1].GetProperty("mapId").GetInt32());

        var session = Assert.Single(bridge.GetActiveSessions());
        Assert.True(session.IsLoggedIn);
        Assert.Equal(1744079L, session.Uid);
        Assert.Equal("cn", session.Region);
        Assert.Equal("Home", session.Scene);
        Assert.Equal(8848123L, session.GameRoomId);
        Assert.Equal("7391", session.GamePwd);
        Assert.True(session.IsMaster);
    }

    [Fact]
    public void HubLocalBridgeServer_RoutesCommandsToMatchingUidAndRegionOnly()
    {
        using var bridge = new HubLocalBridgeServer();
        bridge.Start();

        var cmdId = bridge.EnqueueCreateRoom("大号车队", "4829", mapType: 4, targetUid: 1744079, targetRegion: "cn");

        var altCn = """{"protocol":1,"sessionId":"alt-cn","isLoggedIn":true,"uid":2222222,"region":"cn","scene":"Home"}""";
        var altInt = """{"protocol":1,"sessionId":"alt-int","isLoggedIn":true,"uid":1744079,"region":"int","scene":"Home"}""";
        var mainCn = """{"protocol":1,"sessionId":"main-cn","isLoggedIn":true,"uid":1744079,"region":"cn","scene":"Home"}""";

        using (var d1 = JsonDocument.Parse(bridge.HandleRequestInternal("POST", "/api/bridge/heartbeat", bridge.Token, altCn).Body))
            Assert.Equal(0, d1.RootElement.GetProperty("commands").GetArrayLength());

        using (var d2 = JsonDocument.Parse(bridge.HandleRequestInternal("POST", "/api/bridge/heartbeat", bridge.Token, altInt).Body))
            Assert.Equal(0, d2.RootElement.GetProperty("commands").GetArrayLength());

        using (var d3 = JsonDocument.Parse(bridge.HandleRequestInternal("POST", "/api/bridge/heartbeat", bridge.Token, mainCn).Body))
        {
            var cmds = d3.RootElement.GetProperty("commands");
            Assert.Equal(1, cmds.GetArrayLength());
            Assert.Equal(cmdId, cmds[0].GetProperty("commandId").GetString());
        }

        Assert.Equal(3, bridge.GetActiveSessions().Count);
    }

    [Fact]
    public void HubSettlementService_ParsesGameFinishedEventAndNormalizesRegion()
    {
        var json = """
        {
          "eventId": "ev-fin-1",
          "sessionId": "s1",
          "type": "game_finished",
          "atMs": 2000,
          "data": {
            "region": "abroad",
            "uid": 880011,
            "replayId": "1234567890123456",
            "gameRoomId": 778899,
            "winnerId": 880011,
            "finishTime": 1764100000,
            "mapId": 5,
            "mapType": 4,
            "round": 11,
            "gameVersion": "3.3.0",
            "players": [
              { "playerId": 880011, "nick": "Alice", "slot": 0, "rank": 1, "heroId": 101, "level": 4, "gold": 42, "hp": 12, "maxHp": 12, "totalDamage": 28, "killCount": 2 }
            ]
          }
        }
        """;

        var env = JsonSerializer.Deserialize<BridgeEventEnvelope>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var summary = HubSettlementService.ParseGameFinishedEvent(env);
        Assert.NotNull(summary);
        Assert.Equal("int", summary!.Region);
        Assert.Equal("1234567890123456", summary.ReplayId);
        Assert.Equal(11, summary.Round);
        var p = Assert.Single(summary.Players);
        Assert.Equal("880011", p.PlayerId);
        Assert.Equal("Alice", p.Name);
        Assert.Equal(28, p.TotalDamage);
    }

    [Fact]
    public async Task HubApiClient_SendsBearerTokenAndParsesStructuredErrors()
    {
        HttpRequestMessage? lastReq = null;
        var handler = new StubHandler(req =>
        {
            lastReq = req;
            if (req.RequestUri!.AbsolutePath == "/api/hub/rooms")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"region":"cn","rooms":[]}""", Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri!.AbsolutePath == "/api/hub/rooms/r1/disband" && req.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (req.RequestUri!.AbsolutePath == "/api/hub/avatars/u_01")
            {
                var content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"code":"unauthorized","message":"令牌无效"}}""", Encoding.UTF8, "application/json")
            };
        });

        using var api = new HubApiClient("http://127.0.0.1:56700", handler);
        api.SetToken("my-hub-token");

        var list = await api.ListRoomsAsync("cn", "pve", "recruiting");
        Assert.Equal("cn", list.Region);
        Assert.NotNull(lastReq);
        Assert.Equal("Bearer", lastReq!.Headers.Authorization?.Scheme);
        Assert.Equal("my-hub-token", lastReq.Headers.Authorization?.Parameter);
        Assert.Contains("q=pve", lastReq.RequestUri!.Query);

        await api.DisbandRoomAsync("r1");
        Assert.Equal("/api/hub/rooms/r1/disband", lastReq!.RequestUri!.AbsolutePath);

        var avatar = await api.DownloadAvatarAsync("/api/hub/avatars/u_01?v=1");
        Assert.NotNull(avatar);
        Assert.Equal("image/png", avatar!.Value.ContentType);
        Assert.Equal(4, avatar.Value.Bytes.Length);

        var ex = await Assert.ThrowsAsync<HubApiException>(() => api.GetMeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("unauthorized", ex.ErrorCode);
        Assert.Equal("令牌无效", ex.Message);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_fn(request));
    }
}
