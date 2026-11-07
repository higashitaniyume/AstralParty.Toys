using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AstralParty.Toys.Services.Game;

/// <summary>
/// 为同一份游戏安装创建“第二实例外壳”。
///
/// UnityPlayer 的单实例互斥与 exe 路径绑定：同一个 exe 路径再启动时，Unity 会把已有实例的
/// 窗口前置并退出新进程。这里把 exe/DLL 以硬链接、数据目录以 junction 链接到另一个目录，
/// 让新进程拥有不同的 exe 路径，同时仍读写同一份游戏数据与加载器目录。
///
/// 账号隔离仍由加载器的 <c>CESIUM_PROFILE</c> 环境变量负责（每个子进程不同），
/// 不依赖共享的 <c>doorstop_config.json</c> 的 profile 字段。
/// </summary>
public static class GameInstanceService
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);

    /// <summary>外壳根目录（不放在游戏目录，避免 Program Files 写权限问题）。</summary>
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstralParty.Toys", "instances");

    /// <summary>指定默认档案在实例外壳里的子目录名（仅为定位已存在的旧默认外壳进程，
    /// 默认档本身从此不再由 Toys 创建新外壳）。</summary>
    private static string DefaultShellKey => "default";

    /// <summary>给定游戏目录与 profile，返回“这个档位对应的可启动 exe 路径”：
    /// 非默认档返回外壳内 exe（尚未 EnsureInstance 时路径亦可推导，仅用于查进程）；
    /// 默认档返回游戏目录内的原版 exe（默认档不再开外壳，见 HybridWindow.AccountProfiles）。
    /// 找不到游戏 exe 时返回 null。</summary>
    public static string? GetExpectedExePath(string gameDirectory, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return null;
        var fullGame = Path.GetFullPath(gameDirectory);
        var gameExe = SpeedhackManager.ContainsGameExe(fullGame);
        if (gameExe is null) return null;
        var exeName = Path.GetFileName(gameExe);
        var trimmed = (profileName ?? "").Trim();
        if (trimmed.Length == 0) return gameExe;
        return Path.Combine(Root, ShortHash(fullGame), SanitizeProfileKey(trimmed), exeName);
    }

    /// <summary>按“档位对应的 exe 路径”找一个已在运行的游戏进程。按 MainModule 路径精确匹配，
    /// 拿不到模块路径时退回窗口标题后缀兜底（加载器给窗口加了 “ · &lt;profile&gt;” 后缀）；
    /// 未找到返回 null。进程已退出或枚举失败均视为未运行。</summary>
    public static Process? FindRunningInstance(string gameDirectory, string? profileName)
    {
        try
        {
            var expected = GetExpectedExePath(gameDirectory, profileName);
            if (expected is null) return null;
            var processName = Path.GetFileNameWithoutExtension(expected);
            if (string.IsNullOrEmpty(processName)) return null;
            Process[] candidates;
            try { candidates = Process.GetProcessesByName(processName); }
            catch { return null; }
            foreach (var p in candidates)
            {
                try
                {
                    if (p.HasExited) continue;
                    string? modulePath = null;
                    try { modulePath = p.MainModule?.FileName; } catch { }
                    if (!string.IsNullOrEmpty(modulePath) &&
                        string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(expected),
                            StringComparison.OrdinalIgnoreCase))
                        return p;
                }
                catch { }
            }
            // 兜底：按窗口标题后缀（profileWindowTitle 打开时）匹配。
            var profile = (profileName ?? "").Trim();
            foreach (var p in candidates)
            {
                try
                {
                    if (p.HasExited) continue;
                    string title;
                    try { title = p.MainWindowTitle ?? ""; } catch { continue; }
                    if (string.IsNullOrEmpty(title)) continue;
                    if (profile.Length == 0)
                    {
                        // 默认档：没有 “ · ” 后缀即视为默认窗口。
                        if (!title.Contains(" · ")) return p;
                    }
                    else if (title.EndsWith(" · " + profile, StringComparison.Ordinal))
                    {
                        return p;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>把已找到的游戏进程窗口置于最前（最小化时先恢复；必要时借 AttachThreadInput
    /// 绕过 Windows 的前台锁定）。调用失败返回 false，不抛异常。</summary>
    public static bool TryFocusProcess(Process? process)
    {
        if (process is null) return false;
        try
        {
            if (process.HasExited) return false;
            var hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero) return false;
            ShowWindow(hwnd, IsIconic(hwnd) ? 9 /*SW_RESTORE*/ : 5 /*SW_SHOW*/);
            uint fgProcessId = 0;
            var fg = GetForegroundWindow();
            uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out fgProcessId) : 0;
            uint selfThread = NativeCurrentThreadId();
            if (fgThread != 0 && fgThread != selfThread)
            {
                AttachThreadInput(fgThread, selfThread, true);
                try
                {
                    SetForegroundWindow(hwnd);
                    BringWindowToTop(hwnd);
                }
                finally { AttachThreadInput(fgThread, selfThread, false); }
            }
            else
            {
                SetForegroundWindow(hwnd);
                BringWindowToTop(hwnd);
            }
            return true;
        }
        catch { return false; }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private static uint NativeCurrentThreadId()
    {
        try { return GetCurrentThreadId(); } catch { return 0; }
    }

    /// <summary>给定档位若已有运行进程则置前并返回 true（调用方应直接返回、不再新起进程）。</summary>
    public static bool TryFocusRunningInstance(string gameDirectory, string? profileName)
    {
        var existing = FindRunningInstance(gameDirectory, profileName);
        if (existing is null) return false;
        return TryFocusProcess(existing);
    }

    /// <summary>确保实例外壳存在并返回可启动的 exe 路径。</summary>
    public static string EnsureInstance(string gameDirectory, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new InvalidOperationException("游戏目录为空。");

        var fullGame = Path.GetFullPath(gameDirectory);
        var gameExe = SpeedhackManager.ContainsGameExe(fullGame)
            ?? throw new InvalidOperationException("没有找到 AstralParty 游戏主程序。");
        var exeName = Path.GetFileName(gameExe);

        var shell = Path.Combine(Root, ShortHash(fullGame), SanitizeProfileKey(profileName));
        Directory.CreateDirectory(shell);

        // 根目录文件：硬链接（同盘瞬时、不额外占空间）。跳过 PDB 和旧备份。
        foreach (var file in Directory.EnumerateFiles(fullGame))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                continue;
            EnsureHardLink(file, Path.Combine(shell, name));
        }

        // 数据目录与加载器目录：junction，保持同一份内容。
        var dataDirName = Path.GetFileNameWithoutExtension(exeName) + "_Data";
        EnsureJunction(Path.Combine(shell, dataDirName), Path.Combine(fullGame, dataDirName));
        EnsureJunction(Path.Combine(shell, "AstralParty_ModLoader"), Path.Combine(fullGame, "AstralParty_ModLoader"));

        foreach (var optional in new[] { "AstralParty_ResourceMods", "_generated" })
        {
            var source = Path.Combine(fullGame, optional);
            if (Directory.Exists(source))
                EnsureJunction(Path.Combine(shell, optional), source);
        }

        var shellExe = Path.Combine(shell, exeName);
        if (!File.Exists(shellExe))
            throw new InvalidOperationException("实例外壳里没有生成游戏主程序。");
        return shellExe;
    }

    /// <summary>把已有文件链接到外壳；源文件更新后重建链接，正在使用的旧外壳保持不动。</summary>
    private static void EnsureHardLink(string source, string link)
    {
        if (File.Exists(link))
        {
            try
            {
                var sourceInfo = new FileInfo(source);
                var linkInfo = new FileInfo(link);
                if (sourceInfo.Length == linkInfo.Length &&
                    sourceInfo.LastWriteTimeUtc == linkInfo.LastWriteTimeUtc)
                    return;
            }
            catch
            {
                return;
            }

            try { File.Delete(link); }
            catch { return; }   // 实例正在运行，保留旧链接
        }

        if (CreateHardLinkW(link, source, IntPtr.Zero)) return;

        // 跨卷或文件系统不支持硬链接时退回复制（仍然比不能多开好）。
        try { File.Copy(source, link, overwrite: false); }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法为实例准备 {Path.GetFileName(source)}：{ex.Message}", ex);
        }
    }

    /// <summary>创建/校正目录 junction；已存在且指向同一目标时直接复用。</summary>
    private static void EnsureJunction(string link, string target)
    {
        if (!Directory.Exists(target)) return;

        if (Directory.Exists(link))
        {
            try
            {
                var existing = Directory.ResolveLinkTarget(link, returnFinalTarget: false);
                if (existing != null &&
                    string.Equals(Path.GetFullPath(existing.FullName), Path.GetFullPath(target),
                        StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch
            {
                // 不是 reparse point 或无法解析：当作需要重建
            }

            try { Directory.Delete(link, recursive: false); }
            catch { return; }   // 实例正在运行，保留旧目录
        }

        var psi = new ProcessStartInfo("cmd.exe",
            $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动目录链接命令。");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"无法创建目录链接 {Path.GetFileName(link)}：{error.Trim()}");
    }

    /// <summary>实例目录按安装路径分开，避免国服/国际服互相覆盖。</summary>
    private static string ShortHash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        return Convert.ToHexString(bytes.AsSpan(0, 4)).ToLowerInvariant();
    }

    /// <summary>profile 名只用于目录名；默认档案固定为 default。</summary>
    private static string SanitizeProfileKey(string? profileName)
    {
        var name = (profileName ?? "").Trim();
        if (name.Length == 0) return "default";

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.' ? ch : '_');
        }
        var result = builder.ToString();
        return result.Length > 32 ? result[..32] : result;
    }
}
