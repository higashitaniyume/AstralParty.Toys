using static AstralParty.Toys.Services.Account.AccountProtocol;

namespace AstralParty.Toys.Services.Account;

public sealed record AccountCharacter(int Id, long? FightCount);
public sealed record AccountPlayer(string Uid, string Name, long Level, IReadOnlyList<AccountCharacter> Characters);
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
            return new AccountCharacter(id, Get(role, "FightCount") == null ? null : Number(role, "FightCount"));
        }).Where(c => c.Id > 0).OrderByDescending(c => c.FightCount).ToArray();
        return new(Number(player, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture), Text(player, "Nick"), Number(player, "Level"), characters);
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

    // Account profile contains the short records; 5155 is per-match detail, not the recent list.
    public async Task<IReadOnlyList<AccountMatch>> RecentMatchesAsync(CancellationToken ct)
    {
        await _operations.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var response = await _transport.CallAsync(5153,
                _protocol.Create("GetShowPlayerC2S", ("PlayerId", long.Parse(Uid, System.Globalization.CultureInfo.InvariantCulture))), ct).ConfigureAwait(false);
            return ProjectRecentMatches(_protocol.Parse("GetShowPlayerS2C", response.Payload), Uid);
        }
        finally { _operations.Release(); }
    }

    public static IReadOnlyList<AccountMatch> ProjectRecentMatches(object response, string expectedUid)
    {
        var data = Get(response, "ShowData");
        if (data == null || Number(data, "PlayerId").ToString(System.Globalization.CultureInfo.InvariantCulture) != expectedUid)
            throw new InvalidDataException("account_mismatch");
        return Items(data, "Record").Select(record => new AccountMatch(
            Number(record, "Time"), (int)Number(record, "Rank"), (int)Number(record, "HeroId"),
            (int)Number(record, "Index"), (int)Number(record, "MapType"), Text(record, "ReplayId"), Text(record, "Version")))
            .OrderByDescending(record => record.Time).ToArray();
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
