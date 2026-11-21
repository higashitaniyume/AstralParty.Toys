using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Speedhack;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    // ============================== 安装 / 卸载 ==============================

    public static string DescribeRunningInstall() =>
        "游戏正在运行时也能装：version.dll 由游戏启动时加载，本次写进去要重启游戏才会生效；" +
        "而且正在运行的游戏往往占用着 version.dll，覆盖会失败并提示文件被占用——那样就先完全退出游戏再装。";

    public static string DescribeRunningUninstall() =>
        "游戏正在运行时也能卸：version.dll 已被游戏加载占用，删除通常会失败并提示文件被占用；" +
        "即使删除成功，当前这局也已经加载了加载器，要重启游戏才会恢复正常。";

    public static string DescribeInstalled() => SpeedhackManager.IsGameRunning()
        ? "加载器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载 mod。"
        : $"加载器已安装到游戏目录。启动游戏后会自动加载 mods\\ 目录下的所有 mod"
          + (HasEmbeddedBuiltInMods ? $"（内置：{string.Join("、", BuiltInModIds)}）" : "")
          + "，并在控制台窗口显示日志。";

    /// <summary>安装 / 重新安装**程序集内置的**加载器发布包：直接解压内嵌的 cesium-loader.zip，
    /// 与「导入发布包」走完全同一条代码路径（见 <see cref="InstallPackage"/>），区别只是包的来源。
    /// 包内版本比游戏目录已装的更新时（降级）默认拒绝，需要 allowDowngrade 显式放行。</summary>
    public LoaderInstallResult Install(string gameDirectory, bool overwriteDll, bool allowDowngrade = false,
        LoaderProgressCallback? progress = null)
    {
        var package = EmbeddedPackageBytes;
        if (package is not { Length: > 0 })
            throw new InvalidOperationException(
                $"程序集缺少内置的 CesiumLoader 发布包（{EmbeddedPackageFileName}），无法安装。");
        ToysLog.Info("modloader", $"安装内置 CesiumLoader 发布包（{FormatBytes(package.Length)}，内置 {BuiltInModIds.Count} 个模组）");
        return InstallPackage(gameDirectory, package, overwriteDll, allowDowngrade, progress);
    }

    /// <summary>从游戏目录删除 version.dll 与 AstralParty_ModLoader 目录。DLL 与内置不一致时默认拒绝，force 才删除。</summary>
    public UninstallResult Uninstall(string gameDirectory, bool force)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory ?? "(未选择)"}");

        var dllPath = Path.Combine(gameDirectory, LoaderDllName);
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var dllExists = File.Exists(dllPath);
        var loaderRootExists = Directory.Exists(loaderRoot);

        if (!dllExists && !loaderRootExists)
            return new UninstallResult { Message = "游戏目录中没有加载器文件，无需卸载。" };

        if (dllExists && !force && ClassifyLoaderOrigin(gameDirectory, dllPath).Length == 0)
            throw new InvalidOperationException(
                "游戏目录里的 version.dll 不是本工具装的（来源不明）——未删除任何文件。如确认要删除请勾选「强制卸载」。");

        using var retained = BeginRetainedOperation(gameDirectory, "uninstall-full");
        var result = new UninstallResult();
        if (dllExists)
        {
            try
            {
                File.Delete(dllPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"删除 version.dll 失败：{ex.Message}。" +
                    (SpeedhackManager.IsGameRunning()
                        ? "游戏正在运行时该文件已被加载占用，请完全退出游戏后再卸载（本次没有改动任何文件）。"
                        : "请检查文件权限后重试（本次没有改动任何文件）。"), ex);
            }
            result.RemovedDll = true;
            // version.dll 的同名符号文件（2.2.5 起才会随包安装）：随 dll 一起清理。
            // 删不掉就留着（只是个调试符号，不影响卸载结果）。
            var dllPdbPath = Path.ChangeExtension(dllPath, ".pdb");
            try
            {
                if (File.Exists(dllPdbPath)) File.Delete(dllPdbPath);
            }
            catch
            {
                // 忽略：符号文件残留无副作用
            }
        }
        // 发布包落在游戏 exe 目录的其它文件（NOTICE / LICENSE / README.txt / CHANGELOG.md）。
        // 只删本工具落过的那些：通用名文件必须出现在安装记录里才删，绝不删玩家自己的同名文件。
        foreach (var relative in RootFilesOwnedByLoader(ReadRetainedInstallation(gameDirectory)))
        {
            if (relative.Equals(LoaderDllName, StringComparison.OrdinalIgnoreCase)) continue;      // 上面已处理
            if (relative.Equals(Path.ChangeExtension(LoaderDllName, ".pdb"), StringComparison.OrdinalIgnoreCase)) continue;
            var path = Path.Combine(gameDirectory, relative);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 删不掉就留着（多半是被占用）；它不影响卸载结果
            }
        }
        if (loaderRootExists)
        {
            try
            {
                Directory.Delete(loaderRoot, recursive: true);
                result.RemovedConfig = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Message = $"version.dll 已删除，但 {LoaderFolderName} 目录删除失败：{ex.Message}（可能仍有文件被占用）。";
                return result;
            }
        }

        retained.Complete(disabled: false, removed: true);
        result.Message = SpeedhackManager.IsGameRunning()
            ? "加载器已卸载；当前正在运行的游戏进程仍加载着加载器，重启游戏后恢复正常。"
            : "加载器已卸载（游戏恢复正常）。";
        return result;
    }

}
