using System.Text.Json;
using AstralParty.Toys.Services.Replay;

namespace AstralParty.Toys.Services.Hub;

/// <summary>
/// 两阶段对局结算解析器：
/// Stage 1 直接消费游戏内模组回传的 <c>GameFinishS2C</c> 即时汇总；
/// Stage 2 在后台轮询官方回放 CDN 下载完整回放并用 <see cref="ReplayAnalyzer"/> 解析明细。
/// </summary>
public sealed class HubSettlementService
{
    private readonly string _appDirectory;
    private readonly ReplayLibraryService _libraryService;

    public HubSettlementService(string appDirectory, ReplayLibraryService libraryService)
    {
        _appDirectory = appDirectory;
        _libraryService = libraryService;
    }

    public static HubInstantSummaryDto? ParseGameFinishedEvent(BridgeEventEnvelope env)
    {
        if (!string.Equals(env.Type, "game_finished", StringComparison.Ordinal) ||
            env.Data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var d = env.Data;
        var summary = new HubInstantSummaryDto
        {
            Region = NormalizeRegion(ReadString(d, "region")),
            ReplayId = ReadString(d, "replayId").Trim(),
            GameRoomId = ReadLong(d, "gameRoomId"),
            WinnerId = ReadLong(d, "winnerId"),
            FinishTime = ReadLong(d, "finishTime"),
            MapId = ReadInt(d, "mapId"),
            MapType = ReadInt(d, "mapType"),
            Round = ReadInt(d, "round"),
            GameVersion = ReadString(d, "gameVersion").Trim(),
        };

        if (d.TryGetProperty("players", out var playersEl) && playersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in playersEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var pid = ReadString(item, "playerId");
                if (string.IsNullOrEmpty(pid))
                {
                    var pidNum = ReadLong(item, "playerId");
                    if (pidNum != 0) pid = pidNum.ToString();
                }

                summary.Players.Add(new HubMatchPlayerDto
                {
                    PlayerId = pid,
                    Name = ReadString(item, "nick").Length > 0 ? ReadString(item, "nick") : ReadString(item, "name"),
                    Level = ReadInt(item, "level"),
                    Gold = ReadInt(item, "gold"),
                    HeroId = ReadInt(item, "heroId"),
                    Slot = ReadInt(item, "slot"),
                    Rank = ReadInt(item, "rank"),
                    Hp = ReadInt(item, "hp"),
                    MaxHp = ReadInt(item, "maxHp"),
                    IsBot = ReadBool(item, "isBot"),
                    PlayerLevel = ReadInt(item, "accountLevel"),
                    KillCount = ReadInt(item, "killCount"),
                    TotalDamage = ReadInt(item, "totalDamage"),
                    TotalInjured = ReadInt(item, "totalInjured"),
                    TotalDie = ReadInt(item, "totalDie"),
                    TreatmentScore = ReadInt(item, "treatmentScore"),
                    MovePoint = ReadInt(item, "movePoint"),
                    TotalGold = ReadInt(item, "totalGold"),
                    PveTransferGold = ReadInt(item, "pveTransferGold"),
                });
            }
        }

        return summary;
    }

    public object BuildStage1Payload(
        HubInstantSummaryDto summary,
        string language,
        Func<string?, string> assetUrlMapper,
        AssetLocator assets,
        string? hubRoomId = null,
        string? stage2Status = null,
        string? stage2Message = null)
    {
        var config = CreateConfig(language);
        var mapName = summary.MapId > 0 ? config.Map(summary.MapId) : "未知地图";
        var mapImage = summary.MapId > 0
            ? assetUrlMapper(assets.Map(summary.MapId, config.MapAssets(summary.MapId)))
            : "";

        var players = summary.Players
            .OrderBy(p => p.Rank > 0 ? p.Rank : int.MaxValue)
            .ThenBy(p => p.Slot)
            .Select(p =>
            {
                var heroName = p.HeroId > 0 ? config.Character(p.HeroId) : "未知角色";
                var heroImage = p.HeroId > 0
                    ? assetUrlMapper(assets.Character(p.HeroId, config.CharacterAssets(p.HeroId)))
                    : "";
                return new
                {
                    p.PlayerId,
                    p.Name,
                    p.Slot,
                    p.Rank,
                    p.HeroId,
                    heroName,
                    heroImage,
                    p.Level,
                    p.Gold,
                    p.Hp,
                    p.MaxHp,
                    p.IsBot,
                    p.PlayerLevel,
                    p.KillCount,
                    p.TotalDamage,
                    p.TotalInjured,
                    p.TotalDie,
                    p.TreatmentScore,
                    p.MovePoint,
                    p.TotalGold,
                    p.PveTransferGold,
                };
            })
            .ToArray();

        return new
        {
            hubRoomId = hubRoomId ?? "",
            region = NormalizeRegion(summary.Region),
            summary.ReplayId,
            summary.GameRoomId,
            summary.WinnerId,
            summary.FinishTime,
            summary.MapId,
            mapName,
            mapImage,
            summary.MapType,
            summary.Round,
            summary.GameVersion,
            canPollReplay = ReplayCdn.IsValidReplayId(summary.ReplayId, out _),
            stage2Status = stage2Status ?? (ReplayCdn.IsValidReplayId(summary.ReplayId, out _) ? "pending" : "unavailable"),
            stage2Message = stage2Message ?? "",
            players,
        };
    }

    public async Task<(bool Ok, string Message, ReplayReport? Report)> PollAndAnalyzeReplayAsync(
        string region,
        string replayId,
        string language,
        AssetLocator assets,
        string assetDirectory,
        IReadOnlyList<string> materialDirectories,
        CancellationToken ct)
    {
        if (!ReplayCdn.IsValidReplayId(replayId, out var idErr))
        {
            return (false, idErr, null);
        }

        var id = replayId.Trim();
        var existing = _libraryService.LibraryFile(id);
        if (!File.Exists(existing))
        {
            var endpoint = string.Equals(NormalizeRegion(region), "int", StringComparison.OrdinalIgnoreCase)
                ? ReplayCdnEndpoint.JpProd
                : ReplayCdnEndpoint.CnProd;

            int[] delaysMs = [2000, 3000, 5000, 8000, 12000, 15000, 15000];
            ReplayOperationResult? lastResult = null;
            for (var attempt = 0; attempt < delaysMs.Length; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                lastResult = await _libraryService.DownloadAsync(id, endpoint, progress: null, ct).ConfigureAwait(false);
                if (File.Exists(existing)) break;
                if (attempt + 1 < delaysMs.Length)
                {
                    await Task.Delay(delaysMs[attempt], ct).ConfigureAwait(false);
                }
            }

            if (!File.Exists(existing))
            {
                var detail = lastResult?.Messages.FirstOrDefault() ?? "官方回放 CDN 暂未生成该局文件，请稍后手动重试。";
                return (false, detail, null);
            }
        }

        var analyzer = new ReplayAnalyzer(
            _appDirectory,
            assetDirectory,
            materialDirectories,
            useEmbeddedAssets: true,
            language: language);
        var report = await Task.Run(() => analyzer.Analyze(existing), ct).ConfigureAwait(false);
        return (true, "已获取官方回放并完成详细战报解析。", report);
    }

    public object BuildStage2Payload(
        string replayId,
        ReplayReport report,
        Func<string?, string> assetUrlMapper)
    {
        var perfById = report.Performance.ToDictionary(x => x.PlayerId);
        var players = report.Players.Select(p =>
        {
            perfById.TryGetValue(p.Id, out var perf);
            var diceAll = perf?.Dice.SelectMany(d => d.Values).ToArray() ?? Array.Empty<int>();
            var avgDice = diceAll.Length > 0 ? Math.Round(diceAll.Average(), 2) : 0.0;
            return new
            {
                playerId = p.Id.ToString(),
                p.Nickname,
                p.Slot,
                p.AccountLevel,
                p.HeroId,
                p.HeroName,
                heroImage = assetUrlMapper(p.AvatarPath),
                p.FinalStatus,
                p.Hp,
                p.MaxHp,
                p.Gold,
                p.Attack,
                p.Defense,
                p.Damage,
                p.Injured,
                p.Kills,
                p.Healing,
                p.MovePoints,
                p.UsedCards,
                p.UsedSkills,
                p.TransferGold,
                p.BoughtRelics,
                p.FinalBossKill,
                p.SelectedRelicCount,
                p.SelectedRelicsText,
                diceCount = diceAll.Length,
                avgDice,
                selectedRelics = p.SelectedRelics.Select(r => new
                {
                    relicId = r.Id,
                    r.Name,
                    qualityName = r.Quality,
                    icon = assetUrlMapper(r.ImagePath),
                }).ToArray(),
            };
        }).ToArray();

        return new
        {
            replayId,
            report.FilePath,
            report.GameVersion,
            report.MapId,
            report.MapName,
            mapImage = assetUrlMapper(report.MapImagePath),
            report.MapType,
            report.RoundCount,
            report.TurnCount,
            report.DurationText,
            report.ResultText,
            report.WinnerText,
            report.BossName,
            report.AwardsText,
            players,
            commandStats = report.CommandStats.Take(8).Select(s => new { s.Name, s.Count, ratioText = s.Percent.ToString("P1") }).ToArray(),
            actionStats = report.ActionStats.Take(10).Select(s => new { s.Name, s.Count, ratioText = s.Percent.ToString("P1") }).ToArray(),
            relics = report.Relics.Select(r => new
            {
                r.Round,
                r.Source,
                r.PlayerName,
                r.RelicId,
                r.RelicName,
                qualityName = r.Quality,
                optionsText = r.OptionsText,
            }).ToArray(),
        };
    }

    private ConfigCatalog CreateConfig(string language)
        => new(new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")),
            Path.Combine(_appDirectory, "GameData"),
            language);

    public static string NormalizeRegion(string? region)
    {
        var r = (region ?? "").Trim().ToLowerInvariant();
        if (r is "int" or "abroad" or "jp" or "global") return "int";
        if (r is "cn" or "china" or "aotcn") return "cn";
        return "cn";
    }

    private static string ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : "";

    private static int ReadInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.TryGetInt32(out var v) ? v : 0;

    private static long ReadLong(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.TryGetInt64(out var v) ? v : 0;

    private static bool ReadBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False) && el.GetBoolean();
}
