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
          + (HasEmbeddedBuiltInMods ? $"（内置：{string.Join("、", EmbeddedBuiltInModIdList)}）" : "")
          + "，并在控制台窗口显示日志。";

    /// <summary>把内嵌文件写入游戏目录 + 创建目录结构。version.dll 目标已存在、且**不是本工具装的**
    /// （既不是内置那份、也没有记录在安装清单里）时，必须 overwriteDll 才会覆盖。
    /// 已装加载器比内置的更新时（降级）默认拒绝，需要 allowDowngrade 显式放行。</summary>
    public void Install(string gameDirectory, bool overwriteDll, bool includeSampleMod, bool allowDowngrade = false,
        LoaderProgressCallback? progress = null)
    {
        if (!HasEmbeddedLoader)
            throw new InvalidOperationException("程序集缺少内置加载器资源（version.dll），无法安装。");
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory}");
        if (string.IsNullOrEmpty(SpeedhackManager.ContainsGameExe(gameDirectory)))
            throw new InvalidOperationException("所选目录里没有找到游戏主程序（AstralParty*.exe），确认这是游戏 exe 所在的目录？");

        // 降级闸门: 游戏目录里已装的加载器比本程序内置的更新时，默认拒绝这次"安装"。
        // 场景: 用户装过更新版的加载器（点「从 GitHub 更新」装的）却拿了较旧版本的 Toys ——
        //       点一下「安装加载器」就把新加载器静默换回旧版，功能无声退化。
        // 已装版本只认安装清单；没有清单（0.4.7 之前的老安装）时无法比较，只能放行 ——
        // 那种情况下界面上「已装版本」显示为未知，用户能看出工具并没有在装懂。
        var installedBeforeInstall = ReadCurrentInstallationVersion(gameDirectory);
        if (!allowDowngrade && EmbeddedVersion.Length > 0 && installedBeforeInstall.Length > 0
            && CompareSemVer(EmbeddedVersion, installedBeforeInstall) < 0)
            throw new InvalidOperationException(
                $"游戏目录里已装的加载器（{installedBeforeInstall}）比本程序内置的（{EmbeddedVersion}）更新 —— 这是一次降级，已阻止。" +
                "如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。");

        var targetDll = Path.Combine(gameDirectory, LoaderDllName);
        if (File.Exists(targetDll) && !overwriteDll && ClassifyLoaderOrigin(gameDirectory, targetDll).Length == 0)
            throw new InvalidOperationException(
                "游戏目录里的 version.dll 不是本工具装的（来源不明，可能是旧版独立变速器或第三方文件）——" +
                "如确定要覆盖，请勾选「允许覆盖其它 version.dll」。");

        var remainDisabled = IsRetainedDisabled(gameDirectory);
        ToysLog.Info("modloader",
            $"开始安装内置 CesiumLoader {EmbeddedVersion}（目标 {gameDirectory}，内置模组 {(includeSampleMod ? EmbeddedBuiltInMods.Count.ToString() : "0")} 个）");
        using var retained = BeginRetainedOperation(gameDirectory, "install-embedded",
            new[] { EmbeddedLoaderDll!, EmbeddedConfig ?? Array.Empty<byte>(), EmbeddedSdkDll ?? Array.Empty<byte>() }
                .Concat(includeSampleMod ? EmbeddedBuiltInMods.SelectMany(m => new[] { m.Dll, m.Sidecar ?? Array.Empty<byte>() }) : Array.Empty<byte[]>()).ToArray());
        try
        {
            progress?.Invoke("install", 15, "正在写入加载器文件…");
            if (!remainDisabled) WriteAllBytesProtected(targetDll, EmbeddedLoaderDll!);

            // 目录结构
            var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
            Directory.CreateDirectory(Path.Combine(loaderRoot, ModsFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, DisabledModsFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, SdkFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, LogsFolderName));

            // doorstop_config.json（加载器配置：enabled / 变速基础倍率 / SDK 版本等）
            // 已存在则保留用户配置(不覆盖, 避免重置变速/开关设置); 不存在才写模板。
            var configPath = Path.Combine(loaderRoot, ConfigFileName);
            var isNewConfig = !File.Exists(configPath);
            if (isNewConfig && HasEmbeddedConfig)
                WriteAllBytesProtected(configPath, EmbeddedConfig!);
            // 老配置可能停在旧 SDK 号：把 sdkVersion 抬到随包版本，否则新版内置 mod 会被加载器拒绝加载
            SyncLoaderConfigSdkVersion(configPath, EmbeddedSdkVersion);
            // 老配置里没有 Steam 绕过开关：补齐（只追加缺失的键，不动用户已有值与注释）
            SyncLoaderConfigSteamKeys(configPath);
            // 老配置里没有 profileWindowTitle：一并补齐，让配置自文档化、也防止旧版 Toys 保存时删掉它
            SyncLoaderConfigProfileWindowTitle(configPath);
            // 首次安装默认从 Steam 启动；升级/重装保留已有启动方式。
            // 模板可能默认开启绕过，须同时关闭主开关、匹配和房间列表查询。
            if (isNewConfig && !SetSteamBypassProfile(gameDirectory, enabled: false))
                throw new InvalidDataException("无法设置首次安装的 Steam 启动方式，加载器配置不完整。");

            // 内置 SDK
            if (HasEmbeddedSdk)
                WriteAllBytesProtected(Path.Combine(loaderRoot, SdkFolderName, SdkDllName), EmbeddedSdkDll!);

            // 内置 mod（可选）：每 mod 一个文件夹 mods\{ModId}\{ModId}.dll + sidecar
            // 已存在的同名文件会被覆盖为随包版本（升级工具时一并升级内置 mod）。
            // ★ 保留用户原先设置的 enabled 开关状态，避免更新加载器时把已禁用的内置 mod 重置为开启。
            if (includeSampleMod)
            {
                progress?.Invoke("install", 45, "正在安装内置模组…");
                var preservedEnabled = CaptureInstalledModsEnabled(loaderRoot);
                foreach (var mod in EmbeddedBuiltInMods)
                {
                    // 玩家已经把某个内置 mod 停用了（躺在 disabled_mods 里）就写回原处，不能因为重装而重新启用
                    var modDir = Path.Combine(ResolveModRoot(loaderRoot, mod.Id), mod.Id);
                    Directory.CreateDirectory(modDir);
                    WriteAllBytesProtected(Path.Combine(modDir, mod.Id + ".dll"), mod.Dll);
                    if (mod.Sidecar is not null)
                    {
                        var sidecarBytes = mod.Sidecar;
                        if (preservedEnabled.TryGetValue(mod.Id, out var previousEnabled))
                        {
                            var sidecarText = System.Text.Encoding.UTF8.GetString(mod.Sidecar);
                            var updatedJson = SetJsonBool(sidecarText, "enabled", previousEnabled);
                            sidecarBytes = System.Text.Encoding.UTF8.GetBytes(updatedJson);
                        }
                        WriteAllBytesProtected(Path.Combine(modDir, mod.Id + ".json"), sidecarBytes);
                    }
                    if (mod.Data.Count > 0)
                    {
                        var dataDir = Path.Combine(modDir, "data");
                        Directory.CreateDirectory(dataDir);
                        foreach (var pair in mod.Data)
                            WriteAllBytesProtected(Path.Combine(dataDir, pair.Key), pair.Value);
                    }
                }
            }

            // 安装清单：记录本次安装的版本与各文件 SHA256。
            // ★ 必须写 —— 否则「已安装版本」永远读不出（ReadInstalledVersion 读的就是这个文件），
            //   用户从旧版升级上来时界面无法显示"已装 2.1.7 / 随包 2.2.0"，也就无从判断需不需要更新。
            //   走内嵌资源安装与走 GitHub 发布包安装写的是同一份清单格式（见 tools\package-modloader.ps1）。
            progress?.Invoke("install", 85, "正在写入安装清单…");
            WriteInstalledManifest(gameDirectory, loaderRoot);
            if (remainDisabled) SetInstalledLoaderHash(gameDirectory, EmbeddedLoaderHash!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ToysLog.Error("modloader", "安装内置加载器时写入游戏目录失败", ex);
            throw new IOException(
                $"写入游戏目录失败：{ex.Message}。" +
                (SpeedhackManager.IsGameRunning() ? "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再安装。" : ""), ex);
        }

        SaveStoredGameDirectory(gameDirectory);
        retained.Complete(EmbeddedVersion, EmbeddedLoaderHash, remainDisabled, packageHash: "");
        ToysLog.Info("modloader", $"内置 CesiumLoader {EmbeddedVersion} 安装完成" + (remainDisabled ? "（保留停用状态）" : ""));
        progress?.Invoke("done", 100, "加载器安装完成");
    }

    /// <summary>写安装清单（AstralParty_ModLoader\cesium-loader.json）：版本 + 各文件 SHA256。
    ///
    /// 与发布包（tools\package-modloader.ps1 生成的同名文件）是同一套字段，用于两件事：
    ///   1) 界面显示「已安装版本」；2) 与随包版本比较，判断该不该提示用户更新。
    /// 走内嵌资源安装与走 GitHub 发布包安装写的是同一份清单，所以两条安装路径的升级判断一致。
    /// 哈希取的是**落盘后**的实际内容（用户保留了旧 doorstop_config.json 时，记录的就是用户那份），
    /// 因此清单反映真实安装状态。写入失败必须报告安装未完整完成，避免恢复时使用旧版本。</summary>
    private static void WriteInstalledManifest(string gameDirectory, string loaderRoot)
    {
        if (string.IsNullOrEmpty(EmbeddedVersion)) return;   // 资源缺失/版本未知时不写，避免留下误导性清单
        try
        {
            var files = new Dictionary<string, string>();
            void Add(string absolutePath, string relative)
            {
                try
                {
                    if (File.Exists(absolutePath))
                        files[relative] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolutePath))).ToLowerInvariant();
                }
                catch
                {
                    // 单个文件读不到就跳过它，不影响其余条目
                }
            }

            Add(Path.Combine(gameDirectory, LoaderDllName), LoaderDllName);
            Add(Path.Combine(loaderRoot, ConfigFileName), LoaderFolderName + "/" + ConfigFileName);
            foreach (var mod in EmbeddedBuiltInMods)
            {
                var prefix = $"{LoaderFolderName}/{ModsFolderName}/{mod.Id}/";
                var modFolder = Path.Combine(ResolveModRoot(loaderRoot, mod.Id), mod.Id);
                Add(Path.Combine(modFolder, mod.Id + ".dll"), prefix + mod.Id + ".dll");
                Add(Path.Combine(modFolder, mod.Id + ".json"), prefix + mod.Id + ".json");
                foreach (var fileName in mod.Data.Keys)
                    Add(Path.Combine(modFolder, "data", fileName), prefix + "data/" + fileName);
            }

            var manifest = new Dictionary<string, object?>
            {
                ["name"] = "CesiumLoader",
                ["version"] = EmbeddedVersion,
                ["tag"] = "modloader-" + EmbeddedVersion,
                ["repo"] = "higashitaniyume/CesiumLoader",
                ["layout"] = "game-dir",
                ["source"] = "toys-embedded",
                ["installed_at"] = DateTime.UtcNow.ToString("o"),
                ["sdkVersion"] = EmbeddedSdkVersion,
                ["files"] = files
            };
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            WriteAllBytesProtected(
                Path.Combine(loaderRoot, InstalledManifestFileName),
                System.Text.Encoding.UTF8.GetBytes(json));
        }
        catch (Exception ex)
        {
            throw new IOException("无法保存加载器版本清单，本次安装未完整完成。", ex);
        }
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
