using System.Collections;
using static AstralParty.Toys.Services.Account.AccountProtocol;

namespace AstralParty.Toys.Services.Account;

public sealed record AccountCharacter(int Id, long? FightCount, int Level, int Exp, bool Breakthrough, long Adorn);
public sealed record AccountPlayer(
    string Uid, string Name, long Level, long Exp, long WinCount, long CreateTime, long LoginTime,
    int CreditScore, bool IsShowData, bool IsShowFight, long PraiseNum, int StandingPainting,
    IReadOnlyList<int> Achievements, IReadOnlyList<AccountCharacter> Characters, IReadOnlyDictionary<int, int> WinMaps);
public sealed record AccountStatistics(long FightCount, long WinCount, long RoleCardCount, int UseHero, long AdornCount, long SkinCount);
public sealed record AccountShowcase(
    string Uid, AccountStatistics? Statistics, long PraiseNum, bool IsShowData, bool IsShowFight,
    int StandingPainting, IReadOnlyList<int> Achievements, IReadOnlyList<AccountMatch> Matches);
public sealed record AccountMatchPlayer(
    string PlayerId, string Name, int Level, int Gold, int HeroId, int Slot, int Rank, int MapType,
    bool GaveUp, int HeadIcon, int PlayerLevel);
public sealed record AccountMatchDetail(int Index, IReadOnlyList<AccountMatchPlayer> Players);
public sealed record AccountFriend(string Uid, string Name, string Note, long Level, int HeadIcon, string Status, long OfflineTime);
public sealed record AccountMatch(long Time, int Rank, int HeroId, int Index, int MapType, string ReplayId, string Version);

public sealed class AccountSession : IAsyncDisposable
{
    private readonly AccountProtocol _protocol;
    private readonly AccountTransport _transport;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _operations = new(1, 1);
    private Task _heartbeat = Task.CompletedTask;
    public bool Connected => _transport.Connected;
    public string Uid { get; private set; } = "";

    public AccountSession(string protocolDirectory, AccountCredentials credentials)
    {
        _protocol = new AccountProtocol(protocolDirectory);
        _transport = new AccountTransport(credentials);
    }

    public async Task<AccountPlayer> LoginAsync(AccountCredentials credentials, string expectedUid, CancellationToken ct)
    {
        var bytes = Convert.FromBase64String(credentials.ConnectRequestBase64);
        var login = _protocol.Parse("ConnectC2S", bytes);
        if (Text(login, "Auth") != "China" || Get(login, "China") == null || Get(login, "Dev") != null)
            throw new InvalidDataException("invalid_credentials");
        await _transport.ConnectAsync(ct).ConfigureAwait(false);
        var frame = await _transport.CallAsync(5001, bytes, ct).ConfigureAwait(false);
        var response = _protocol.Parse("ConnectS2C", frame.Payload);
        var player = Get(response, "Player");
        long session = Number(response, "SessionId");
        if (session == 0 || Number(player, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture) != expectedUid)
            throw new InvalidDataException("account_mismatch");
        _transport.SessionId = session;
        Uid = expectedUid;
        var result = ProjectPlayer(player!);
        _heartbeat = HeartbeatAsync();
        return result;
    }

    public static AccountPlayer ProjectPlayer(object player)
    {
        var characters = Items(player, "RoleCard").Select(entry =>
        {
            var role = Get(entry, "Value");
            int id = (int)Number(role, "DefId");
            if (id == 0) id = (int)Number(entry, "Key");
            return new AccountCharacter(id, Get(role, "FightCount") == null ? null : Number(role, "FightCount"),
                (int)Number(role, "Lv"), (int)Number(role, "Exp"), Flag(role, "IsBreakThrough"), Number(role, "UseAdorn"));
        }).Where(c => c.Id > 0).OrderByDescending(c => c.FightCount).ThenBy(c => c.Id).ToArray();
        var show = Get(player, "ShowPlayer");
        int deduct = (int)Number(Get(player, "CreditInfo"), "DeductScore");
        var winMaps = IntMap(player, "WinMap").OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return new(Number(player, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture), Text(player, "Nick"),
            Number(player, "Level"), Number(player, "Exp"), Number(player, "WinCount"),
            Number(player, "CreateTime"), Number(player, "LoginTime"), Math.Clamp(100 - deduct, 0, 100),
            Flag(show, "IsShowData"), Flag(show, "IsShowFight"), Number(show, "PraiseNum"),
            (int)Number(show, "StandingPainting"), Achievements(show), characters, winMaps);
    }

    public static AccountFriend ProjectFriend(object friend) => new(
        Number(friend, "PlayerId").ToString(System.Globalization.CultureInfo.InvariantCulture), Text(friend, "Name"), Text(friend, "Note"),
        Number(friend, "Lv"), (int)Number(friend, "HeadIcon"),
        FriendStatus(Flag(friend, "IsOnline"), Flag(friend, "IsBusy"), Number(friend, "RoomId"), Text(friend, "State")), Number(friend, "OfflineTime"));

    public static string FriendStatus(bool online, bool busy, long room, string state)
    {
        if (!online) return "offline";
        if (room > 0) return state switch { "Running" => "playing", "Wait" or "ChoiceHero" or "ChoiceSkin" or "Ready1" or "Finish" => "room", _ => "unknown" };
        return busy ? "busy" : "online";
    }

    // 5163 verified in current Core.Net.FriendListC2SRPC; 5164 is the response.
    public async Task<IReadOnlyList<AccountFriend>> FriendsAsync(CancellationToken ct)
    {
        await _operations.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ReadFriendPagesAsync(async (page, token) =>
            {
                var response = await _transport.CallAsync(5163, _protocol.Create("FriendListC2S", ("Page", page), ("OnlyOnline", false)), token).ConfigureAwait(false);
                var data = _protocol.Parse("FriendListS2C", response.Payload);
                return (Items(data, "Friends").Select(ProjectFriend).ToArray(), Flag(data, "IsEnd"));
            }, ct).ConfigureAwait(false);
        }
        finally { _operations.Release(); }
    }

    public static async Task<IReadOnlyList<AccountFriend>> ReadFriendPagesAsync(
        Func<int, CancellationToken, Task<(AccountFriend[] Friends, bool IsEnd)>> fetch, CancellationToken ct)
    {
        var friends = new Dictionary<string, AccountFriend>(StringComparer.Ordinal);
        for (int page = 0; page < 100; page++)
        {
            ct.ThrowIfCancellationRequested();
            var (batch, isEnd) = await fetch(page, ct).ConfigureAwait(false);
            foreach (var friend in batch)
                if (!AccountCredentials.ValidUid(friend.Uid) || !friends.TryAdd(friend.Uid, friend)) throw new AccountQueryException("repeated_friend_page");
            if (isEnd) return friends.Values.OrderBy(f => f.Status == "offline").ThenBy(f => f.Name, StringComparer.Ordinal).ToArray();
            if (batch.Length == 0) throw new AccountQueryException("missing_friend_end");
        }
        throw new AccountQueryException("friend_page_limit"); // Never publish a partial snapshot as complete.
    }

    // 5153 returns both the profile statistics and the short recent-match records; 5155 is one match's detail.
    public async Task<AccountShowcase> ShowcaseAsync(CancellationToken ct)
    {
        await _operations.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var response = await _transport.CallAsync(5153,
                _protocol.Create("GetShowPlayerC2S", ("PlayerId", long.Parse(Uid, System.Globalization.CultureInfo.InvariantCulture))), ct).ConfigureAwait(false);
            return ProjectShowcase(_protocol.Parse("GetShowPlayerS2C", response.Payload), Uid);
        }
        finally { _operations.Release(); }
    }

    public static AccountShowcase ProjectShowcase(object response, string expectedUid)
    {
        var data = Get(response, "ShowData");
        if (data == null || Number(data, "PlayerId").ToString(System.Globalization.CultureInfo.InvariantCulture) != expectedUid)
            throw new InvalidDataException("account_mismatch");
        var statistics = Get(data, "Statistics");
        return new(expectedUid, statistics == null ? null : new AccountStatistics(
                Number(statistics, "FightCount"), Number(statistics, "WinFightCount"), Number(statistics, "RoleCardCount"),
                (int)Number(statistics, "UseHero"), Number(statistics, "AdornCount"), Number(statistics, "SkinCount")),
            Number(data, "PraiseNum"), Flag(data, "IsShowData"), Flag(data, "IsShowFight"),
            (int)Number(data, "StandingPainting"), Achievements(data), ProjectRecentMatches(response, expectedUid));
    }

    public static IReadOnlyList<AccountMatch> ProjectRecentMatches(object response, string expectedUid)
    {
        var data = Get(response, "ShowData");
        if (data == null || Number(data, "PlayerId").ToString(System.Globalization.CultureInfo.InvariantCulture) != expectedUid)
            throw new InvalidDataException("account_mismatch");
        return RecordItems(data).OrderByDescending(record => record.Time).ToArray();
    }

    public async Task<AccountMatchDetail> MatchDetailAsync(int index, CancellationToken ct)
    {
        if (index is < 0 or > 10000) throw new AccountQueryException("invalid_match_index");
        await _operations.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var response = await _transport.CallAsync(5155, _protocol.Create("GetPlayerFightRecordC2S",
                ("PlayerId", long.Parse(Uid, System.Globalization.CultureInfo.InvariantCulture)), ("Index", index)), ct).ConfigureAwait(false);
            return ProjectMatchDetail(_protocol.Parse("GetPlayerFightRecordS2C", response.Payload), index);
        }
        finally { _operations.Release(); }
    }

    public static AccountMatchDetail ProjectMatchDetail(object response, int index)
    {
        var players = Items(response, "RecordData").Take(16).Select(row => new AccountMatchPlayer(
            Number(row, "PlayerId").ToString(System.Globalization.CultureInfo.InvariantCulture), Text(row, "Name"),
            (int)Number(row, "Lv"), (int)Number(row, "Gold"), (int)Number(row, "HeroId"), (int)Number(row, "Slot"),
            (int)Number(row, "Rank"), (int)Number(row, "MapType"), Flag(row, "IsGiveUp"),
            (int)Number(row, "HeadIcon"), (int)Number(row, "PlayerLevel"))).ToArray();
        return new(index, players);
    }

    private static IEnumerable<AccountMatch> RecordItems(object data) =>
        Items(data, "Record").Select(record => new AccountMatch(
            Number(record, "Time"), (int)Number(record, "Rank"), (int)Number(record, "HeroId"),
            (int)Number(record, "Index"), (int)Number(record, "MapType"), Text(record, "ReplayId"), Text(record, "Version")));

    private static int[] Achievements(object? holder) =>
        Items(holder, "AchieveId").Select(value => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture))
            .Where(id => id > 0).Take(6).ToArray();

    private static IEnumerable<KeyValuePair<int, int>> IntMap(object? holder, string name)
    {
        if (Get(holder, name) is not IEnumerable entries) yield break;
        foreach (var entry in entries)
        {
            int key = (int)Number(entry, "Key");
            if (key > 0) yield return new KeyValuePair<int, int>(key, (int)Number(entry, "Value"));
        }
    }

    private async Task HeartbeatAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _lifetime.Token).ConfigureAwait(false);
                await _transport.CallAsync(5003, _protocol.Create("HeartbeatC2S", ("Client", Environment.TickCount64)), _lifetime.Token).ConfigureAwait(false);
            }
        }
        catch { _transport.Stop(); }
    }

    public void Stop() { _lifetime.Cancel(); _transport.Stop(); }
    public async ValueTask DisposeAsync()
    {
        Stop();
        await _transport.DisposeAsync().ConfigureAwait(false);
        await _heartbeat.ConfigureAwait(false);
        await _operations.WaitAsync().ConfigureAwait(false);
        try { _protocol.Dispose(); }
        finally { _operations.Release(); }
    }
}
