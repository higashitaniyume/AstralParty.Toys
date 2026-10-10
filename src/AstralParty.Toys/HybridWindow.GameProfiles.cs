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
    // ============================== 游戏档案（多位置管理） ==============================

    /// <summary>把「当前游戏」目录写进两个管理器各自的状态文件，让模组页 / 变速器页看到的是同一个游戏。</summary>
    private void SyncActiveGameToManagers()
    {
        // 同步已保存的选择无需探测目录；离线网络路径的存在检查会等待系统超时。
        var dir = _gameLibrary.GetActive()?.Directory;
        if (string.IsNullOrWhiteSpace(dir)) return;
        _modManager.SaveStoredGameDirectory(dir);
        _speedhackManager.SaveStoredGameDirectory(dir);
    }

    private int _gameProfilesRequest;

    private async Task PushGameProfilesAsync()
    {
        var request = ++_gameProfilesRequest;
        try
        {
            // GetProfiles 会校验每个游戏位置，包含非当前的网络目录；全部移出界面线程。
            var payload = await Task.Run(() =>
            {
                var profiles = _gameLibrary.GetProfiles();
                return new
                {
                    profiles,
                    activeId = profiles.FirstOrDefault(p => p.Active)?.Id,
                    drives = GameLibraryService.FixedDriveRoots()
                };
            });
            if (request == _gameProfilesRequest)
                Post(new { type = "gameProfiles", payload });
        }
        catch (Exception ex)
        {
            if (request == _gameProfilesRequest)
                Post(new { type = "error", message = $"读取游戏位置失败：{ex.Message}" });
        }
    }

    private void HandleGameProfileBrowse()
    {
        var current = _gameLibrary.GetActiveDirectory();
        var dialog = new OpenFolderDialog
        {
            Title = "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
            InitialDirectory = current is { Length: > 0 } && Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(this) != true || dialog.FolderName is not { Length: > 0 } folder) return;

        if (GameLibraryService.DetectExe(folder) is null)
        {
            Post(new { type = "toast", message = "这个目录里没有检测到游戏主程序。请确认所选目录包含游戏 exe、UnityPlayer.dll 和 *_Data 文件夹。" });
            return;
        }
        var view = _gameLibrary.AddDirectory(folder, activate: true);
        SyncActiveGameToManagers();
        Post(new { type = "toast", message = $"已添加并切换到：{view.Label}" });
        _ = PushGameProfilesAsync();
        _ = PushModStatusAsync();
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleGameProfileAddPath(string directory, bool activate)
    {
        if (!Directory.Exists(directory) || GameLibraryService.DetectExe(directory) is null)
        {
            Post(new { type = "toast", message = "这个目录已经不在了，或里面没有 AstralParty*.exe。" });
            return;
        }
        var view = _gameLibrary.AddDirectory(directory, activate);
        if (activate) SyncActiveGameToManagers();
        Post(new { type = "toast", message = activate ? $"已添加并切换到：{view.Label}" : $"已添加：{view.Label}" });
        _ = PushGameProfilesAsync();
        if (activate) { _ = PushModStatusAsync(); _ = PushSpeedhackStatusAsync(); }
    }

    private void HandleGameProfileSetActive(string id)
    {
        _gameLibrary.SetActive(id);
        SyncActiveGameToManagers();
        _ = PushGameProfilesAsync();
        _ = PushModStatusAsync();
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleGameProfileOpen(string id)
    {
        var directory = _gameLibrary.GetDirectory(id);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            Post(new { type = "toast", message = "这个目录已经不在了。" });
            return;
        }
        try { Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
        catch (Exception ex) { Post(new { type = "toast", message = $"打不开目录：{ex.Message}" }); }
    }

    private void HandleGameScanQuick()
    {
        Post(new { type = "gameScanProgress", payload = new { mode = "quick", running = true, message = "正在扫描 Steam 库、注册表与常见安装位置…" } });
        _ = Task.Run(() =>
        {
            try
            {
                var candidates = _gameLibrary.QuickScan();
                Post(new { type = "gameScanResult", payload = new { mode = "quick", candidates } });
            }
            catch (Exception ex)
            {
                Post(new { type = "gameScanProgress", payload = new { mode = "quick", running = false, message = $"扫描出错：{ex.Message}" } });
            }
        });
    }

    private void HandleGameScanDeep(string? drive)
    {
        if (string.IsNullOrWhiteSpace(drive) || !Directory.Exists(drive))
        {
            Post(new { type = "toast", message = "请选择一个要深度扫描的盘符。" });
            return;
        }
        _deepScanCts?.Cancel();
        var cts = new CancellationTokenSource();
        _deepScanCts = cts;
        var driveRoot = drive;
        _ = Task.Run(() =>
        {
            try
            {
                Post(new { type = "gameScanProgress", payload = new { mode = "deep", running = true, message = $"正在深度扫描 {driveRoot} …" } });
                var candidates = _gameLibrary.DeepScan(driveRoot, cts.Token, (count, current) =>
                    Post(new { type = "gameScanProgress", payload = new { mode = "deep", running = true, message = $"已扫描 {count} 个目录…", current } }));
                Post(new { type = "gameScanResult", payload = new { mode = "deep", candidates } });
            }
            catch (OperationCanceledException)
            {
                Post(new { type = "gameScanProgress", payload = new { mode = "deep", running = false, message = "已取消深度扫描。" } });
            }
            catch (Exception ex)
            {
                Post(new { type = "gameScanProgress", payload = new { mode = "deep", running = false, message = $"深度扫描出错：{ex.Message}" } });
            }
            finally
            {
                if (_deepScanCts == cts) _deepScanCts = null;
                cts.Dispose();
            }
        });
    }


    // ============================== 启动游戏 ==============================


    /// <summary>Steam AppID 2622000（Astral Party / 吉星派对）。</summary>
    private const string SteamLaunchUrl = "steam://rungameid/2622000";

    /// <summary>
    /// 启动游戏。首页「启动游戏」下面那两个单选框（二选一）决定走哪条路：
    ///   · 从 Steam 启动（默认）—— 交给 steam:// 协议，能带上 Steam 的 DRM / 云存档 / 更新检查。
    ///     Steam 没装或 steam:// 协议没注册时，退回直接启动探测到的游戏主程序。
    ///   · 绕过 Steam 启动 —— 直接拉起游戏主程序，完全不经过 Steam。游戏本体在"非 Steam 客户端"下会在
    ///     启动早期自己退出（[SteamManager] 非Steam客户端启动），所以这条路依赖加载器的 Steam 绕过；
    ///     那个开关与首页这个单选框是同一个配置，勾选时就已经写进 doorstop_config.json 了。
    /// </summary>
    private void HandleLaunchGame(bool bypassSteam)
    {
        // 不再因为"游戏已经在运行"就拦截再次启动 —— 保存了多个游戏位置时，
        // 用户可能想在已开一个的情况下再启动另一个（例如国服 + 国际服），或第一次没起来想重试。
        // 也不再按区服猜"这个版本在不在 Steam 上"：用户在下拉栏选了哪个版本、又选了哪种启动方式就照做，
        // 不警告、不识别，后果自负（当前"通过 Steam 启动"只有固定 appid 这一条路；"绕过 Steam" = 直启选中的版本）。
        if (!bypassSteam)
        {
            try
            {
                Process.Start(new ProcessStartInfo(SteamLaunchUrl) { UseShellExecute = true });
                Post(new { type = "toast", message = "已交给 Steam 启动 Astral Party，稍等一下。" });
                return;
            }
            catch (Exception steamError)
            {
                LaunchGameDirectly($"Steam 不可用（{steamError.Message}），已直接启动游戏主程序。");
                return;
            }
        }

        LaunchGameDirectly("已绕过 Steam，直接启动游戏主程序。");
    }

    /// <summary>首页「启动方式」单选框：把选择同步进加载器配置（保留注释）。
    /// 「绕过 Steam 启动」= 整套绕过打开；「从 Steam 启动」= 整套关掉（完全回到原版行为，Steam 真大厅可用）。
    /// 失败只提示、不改单选框状态 —— 单选框本身是前端偏好，配置同步是尽力而为。</summary>
    private void HandleSetSteamBypass(bool bypass)
    {
        try
        {
            var directory = RequireModGameDirectory();
            if (_modManager.SetSteamBypassProfile(directory, bypass))
            {
                Post(new
                {
                    type = "toast",
                    message = bypass
                        ? "已切换到绕过 Steam：加载器的 Steam 绕过已打开（重启游戏生效）"
                        : "已切换到从 Steam 启动：加载器的 Steam 绕过已整套关闭，Steam 大厅联机恢复正常（重启游戏生效）"
                });
            }
            else
            {
                Post(new
                {
                    type = "toast",
                    message = "已记住这个选择，但没能同步到加载器配置（还没装加载器？在「模组」页安装一次就会同步）。"
                });
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"已记住这个选择，但同步加载器配置失败：{ex.Message}" });
        }
    }

    /// <summary>把加载器配置里当前的 Steam 绕过状态推给首页，让「启动方式」单选框一进界面就跟配置一致。
    ///
    /// 判定用 <see cref="ModManager.IsSteamBypassActive"/>（三个键任一为真即算绕过）：
    /// 只要还有 hook 会生效，Steam 那边的行为就不是原版，首页就不能显示成「从 Steam 启动」。
    ///
    /// 没装加载器（没有 doorstop_config.json）时不推：那时没有"配置"可跟随，首页保留自己的默认 ——「从 Steam 启动」。
    /// 推送失败也不影响任何功能，首页会退回它自己记住的选择。</summary>
    private int _steamBypassSyncRequest;

    private async Task PostSteamBypassSyncAsync()
    {
        var request = ++_steamBypassSyncRequest;
        try
        {
            var enabled = await Task.Run<bool?>(() =>
            {
                var gameDirectory = _modManager.GetStoredGameDirectory();
                if (string.IsNullOrEmpty(gameDirectory)) return null;
                if (!File.Exists(_modManager.LoaderConfigPath(gameDirectory))) return null;
                return ModManager.IsSteamBypassActive(_modManager.ReadLoaderConfig(gameDirectory));
            });
            if (request == _steamBypassSyncRequest && enabled.HasValue)
                Post(new { type = "steamBypassSync", payload = new { enabled = enabled.Value } });
        }
        catch
        {
            // 网络位置离线或读不到配置时，保留首页已经记住的选择。
        }
    }

    /// <summary>把本程序的版本信息推给界面（顶部栏版本胶囊 / 底部状态栏 / 版本详情弹窗都用它）。

    /// <summary>直接拉起游戏主程序（不经过 Steam），成功后把 successMessage（必要时再加一句提醒）发给前端。</summary>
    private void LaunchGameDirectly(string successMessage)
    {
        var gameDirectory = _speedhackManager.ResolveGameDirectory();
        var gameExe = string.IsNullOrEmpty(gameDirectory) ? null : SpeedhackManager.ContainsGameExe(gameDirectory);
        if (gameExe is null)
        {
            Post(new
            {
                type = "toast",
                message = "没能启动游戏：没有找到游戏主程序。请先在「变速器」页选择游戏目录，或确认游戏已安装。"
            });
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(gameExe)
            {
                UseShellExecute = true,
                WorkingDirectory = gameDirectory!
            });
            Post(new { type = "toast", message = successMessage + SteamBypassCaveat(gameDirectory!) });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"启动游戏失败：{ex.Message}" });
        }
    }

    /// <summary>直接启动（绕过 Steam）时的提醒：没装加载器、或加载器的 Steam 绕过没生效、且 Steam 又没在运行的话，
    /// 游戏会在启动早期打印"[ERROR] [SteamManager] 非Steam客户端启动, 退出游戏"然后自己退出。
    /// 只提醒不拦截 —— Steam 在后台跑着时直接启动一般也能进游戏。
    ///
    /// 这里只看 <c>steamBypassEnabled</c>（阶段1 = 那道自杀门）：只有它决定"启动早期会不会自己退出"，
    /// 与匹配 / 房间列表那几个键无关（那几个影响的是联机大厅，不是能不能进游戏）。</summary>
    private string SteamBypassCaveat(string gameDirectory)
    {
        try
        {
            if (Process.GetProcessesByName("steam").Length > 0) return "";   // Steam 在跑：直接启动一般也能进游戏
            if (!_modManager.GetStatus().Installed)
            {
                return "（提示：Steam 没在运行，而且还没安装加载器 —— 缺了它的 Steam 绕过，游戏会在启动早期自己退出。"
                     + "可在「模组」页一键安装加载器，或改选「从 Steam 启动」）";
            }

            if (_modManager.ReadLoaderConfig(gameDirectory).SteamBypassEnabled) return "";
            return "（提示：Steam 没在运行，加载器的「Steam 绕过」是关的，游戏可能会在启动早期自己退出 ——"
                 + "可在「模组 → 加载器设置」里打开它）";
        }
        catch
        {
            return "";
        }
    }

}
