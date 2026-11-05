using System.IO;
using System.Text.Json;
using System.Windows;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

// 设置页「卸载本程序」。默认只删程序；加载器和设置要用户勾选，回放永远不删。
public partial class HybridWindow
{
    private bool _appUninstallRunning;

    private static string? AppUninstallBlockedReason() =>
        AppUninstaller.UnsupportedReason(Environment.ProcessPath, AppRuntimeDetector.Detect().IsInstaller);

    private void HandleAppUninstallInfo()
    {
        var games = _gameLibrary.GetProfiles().Select(profile =>
        {
            var loader = profile.DirectoryExists ? _modManager.InspectLoader(profile.Directory) : default;
            return new
            {
                id = profile.Id,
                label = profile.Label,
                editionLabel = profile.EditionLabel,
                directory = profile.Directory,
                hasLoader = loader.HasFiles,
                managed = loader.Managed,
                version = loader.Version
            };
        }).ToArray();
        Post(new
        {
            type = "appUninstallInfo",
            payload = new
            {
                blockedReason = AppUninstallBlockedReason(),
                exePath = Environment.ProcessPath,
                gameRunning = SpeedhackManager.IsGameRunning(),
                games,
                settingsDirectory = SpeedhackManager.ResolveProfileDirectory(),
                libraryRoot = _libraryService.LibraryRoot
            }
        });
    }

    private async Task HandleAppUninstallAsync(JsonElement root)
    {
        if (_appUninstallRunning) return;
        var blocked = AppUninstallBlockedReason();
        if (blocked is not null) { PostAppUninstallResult(false, blocked, Array.Empty<string>()); return; }

        var gameIds = root.TryGetProperty("gameIds", out var ids) && ids.ValueKind == JsonValueKind.Array
            ? ids.EnumerateArray().Select(id => id.GetString() ?? "").ToHashSet()
            : new HashSet<string>();
        var removeSettings = root.TryGetProperty("removeSettings", out var settings) && settings.ValueKind == JsonValueKind.True;

        if (gameIds.Count > 0 && SpeedhackManager.IsGameRunning())
        {
            PostAppUninstallResult(false, "游戏正在运行，加载器文件被占用。请先退出游戏再卸载（什么都没有删除）。", Array.Empty<string>());
            return;
        }

        _appUninstallRunning = true;
        var removedLoaders = new List<string>();
        try
        {
            var profiles = _gameLibrary.GetProfiles();
            foreach (var profile in profiles.Where(p => gameIds.Contains(p.Id)))
            {
                // 再查一次：只动本工具装的加载器，别的 version.dll 就算收到请求也不碰
                if (!profile.DirectoryExists || _modManager.InspectLoader(profile.Directory) is not { HasFiles: true, Managed: true }) continue;
                try
                {
                    _modManager.Uninstall(profile.Directory, force: false);
                    removedLoaders.Add(profile.Directory);
                }
                catch (Exception ex)
                {
                    PostAppUninstallResult(false, $"移除 {profile.Directory} 的加载器时失败：{ex.Message}。程序本身没有删除，可以处理后再试一次。", removedLoaders);
                    return;
                }
            }
            _ = PushModStatusAsync();

            var exePath = Environment.ProcessPath!;
            var directories = new List<string>();
            var extract = AppUninstaller.ExtractDirectory(exePath, AppContext.BaseDirectory, Path.GetTempPath());
            var files = new List<string>();
            var emptyOnly = new List<string>();
            if (extract is not null)
            {
                // 只删当前版本这一层：上一层按程序名分，可能还有同名、正在运行的另一份，只有空了才删
                directories.Add(extract);
                emptyOnly.Add(Path.GetDirectoryName(extract)!);
            }
            if (removeSettings)
            {
                var keepHistory = profiles.Any(p => p.DirectoryExists && _modManager.InspectLoader(p.Directory) is { HasFiles: true, Managed: true });
                var profileDirectory = SpeedhackManager.ResolveProfileDirectory();
                var items = AppUninstaller.SettingsItems(profileDirectory, keepHistory);
                files.AddRange(items.Files);
                directories.AddRange(items.Directories);
                emptyOnly.Add(profileDirectory);
                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                directories.Add(Path.Combine(localApp, "AstralParty.Toys"));   // WebView2 缓存、创意工坊封面
            }

            using (AppUninstaller.Launch(new AppUninstallPlan
            {
                ProcessId = Environment.ProcessId,
                ExePath = exePath,
                Directories = directories,
                Files = files,
                EmptyOnlyDirectories = emptyOnly
            })) { }

            PostAppUninstallResult(true, "", removedLoaders);
            await Task.Delay(3000);   // 留时间给用户看完提示，脚本会一直等到程序退出
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            PostAppUninstallResult(false, $"卸载没有完成：{ex.Message}。程序本身没有删除。", removedLoaders);
        }
        finally
        {
            _appUninstallRunning = false;
        }
    }

    private void PostAppUninstallResult(bool ok, string message, IReadOnlyList<string> removedLoaders) =>
        Post(new { type = "appUninstallResult", payload = new { ok, message, removedLoaders, exePath = Environment.ProcessPath } });
}
