using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AstralParty.Toys.Services.Skins;
using AstralParty.Toys.Services.Speedhack;
using Microsoft.Win32;

namespace AstralParty.Toys;

/// <summary>「自定义 → 主页背景」：第三方模组 HomeBackgroundMod（TenkinMori）的设置页。</summary>
public partial class HybridWindow
{
    private HomeBackgroundService? _homeBackground;
    private HomeBackgroundService HomeBackground => _homeBackground ??= new HomeBackgroundService(_modManager);
    private bool _homeBgBusy;

    private string? ResolveHomeBgGameDirectory()
    {
        try
        {
            var dir = _modManager.GetStatus().GameDirectory;
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) return dir;
        }
        catch { }
        return null;
    }

    private void HandleHomeBackgroundMessage(string type, JsonElement root)
    {
        var game = ResolveHomeBgGameDirectory();
        if (game == null)
        {
            if (type == "homeBgGetStatus") Post(new { type = "homeBgStatus", payload = new { gameDirectory = "" } });
            else Post(new { type = "error", message = "还没有选择游戏目录" });
            return;
        }

        switch (type)
        {
            case "homeBgGetStatus":
                PushHomeBgStatus(game);
                break;

            case "homeBgPickImage":
                {
                    var exts = string.Join(";", HomeBackgroundService.ImageExtensions.Select(e => "*" + e));
                    var dialog = new OpenFileDialog { Title = "选择主页背景图片", Filter = $"图片 ({exts})|{exts}|所有文件 (*.*)|*.*" };
                    if (dialog.ShowDialog(this) != true) break;
                    RunHomeBg(game, "正在转换图片…", () =>
                    {
                        HomeBackground.SetImage(game, dialog.FileName);
                        return SpeedhackManager.IsGameRunning()
                            ? "已换成这张图片：回到游戏主页就能看到（第一次启用要重启游戏）"
                            : "已换成这张图片，下次启动游戏生效";
                    });
                }
                break;

            case "homeBgPickVideo":
                {
                    var exts = string.Join(";", HomeBackgroundService.VideoExtensions.Select(e => "*" + e));
                    var dialog = new OpenFileDialog { Title = "选择主页背景视频", Filter = $"视频 ({exts})|{exts}|所有文件 (*.*)|*.*" };
                    if (dialog.ShowDialog(this) != true) break;
                    RunHomeBg(game, "正在复制视频…", () =>
                    {
                        HomeBackground.SetVideo(game, dialog.FileName);
                        return "已换成这段视频，重启游戏后生效";
                    });
                }
                break;

            case "homeBgSetHideTheme":
                {
                    var hide = root.TryGetProperty("hide", out var h) && h.GetBoolean();
                    RunHomeBg(game, null, () =>
                    {
                        HomeBackground.SetHideThemeVideo(game, hide);
                        return hide ? "已设置：隐藏主题自带的背景动画" : "已设置：保留主题自带的背景动画";
                    });
                }
                break;

            case "homeBgRestore":
                RunHomeBg(game, null, () =>
                {
                    HomeBackground.RestoreDefault(game);
                    return "已恢复游戏默认背景，重启游戏后生效（你设置的背景文件还留着）";
                });
                break;

            case "homeBgOpenFolder":
                {
                    var dir = HomeBackgroundService.ModDirectory(game);
                    if (Directory.Exists(dir)) Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    else Post(new { type = "error", message = "还没有装过主页背景模组" });
                }
                break;
        }
    }

    /// <summary>改文件的操作放到后台：转换大图、复制视频可能要一两秒。一次只做一件。</summary>
    private void RunHomeBg(string game, string? busyMessage, Func<string> work)
    {
        if (_homeBgBusy) { Post(new { type = "error", message = "上一个操作还没完成" }); return; }
        _homeBgBusy = true;
        if (busyMessage != null) Post(new { type = "homeBgBusy", payload = new { busy = true, message = busyMessage } });
        _ = Task.Run(() =>
        {
            try
            {
                var message = work();
                Post(new { type = "toast", message });
            }
            catch (Exception ex)
            {
                Post(new { type = "error", message = "主页背景：" + ex.Message });
            }
            finally
            {
                _homeBgBusy = false;
                Post(new { type = "homeBgBusy", payload = new { busy = false, message = "" } });
                PushHomeBgStatus(game);
            }
        });
    }

    private void PushHomeBgStatus(string game)
    {
        try
        {
            Post(new { type = "homeBgStatus", payload = HomeBackground.GetStatus(game) });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "读取主页背景状态失败：" + ex.Message });
        }
    }
}
