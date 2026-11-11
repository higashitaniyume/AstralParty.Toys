using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.Account;
using AstralParty.Toys.Services.Common;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class MyAccountTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "ToysAccountTests-" + Guid.NewGuid().ToString("N"));
    public MyAccountTests() => Directory.CreateDirectory(_scratch);
    public void Dispose() => Directory.Delete(_scratch, true);

    [Theory]
    [InlineData("1", true)]
    [InlineData("1744079", true)]
    [InlineData("9223372036854775807", true)]
    [InlineData("9223372036854775808", false)]
    [InlineData("../1", false)]
    [InlineData("01", false)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData("１", false)]
    public void UidIsStrictAndPathSafe(string uid, bool expected) => Assert.Equal(expected, AccountCredentials.ValidUid(uid));

    [Theory]
    [InlineData("se-jump-cn-01.feimogames.com", 8800, true)]
    [InlineData("SE-JUMP-CN-01.FEIMOGAMES.COM", 8800, true)]
    [InlineData("se-jump-cn-01.feimogames.com.attacker.invalid", 8800, false)]
    [InlineData("127.0.0.1", 8800, false)]
    [InlineData("localhost", 8800, false)]
    [InlineData("se-jump-cn-01.feimogames.com", 80, false)]
    public void OnlyVerifiedEndpointIsAccepted(string host, int port, bool expected) => Assert.Equal(expected, AccountCredentials.IsOfficialEndpoint(host, port));

    private string WriteCredential(string uid, object value)
    {
        string directory = Path.Combine(AccountCredentials.Root(_scratch), uid);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "account.json");
        File.WriteAllText(path, JsonSerializer.Serialize(value));
        return path;
    }

    private static object Credential(string host = "se-jump-cn-01.feimogames.com", string token = "AQID") =>
        new { host, port = 8800, clientVersion = "synthetic", connectRequestBase64 = token, protocolVersion1 = 3, protocolVersion2 = 3, protocolVersion3 = 0 };

    [Fact]
    public void DiscoveryListsMultipleUidsWithoutParsingOrSelectingTheirCredentials()
    {
        var one = WriteCredential("100", Credential());
        WriteCredential("200", Credential());
        WriteCredential("not-a-uid", Credential());
        File.WriteAllText(one, "malformed JSON with secret sentinel");
        Assert.Equal(new[] { "100", "200" }, AccountCredentials.Discover(_scratch));
    }

    [Fact]
    public void ReadsOnlyExplicitAccountAndNeverLeaksSecretsInValidationErrors()
    {
        WriteCredential("100", Credential());
        WriteCredential("200", Credential("attacker.invalid", "SECRET_SENTINEL"));
        Assert.Equal("AQID", AccountCredentials.Read(_scratch, "100").ConnectRequestBase64);
        var error = Assert.Throws<InvalidDataException>(() => AccountCredentials.Read(_scratch, "200"));
        Assert.Equal("invalid_credentials", error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("SECRET_SENTINEL", error.ToString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"host\":\"se-jump-cn-01.feimogames.com\",\"port\":8800,\"clientVersion\":\"x\",\"connectRequestBase64\":\"%%%\"}")]
    public void RejectsMalformedCredentialWithoutEchoingContent(string content)
    {
        var path = WriteCredential("100", Credential());
        File.WriteAllText(path, content);
        Assert.Equal("invalid_credentials", Assert.Throws<InvalidDataException>(() => AccountCredentials.Read(_scratch, "100")).Message);
    }

    [Fact]
    public void RejectsOversizeCredentialBeforeParsing()
    {
        var path = WriteCredential("100", Credential());
        File.WriteAllText(path, new string('x', AccountCredentials.MaxFileBytes + 1));
        Assert.Throws<InvalidDataException>(() => AccountCredentials.Read(_scratch, "100"));
    }

    [Fact]
    public void DeletesOnlySelectedCredentialAndPreservesOtherFilesAndAccounts()
    {
        var selected = WriteCredential("100", Credential());
        var other = WriteCredential("200", Credential());
        var note = Path.Combine(Path.GetDirectoryName(selected)!, "note.txt");
        File.WriteAllText(note, "keep");
        AccountCredentials.Delete(_scratch, "100");
        Assert.False(File.Exists(selected));
        Assert.True(File.Exists(other));
        Assert.Equal("keep", File.ReadAllText(note));
        Assert.True(Directory.Exists(Path.GetDirectoryName(selected)));
        Assert.Equal(new[] { "200" }, AccountCredentials.Discover(_scratch));
    }

    [Theory]
    [InlineData("../100")]
    [InlineData("100/account.json")]
    [InlineData("")]
    public void DeleteRejectsInvalidUidWithoutTouchingCredentials(string uid)
    {
        var path = WriteCredential("100", Credential());
        Assert.Throws<InvalidDataException>(() => AccountCredentials.Delete(_scratch, uid));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteDoesNotNeedToParseBrokenCredentialsAndCanBeRepeated()
    {
        var path = WriteCredential("100", Credential());
        File.WriteAllText(path, "malformed secret sentinel");
        AccountCredentials.Delete(_scratch, "100");
        AccountCredentials.Delete(_scratch, "100");
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ConsentIsExplicitPersistentAndLocal()
    {
        var consent = new AccountConsent(_scratch);
        Assert.False(consent.Granted);
        Assert.Empty(Directory.GetFiles(_scratch));
        consent.Accept();
        Assert.True(new AccountConsent(_scratch).Granted);

        // 同意记录并入统一配置，不再单独留一个 txt
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_scratch, ToysPaths.ConfigFileName)));
        Assert.True(document.RootElement.GetProperty("app").GetProperty("myAccountConsent").GetBoolean());
    }

    [Theory]
    [InlineData(false, false, 0, "None", "offline")]
    [InlineData(false, true, 10, "Running", "offline")]
    [InlineData(true, false, 0, "Running", "online")]
    [InlineData(true, true, 0, "None", "busy")]
    [InlineData(true, false, 10, "Running", "playing")]
    [InlineData(true, true, 10, "Wait", "room")]
    [InlineData(true, false, 10, "ChoiceHero", "room")]
    [InlineData(true, false, 10, "Ready1", "room")]
    [InlineData(true, false, 10, "FutureState", "unknown")]
    public void FriendStateDoesNotConfuseOnlineOrBusyWithPlaying(bool online, bool busy, long room, string raw, string expected) =>
        Assert.Equal(expected, AccountSession.FriendStatus(online, busy, room, raw));

    [Fact]
    public void DisplayProjectionCannotExposeTokensOrRoomPasswords()
    {
        var player = AccountSession.ProjectPlayer(new
        {
            Id = 100L, Nick = "<script>name</script>", Level = 20, Token = "SECRET_SENTINEL",
            RoleCard = new Dictionary<int, object> { [1] = new { DefId = 1, FightCount = 45 }, [2] = new { DefId = 2, FightCount = 3 }, [3] = new { DefId = 3 } }
        });
        Assert.Equal(45, player.Characters[0].FightCount);
        Assert.Null(player.Characters[2].FightCount);
        var friend = AccountSession.ProjectFriend(new { PlayerId = 200L, Name = "friend", Pwd = "SECRET_SENTINEL", IsOnline = true, IsBusy = true, RoomId = 0 });
        string json = JsonSerializer.Serialize(new { player, friend });
        Assert.DoesNotContain("SECRET_SENTINEL", json);
        Assert.DoesNotContain("Pwd", json);
        Assert.DoesNotContain("Token", json);
        Assert.Equal("busy", friend.Status);
    }

    private static AccountFriend Friend(int id) => new(id.ToString(), "friend", "", 1, 0, id == 1 ? "offline" : "online", 0);

    [Fact]
    public async Task FriendPaginationStartsAtZeroAndOnlyReturnsCompleteSortedList()
    {
        var pages = new List<int>();
        var friends = await AccountSession.ReadFriendPagesAsync((page, ct) =>
        {
            pages.Add(page);
            return Task.FromResult((new[] { Friend(page + 1) }, page == 2));
        }, CancellationToken.None);
        Assert.Equal(new[] { 0, 1, 2 }, pages);
        Assert.Equal(3, friends.Count);
        Assert.Equal("offline", friends[^1].Status);
    }

    [Fact]
    public async Task EmptyFinalPageIsAValidCompleteEmptyFriendList()
    {
        var friends = await AccountSession.ReadFriendPagesAsync((_, _) => Task.FromResult((Array.Empty<AccountFriend>(), true)), CancellationToken.None);
        Assert.Empty(friends);
    }

    [Fact]
    public async Task RepeatedPagesNeverProduceFalseCompleteSnapshot() =>
        await Assert.ThrowsAsync<AccountQueryException>(() => AccountSession.ReadFriendPagesAsync((_, _) => Task.FromResult((new[] { Friend(1) }, false)), CancellationToken.None));

    [Fact]
    public async Task MissingEndMarkerAndEmptyPageFailClosed() =>
        await Assert.ThrowsAsync<AccountQueryException>(() => AccountSession.ReadFriendPagesAsync((_, _) => Task.FromResult((Array.Empty<AccountFriend>(), false)), CancellationToken.None));

    [Fact]
    public async Task PaginationIsBounded() =>
        await Assert.ThrowsAsync<AccountQueryException>(() => AccountSession.ReadFriendPagesAsync((page, _) => Task.FromResult((new[] { Friend(page + 1) }, false)), CancellationToken.None));

    [Fact]
    public async Task CancelledPaginationDoesNotIssueRequests()
    {
        int calls = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AccountSession.ReadFriendPagesAsync((_, _) => { calls++; return Task.FromResult((Array.Empty<AccountFriend>(), true)); }, cts.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task FrameMatchesVerified35ByteBigEndianHeader()
    {
        byte[] bytes = AccountFrame.Encode(5163, 123, 101, [1, 2, 3]);
        Assert.Equal(38, bytes.Length);
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(bytes));
        Assert.Equal(123, BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(4)));
        Assert.Equal(5163, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(12)));
        Assert.Equal(1, bytes[14]);
        using var stream = new MemoryStream(bytes);
        var frame = await AccountFrame.ReadAsync(stream, CancellationToken.None);
        Assert.Equal(101, frame.Upsn);
        Assert.Equal(new byte[] { 1, 2, 3 }, frame.Payload);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(AccountFrame.MaxPayloadLength + 1)]
    public async Task RejectsInvalidFrameLength(int length)
    {
        var bytes = new byte[35];
        BinaryPrimitives.WriteInt32BigEndian(bytes, length);
        using var stream = new MemoryStream(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => AccountFrame.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public void ActualShippedSchemaCanCreateAndParseFriendQuery()
    {
        using var protocol = new AccountProtocol(Path.Combine(AppContext.BaseDirectory, "Protocol"));
        var query = protocol.Parse("FriendListC2S", protocol.Create("FriendListC2S", ("Page", 2), ("OnlyOnline", false)));
        Assert.Equal(2, AccountProtocol.Number(query, "Page"));
        Assert.False(AccountProtocol.Flag(query, "OnlyOnline"));
        var heartbeat = protocol.Parse("HeartbeatC2S", protocol.Create("HeartbeatC2S", ("Client", 123L)));
        Assert.Equal(123, AccountProtocol.Number(heartbeat, "Client"));
    }

    [Fact]
    public void RecentMatchQueryAndResponseRoundTripWithShippedSchema()
    {
        using var protocol = new AccountProtocol(Path.Combine(AppContext.BaseDirectory, "Protocol"));
        var query = protocol.Parse("GetShowPlayerC2S", protocol.Create("GetShowPlayerC2S", ("PlayerId", 100L)));
        Assert.Equal(100, AccountProtocol.Number(query, "PlayerId"));
        var shortRecord = protocol.Parse("ShowPlayerShortFight", protocol.Create("ShowPlayerShortFight",
            ("Time", 1794300000L), ("Rank", 2), ("HeroId", 7), ("Index", 3), ("MapType", 4), ("ReplayId", "12345678901234567890"), ("Version", "3.3.0")));
        var show = protocol.Parse("ShowPlayer", protocol.Create("ShowPlayer", ("PlayerId", 100L)));
        var records = AccountProtocol.Get(show, "Record")!;
        records.GetType().GetMethod("Add", [shortRecord.GetType()])!.Invoke(records, [shortRecord]);
        var response = protocol.Parse("GetShowPlayerS2C", protocol.Create("GetShowPlayerS2C", ("ShowData", show)));
        var match = Assert.Single(AccountSession.ProjectRecentMatches(response, "100"));
        Assert.Equal(new AccountMatch(1794300000L, 2, 7, 3, 4, "12345678901234567890", "3.3.0"), match);
    }

    [Fact]
    public void RecentMatchProjectionSortsNewestFirstAndDoesNotExposeOtherProfileFields()
    {
        var response = new { ShowData = new { PlayerId = 100L, Token = "SECRET_SENTINEL", Record = new[] {
            new { Time = 10L, ReplayId = "", Rank = 0, HeroId = 1, Index = 0, MapType = 4, Version = "" },
            new { Time = 20L, ReplayId = "123", Rank = 1, HeroId = 2, Index = 1, MapType = 12, Version = "3.3.0" }
        } } };
        var matches = AccountSession.ProjectRecentMatches(response, "100");
        Assert.Equal(new long[] { 20, 10 }, matches.Select(m => m.Time));
        Assert.Equal("", matches[1].ReplayId);
        Assert.DoesNotContain("SECRET_SENTINEL", JsonSerializer.Serialize(matches));
    }

    [Fact]
    public void RecentMatchesRejectMissingOrOtherAccountProfile()
    {
        Assert.Throws<InvalidDataException>(() => AccountSession.ProjectRecentMatches(new { }, "100"));
        Assert.Throws<InvalidDataException>(() => AccountSession.ProjectRecentMatches(new { ShowData = new { PlayerId = 200L } }, "100"));
        Assert.Empty(AccountSession.ProjectRecentMatches(new { ShowData = new { PlayerId = 100L } }, "100"));
    }

    [Fact]
    public void RecentMatchUiReusesDownloadWorkflowAndOnlyOffersValidReplayIds()
    {
        var account = Web("js/account.js");
        Assert.Contains("id=\"accountRecentPanel\"", Web("index.html"));
        Assert.Contains("const tabs = ['accountCharacters', 'accountFriends', 'accountRecent']", account);
        Assert.Contains("m.canDownload === true && /^\\d{1,32}$/.test(m.replayId)", account);
        Assert.Contains("openDownloadDialog({ replayId: id, cdn: 'CnProd', autoStart: true })", account);
        Assert.Contains("p.uid !== state.uid || !active()", account);
        Assert.Contains("p.requestId !== state.requestId", account);
        Assert.Single(Regex.Matches(account, "type: 'myAccountRecentRefresh'").Cast<Match>());
        Assert.Contains("Date.now() - state.matchesAttempt < 3000", account);
        Assert.Contains("<strong data-account-map=", account);
        Assert.Contains("m.mapName || t(m.mapLoading ? 'account.mapLoading' : 'account.unknownMap')", account);
        Assert.Contains("title.textContent = p.mapName || t('account.unknownMap')", account);
        var download = Web("js/replay/download.js");
        Assert.Contains("openDownloadDialog(preset = {})", download);
        Assert.Contains("preset.autoStart === true) this.startDownload()", download);
        Assert.Contains("this.cdnOptions().some(item => item.value === preset.cdn)", download);
    }

    [Fact]
    public void IndexedMapIdReadsOnlyMatchingIndexWithoutImportingReplay()
    {
        var service = new AstralParty.Toys.Services.Replay.ReplayLibraryService(AppContext.BaseDirectory, profileDirectory: _scratch);
        var root = Path.Combine(_scratch, "library");
        Directory.CreateDirectory(root);
        service.SaveSettings(new AstralParty.Toys.Services.Replay.ReplayLibrarySettings { LibraryRoot = root });
        Directory.CreateDirectory(service.IndexDirectory);
        File.WriteAllText(Path.Combine(service.IndexDirectory, "123.json"), "{\"schemaVersion\":1,\"replayId\":\"123\",\"mapId\":101}");
        Assert.Equal(101, service.IndexedMapId("123"));
        Assert.Equal(0, service.IndexedMapId("124"));
        Assert.Equal(0, service.IndexedMapId("../123"));
        Assert.False(File.Exists(Path.Combine(root, "123", "123")));
    }

    [Fact]
    public void AccountNavigationIsAlwaysVisibleWithoutAnInterfaceToggle()
    {
        Assert.Contains("class=\"nav-crumb-btn\" data-page=\"account\">", Web("index.html"));
        Assert.DoesNotContain("data-page=\"account\" hidden", Web("index.html"));
        var router = Web("js/core/router.js");
        Assert.DoesNotContain("showMyAccount", router);
        Assert.DoesNotContain("AccountNavigation", router);
        Assert.Contains("window.MyAccountModule?.onShow()", router);
        Assert.Contains("window.MyAccountModule?.onHide()", router);
        var settings = Web("js/v2/settings.js");
        Assert.DoesNotContain("v2gMyAccount", settings);
        Assert.DoesNotContain("account.showNavigation", settings);
        foreach (var lang in new[] { "zh-Hans", "zh-Hant", "en", "ja" })
            Assert.DoesNotContain("account.showNavigation", Web($"js/i18n/dict-{lang}.js"));
    }

    [Fact]
    public void CredentialInstallationTargetsPublishedMarketDetail()
    {
        var marketId = typeof(HybridWindow).GetProperty("CredentialModMarketId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(marketId);
        Assert.Equal("AccountCredentialMod", marketId.GetValue(null));
        var account = Web("js/account.js");
        Assert.Contains("el('accountInstallBtn').onclick = () => post({ type: 'myAccountInstallMod' })", account);
        Assert.Contains("else if (action === 'install') post({ type: 'myAccountInstallMod' })", account);
        Assert.Contains("if (p.id) window.WorkshopModule?.openExternalMod({ id: p.id })", account);
        Assert.Contains("openDetail(pending.id, pending.version ? 'versions' : 'details')", Web("js/workshop/module.js"));
    }

    [Fact]
    public void FriendsRefreshOnlyThroughManualButtonWithoutAutomaticCountdown()
    {
        var account = Web("js/account.js");
        Assert.Contains("el('accountRefreshBtn').onclick = () => post({ type: 'myAccountRefresh' })", account);
        Assert.Single(Regex.Matches(account, "type: 'myAccountRefresh'").Cast<Match>());
        Assert.Contains("Date.now() - Date.parse(state.capturedAt) < 3000", account);
        Assert.DoesNotContain("< 10000", account);
        Assert.DoesNotContain("nextRefreshAt", account);
        Assert.DoesNotContain("account.next", account);
        Assert.Contains("data-i18n=\"account.manual\"", Web("index.html"));
        Assert.Contains("'account.refresh': '刷新'", Web("js/i18n/dict-zh-Hans.js"));
        foreach (var lang in new[] { "zh-Hans", "zh-Hant", "en", "ja" })
        {
            var dictionary = Web($"js/i18n/dict-{lang}.js");
            Assert.Contains("'account.manual':", dictionary);
            Assert.DoesNotContain("'account.auto':", dictionary);
            Assert.DoesNotContain("'account.next':", dictionary);
        }
    }

    private static string Web(string path)
    {
        using var stream = EmbeddedWebStore.Open(path);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void PageAssetsNavigationAndLifecycleAreEmbedded()
    {
        string index = Web("index.html");
        Assert.Contains("data-page=\"account\"", index);
        Assert.Contains("id=\"accountView\"", index);
        Assert.Contains("js/account.js", index);
        Assert.Contains("css/account.css", index);
        Assert.Contains("MyAccountModule?.onHide()", Web("js/core/router.js"));
        Assert.Contains("myAccountLeave", Web("js/account.js"));
        Assert.DoesNotContain("connectRequestBase64", Web("js/account.js"));
        Assert.DoesNotContain("fetch(", Web("js/account.js"));
        Assert.DoesNotContain("account.scope", index);
        Assert.DoesNotContain("account.snapshot", index);
        Assert.Contains("p.requestId !== state.requestId", Web("js/account.js"));
        Assert.Contains("width: 100%; max-width: none", Web("css/account.css"));
        Assert.Contains(".account-friends { display: grid;", Web("css/account.css"));
        Assert.Contains("openModal(t('account.privacy')", Web("js/account.js"));
        Assert.Contains("workshop-consent-warn", Web("js/account.js"));
        Assert.Contains("workshop-consent-url account-consent-path", Web("js/account.js"));
        Assert.Contains("credentialRoot", Web("js/account.js"));
        Assert.Contains("id=\"accountDeleteBtn\"", index);
        Assert.Contains("id=\"accountTutorialBtn\"", index);
        Assert.Contains("openModal(t('account.tutorial')", Web("js/account.js"));
        Assert.Contains("'account.installStatus'", Web("js/account.js"));
        Assert.Contains("'account.enableStatus'", Web("js/account.js"));
        Assert.Contains("if (refresh) post({ type: 'myAccountDiscover' })", Web("js/account.js"));
        Assert.Contains("type: 'myAccountDeleteRequest'", Web("js/account.js"));
        Assert.Contains("type: 'myAccountDelete', token: p.token", Web("js/account.js"));
        Assert.Contains("el('accountDeleteCancelBtn').focus()", Web("js/account.js"));
    }

    [Fact]
    public void AccountButtonsHaveVisibleBoundariesAndInteractionStates()
    {
        var css = Web("css/account.css");
        Assert.Contains(".account-page .v2-btn {", css);
        Assert.Contains("border: 1px solid color-mix(in srgb, var(--v2-text) 40%, transparent)", css);
        Assert.Contains(".account-page .v2-btn:hover:not(:disabled)", css);
        Assert.Contains(".account-page .v2-btn:active:not(:disabled)", css);
        Assert.Contains(".account-page .v2-btn:disabled", css);
        Assert.Contains(".account-tabs button[aria-selected=\"true\"]", css);
    }

    [Fact]
    public void AllAccountTranslationsHaveFourNonemptyVersions()
    {
        var dictionaries = new[] { "zh-Hans", "zh-Hant", "en", "ja" }.Select(lang => Web($"js/i18n/dict-{lang}.js")).ToArray();
        var keys = Regex.Matches(dictionaries[0], "'((?:account\\.|nav.account)[^']*)'\\s*:").Select(m => m.Groups[1].Value).ToArray();
        Assert.True(keys.Length >= 50);
        foreach (var key in keys)
            foreach (var dict in dictionaries) Assert.Matches("'" + Regex.Escape(key) + "'\\s*:\\s*'[^']+'", dict);
    }
}
