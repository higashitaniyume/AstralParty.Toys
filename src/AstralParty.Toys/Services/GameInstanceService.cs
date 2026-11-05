using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AstralParty.Toys.Services;

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
