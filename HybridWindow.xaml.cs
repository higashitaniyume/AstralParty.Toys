using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _appDirectory = AppContext.BaseDirectory;
    private readonly string _assetDirectory;
    private readonly IReadOnlyList<string> _materialDirectories;
    private readonly HomeDataService _homeDataService;
    private readonly SpeedhackManager _speedhackManager;
    private ReplayAnalyzer? _analyzer;
    private ReplayReport? _report;
    private bool _webReady;
    private readonly Dictionary<string, string> _replayLibrary = new(StringComparer.Ordinal);

    public HybridWindow()
    {
        InitializeComponent();
        _assetDirectory = Path.Combine(_appDirectory, "Assets");
        _materialDirectories = MaterialSource.DiscoverAll(_appDirectory);
        _homeDataService = new HomeDataService(_appDirectory);
        _speedhackManager = new SpeedhackManager(_appDirectory);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var webRoot = Path.Combine(_appDirectory, "WebUI");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new FileNotFoundException("找不到 WebUI/index.html。", Path.Combine(webRoot, "index.html"));

            StartupDetail.Text = "初始化本地界面";
            var userDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AstralParty.Toys", "WebView2");
            Directory.CreateDirectory(userDataDirectory);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await WebView.EnsureCoreWebView2Async(environment);
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.astral.local", webRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            WebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://assets.astral.local/*", CoreWebView2WebResourceContext.Image);
            WebView.CoreWebView2.WebResourceRequested += EmbeddedAssetRequested;
            for (var index = 0; index < _materialDirectories.Count; index++)
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    $"materials{index}.astral.local", _materialDirectories[index], CoreWebView2HostResourceAccessKind.Allow);
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            WebView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                TryOpenExternal(args.Uri);
            };
            WebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith("https://app.astral.local/", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                    TryOpenExternal(args.Uri);
                }
            };
            WebView.Source = new Uri("https://app.astral.local/index.html");
        }
        catch (Exception ex)
        {
            StartupDetail.Text = $"主界面启动失败：{ex.Message}";
            ClassicButton.Visibility = Visibility.Visible;
        }
    }

    private void EmbeddedAssetRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        var stream = EmbeddedAssetStore.OpenWebPath(uri.AbsolutePath);
        var contentType = Path.GetExtension(uri.AbsolutePath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/webp";
        e.Response = stream is null
            ? WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 404, "Not Found", "Content-Type: text/plain")
            : WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                stream, 200, "OK",
                $"Content-Type: {contentType}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!e.Source.StartsWith("https://app.astral.local/", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            switch (type)
            {
                case "ready":
                    _webReady = true;
                    StartupOverlay.Visibility = Visibility.Collapsed;
                    Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                    await SendReplayLibraryAsync();
                    var args = Environment.GetCommandLineArgs();
                    if (args.Length > 1 && File.Exists(args[1])) await LoadReplayAsync(args[1]);
                    break;
                case "getHomeData":
                    Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                    break;
                case "openReplay":
                    await PickAndLoadReplayAsync();
                    break;
                case "openRecentReplay":
                    if (root.TryGetProperty("token", out var tokenElement) &&
                        tokenElement.GetString() is { Length: > 0 } token &&
                        _replayLibrary.TryGetValue(token, out var replayPath) && File.Exists(replayPath))
                        await LoadReplayAsync(replayPath);
                    break;
                case "refreshReplays":
                    await SendReplayLibraryAsync();
                    break;
                case "getFrame":
                    if (root.TryGetProperty("index", out var indexElement)) await SendFrameDetailAsync(indexElement.GetInt32());
                    break;
                case "export":
                    await ExportAsync();
                    break;
                case "exportRelics":
                    var relicFormat = root.TryGetProperty("format", out var formatElement) ? formatElement.GetString() : "csv";
                    await ExportRelicsAsync(relicFormat);
                    break;
                case "openClassic":
                    OpenClassicWindow();
                    break;
                case "openBrowser":
                    if (root.TryGetProperty("url", out var urlElement) &&
                        urlElement.GetString() is { Length: > 0 } targetUrl)
                    {
                        TryOpenExternal(targetUrl);
                    }
                    break;
                case "speedhackStatus":
                    PushSpeedhackStatus();
                    break;
                case "speedhackDetect":
                    HandleSpeedhackDetect();
                    break;
                case "speedhackBrowse":
                    HandleSpeedhackBrowse();
                    break;
                case "speedhackInstall":
                    var overwriteDll = root.TryGetProperty("overwriteDll", out var overwriteElement) &&
                                       overwriteElement.GetBoolean();
                    HandleSpeedhackInstall(overwriteDll);
                    break;
                case "speedhackUninstall":
                    var force = root.TryGetProperty("force", out var forceElement) && forceElement.GetBoolean();
                    HandleSpeedhackUninstall(force);
                    break;
                case "speedhackSaveConfig":
                    if (root.TryGetProperty("config", out var configElement))
                    {
                        var afterSave = root.TryGetProperty("afterSave", out var afterSaveElement)
                            ? afterSaveElement.GetString() : null;
                        var saveOverwrite = root.TryGetProperty("overwriteDll", out var saveOverwriteElement) &&
                                            saveOverwriteElement.GetBoolean();
                        HandleSpeedhackSaveConfig(configElement.GetRawText(), afterSave, saveOverwrite);
                    }
                    break;
                case "speedhackResetConfig":
                    HandleSpeedhackResetConfig();
                    break;
                case "speedhackPushConfig":
                    HandleSpeedhackPushConfig();
                    break;
                case "speedhackEditConfig":
                    HandleSpeedhackEditConfig();
                    break;
                case "speedhackOpenGameDir":
                    HandleSpeedhackOpenGameDir();
                    break;
                case "closeWindow":
                    Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = ex.Message });
        }
    }

    private static void TryOpenExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private async Task SendReplayLibraryAsync()
    {
        var directory = FindReplayDirectory();
        var files = await Task.Run(() =>
        {
            if (!Directory.Exists(directory)) return new List<FileInfo>();
            try
            {
                return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Length > 0)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(100)
                    .ToList();
            }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        });

        _replayLibrary.Clear();
        var items = files.Select((file, index) =>
        {
            var token = $"replay-{index}-{file.LastWriteTimeUtc.Ticks}";
            _replayLibrary[token] = file.FullName;
            var parentName = file.Directory?.Name;
            return new
            {
                token,
                name = string.IsNullOrWhiteSpace(parentName) ? file.Name : $"回放 {parentName}",
                fileName = file.Name,
                sizeText = FormatLibrarySize(file.Length),
                modifiedText = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
            };
        }).ToList();

        Post(new { type = "replayLibrary", payload = new { directory, available = Directory.Exists(directory), items } });
    }

    private static string FindReplayDirectory()
    {
        var localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "feimo");
        var candidates = new[]
        {
            Path.Combine(localLow, "AstralParty_CN", "Temp", "Replay"),
            Path.Combine(localLow, "AstralParty", "_CN", "Temp", "Replay")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

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
            Post(new { type = "toast", message = $"已导出到 {dialog.FileName}" });
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
        statistics = new { commands = report.CommandStats, actions = report.ActionStats, relicQualities = report.RelicQualityStats },
        frames = report.Frames.Select(frame => new
        {
            frame.Index, frame.Offset, frame.CmdId, frame.MessageName, frame.PayloadLength,
            payloadBase64 = Convert.ToBase64String(frame.Payload)
        }),
        warnings = report.Warnings
    };

    private string AssetUrl(string path)
    {
        if (EmbeddedAssetStore.TryGetRelativePath(path, out var embeddedPath))
            return "https://assets.astral.local/" + EncodePath(embeddedPath);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
        var relative = Path.GetRelativePath(_assetDirectory, path).Replace('\\', '/');
        if (!relative.StartsWith("../", StringComparison.Ordinal))
            return "https://assets.astral.local/" + EncodePath(relative);

        for (var index = 0; index < _materialDirectories.Count; index++)
        {
            relative = Path.GetRelativePath(_materialDirectories[index], path).Replace('\\', '/');
            if (!relative.StartsWith("../", StringComparison.Ordinal))
                return $"https://materials{index}.astral.local/" + EncodePath(relative);
        }
        return "";
    }

    private static string EncodePath(string relative) => string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));

    // ============================== 变速器（游戏工具） ==============================

    private void PushSpeedhackStatus()
    {
        var status = _speedhackManager.GetStatus();
        SpeedhackEditorModel? editor = null;
        var configBroken = false;
        try
        {
            editor = SpeedhackEditorMapper.MapToEditor(_speedhackManager.LoadProfileConfig());
        }
        catch (Exception)
        {
            configBroken = true;
        }

        Post(new { type = "speedhackStatus", payload = new { status, config = editor, configBroken } });
    }

    private void HandleSpeedhackDetect()
    {
        var directory = SpeedhackManager.DetectGameDirectory();
        if (directory is null)
        {
            Post(new { type = "toast", message = "自动检测未找到游戏目录，请点「选择…」手动指定。" });
        }
        else
        {
            _speedhackManager.SaveStoredGameDirectory(directory);
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
        }
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackBrowse()
    {
        var current = _speedhackManager.ResolveGameDirectory();
        var dialog = new OpenFolderDialog
        {
            Title = "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
            InitialDirectory = current is { Length: > 0 } && Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(this) == true && dialog.FolderName is { Length: > 0 })
        {
            _speedhackManager.SaveStoredGameDirectory(dialog.FolderName);
            Post(new { type = "toast", message = $"已选择游戏目录：{dialog.FolderName}" });
            PushSpeedhackStatus();
        }
    }

    private string RequireGameDirectory()
    {
        var directory = _speedhackManager.GetStatus().GameDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("尚未选择有效的游戏目录，请先自动检测或手动选择。");
        return directory;
    }

    private void HandleSpeedhackInstall(bool overwriteDll)
    {
        _speedhackManager.Install(RequireGameDirectory(), overwriteDll);
        Post(new { type = "toast", message = "变速器已安装到游戏目录。进入游戏后按配置的快捷键即可变速（需关闭垂直同步）。" });
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackUninstall(bool force)
    {
        var result = _speedhackManager.Uninstall(RequireGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackSaveConfig(string rawJson, string? afterSave, bool overwriteDll)
    {
        var model = JsonSerializer.Deserialize<SpeedhackEditorModel>(rawJson, WebReadOptions);
        if (model is null) throw new InvalidOperationException("配置内容为空，无法保存。");

        var config = _speedhackManager.LoadProfileConfig();
        SpeedhackEditorMapper.ApplyToConfig(config, model);
        _speedhackManager.SaveProfileConfig(config);

        if (string.Equals(afterSave, "install", StringComparison.Ordinal))
        {
            _speedhackManager.Install(RequireGameDirectory(), overwriteDll);
            Post(new { type = "toast", message = $"变速器已安装；每次进入游戏将自动使用 {model.BaseSpeed:0.##} 倍速。" });
            PushSpeedhackStatus();
            return;
        }

        var currentStatus = _speedhackManager.GetStatus();
        var installedDirectory = currentStatus.Installed ? currentStatus.GameDirectory : null;
        if (!string.IsNullOrEmpty(installedDirectory)) _speedhackManager.PushConfigToGame(installedDirectory);

        Post(new
        {
            type = "toast",
            message = installedDirectory is { Length: > 0 }
                ? $"配置已保存并同步到游戏目录；下次进入游戏自动使用 {model.BaseSpeed:0.##} 倍速。"
                : "配置已保存到本地主配置，安装变速器时会一并复制到游戏目录。"
        });
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackResetConfig()
    {
        _speedhackManager.ResetProfileToTemplate();
        var installedDirectory = _speedhackManager.GetStatus().Installed
            ? _speedhackManager.GetStatus().GameDirectory : null;
        if (!string.IsNullOrEmpty(installedDirectory)) _speedhackManager.PushConfigToGame(installedDirectory);

        Post(new { type = "toast", message = "已恢复默认模板（Ctrl 切换 2 倍速）。" });
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackPushConfig()
    {
        var directory = RequireGameDirectory();
        if (!File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
            throw new InvalidOperationException("游戏目录尚未安装变速器，请先安装。");
        _speedhackManager.PushConfigToGame(directory);
        Post(new { type = "toast", message = "配置已同步到游戏目录，重启游戏后生效。" });
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackEditConfig()
    {
        var profilePath = _speedhackManager.EnsureProfileConfig();
        SpeedhackManager.OpenWithDefaultEditor(profilePath);
    }

    private void HandleSpeedhackOpenGameDir()
    {
        var directory = RequireGameDirectory();
        SpeedhackManager.OpenInExplorer(directory);
    }

    private static readonly JsonSerializerOptions WebReadOptions = new() { PropertyNameCaseInsensitive = true };

    private void Post(object message)
    {
        if (!_webReady || WebView.CoreWebView2 is null) return;
        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions));
    }

    private void ClassicButton_Click(object sender, RoutedEventArgs e) => OpenClassicWindow();

    private void OpenClassicWindow()
    {
        var window = new MainWindow();
        Application.Current.MainWindow = window;
        window.Show();
        Close();
    }
}
