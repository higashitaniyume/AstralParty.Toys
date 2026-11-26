using System.Text.Json;

namespace AstralParty.Toys.Services.Hub;

public sealed class HubServerConfig
{
    public int Protocol { get; set; } = 1;
    public bool MicrosoftAuthEnabled { get; set; }
    public string MicrosoftClientId { get; set; } = "";
    public string MicrosoftTenant { get; set; } = "common";
    public string[] Regions { get; set; } = ["cn", "int"];
    public int PollWaitSeconds { get; set; } = 25;
    public int MaxRoomCapacity { get; set; } = 4;
    public long ServerTime { get; set; }
}

public sealed class HubPrivacySettings
{
    public bool ShowLinkedAccounts { get; set; } = true;
    public bool ShowProfileMeta { get; set; } = true;
    public bool ShowCreditAndPraise { get; set; } = true;
    public bool ShowSummaryStats { get; set; } = true;
    public bool ShowAchievements { get; set; } = true;
    public bool ShowWinMaps { get; set; } = true;
    public bool ShowCharacters { get; set; } = true;
    public bool ShowRecentMatches { get; set; } = true;
    public bool ShowMatchDetails { get; set; }
    public bool ShowLobbyPresence { get; set; } = true;
}

public sealed class HubAchievementDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class HubCharacterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public long? FightCount { get; set; }
    public int Level { get; set; }
    public bool Breakthrough { get; set; }
}

public sealed class HubWinMapDto
{
    public int MapId { get; set; }
    public string Name { get; set; } = "";
    public int Wins { get; set; }
}

public sealed class HubMatchPlayerDto
{
    public string PlayerId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Gold { get; set; }
    public int HeroId { get; set; }
    public string Character { get; set; } = "";
    public int Slot { get; set; }
    public int Rank { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public bool GaveUp { get; set; }
    public bool IsBot { get; set; }
    public int PlayerLevel { get; set; }
    public int HeadIcon { get; set; }
    public int KillCount { get; set; }
    public int TotalDamage { get; set; }
    public int TotalInjured { get; set; }
    public int TotalDie { get; set; }
    public int TreatmentScore { get; set; }
    public int MovePoint { get; set; }
    public int TotalGold { get; set; }
    public int PveTransferGold { get; set; }
}

public sealed class HubRecentMatchDto
{
    public int Index { get; set; }
    public int Rank { get; set; }
    public int HeroId { get; set; }
    public string Character { get; set; } = "";
    public int MapType { get; set; }
    public string MapName { get; set; } = "";
    public string ReplayId { get; set; } = "";
    public string Version { get; set; } = "";
    public long PlayedAt { get; set; }
    public List<HubMatchPlayerDto>? Players { get; set; }
}

public sealed class HubGameSnapshot
{
    public string Nick { get; set; } = "";
    public int HeadIcon { get; set; }
    public long Level { get; set; }
    public long Exp { get; set; }
    public long ExpNeed { get; set; }
    public long WinCount { get; set; }
    public long FightCount { get; set; }
    public long RoleCardCount { get; set; }
    public int UseHero { get; set; }
    public string UseHeroName { get; set; } = "";
    public long AdornCount { get; set; }
    public long SkinCount { get; set; }
    public int CreditScore { get; set; }
    public long PraiseNum { get; set; }
    public long CreatedAtGame { get; set; }
    public long LastLoginAtGame { get; set; }
    public bool GameShowData { get; set; } = true;
    public bool GameShowFight { get; set; } = true;
    public List<HubAchievementDto> Achievements { get; set; } = [];
    public List<HubCharacterDto> Characters { get; set; } = [];
    public List<HubWinMapDto> WinMaps { get; set; } = [];
    public List<HubRecentMatchDto> RecentMatches { get; set; } = [];
    public long CapturedAt { get; set; }
}

public sealed class HubBindingSummaryDto
{
    public string Region { get; set; } = "";
    public string Uid { get; set; } = "";
    public string Nick { get; set; } = "";
    public int HeadIcon { get; set; }
    public long Level { get; set; }
    public bool IsPrimary { get; set; }
    public long UpdatedAt { get; set; }
    public HubGameSnapshot? Snapshot { get; set; }
}

public sealed class HubUserProfileDto
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string PrimaryRegion { get; set; } = "";
    public string PrimaryUid { get; set; } = "";
    public bool IsSelf { get; set; }
    public bool ProfileHidden { get; set; }
    public HubPrivacySettings? Privacy { get; set; }
    public List<HubBindingSummaryDto> Bindings { get; set; } = [];
    public long CreatedAt { get; set; }
    public long LastSeenAt { get; set; }
}

public sealed class HubAuthResponse
{
    public string HubToken { get; set; } = "";
    public long ExpiresAt { get; set; }
    public HubUserProfileDto Me { get; set; } = new();
}

public sealed class HubInstantSummaryDto
{
    public string Region { get; set; } = "";
    public string ReplayId { get; set; } = "";
    public long GameRoomId { get; set; }
    public long WinnerId { get; set; }
    public long FinishTime { get; set; }
    public int MapId { get; set; }
    public int MapType { get; set; }
    public int Round { get; set; }
    public string GameVersion { get; set; } = "";
    public List<HubMatchPlayerDto> Players { get; set; } = [];
}

public sealed class HubGameRoomInfoDto
{
    public long GameRoomId { get; set; }
    public string GamePwd { get; set; } = "";
    public int RoomServerId { get; set; }
    public int MapId { get; set; }
    public int MapType { get; set; }
    public long PublishedAt { get; set; }
}

public sealed class HubRoomMemberDto
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string ActiveUid { get; set; } = "";
    public string ActiveNick { get; set; } = "";
    public int ActiveHeadIcon { get; set; }
    public long ActiveLevel { get; set; }
    public bool IsHost { get; set; }
    public bool Online { get; set; }
    public bool Self { get; set; }
    public string GameState { get; set; } = "idle";
    public string JoinError { get; set; } = "";
    public long JoinedAt { get; set; }
    public long LastSeenAt { get; set; }
}

public sealed class HubRoomSummaryDto
{
    public string RoomId { get; set; } = "";
    public string Region { get; set; } = "";
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public bool HasPassword { get; set; }
    public int Capacity { get; set; }
    public int MemberCount { get; set; }
    public int OnlineCount { get; set; }
    public string Status { get; set; } = "recruiting";
    public string HostUserId { get; set; } = "";
    public string HostDisplayName { get; set; } = "";
    public string HostAvatarUrl { get; set; } = "";
    public string HostActiveUid { get; set; } = "";
    public long CreatedAt { get; set; }
    public long LastActivityAt { get; set; }
}

public sealed class HubRoomDetailDto
{
    public string RoomId { get; set; } = "";
    public string Region { get; set; } = "";
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public bool HasPassword { get; set; }
    public int Capacity { get; set; }
    public int OnlineCount { get; set; }
    public string Status { get; set; } = "recruiting";
    public string HostUserId { get; set; } = "";
    public long Epoch { get; set; }
    public long Cursor { get; set; }
    public long CreatedAt { get; set; }
    public long LastActivityAt { get; set; }
    public HubGameRoomInfoDto? GameRoom { get; set; }
    public HubInstantSummaryDto? LatestMatch { get; set; }
    public List<HubRoomMemberDto> Members { get; set; } = [];
    public long ServerTime { get; set; }
}

public sealed class HubRoomEventDto
{
    public long Seq { get; set; }
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Text { get; set; } = "";
    public string? ClientMsgId { get; set; }
    public long At { get; set; }
}

public sealed class HubStreamResponse
{
    public int Protocol { get; set; } = 1;
    public long Epoch { get; set; }
    public HubRoomDetailDto Room { get; set; } = new();
    public List<HubRoomEventDto> Events { get; set; } = [];
    public long NextAfter { get; set; }
    public long ServerTime { get; set; }
}

public sealed class HubMatchSummaryDto
{
    public string MatchId { get; set; } = "";
    public string HubRoomId { get; set; } = "";
    public string Region { get; set; } = "";
    public string ReplayId { get; set; } = "";
    public long CreatedAt { get; set; }
    public HubInstantSummaryDto Summary { get; set; } = new();
}

public sealed class HubRoomListResponse
{
    public string Region { get; set; } = "";
    public List<HubRoomSummaryDto> Rooms { get; set; } = [];
}

public sealed class HubFinishResponse
{
    public HubRoomDetailDto Room { get; set; } = new();
    public HubMatchSummaryDto Match { get; set; } = new();
}

// ---------- 本地桥接 (HubBridgeMod ↔ Toys) 模型 ----------

public sealed class BridgeEndpointInfo
{
    public int Port { get; set; }
    public string Token { get; set; } = "";
    public int Pid { get; set; }
    public long UpdatedAt { get; set; }
}

public sealed class BridgeCommandDto
{
    public string CommandId { get; set; } = "";
    public string Action { get; set; } = "";
    public long GameRoomId { get; set; }
    public string GamePwd { get; set; } = "";
    public int RoomServerId { get; set; }
    public string RoomName { get; set; } = "";
    public int MapType { get; set; }
    public int MapId { get; set; }
}

public sealed class BridgeEventEnvelope
{
    public string EventId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Type { get; set; } = "";
    public long AtMs { get; set; }
    public JsonElement Data { get; set; }
}

public sealed class BridgeSessionState
{
    public string SessionId { get; set; } = "";
    public bool IsLoggedIn { get; set; }
    public long Uid { get; set; }
    public string Region { get; set; } = "";
    public string Scene { get; set; } = "";
    public long GameRoomId { get; set; }
    public string GamePwd { get; set; } = "";
    public int RoomServerId { get; set; }
    public int MapId { get; set; }
    public int MapType { get; set; }
    public long MasterId { get; set; }
    public bool IsMaster { get; set; }
    public string RoomState { get; set; } = "";
    public int PlayerCount { get; set; }
    public DateTimeOffset LastHeartbeatUtc { get; set; }
}
