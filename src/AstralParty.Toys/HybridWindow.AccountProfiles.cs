using System.Diagnostics;
using System.IO;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    // ============================== 游戏多开（账号 profile + 实例外壳） ==============================
    //
    // 每个 profile 是一套独立的 LocalLow 目录（账号令牌 / SaveFile.es3 / 回放 / 下载缓存），
    // 由加载器(version.dll)在游戏进程内做目录重定向。
    //
    // UnityPlayer 的单实例互斥与 exe 路径绑定：同一 exe 路径只能开一个实例。需要同时多开时，
    // Toys 会用 GameInstanceService 在 LocalAppData 下建一个实例外壳（exe/DLL 硬链接、
    // 数据目录 junction），并从外壳 exe 启动；账号目录通过子进程的 CESIUM_PROFILE 环境变量
    // 指定（优先级高于 doorstop_config.json 的 profile 字段）。
    //
    // 目录布局（与加载器一致）：
    //   %USERPROFILE%\AppData\LocalLow\<公司>\<产品>\              默认档案（游戏自己的目录）
    //   %USERPROFILE%\AppData\LocalLow\<公司>\<产品>_profiles\<名>\ 一个 profile

    private int _accountProfilesRequest;

    private AccountProfileService CreateAccountProfileService()
    {
        string? gameDirectory = null;
        try { gameDirectory = _modManager.GetStoredGameDirectory(); }
        catch { /* 读不到就当没选 */ }
        return new AccountProfileService(gameDirectory);
    }

    private async Task PushAccountProfilesAsync()
    {
        var request = ++_accountProfilesRequest;
        try
        {
            var payload = await Task.Run(() =>
            {
                var service = CreateAccountProfileService();
                var gameDirectory = service.GameDirectory;
                var loaderInstalled = gameDirectory is not null &&
                                      File.Exists(_modManager.LoaderConfigPath(gameDirectory));
                var config = gameDirectory is not null && loaderInstalled
                    ? _modManager.ReadLoaderConfig(gameDirectory)
                    : null;
                var activeName = config?.Profile ?? "";

                return new
                {
                    gameDirectory,
                    sourceDirectory = service.SourceDirectory,
                    profilesRoot = service.ProfilesRoot,
                    activeName,
                    loaderInstalled,
                    // 窗口标题开关：配置里没有该键时加载器按默认（开）处理，界面显示也跟随这个默认。
                    windowTitleEnabled = config?.ProfileWindowTitle ?? true,
                    canManage = service.Error is null,
                    error = service.Error,
                    // 名字规则由宿主下发，页面用它做实时校验（两边不各写一份）
                    namePattern = AccountProfileService.NamePattern,
                    nameMaxLength = AccountProfileService.MaxNameLength,
                    profiles = service.List(activeName).Select(p => new
                    {
                        name = p.Name,
                        directory = p.Directory,
                        isDefault = p.IsDefault,
                        active = p.Active,
                        exists = p.Exists,
                        bytes = p.Bytes,
                        replays = p.ReplayCount,
                        lastWrite = p.LastWriteUtc == default ? null : p.LastWriteUtc.ToString("o"),
                        cacheSeeded = p.CacheSeeded,
                        note = p.Note
                    }).ToList()
                };
            });

            if (request == _accountProfilesRequest)
                Post(new { type = "accountProfiles", payload });
        }
        catch (Exception ex)
        {
            if (request == _accountProfilesRequest)
                Post(new { type = "error", message = $"读取多开档案失败：{ex.Message}" });
        }
    }

    /// <summary>游戏主程序是否正在运行（正在跑时改名/删除会失败，先明确拦下来）。</summary>
    private static bool GameIsRunning(string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return false;
        try
        {
            var exe = SpeedhackManager.ContainsGameExe(gameDirectory);
            if (exe is null) return false;
            var processName = Path.GetFileNameWithoutExtension(exe);
            return processName.Length > 0 && Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private void HandleAccountProfileCreate(string name)
    {
        var service = CreateAccountProfileService();
        if (service.Error is not null)
        {
            Post(new { type = "toast", message = service.Error });
            return;
        }

        var trimmed = (name ?? "").Trim();
        try
        {
            service.Create(trimmed);
            Post(new
            {
                type = "toast",
                message = $"已新建 profile「{trimmed}」。首次用它启动时会把下载缓存硬链接过来，账号是空白的。"
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"新建 profile 失败：{ex.Message}" });
        }
        _ = PushAccountProfilesAsync();
    }

    private void HandleAccountProfileRename(string name, string newName)
    {
        var service = CreateAccountProfileService();
        if (service.Error is not null)
        {
            Post(new { type = "toast", message = service.Error });
            return;
        }
        if (GameIsRunning(service.GameDirectory))
        {
            Post(new { type = "toast", message = "游戏正在运行：先退出游戏，再改名或删除 profile。" });
            return;
        }

        try
        {
            service.Rename(name, newName);
            // 配置里正指向旧名字时一起改掉，否则下次启动会找不到目录（加载器会退回默认档案）。
            var gameDirectory = service.GameDirectory!;
            if (File.Exists(_modManager.LoaderConfigPath(gameDirectory)))
            {
                var active = _modManager.ReadLoaderConfig(gameDirectory).Profile ?? "";
                if (string.Equals(active.Trim(), (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                    _modManager.SetLoaderProfile(gameDirectory, newName.Trim());
            }
            Post(new { type = "toast", message = $"已改名为「{newName.Trim()}」。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"改名失败：{ex.Message}" });
        }
        _ = PushAccountProfilesAsync();
    }

    private async void HandleAccountProfileDelete(string name)
    {
        var service = CreateAccountProfileService();
        if (service.Error is not null)
        {
            Post(new { type = "toast", message = service.Error });
            return;
        }
        if (GameIsRunning(service.GameDirectory))
        {
            Post(new { type = "toast", message = "游戏正在运行：先退出游戏，再改名或删除 profile。" });
            return;
        }

        try
        {
            var (outcome, error) = await Task.Run(() =>
            {
                var result = service.Delete(name, out var message);
                return (result, message);
            });

            if (outcome == DeleteOutcome.Failed)
            {
                Post(new { type = "toast", message = $"删除失败：{(string.IsNullOrEmpty(error) ? "文件可能被占用" : error)}" });
            }
            else
            {
                // 配置正指向这个 profile 时改回默认档案，避免下次启动被指向一个不存在的目录。
                var gameDirectory = service.GameDirectory!;
                if (File.Exists(_modManager.LoaderConfigPath(gameDirectory)))
                {
                    var active = _modManager.ReadLoaderConfig(gameDirectory).Profile ?? "";
                    if (string.Equals(active.Trim(), (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                        _modManager.SetLoaderProfile(gameDirectory, "");
                }
                Post(new { type = "toast", message = $"已删除 profile「{name}」。{RecycleBinHelper.Describe(outcome, toRecycleBin: true)}" });
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"删除失败：{ex.Message}" });
        }
        _ = PushAccountProfilesAsync();
    }

    /// <summary>把配置里的 profile 改成指定值（空 = 默认档案）。不启动游戏。</summary>
    private bool TryApplyAccountProfile(string? name, out string error)
    {
        error = "";
        string gameDirectory;
        try { gameDirectory = RequireModGameDirectory(); }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        var trimmed = (name ?? "").Trim();
        if (!File.Exists(_modManager.LoaderConfigPath(gameDirectory)))
        {
            error = "还没安装加载器：profile 切换需要加载器（在「模组」页安装一次）。";
            return false;
        }
        if (!_modManager.SetLoaderProfile(gameDirectory, trimmed))
        {
            error = "没能写入加载器配置（doorstop_config.json），请检查文件是否只读。";
            return false;
        }
        return true;
    }

    private void HandleAccountProfileActivate(string? name)
    {
        if (!TryApplyAccountProfile(name, out var error))
        {
            Post(new { type = "toast", message = error });
            return;
        }

        var trimmed = (name ?? "").Trim();
        Post(new
        {
            type = "toast",
            message = trimmed.Length == 0
                ? "已切回默认账号目录（下次启动游戏生效）。"
                : $"已切换到 profile「{trimmed}」（下次启动游戏生效）。"
        });
        _ = PushAccountProfilesAsync();
    }

    private void HandleAccountProfileOpen(string? name)
    {
        var service = CreateAccountProfileService();
        var trimmed = (name ?? "").Trim();
        var directory = trimmed.Length == 0
            ? service.SourceDirectory
            : service.ResolveDirectory(trimmed);

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            Post(new { type = "toast", message = "这个目录已经不存在了。" });
            return;
        }

        try { Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
        catch (Exception ex) { Post(new { type = "toast", message = $"打不开目录：{ex.Message}" }); }
    }

    /// <summary>切到指定 profile 并启动游戏。
    ///
    /// 启动方式：默认档案（空名）用界面上选的方式（从 Steam / 绕过 Steam / 从 TapTap / 绕过 TapTap）；
    /// **其它档案绕过 Steam 或 TapTap 直启** —— 换档案就是为了换账号，不能再让平台客户端决定
    /// 用哪个账号/会话，也避免把启动重定向回它自己注册的那份安装。
    /// 顺带把加载器的绕过开关同步成对应模式（与首页单选框是同一组配置）。</summary>
    private void HandleAccountProfileLaunch(string? name, string mode)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length > 0 && mode != "bypassTapTap")
        {
            mode = "bypassSteam";
        }

        if (!TryApplyAccountProfile(trimmed, out var error))
        {
            Post(new { type = "toast", message = error });
            return;
        }

        // 配置同步是尽力而为：同步失败也照常启动。
        try
        {
            var gameDirectory = RequireModGameDirectory();
            if (File.Exists(_modManager.LoaderConfigPath(gameDirectory)))
            {
                var steamBypass = mode == "bypassSteam";
                var tapTapBypass = mode == "bypassTapTap";
                _modManager.SetSteamBypassProfile(gameDirectory, steamBypass);
                _modManager.SetTapTapBypassProfile(gameDirectory, tapTapBypass);

                // 首页「启动方式」单选框跟着走（同一组配置，避免两处显示不一致）
                Post(new { type = "steamBypassSync", payload = new { enabled = steamBypass } });
                Post(new { type = "launchModeSync", payload = new { mode } });
            }
        }
        catch
        {
            // 忽略
        }

        // 先把“当前档案”推回界面，再拉游戏：启动后这几秒界面还能看到切换结果。
        _ = PushAccountProfilesAsync();

        // 非默认档案始终走独立实例外壳；默认档案只有在已有实例运行时才需要外壳。
        var separateInstance = trimmed.Length > 0 || SpeedhackManager.IsGameRunning();
        HandleLaunchGame(mode, trimmed, separateInstance);
    }

    private void HandleAccountProfileLaunch(string? name, bool bypassSteam)
        => HandleAccountProfileLaunch(name, bypassSteam ? "bypassSteam" : "steam");

    /// <summary>「游戏窗口标题后追加 profile 名」开关（写加载器的 profileWindowTitle）。
    /// 默认开；关掉只影响标题显示，不影响目录隔离。</summary>
    private void HandleAccountProfileSetWindowTitle(bool enabled)
    {
        string gameDirectory;
        try { gameDirectory = RequireModGameDirectory(); }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = ex.Message });
            return;
        }

        if (!File.Exists(_modManager.LoaderConfigPath(gameDirectory)))
        {
            Post(new { type = "toast", message = "还没安装加载器：这个开关需要加载器（在「模组」页安装一次）。" });
            _ = PushAccountProfilesAsync();
            return;
        }

        if (!_modManager.SetProfileWindowTitle(gameDirectory, enabled))
        {
            Post(new { type = "toast", message = "没能写入加载器配置（doorstop_config.json），请检查文件是否只读。" });
            _ = PushAccountProfilesAsync();
            return;
        }

        Post(new
        {
            type = "toast",
            message = enabled
                ? "已开启：下次启动游戏后，窗口标题会带上 profile 名。"
                : "已关闭：游戏窗口标题保持原样（下次启动生效）。"
        });
        _ = PushAccountProfilesAsync();
    }

    /// <summary>保存档案备注（空串 = 清除）。备注写在档案目录的 .profile.json 里。</summary>
    private void HandleAccountProfileSetNote(string name, string? note)
    {
        var service = CreateAccountProfileService();
        if (service.Error is not null)
        {
            Post(new { type = "toast", message = service.Error });
            return;
        }

        try
        {
            service.SetNote(name, note);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"保存备注失败：{ex.Message}" });
        }
        _ = PushAccountProfilesAsync();
    }
}
