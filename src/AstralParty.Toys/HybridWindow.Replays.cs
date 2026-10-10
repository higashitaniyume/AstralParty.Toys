using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private void OpenSpectator()
    {
        TryOpenExternal(SpectatorUri.AbsoluteUri);
    }

    /// <summary>
    /// 内嵌资源的唯一入口。必须保持"只有一个处理器"——见 <see cref="InitializeWebViewAsync"/> 里的说明：
    /// 多个 filter 共用一个事件，处理器要自己按 host 判断该不该接管，否则会互相覆盖响应。
    /// </summary>

    // ============================== 回放库 ==============================

    /// <summary>重新扫描游戏回放目录与回放库；runMaintain 为真时先跑一次自动托管。</summary>
    private async Task SendReplayLibraryAsync(bool runMaintain)
    {
        LibrarySnapshot snapshot;
        string? maintainMessage = null;
        try
        {
            snapshot = await Task.Run(() =>
            {
                if (runMaintain && _libraryService.Settings.AutoMaintain && !_libraryService.IsSameFolder())
                {
                    var maintained = _libraryService.Maintain();
                    if (maintained.Applied > 0) maintainMessage = maintained.Messages[0];
                }
                return _libraryService.Snapshot();
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"回放列表读取失败：{ex.Message}" });
            return;
        }

        Post(new { type = "replayLibrary", payload = BuildLibraryPayload(snapshot) });
        if (maintainMessage is not null) Post(new { type = "toast", message = maintainMessage });
    }

    private object BuildLibraryPayload(LibrarySnapshot snapshot)
    {
        _replayLibrary.Clear();
        var entries = new List<object>(snapshot.Entries.Count);
        for (var index = 0; index < snapshot.Entries.Count; index++)
        {
            var entry = snapshot.Entries[index];
            var token = $"replay-{index}";
            _replayLibrary[token] = entry.LibraryPath ?? entry.GamePath ?? "";
            var meta = entry.Meta;
            entries.Add(new
            {
                token,
                replayId = entry.ReplayId,
                inGame = entry.InGame,
                inLibrary = entry.InLibrary,
                healthy = meta?.Healthy ?? false,
                error = meta?.Error ?? "",
                sizeText = FormatLibrarySize(entry.SizeBytes),
                sizeBytes = entry.SizeBytes,
                modifiedText = entry.ModifiedUtc == DateTime.UnixEpoch
                    ? "—" : entry.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                sortTime = entry.SortTime,
                finishText = meta is { FinishTime: > 0 } ? FormatUnixTime(meta.FinishTime) : "—",
                durationText = meta is { DurationSeconds: > 0 } m2
                    ? TimeSpan.FromSeconds(m2.DurationSeconds).ToString(@"hh\:mm\:ss") : "—",
                mapName = meta?.MapName is { Length: > 0 } mapName ? mapName : "未知地图",
                mapId = meta?.MapId ?? 0,
                roundCount = meta?.RoundCount ?? 0,
                frameCount = meta?.FrameCount ?? 0,
                gameVersion = meta?.GameVersion is { Length: > 0 } version ? version : "—",
                winnerText = meta is { WinnerName.Length: > 0 } ? meta.WinnerName
                    : meta is { WinnerId: > 0 } ? $"玩家 {meta.WinnerId}" : "—",
                playersText = meta is null || meta.Players.Count == 0
                    ? "—"
                    : string.Join(" · ", meta.Players.Select(p => p.Nick.Length > 0 ? p.Nick : $"玩家 {p.Id}")),
                heroesText = meta is null || meta.Players.Count == 0
                    ? "—"
                    : string.Join(" · ", meta.Players.Select(p => p.HeroName))
            });
        }

        return new
        {
            directory = snapshot.GameDirectory,
            available = snapshot.GameDirectoryAvailable,
            libraryRoot = snapshot.LibraryRoot,
            libraryAvailable = snapshot.LibraryAvailable,
            libraryCount = snapshot.LibraryCount,
            librarySizeText = FormatLibrarySize(snapshot.LibraryBytes),
            gameCount = snapshot.GameCount,
            capacity = snapshot.GameSlotCapacity,
            keepInGame = snapshot.KeepInGame,
            autoMaintain = snapshot.AutoMaintain,
            gameFull = snapshot.GameFull,
            gameOverKeep = snapshot.GameOverKeep,
            gameRunning = snapshot.GameRunning,
            sameFolder = snapshot.SameFolder,
            warnings = snapshot.Warnings,
            entries
        };
    }

    private static List<string> ReadIds(JsonElement root)
    {
        var ids = new List<string>();
        if (root.TryGetProperty("ids", out var element) && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } id) ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>归档游戏目录里最旧的 count 局（保留名额以内的部分不动）。</summary>
    private ReplayOperationResult ArchiveOldest(int count)
    {
        var snapshot = _libraryService.Snapshot();
        var gameEntries = snapshot.Entries.Where(x => x.InGame).ToList();
        var excess = gameEntries.Count - ReplayLibraryService.GameSlotCapacity;
        var take = Math.Max(excess, 0) > 0 ? Math.Max(excess, 0) : Math.Min(count, gameEntries.Count);
        var targets = gameEntries.Skip(Math.Max(gameEntries.Count - take, 0)).Select(x => x.ReplayId).ToList();
        if (targets.Count == 0) return ReplayOperationResult.Fail("游戏目录里没有可归档的回放。");

        var result = _libraryService.Archive(targets);
        result.Messages.Insert(0, $"已归档 {result.Applied} 局最旧的回放，游戏内席位腾出 {result.Applied} 个。");
        return result;
    }

    private async Task RunLibraryOperationAsync(Func<ReplayOperationResult> operation)
    {
        ReplayOperationResult result;
        try
        {
            result = await Task.Run(operation);
        }
        catch (Exception ex)
        {
            result = ReplayOperationResult.Fail($"操作失败：{ex.Message}");
        }

        Post(new
        {
            type = "libraryResult",
            payload = new
            {
                ok = result.Ok,
                applied = result.Applied,
                skipped = result.Skipped,
                failed = result.Failed,
                needsConfirmation = result.NeedsConfirmation,
                pendingIds = result.PendingIds,
                summary = result.Messages.Count > 0 ? result.Messages[0] : (result.Ok ? "操作完成。" : "操作失败。"),
                messages = result.Messages
            }
        });
        await SendReplayLibraryAsync(runMaintain: false);
    }

    private async Task HandleLibraryImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要导入回放库的回放文件",
            Filter = "回放文件|*|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;
        await RunLibraryOperationAsync(() => _libraryService.Import(path));
    }

    /// <summary>
    /// 按回放 ID 从官方 CDN 下载一局并收进回放库。
    /// 地址与游戏自己用的一模一样（<c>_cdnBaseUrl + replayId</c>，见 <see cref="ReplayCdn"/>）；
    /// 区服默认跟随「当前游戏」的版本（国际服 → jp，其余 → cn），界面可以单独改。
    /// 进度只回给下载弹窗（<c>replayDownloadState</c>），结果也只在那儿收口，
    /// 所以这里刻意**不**再发 libraryResult——否则同一个失败会弹两次。
    /// </summary>
    private async Task HandleReplayDownloadAsync(JsonElement root)
    {
        var replayId = (root.TryGetProperty("replayId", out var idElement) ? idElement.GetString() : null)?.Trim() ?? "";
        var endpoint = ReplayCdn.TryParse(root.TryGetProperty("cdn", out var cdnElement) ? cdnElement.GetString() : null)
                       ?? ReplayCdn.FromEdition(_gameLibrary.GetActive()?.Edition);

        if (!ReplayCdn.IsValidReplayId(replayId, out var idError))
        {
            PostDownloadState(replayId, "failed", 0, null, [idError]);
            return;
        }
        if (_replayDownloadCts is not null)
        {
            PostDownloadState(replayId, "failed", 0, null, ["已经有一个回放在下载了，等它结束再试。"]);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _replayDownloadCts = cancellation;
        long receivedBytes = 0;
        long? totalBytes = null;
        // 用 InlineProgress 而不是 Progress<T>：后者的回调是异步投递到 UI 上下文的，
        // 排在收尾消息之后到达时会让进度条在「完成」之后又跳回去一次。
        var progress = new InlineProgress<ReplayDownloadProgress>(value =>
        {
            receivedBytes = value.ReceivedBytes;
            totalBytes = value.TotalBytes;
            PostDownloadState(replayId, "running", value.ReceivedBytes, value.TotalBytes, []);
        });

        ReplayOperationResult result;
        try
        {
            PostDownloadState(replayId, "running", 0, null, []);
            result = await _libraryService.DownloadAsync(replayId, endpoint, progress, cancellation.Token);
        }
        catch (Exception ex)
        {
            result = ReplayOperationResult.Fail($"下载失败：{ex.Message}");
        }
        finally
        {
            _replayDownloadCts = null;
            cancellation.Dispose();
        }

        PostDownloadState(replayId, result.Ok ? "done" : result.Canceled ? "canceled" : "failed",
            receivedBytes, totalBytes, result.Messages);
        await SendReplayLibraryAsync(runMaintain: false);
    }

    /// <summary>把下载进度 / 结果推给界面（下载弹窗按 replayId 认领）。</summary>
    private void PostDownloadState(string replayId, string state, long receivedBytes, long? totalBytes, IReadOnlyList<string> messages)
    {
        Post(new
        {
            type = "replayDownloadState",
            payload = new
            {
                replayId,
                state,
                receivedBytes,
                totalBytes,
                percent = totalBytes is > 0 ? (int)Math.Clamp(receivedBytes * 100 / totalBytes.Value, 0, 100) : (int?)null,
                summary = messages.Count > 0 ? messages[0] : "",
                messages
            }
        });
    }

    private void HandleLibraryOpenFolder()
    {
        try
        {
            _libraryService.EnsureLibrary();
            SpeedhackManager.OpenInExplorer(_libraryService.LibraryRoot);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开库目录失败：{ex.Message}" });
        }
    }

    private void HandleLibraryBrowseRoot()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择回放库目录（不要和游戏的 Temp\\Replay 选成同一个）",
            InitialDirectory = Directory.Exists(_libraryService.LibraryRoot) ? _libraryService.LibraryRoot : null
        };
        if (dialog.ShowDialog(this) != true || dialog.FolderName is not { Length: > 0 } folder) return;
        _libraryService.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = folder,
            AutoMaintain = _libraryService.Settings.AutoMaintain,
            KeepInGame = _libraryService.Settings.KeepInGame
        });
        Post(new { type = "toast", message = $"回放库已切换到：{folder}" });
        Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
    }

    private void HandleLibrarySaveSettings(JsonElement root)
    {
        var root_ = root.TryGetProperty("libraryRoot", out var rootElement) ? rootElement.GetString() : null;
        var autoMaintain = root.TryGetProperty("autoMaintain", out var autoElement) && autoElement.GetBoolean();
        var keepInGame = root.TryGetProperty("keepInGame", out var keepElement) && keepElement.TryGetInt32(out var parsedKeep)
            ? parsedKeep
            : ReplayLibraryService.GameSlotCapacity;

        _libraryService.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = string.IsNullOrWhiteSpace(root_) ? null : root_,
            AutoMaintain = autoMaintain,
            KeepInGame = Math.Clamp(keepInGame, 1, ReplayLibraryService.GameSlotCapacity)
        });
        Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
        Post(new { type = "toast", message = "回放库设置已保存。" });
    }

    private object BuildLibrarySettingsPayload() => new
    {
        libraryRoot = _libraryService.LibraryRoot,
        defaultLibraryRoot = ReplayLibraryService.DefaultLibraryRoot(),
        autoMaintain = _libraryService.Settings.AutoMaintain,
        keepInGame = _libraryService.Settings.KeepInGame,
        capacity = ReplayLibraryService.GameSlotCapacity
    };


    private static string FormatUnixTime(long seconds)
        => DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private static string FormatLibrarySize(long length) => length switch
    {
        >= 1024 * 1024 => $"{length / 1024d / 1024d:0.00} MB",
        >= 1024 => $"{length / 1024d:0.0} KB",
        _ => $"{length} B"
    };


    private async Task PickAndLoadReplayAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择吉星派对回放文件",
            Filter = "回放文件|*|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) await LoadReplayAsync(dialog.FileName);
    }

    private async Task LoadReplayAsync(string path)
    {
        Post(new { type = "loading", active = true, message = "正在准备协议和配置…" });
        try
        {
            var progress = new Progress<string>(message => Post(new { type = "loading", active = true, message }));
            _report = await Task.Run(() =>
            {
                _analyzer ??= new ReplayAnalyzer(_appDirectory, _assetDirectory, _materialDirectories);
                return _analyzer.Analyze(path, progress);
            });
            Post(new { type = "report", payload = BuildClientReport(_report) });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = ex.Message });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    private async Task SendFrameDetailAsync(int index)
    {
        if (_report is null || _analyzer is null || index < 0 || index >= _report.Frames.Count) return;
        var frame = _report.Frames[index];
        var detail = await Task.Run(() => _analyzer.FormatFrame(frame));
        Post(new
        {
            type = "frameDetail",
            payload = new { frame.Index, frame.OffsetText, frame.CmdId, frame.MessageName, frame.PayloadLength, detail }
        });
    }

    private async Task ExportRelicsAsync(string? format)
    {
        if (_report is null) return;

        var baseName = Path.GetFileNameWithoutExtension(_report.FileName);
        var isTxt = string.Equals(format, "txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(format, "text", StringComparison.OrdinalIgnoreCase);
        var isCsv = !isTxt && string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

        var dialog = new SaveFileDialog
        {
            Title = isTxt 
                ? "导出筹码时间线 (自然语言 AI 文本)" 
                : isCsv 
                    ? "导出筹码时间线表格 (CSV)" 
                    : "导出筹码时间线数据 (JSON)",
            Filter = isTxt
                ? "AI 分析文本 (*.txt)|*.txt|所有文件 (*.*)|*.*"
                : isCsv 
                    ? "CSV 逗号分隔表格 (*.csv)|*.csv|所有文件 (*.*)|*.*" 
                    : "JSON 数据文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = isTxt
                ? $"{baseName}.relics.ai.txt"
                : isCsv 
                    ? $"{baseName}.relics.csv" 
                    : $"{baseName}.relics.json"
        };

        if (dialog.ShowDialog(this) != true) return;

        Post(new { type = "loading", active = true, message = "正在导出筹码时间线…" });
        try
        {
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (ext == ".txt" || isTxt)
            {
                var text = await Task.Run(() => BuildRelicsAiSummary(_report));
                await File.WriteAllTextAsync(dialog.FileName, text, Encoding.UTF8);
            }
            else if (ext == ".csv" || isCsv)
            {
                var sb = new StringBuilder();
                // 写入 UTF-8 BOM 避免 Excel 打开中文乱码
                sb.AppendLine("帧号,回合,玩家ID,玩家名称,玩家角色,操作类型,档位,筹码ID,筹码名称,品质,来源,刷新状态,候选选项");
                foreach (var r in _report.Relics)
                {
                    string EscapeCsv(string val) => $"\"{val.Replace("\"", "\"\"")}\"";
                    sb.AppendLine(string.Join(",",
                        r.FrameIndex,
                        r.Round,
                        r.PlayerId,
                        EscapeCsv(r.PlayerName),
                        EscapeCsv(r.HeroName),
                        EscapeCsv(r.Kind),
                        EscapeCsv(r.LevelText),
                        r.RelicId,
                        EscapeCsv(r.RelicName),
                        EscapeCsv(r.Quality),
                        EscapeCsv(r.Source),
                        EscapeCsv(r.RefreshText),
                        EscapeCsv(r.OptionsText)
                    ));
                }
                var utf8Bom = new UTF8Encoding(true);
                await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), utf8Bom);
            }
            else
            {
                var payload = new
                {
                    replayFile = _report.FileName,
                    totalRelicEvents = _report.Relics.Count,
                    relics = _report.Relics.Select(r => new
                    {
                        frameIndex = r.FrameIndex,
                        round = r.Round,
                        playerId = r.PlayerId,
                        playerName = r.PlayerName,
                        heroName = r.HeroName,
                        actionKind = r.Kind,
                        level = r.Level,
                        relicId = r.RelicId,
                        relicName = r.RelicName,
                        quality = r.Quality,
                        source = r.Source,
                        refreshCount = r.RefreshNumber,
                        isRefresh = r.IsRefresh,
                        refreshText = r.RefreshText,
                        optionsText = r.OptionsText
                    })
                };
                var json = await Task.Run(() => JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
                await File.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);
            }

            Post(new { type = "toast", message = $"已成功导出筹码记录到 {Path.GetFileName(dialog.FileName)}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"导出失败: {ex.Message}" });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    private async Task ExportShopsAsync(string format)
    {
        if (_report is null) return;

        var baseName = Path.GetFileNameWithoutExtension(_report.FileName);
        var dialog = new SaveFileDialog
        {
            Title = "导出商店时间线表格 (CSV)",
            Filter = "CSV 逗号分隔表格 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"{baseName}.shops.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        Post(new { type = "loading", active = true, message = "正在导出商店时间线…" });
        try
        {
            var sb = new StringBuilder();
            // UTF-8 BOM 避免 Excel 打开中文乱码
            sb.AppendLine("帧号,回合,玩家ID,玩家名称,玩家角色,商店类型,候选卡牌,售价,折扣,免费卡,购买结果,已售出槽位,是否关闭");
            foreach (var s in _report.Shops)
            {
                string EscapeCsv(string val) => $"\"{val.Replace("\"", "\"\"")}\"";
                sb.AppendLine(string.Join(",",
                    s.FrameIndex,
                    s.Round,
                    s.PlayerId,
                    EscapeCsv(s.PlayerName),
                    EscapeCsv(s.HeroName),
                    EscapeCsv(s.ShopType),
                    EscapeCsv(s.OptionsText),
                    s.Price,
                    s.Discount,
                    s.FreeCard > 0 ? $"{s.FreeCard} x{s.FreeCardNum}" : "",
                    EscapeCsv(s.PurchaseText),
                    EscapeCsv(s.SoldOutText),
                    s.IsClosed
                ));
            }
            var utf8Bom = new UTF8Encoding(true);
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), utf8Bom);
            Post(new { type = "toast", message = $"已成功导出商店记录到 {Path.GetFileName(dialog.FileName)}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"导出失败: {ex.Message}" });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    public static string BuildRelicsAiSummary(ReplayReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 《吉星派对》(Astral Party) 对局筹码流转与战术决策复盘数据");
        sb.AppendLine($"# 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("# 说明: 本文档专为大语言模型 (AI) 对局复盘设计，包含对局基础背景、参战玩家配置、全局筹码统计以及按回合推进的筹码候选与决策全量时间线。");
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("一、对局背景基本信息");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"- 回放文件: {report.FileName}");
        sb.AppendLine($"- 游戏版本: {report.GameVersion}");
        sb.AppendLine($"- 对局地图: {report.MapName} (地图ID: {report.MapId})");
        sb.AppendLine($"- 模式/难度: {report.MapType} / {report.Difficulty}");
        sb.AppendLine($"- 首领(BOSS): {report.BossName}");
        sb.AppendLine($"- 对局进程: {report.ProgressText}");
        sb.AppendLine($"- 对局结果: {report.ResultText} (最终决胜: {report.WinnerText})");
        sb.AppendLine($"- 回合统计: 共 {report.RoundCount} 回合 (包含 {report.TurnCount} 个行动轮次, 帧数 {report.FrameCount})");
        sb.AppendLine($"- 对局时间: {report.StartTimeText} 至 {report.FinishTimeText} (时长: {report.DurationText})");
        sb.AppendLine($"- 玩家阵亡次数: {report.PlayerDeaths} 次");
        sb.AppendLine();

        sb.AppendLine("================================================================================");
        sb.AppendLine("二、参战玩家与角色战绩概况");
        sb.AppendLine("================================================================================");
        foreach (var p in report.Players)
        {
            sb.AppendLine($"【玩家】{p.Nickname} (ID: {p.Id})");
            sb.AppendLine($"  - 参战角色: {p.HeroName} (英雄ID: {p.HeroId} · 账号等级: {p.AccountLevel})");
            sb.AppendLine($"  - 最终状态: {p.FinalStatus}" + (p.FinalBossKill ? " [★ 最终首领击杀者/MVP]" : ""));
            sb.AppendLine($"  - 生命值: {p.HpText} | 拥有星币: {p.Gold} | 攻防属性: 攻击 {p.Attack} / 防御 {p.Defense}");
            sb.AppendLine($"  - 输出伤害: {p.Damage:N0} | 承受伤害: {p.Injured:N0} | 击杀数: {p.Kills} | 治疗量: {p.Healing}");
            sb.AppendLine($"  - 棋盘移动: {p.MovePoints} 点 | 卡牌使用: {p.UsedCards} 张 | 技能释放: {p.UsedSkills} 次");
            sb.AppendLine($"  - 筹码地块购买次数: {p.BoughtRelics} 次 | 最终持有筹码数: {p.SelectedRelicCount} 个");
            sb.AppendLine($"  - 最终持有筹码清单: {(string.IsNullOrWhiteSpace(p.SelectedRelicsText) ? "无" : p.SelectedRelicsText)}");
            sb.AppendLine();
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("三、全局筹码统计摘要");
        sb.AppendLine("================================================================================");
        var picks = report.Relics.Where(r => r.Kind == "选择").ToList();
        var candidates = report.Relics.Where(r => r.Kind != "选择").ToList();
        var refreshes = report.Relics.Count(r => r.IsRefresh);
        sb.AppendLine($"- 筹码流转记录总数: {report.Relics.Count} 条 (最终选定 {picks.Count} 次，出现候选 {candidates.Count} 次)");
        sb.AppendLine($"- 玩家刷新候选次数: {refreshes} 次");

        var sourceGroups = picks.GroupBy(r => r.Source).OrderByDescending(g => g.Count());
        sb.AppendLine($"- 选定筹码来源分布: " + string.Join("，", sourceGroups.Select(g => $"{g.Key} {g.Count()} 次")));

        var qualityGroups = picks.Where(r => r.RelicId > 0).GroupBy(r => r.Quality).OrderByDescending(g => g.Count());
        sb.AppendLine($"- 选定筹码品质分布: " + string.Join("，", qualityGroups.Select(g => $"{g.Key} {g.Count()} 个")));
        sb.AppendLine();

        sb.AppendLine("================================================================================");
        sb.AppendLine("四、按回合详细筹码流转与决策时间线 (Chronological Relic Timeline)");
        sb.AppendLine("================================================================================");
        var roundGroups = report.Relics.GroupBy(r => r.Round > 0 ? r.Round : 0).OrderBy(g => g.Key);
        foreach (var group in roundGroups)
        {
            var roundName = group.Key > 0 ? $"第 {group.Key} 回合" : "全局 / 开局阶段";
            sb.AppendLine($"--------------------------------------------------------------------------------");
            sb.AppendLine($"【{roundName}】(共 {group.Count()} 条流转事件)");
            sb.AppendLine($"--------------------------------------------------------------------------------");

            var index = 1;
            foreach (var r in group)
            {
                if (r.Kind == "选择")
                {
                    sb.AppendLine($"  [事件 {index++} | 帧 {r.FrameIndex}] 玩家「{r.PlayerName}」（角色：{r.HeroName}）");
                    sb.AppendLine($"    - 动作类型: 【最终选定筹码】");
                    sb.AppendLine($"    - 获得筹码: 【{r.RelicName}】 (品质: {r.Quality} · ID: {r.RelicId} · {r.LevelText})");
                    sb.AppendLine($"    - 筹码来源: {r.Source}");
                    sb.AppendLine($"    - 刷新情况: {(r.RefreshNumber > 0 ? $"此前累计刷新了 {r.RefreshNumber} 次后才确认选择" : "未刷新，直接从初始候选选择")}");
                }
                else if (r.IsRefresh)
                {
                    sb.AppendLine($"  [事件 {index++} | 帧 {r.FrameIndex}] 玩家「{r.PlayerName}」（角色：{r.HeroName}）");
                    sb.AppendLine($"    - 动作类型: 【刷新候选】(第 {Math.Max(1, r.RefreshNumber)} 次刷新)");
                    sb.AppendLine($"    - 来源途径: {r.Source} ({r.LevelText})");
                    sb.AppendLine($"    - 刷新后呈现的新候选池: {r.OptionsText}");
                }
                else
                {
                    sb.AppendLine($"  [事件 {index++} | 帧 {r.FrameIndex}] 玩家「{r.PlayerName}」（角色：{r.HeroName}）");
                    sb.AppendLine($"    - 动作类型: 【首次呈现候选】");
                    sb.AppendLine($"    - 来源途径: {r.Source} ({r.LevelText})");
                    sb.AppendLine($"    - 初始候选池: {r.OptionsText}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("五、供 AI 智能分析与复盘的建议 Prompt 方向");
        sb.AppendLine("================================================================================");
        sb.AppendLine("你可以直接复制以下 Prompt 向大语言模型提问：");
        sb.AppendLine("1. 【流派与契合度评估】请结合每位玩家的角色技能与定位，分析他们选取的筹码是否形成了有效联动（Synergy）？有没有错失更优质的候选筹码？");
        sb.AppendLine("2. 【刷新时机与策略分析】请评价各位玩家在各回合中的刷新决策：是否存在盲目刷新浪费机会，或是在关键轮次赌出了核心关键筹码？");
        sb.AppendLine($"3. 【关键胜负手复盘】根据获胜者（{report.WinnerText}）与其他玩家的数据，分析获胜者主要是依靠哪几个核心筹码建立优势并最终斩获胜利的？");
        sb.AppendLine("4. 【改进建议】如果给表现落后或伤害不足的玩家提供改进策略，在筹码获取和资源配置上应该做出哪些针对性调整？");
        sb.AppendLine();
        return sb.ToString();
    }

    private async Task ExportAsync()
    {
        if (_report is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出回放完整数据",
            Filter = "JSON 数据文件 (*.json)|*.json|CSV 筹码表格 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"{Path.GetFileNameWithoutExtension(_report.FileName)}.report.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await ExportRelicsAsync("csv");
            return;
        }

        Post(new { type = "loading", active = true, message = "正在导出完整协议数据…" });
        try
        {
            var json = await Task.Run(() => JsonSerializer.Serialize(BuildExportReport(_report), new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
            await File.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);
            // 只报文件名：完整路径不换行，会长到把居中的 toast 胶囊撑出窗口
            Post(new { type = "toast", message = $"已导出到 {Path.GetFileName(dialog.FileName)}" });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    private object BuildClientReport(ReplayReport report) => new
    {
        file = new { report.FileName, report.FilePath, report.FileSizeText, report.Sha256 },
        assets = report.UiAssets.ToDictionary(pair => pair.Key, pair => AssetUrl(pair.Value)),
        match = new
        {
            report.ReplayId, report.GameVersion, report.RoomId, report.RoomServerId,
            report.MapId, report.MapName, mapImage = AssetUrl(report.MapImagePath),
            report.MapType, report.Difficulty, report.RoundCount, report.TurnCount,
            report.FrameCount, report.CommandTypeCount, report.PlayerDeaths,
            report.GameProgress, report.GameMaxProgress, report.ProgressText,
            report.StartTimeText, report.FinishTimeText, report.DurationText,
            report.ResultText, report.WinnerText, report.BossName, report.AwardsText,
            report.WarningCount
        },
        players = report.Players.Select(x => new
        {
            x.Id, x.Nickname, x.Slot, x.AccountLevel, x.HeroId, x.HeroName,
            avatar = AssetUrl(x.AvatarPath), x.FinalStatus, x.Hp, x.MaxHp, x.HpText,
            x.Gold, x.Attack, x.Defense, x.Damage, x.Injured, x.Kills, x.Healing,
            x.MovePoints, x.UsedCards, x.UsedSkills, x.TransferGold, x.BoughtRelics,
            x.FinalBossKill, x.SelectedRelicCount, x.SelectedRelicsText
        }),
        events = report.Events.Select(x => new
        {
            x.FrameIndex, x.Round, x.RoundText, x.PlayerId, x.PlayerName, x.Type,
            x.Title, x.Description, icon = AssetUrl(x.IconPath)
        }),
        relics = report.Relics.Select(x => new
        {
            x.FrameIndex, x.Round, x.PlayerId, x.PlayerName, x.HeroName, x.Kind, x.Level, x.LevelText,
            x.RelicId, x.RelicName, x.Quality, x.OptionsText, x.Source, x.RefreshNumber, x.IsRefresh,
            x.RefreshText, image = AssetUrl(x.ImagePath), sourceIcon = AssetUrl(x.SourceIconPath)
        }),
        shops = report.Shops.Select(x => new
        {
            x.FrameIndex, x.Round, x.PlayerId, x.PlayerName, x.HeroName, x.ShopType,
            x.OptionsText, x.Price, x.Discount, x.PriceText, x.FreeCard, x.FreeCardNum,
            x.Alreadys, x.BuyIndices, x.BoughtText, x.PurchaseText,
            x.IsClosed, x.HasPurchase, x.SoldOutText
        }),
        frames = report.Frames.Select(x => new { x.Index, x.Offset, x.OffsetText, x.CmdId, x.MessageName, x.PayloadLength }),
        statistics = new
        {
            commands = report.CommandStats,
            actions = report.ActionStats,
            relicQualities = report.RelicQualityStats
        },
        warnings = report.Warnings
    };

    private static object BuildExportReport(ReplayReport report) => new
    {
        schemaVersion = 1,
        source = new { report.FileName, report.FileSizeText, report.Sha256 },
        match = new
        {
            report.ReplayId, report.GameVersion, report.RoomId, report.RoomServerId,
            report.MapId, report.MapName, report.MapType, report.Difficulty,
            report.StartTimeText, report.FinishTimeText, report.DurationText,
            report.ResultText, report.WinnerText, report.RoundCount, report.TurnCount,
            report.PlayerDeaths, report.GameProgress, report.GameMaxProgress,
            report.BossName, report.AwardsText
        },
        players = report.Players,
        timeline = report.Events,
        relics = report.Relics,
        shops = report.Shops,
        statistics = new { commands = report.CommandStats, actions = report.ActionStats, relicQualities = report.RelicQualityStats },
        frames = report.Frames.Select(frame => new
        {
            frame.Index, frame.Offset, frame.CmdId, frame.MessageName, frame.PayloadLength,
            payloadBase64 = Convert.ToBase64String(frame.Payload)
        }),
        warnings = report.Warnings
    };

}
