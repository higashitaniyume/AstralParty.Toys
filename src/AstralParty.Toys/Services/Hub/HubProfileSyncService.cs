using AstralParty.Toys.Services.Account;
using AstralParty.Toys.Services.Replay;

namespace AstralParty.Toys.Services.Hub;

/// <summary>
/// 读取本机游戏账号凭据并组装可上传至组队大厅的非敏感公开快照（<see cref="HubGameSnapshot"/>）。
/// 绝不上传 <c>ConnectRequestBase64</c> 登录凭据、游戏会话令牌或好友列表。
/// </summary>
public sealed class HubProfileSyncService
{
    private readonly string _appDirectory;

    public HubProfileSyncService(string appDirectory)
    {
        _appDirectory = appDirectory;
    }

    public static string ToHubRegion(string? credentialRegion)
    {
        var r = (credentialRegion ?? "").Trim().ToLowerInvariant();
        if (r is "abroad" or "int" or "jp" or "global") return "int";
        if (r is "china" or "cn" or "aotcn") return "cn";
        return "";
    }

    public IReadOnlyList<(string Uid, string Region)> DiscoverLocalAccounts(string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            return [];

        var uids = AccountCredentials.Discover(gameDirectory);
        if (uids.Length == 0) return [];

        using var protocol = new AccountProtocol(Path.Combine(_appDirectory, "Protocol"));
        var list = new List<(string Uid, string Region)>(uids.Length);
        foreach (var uid in uids)
        {
            var region = "cn";
            try
            {
                var cred = AccountCredentials.Read(gameDirectory, uid);
                var mapped = ToHubRegion(cred.Region);
                if (mapped.Length > 0)
                {
                    region = mapped;
                }
                else
                {
                    var bytes = Convert.FromBase64String(cred.ConnectRequestBase64);
                    var login = protocol.Parse("ConnectC2S", bytes);
                    var inferred = ToHubRegion(AccountSession.LoginRegion(login));
                    if (inferred.Length > 0) region = inferred;
                }
            }
            catch
            {
                // 单条凭据解析失败时仍保留 UID 供用户识别，区服兜底 cn。
            }

            list.Add((uid, region));
        }

        return list;
    }

    public async Task<(string Region, HubGameSnapshot Snapshot)> CaptureLocalSnapshotAsync(
        string gameDirectory,
        string uid,
        string language,
        CancellationToken ct)
    {
        var cred = AccountCredentials.Read(gameDirectory, uid);
        var region = ResolveRegion(cred);

        await using var session = new AccountSession(Path.Combine(_appDirectory, "Protocol"), cred);
        var player = await session.LoginAsync(cred, uid, ct).ConfigureAwait(false);
        var showcase = await session.ShowcaseAsync(ct).ConfigureAwait(false);
        var snapshot = await BuildSnapshotAsync(session, player, showcase, language, ct).ConfigureAwait(false);
        return (region, snapshot);
    }

    /// <summary>
    /// 直连读取最新游戏账号快照与实时好友列表。好友列表仅返回给本机界面展示，绝不进入可上传快照。
    /// </summary>
    public async Task<(string Region, HubGameSnapshot Snapshot, object[] Friends, DateTimeOffset CapturedAt)> CaptureLiveAccountAsync(
        string gameDirectory,
        string uid,
        string language,
        Func<string?, string> assetUrlMapper,
        AssetLocator assets,
        CancellationToken ct)
    {
        var cred = AccountCredentials.Read(gameDirectory, uid);
        var region = ResolveRegion(cred);

        await using var session = new AccountSession(Path.Combine(_appDirectory, "Protocol"), cred);
        var player = await session.LoginAsync(cred, uid, ct).ConfigureAwait(false);
        var showcase = await session.ShowcaseAsync(ct).ConfigureAwait(false);
        var snapshot = await BuildSnapshotAsync(session, player, showcase, language, ct).ConfigureAwait(false);

        object[] friendsUi = [];
        try
        {
            var friends = await session.FriendsAsync(ct).ConfigureAwait(false);
            friendsUi = friends.Select(f => (object)new
            {
                f.Uid,
                f.Name,
                f.Note,
                f.Level,
                f.Status,
                image = ResolveHeadIconUrl(f.HeadIcon, assets, assetUrlMapper),
                offlineAt = f.OfflineTime is > 0 and <= 253402300799
                    ? (DateTimeOffset?)DateTimeOffset.FromUnixTimeSeconds(f.OfflineTime)
                    : null,
            }).ToArray();
        }
        catch
        {
            // 好友列表拉取失败时保留已拿到的账号与战绩快照，不阻断整个刷新流程。
        }

        return (region, snapshot, friendsUi, DateTimeOffset.UtcNow);
    }

    private string ResolveRegion(AccountCredentials cred)
    {
        var region = ToHubRegion(cred.Region);
        if (region.Length == 0)
        {
            using var protocol = new AccountProtocol(Path.Combine(_appDirectory, "Protocol"));
            var bytes = Convert.FromBase64String(cred.ConnectRequestBase64);
            region = ToHubRegion(AccountSession.LoginRegion(protocol.Parse("ConnectC2S", bytes)));
        }

        return region.Length == 0 ? "cn" : region;
    }

    private async Task<HubGameSnapshot> BuildSnapshotAsync(
        AccountSession session,
        AccountPlayer player,
        AccountShowcase showcase,
        string language,
        CancellationToken ct)
    {
        var config = new ConfigCatalog(
            new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")),
            Path.Combine(_appDirectory, "GameData"),
            language);

        var stats = showcase.Statistics;
        var recentMatches = new List<HubRecentMatchDto>();
        foreach (var m in showcase.Matches.Take(20))
        {
            List<HubMatchPlayerDto>? detailPlayers = null;
            try
            {
                var detail = await session.MatchDetailAsync(m.Index, ct).ConfigureAwait(false);
                detailPlayers = detail.Players.Select(p => new HubMatchPlayerDto
                {
                    PlayerId = p.PlayerId,
                    Name = p.Name,
                    Level = p.Level,
                    Gold = p.Gold,
                    HeroId = p.HeroId,
                    Character = p.HeroId > 0 ? config.Character(p.HeroId) : "",
                    Slot = p.Slot,
                    Rank = p.Rank,
                    GaveUp = p.GaveUp,
                    PlayerLevel = p.PlayerLevel,
                    HeadIcon = p.HeadIcon,
                }).ToList();
            }
            catch
            {
                // 单局明细拉取失败时保留列表概览，不阻断整个快照同步。
            }

            recentMatches.Add(new HubRecentMatchDto
            {
                Index = m.Index,
                Rank = m.Rank,
                HeroId = m.HeroId,
                Character = m.HeroId > 0 ? config.Character(m.HeroId) : "",
                MapType = m.MapType,
                MapName = "",
                ReplayId = m.ReplayId,
                Version = m.Version,
                PlayedAt = m.Time,
                Players = detailPlayers,
            });
        }

        return new HubGameSnapshot
        {
            Nick = player.Name,
            HeadIcon = showcase.StandingPainting > 0 ? showcase.StandingPainting : player.StandingPainting,
            Level = player.Level,
            Exp = player.Exp,
            ExpNeed = config.PlayerLevelNeedExp((int)player.Level),
            WinCount = stats?.WinCount ?? player.WinCount,
            FightCount = stats?.FightCount ?? 0,
            RoleCardCount = stats?.RoleCardCount ?? 0,
            UseHero = stats?.UseHero ?? 0,
            UseHeroName = stats is { UseHero: > 0 } ? config.Character(stats.UseHero) : "",
            AdornCount = stats?.AdornCount ?? 0,
            SkinCount = stats?.SkinCount ?? 0,
            CreditScore = player.CreditScore,
            PraiseNum = showcase.PraiseNum > 0 ? showcase.PraiseNum : player.PraiseNum,
            CreatedAtGame = player.CreateTime,
            LastLoginAtGame = player.LoginTime,
            GameShowData = showcase.IsShowData,
            GameShowFight = showcase.IsShowFight,
            Achievements = player.Achievements
                .Select(id => new HubAchievementDto { Id = id, Name = config.Achievement(id) })
                .ToList(),
            Characters = player.Characters
                .Select(c => new HubCharacterDto
                {
                    Id = c.Id,
                    Name = c.Id > 0 ? config.Character(c.Id) : "",
                    FightCount = c.FightCount,
                    Level = c.Level,
                    Breakthrough = c.Breakthrough,
                })
                .ToList(),
            WinMaps = player.WinMaps
                .Select(kv => new HubWinMapDto
                {
                    MapId = kv.Key,
                    Name = kv.Key > 0 ? config.Map(kv.Key) : "",
                    Wins = kv.Value,
                })
                .ToList(),
            RecentMatches = recentMatches,
            CapturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    public object DecorateProfileForUi(
        HubUserProfileDto profile,
        string language,
        Func<string?, string> assetUrlMapper,
        AssetLocator assets,
        Func<string?, string>? hubAvatarUrlMapper = null,
        Func<string, int>? indexedMapIdLookup = null)
    {
        var config = new ConfigCatalog(
            new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")),
            Path.Combine(_appDirectory, "GameData"),
            language);

        var bindings = profile.Bindings.Select(b =>
        {
            var snap = b.Snapshot;
            object? snapUi = snap is null ? null : new
            {
                snap.Nick,
                snap.HeadIcon,
                headImage = ResolveAvatarUrl(snap.HeadIcon, snap.UseHero, assets, config, assetUrlMapper),
                snap.Level,
                snap.Exp,
                snap.ExpNeed,
                snap.WinCount,
                snap.FightCount,
                snap.RoleCardCount,
                snap.UseHero,
                useHeroName = string.IsNullOrWhiteSpace(snap.UseHeroName) && snap.UseHero > 0
                    ? config.Character(snap.UseHero)
                    : snap.UseHeroName,
                useHeroImage = snap.UseHero > 0
                    ? assetUrlMapper(assets.Character(snap.UseHero, config.CharacterAssets(snap.UseHero)))
                    : "",
                snap.AdornCount,
                snap.SkinCount,
                snap.CreditScore,
                snap.PraiseNum,
                snap.CreatedAtGame,
                snap.LastLoginAtGame,
                snap.GameShowData,
                snap.GameShowFight,
                snap.CapturedAt,
                achievements = snap.Achievements.Select(a => new
                {
                    a.Id,
                    name = string.IsNullOrWhiteSpace(a.Name) ? config.Achievement(a.Id) : a.Name,
                }).ToArray(),
                characters = snap.Characters.Select(c => new
                {
                    c.Id,
                    name = string.IsNullOrWhiteSpace(c.Name) && c.Id > 0 ? config.Character(c.Id) : c.Name,
                    c.FightCount,
                    c.Level,
                    c.Breakthrough,
                    image = c.Id > 0 ? assetUrlMapper(assets.Character(c.Id, config.CharacterAssets(c.Id))) : "",
                }).ToArray(),
                winMaps = snap.WinMaps.Select(m => new
                {
                    m.MapId,
                    name = string.IsNullOrWhiteSpace(m.Name) && m.MapId > 0 ? config.Map(m.MapId) : m.Name,
                    m.Wins,
                    image = m.MapId > 0 ? assetUrlMapper(assets.Map(m.MapId, config.MapAssets(m.MapId))) : "",
                }).ToArray(),
                recentMatches = snap.RecentMatches.Select(rm =>
                {
                    var indexedMapId = indexedMapIdLookup is not null && !string.IsNullOrWhiteSpace(rm.ReplayId)
                        ? indexedMapIdLookup(rm.ReplayId)
                        : 0;
                    var mapName = !string.IsNullOrWhiteSpace(rm.MapName)
                        ? rm.MapName
                        : indexedMapId > 0 ? config.Map(indexedMapId) : "";
                    var mapImage = indexedMapId > 0
                        ? assetUrlMapper(assets.Map(indexedMapId, config.MapAssets(indexedMapId)))
                        : "";
                    return new
                    {
                        rm.Index,
                        rm.Rank,
                        rm.HeroId,
                        character = string.IsNullOrWhiteSpace(rm.Character) && rm.HeroId > 0
                            ? config.Character(rm.HeroId)
                            : rm.Character,
                        heroImage = rm.HeroId > 0 ? assetUrlMapper(assets.Character(rm.HeroId, config.CharacterAssets(rm.HeroId))) : "",
                        rm.MapType,
                        mapName,
                        mapImage,
                        rm.ReplayId,
                        rm.Version,
                        rm.PlayedAt,
                        canDownload = ReplayCdn.IsValidReplayId(rm.ReplayId, out _),
                        players = rm.Players?.Select(p => new
                        {
                            p.PlayerId,
                            p.Name,
                            p.Level,
                            p.Gold,
                            p.HeroId,
                            character = string.IsNullOrWhiteSpace(p.Character) && p.HeroId > 0
                                ? config.Character(p.HeroId)
                                : p.Character,
                            heroImage = p.HeroId > 0 ? assetUrlMapper(assets.Character(p.HeroId, config.CharacterAssets(p.HeroId))) : "",
                            image = ResolveAvatarUrl(p.HeadIcon, p.HeroId, assets, config, assetUrlMapper),
                            isSelf = string.Equals(p.PlayerId, b.Uid, StringComparison.Ordinal),
                            p.Slot,
                            p.Rank,
                            p.GaveUp,
                            p.PlayerLevel,
                        }).ToArray(),
                    };
                }).ToArray(),
            };

            return new
            {
                b.Region,
                b.Uid,
                b.Nick,
                b.HeadIcon,
                headImage = ResolveAvatarUrl(b.HeadIcon, snap?.UseHero ?? 0, assets, config, assetUrlMapper),
                b.Level,
                b.IsPrimary,
                b.UpdatedAt,
                snapshot = snapUi,
            };
        }).ToArray();

        return new
        {
            profile.UserId,
            profile.Username,
            profile.DisplayName,
            avatarUrl = hubAvatarUrlMapper?.Invoke(profile.AvatarUrl) ?? "",
            profile.PrimaryRegion,
            profile.PrimaryUid,
            profile.IsSelf,
            profile.ProfileHidden,
            profile.HasPassword,
            profile.TwoFactorEnabled,
            profile.Privacy,
            bindings,
            profile.CreatedAt,
            profile.LastSeenAt,
        };
    }

    private static string ResolveHeadIconUrl(int headIcon, AssetLocator assets, Func<string?, string> assetUrlMapper)
    {
        if (headIcon <= 0) return "";
        var photo = assets.Named($"UT_Item_PlayerPhoto_{headIcon}");
        if (photo.Length > 0) return assetUrlMapper(photo);
        var headshot = assets.Named($"UT_AccountHeadShot_{headIcon}");
        return headshot.Length > 0 ? assetUrlMapper(headshot) : "";
    }

    private static string ResolveAvatarUrl(
        int headIcon,
        int fallbackHeroId,
        AssetLocator assets,
        ConfigCatalog config,
        Func<string?, string> assetUrlMapper)
    {
        if (headIcon > 0)
        {
            var photo = assets.Named($"UT_Item_PlayerPhoto_{headIcon}");
            if (photo.Length > 0) return assetUrlMapper(photo);
            var headshot = assets.Named($"UT_AccountHeadShot_{headIcon}");
            if (headshot.Length > 0) return assetUrlMapper(headshot);
        }

        if (fallbackHeroId > 0)
        {
            return assetUrlMapper(assets.Character(fallbackHeroId, config.CharacterAssets(fallbackHeroId)));
        }

        return "";
    }
}
