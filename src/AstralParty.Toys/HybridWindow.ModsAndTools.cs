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
    // ============================== 变速器（游戏工具） ==============================

    private int _speedhackStatusRequest;

    private async Task PushSpeedhackStatusAsync()
    {
        var request = ++_speedhackStatusRequest;
        try
        {
            var payload = await Task.Run(() =>
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
                return new { status, config = editor, configBroken };
            });
            // 切换游戏或刷新后，较早的读取结果不能覆盖最新状态。
            if (request == _speedhackStatusRequest)
                Post(new { type = "speedhackStatus", payload });
        }
        catch (Exception ex)
        {
            if (request == _speedhackStatusRequest)
                Post(new { type = "error", message = $"读取变速器状态失败：{ex.Message}" });
        }
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
            _gameLibrary.AddDirectory(directory, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
            _ = PushGameProfilesAsync();
        }
        _ = PushSpeedhackStatusAsync();
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
            _gameLibrary.AddDirectory(dialog.FolderName, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已选择游戏目录：{dialog.FolderName}" });
            _ = PushGameProfilesAsync();
            _ = PushSpeedhackStatusAsync();
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
        Post(new { type = "toast", message = SpeedhackManager.DescribeInstalled() });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackUninstall(bool force)
    {
        var result = _speedhackManager.Uninstall(RequireGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        _ = PushSpeedhackStatusAsync();
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
            Post(new { type = "toast", message = $"变速器已安装，每次进入游戏将自动使用 {model.BaseSpeed:0.##} 倍速。" + (SpeedhackManager.IsGameRunning() ? "游戏正在运行：本次写入要重启游戏后才会加载。" : "") });
            _ = PushSpeedhackStatusAsync();
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
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackResetConfig()
    {
        _speedhackManager.ResetProfileToTemplate();
        var installedDirectory = _speedhackManager.GetStatus().Installed
            ? _speedhackManager.GetStatus().GameDirectory : null;
        if (!string.IsNullOrEmpty(installedDirectory)) _speedhackManager.PushConfigToGame(installedDirectory);

        Post(new { type = "toast", message = "已恢复默认模板（Ctrl 切换 2 倍速）。" });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackPushConfig()
    {
        var directory = RequireGameDirectory();
        if (!File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
            throw new InvalidOperationException("游戏目录尚未安装变速器，请先安装。");
        _speedhackManager.PushConfigToGame(directory);
        Post(new { type = "toast", message = "配置已同步到游戏目录，重启游戏后生效。" });
        _ = PushSpeedhackStatusAsync();
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


    // ============================== Mod 加载器（游戏工具） ==============================

    private int _modStatusRequest;

    private async Task PushModStatusAsync()
    {
        var request = ++_modStatusRequest;
        try
        {
            var status = await Task.Run(() => _modManager.GetStatus());
            if (request == _modStatusRequest)
                Post(new { type = "modStatus", payload = new { status, builtInMods = ModManager.EmbeddedBuiltInModIdList } });
        }
        catch (Exception ex)
        {
            if (request == _modStatusRequest)
                Post(new { type = "error", message = $"读取模组状态失败：{ex.Message}" });
        }
    }

    private string RequireModGameDirectory()
    {
        var directory = _modManager.GetStatus().GameDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("尚未选择有效的游戏目录，请先自动检测或手动选择。");
        return directory;
    }

    private void HandleModDetect()
    {
        var directory = SpeedhackManager.DetectGameDirectory();
        if (directory is null)
        {
            Post(new { type = "toast", message = "自动检测未找到游戏目录，请点「选择…」手动指定。" });
        }
        else
        {
            _gameLibrary.AddDirectory(directory, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
            _ = PushGameProfilesAsync();
        }
        _ = PushModStatusAsync();
    }

    private void HandleModBrowse()
    {
        var current = _modManager.ResolveGameDirectory();
        var dialog = new OpenFolderDialog
        {
            Title = "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
            InitialDirectory = current is { Length: > 0 } && Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(this) == true && dialog.FolderName is { Length: > 0 })
        {
            _gameLibrary.AddDirectory(dialog.FolderName, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已选择游戏目录：{dialog.FolderName}" });
            _ = PushGameProfilesAsync();
            _ = PushModStatusAsync();
        }
    }

    private void HandleModInstall(bool overwriteDll, bool includeSample, bool allowDowngrade)
    {
        _modManager.Install(RequireModGameDirectory(), overwriteDll, includeSample, allowDowngrade);
        Post(new { type = "toast", message = ModManager.DescribeInstalled() });
        _ = PushModStatusAsync();
        _ = PostSteamBypassSyncAsync();   // 刚装/更新完，首页的启动方式单选框跟上新配置
    }

    private void HandleModUninstall(bool force)
    {
        var result = _modManager.Uninstall(RequireModGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        _ = PushModStatusAsync();
    }

    private void HandleModOpenFolder(string folderName)
    {
        var directory = RequireModGameDirectory();
        var folder = Path.Combine(directory, ModManager.LoaderFolderName, folderName);
        Directory.CreateDirectory(folder);
        ModManager.OpenInExplorer(folder);
    }

    private void HandleModImport(string path)
    {
        var entry = _modManager.ImportMod(RequireModGameDirectory(), path);
        Post(new { type = "toast", message = $"已导入 mod：{entry.FileName}（{FormatBytes(entry.SizeBytes)}）" });
        _ = PushModStatusAsync();
    }

    private void HandleModPickPackageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 CesiumLoader 发布包",
            Filter = "CesiumLoader 发布包|*.zip|SDK DLL|CesiumLoader.SDK.dll",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
            Post(new { type = "modPackageFileSelected", payload = new { path = dialog.FileName } });
    }

    private bool _modPackageImportRunning;

    private async Task HandleModImportPackageAsync(string path, bool overwriteDll, bool allowDowngrade)
    {
        if (_modPackageImportRunning)
        {
            Post(new { type = "modPackageImportResult", payload = new { success = false, message = "已有发布包正在导入，请稍候。" } });
            return;
        }
        _modPackageImportRunning = true;
        try
        {
            var directory = RequireModGameDirectory();
            path = path.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new InvalidOperationException("请填写存在的发布包完整路径。");
            var description = await Task.Run(() => _modManager.ImportReleaseFile(directory, path, overwriteDll, allowDowngrade));
            var message = $"已导入 {description}，重启游戏后生效。";
            Post(new { type = "modPackageImportResult", payload = new { success = true, message } });
            Post(new { type = "toast", message });
            _ = PostSteamBypassSyncAsync();
        }
        catch (Exception ex)
        {
            Post(new { type = "modPackageImportResult", payload = new { success = false, message = $"导入失败：{ex.Message}" } });
        }
        finally
        {
            _modPackageImportRunning = false;
            _ = PushModStatusAsync();
        }
    }

    private void HandleModPickImport()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要导入的 mod DLL",
            Filter = "Mod DLL|*.dll|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0) return;
        var imported = 0;
        foreach (var file in dialog.FileNames)
        {
            try
            {
                _modManager.ImportMod(RequireModGameDirectory(), file);
                imported++;
            }
            catch (Exception ex)
            {
                Post(new { type = "toast", message = $"导入 {Path.GetFileName(file)} 失败：{ex.Message}" });
            }
        }
        if (imported > 0)
            Post(new { type = "toast", message = $"已导入 {imported} 个 mod，重启游戏后生效。" });
        _ = PushModStatusAsync();
    }

    private void HandleModDelete(string fileName)
    {
        var entry = _modManager.DeleteMod(RequireModGameDirectory(), fileName);
        if (entry is null)
        {
            Post(new { type = "toast", message = $"未找到 mod：{fileName}" });
        }
        else
        {
            Post(new { type = "toast", message = $"已删除 mod：{entry.FileName}" });
        }
        _ = PushModStatusAsync();
    }

    private void HandleModToggle(string fileName, bool enabled)
    {
        try
        {
            _modManager.ToggleMod(RequireModGameDirectory(), fileName, enabled);
            Post(new { type = "toast", message = enabled
                ? $"已启用 mod：{fileName}（重启游戏后生效）"
                : $"已禁用 mod：{fileName}（重启游戏后生效）" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"切换 mod 状态失败：{ex.Message}" });
        }
        _ = PushModStatusAsync();
    }

    private void HandleModOpenConfig(string fileName)
    {
        try
        {
            var path = _modManager.OpenModConfig(RequireModGameDirectory(), fileName);
            Post(new { type = "toast", message = $"已用默认编辑器打开配置：{path}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开配置失败：{ex.Message}" });
        }
    }

    private void HandleModOpenConfigRaw(string fileName)
    {
        try
        {
            var path = _modManager.OpenModConfig(RequireModGameDirectory(), fileName);
            Post(new { type = "toast", message = $"已用默认编辑器打开：{path}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开配置失败：{ex.Message}" });
        }
    }

    // ============================== Mod 加载器：联网更新 ==============================

    /// <summary>只把「最新版本号」回推给界面 —— 版本三栏的「GitHub 最新」栏与结论都由前端自己算，
    /// 这里不再拼一句 toast 文案（同一个结论说两遍，用户反而要自己对齐两个说法）。</summary>
    private async Task HandleModCheckUpdateAsync()
    {
        try
        {
            var bytes = await _modManager.DownloadLatestPackageAsync().ConfigureAwait(true);
            var manifest = ModManager.ParsePackageManifest(bytes);
            Post(new { type = "modUpdateCheck", payload = new { latest = manifest?.Version ?? "" } });
            _ = PushModStatusAsync();
        }
        catch (Exception ex)
        {
            Post(new { type = "modUpdateCheck", payload = new { error = ex.Message } });
        }
    }

    private async Task HandleModDownloadUpdateAsync(bool overwriteDll, bool allowDowngrade)
    {
        Post(new { type = "toast", message = "正在下载最新 CesiumLoader 并安装…" });
        try
        {
            var directory = RequireModGameDirectory();
            var bytes = await _modManager.DownloadLatestPackageAsync().ConfigureAwait(true);
            var manifest = ModManager.ParsePackageManifest(bytes);
            _modManager.InstallPackage(directory, bytes, overwriteDll, allowDowngrade);

            var versionText = string.IsNullOrEmpty(manifest?.Version) ? "" : $"（{manifest.Version}）";
            Post(new { type = "toast", message = $"加载器已更新{versionText}。" + (SpeedhackManager.IsGameRunning() ? "游戏正在运行，重启游戏后生效。" : "") });
            _ = PushModStatusAsync();
            _ = PostSteamBypassSyncAsync();   // 刚更新完，首页的启动方式单选框跟上新配置
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"更新失败：{ex.Message}" });
        }
    }


    private void PushSkinProfiles()
    {
        try
        {
            var directory = _modManager.GetStatus().GameDirectory;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                Post(new { type = "skinsProfiles", payload = new { installed = false, activeSkin = "default", profiles = Array.Empty<SkinProfile>() } });
                return;
            }

            var profiles = _skinManager.GetProfiles(directory);
            var activeSkin = _skinManager.GetActiveSkin(directory);
            Post(new { type = "skinsProfiles", payload = new { installed = true, activeSkin, profiles } });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"读取皮肤方案失败: {ex.Message}" });
        }
    }

    private void HandleSkinImportZip()
    {
        try
        {
            var directory = RequireModGameDirectory();
            var dialog = new OpenFileDialog
            {
                Title = "选择皮肤压缩包 (支持 cesium-default-skins 或自定义皮肤 ZIP)",
                Filter = "皮肤压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var importedName = _skinManager.ImportSkinZip(directory, dialog.FileName);
                Post(new { type = "toast", message = $"成功导入皮肤方案: {importedName}" });
                PushSkinProfiles();
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"导入皮肤失败: {ex.Message}" });
        }
    }

    private void HandleSkinDuplicate(JsonElement root)
    {
        try
        {
            var directory = RequireModGameDirectory();
            var source = root.TryGetProperty("source", out var srcElem) ? srcElem.GetString() : "default";
            var newName = root.TryGetProperty("newName", out var nameElem) ? nameElem.GetString() : null;
            var displayName = root.TryGetProperty("displayName", out var dispElem) ? dispElem.GetString() : newName;
            var author = root.TryGetProperty("author", out var authElem) ? authElem.GetString() : "";
            var desc = root.TryGetProperty("description", out var descElem) ? descElem.GetString() : "";

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(newName))
            {
                Post(new { type = "toast", message = "方案名称不能为空" });
                return;
            }

            var created = _skinManager.DuplicateProfile(directory, source, newName, displayName ?? newName, author ?? "", desc ?? "");
            Post(new { type = "toast", message = $"已创建新皮肤方案: {created}" });
            PushSkinProfiles();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"新建方案失败: {ex.Message}" });
        }
    }

    private (string Token, string Directory, string Name, string Path)? _pendingSkinExport;
    private void HandleExportSkin(JsonElement root, bool confirmed)
    {
        try
        {
            string directory, name, path;
            if (confirmed)
            {
                if (_pendingSkinExport is not { } pending || root.GetProperty("token").GetString() != pending.Token) throw new InvalidOperationException("导出确认已过期，请重新导出。");
                _pendingSkinExport = null;
                directory = pending.Directory; name = pending.Name; path = pending.Path;
                if (!directory.Equals(RequireModGameDirectory(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前游戏已切换，请重新导出。");
            }
            else
            {
                _pendingSkinExport = null;
                name = root.GetProperty("name").GetString() ?? "";
                if (name == SkinManager.GameDefault) throw new InvalidOperationException("游戏默认方案没有外部图片可导出。");
                directory = RequireModGameDirectory();
                var save = new SaveFileDialog { Title = "导出皮肤方案 ZIP", Filter = "ZIP 压缩包 (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true, FileName = name + ".zip", OverwritePrompt = false };
                if (save.ShowDialog(this) != true) return;
                path = save.FileName;
                if (File.Exists(path)) {
                    var token = Guid.NewGuid().ToString("N");
                    _pendingSkinExport = (token, directory, name, path);
                    Post(new { type = "skinsExportConfirm", payload = new { token, fileName = Path.GetFileName(path) } });
                    return;
                }
            }
            _skinManager.ExportSkinZip(directory, name, path);
            Post(new { type = "toast", message = "皮肤方案已导出为 ZIP，可通过导入 ZIP 使用。" });
        }
        catch (Exception ex) { Post(new { type = "toast", message = "导出失败：" + ex.Message }); }
    }
    private bool _skinDownloadRunning;
    private async Task DownloadDefaultSkinsAsync(bool overwrite)
    {
        if (_skinDownloadRunning) return;
        string? temporary = null;
        try
        {
            var directory = RequireModGameDirectory();
            var defaultDirectory = Path.Combine(_skinManager.GetSkinsDirectory(directory), "default");
            if (Directory.Exists(defaultDirectory) && Directory.EnumerateFiles(defaultDirectory).Any()
                && !overwrite) { Post(new { type = "skinsDownloadConfirm" }); return; }
            _skinDownloadRunning = true;
            Post(new { type = "skinsDownloadState", payload = new { running = true } });
            temporary = Path.Combine(Path.GetTempPath(), "cesium-default-skins-" + Guid.NewGuid().ToString("N") + ".zip");
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await client.GetAsync("https://github.com/higashitaniyume/CesiumLoader/releases/latest/download/cesium-default-skins.zip", System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException("最新 Release 尚未提供默认皮肤包，请先发布包含 cesium-default-skins.zip 的版本。");
            response.EnsureSuccessStatusCode();
            using (var input = await response.Content.ReadAsStreamAsync())
            using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer)) > 0)
                {
                    total += count;
                    if (total > 256L * 1024 * 1024) throw new InvalidOperationException("默认皮肤包超过 256 MB");
                    await output.WriteAsync(buffer.AsMemory(0, count));
                }
            }
            // 下载完整后再导入，只允许 default 下的图片和元数据。
            using (var archive = System.IO.Compression.ZipFile.OpenRead(temporary))
            {
                var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
                if (!entries.Any(e => Path.GetExtension(e.Name).Equals(".png", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("默认包中没有卡面图片");
                foreach (var entry in entries)
                {
                    var relative = entry.FullName.Replace('\\', '/');
                    if (!relative.StartsWith("AstralParty_ModLoader/skins/default/", StringComparison.Ordinal)
                        || entry.Length > 32L * 1024 * 1024) throw new InvalidOperationException("默认包内容不符合预期");
                }
                if (entries.Sum(e => e.Length) > 512L * 1024 * 1024) throw new InvalidOperationException("默认包解压体积过大");
            }
            _skinManager.ImportSkinZip(directory, temporary);
            PushSkinProfiles();
            Post(new { type = "toast", message = "默认皮肤包已下载并安装，可选择 default 方案使用。" });
        }
        catch (Exception ex) { Post(new { type = "toast", message = "下载默认包失败：" + ex.Message }); }
        finally
        {
            _skinDownloadRunning = false;
            Post(new { type = "skinsDownloadState", payload = new { running = false } });
            if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
        }
    }
    private (string Token, string Directory, string Skin, string File, System.Windows.Media.Imaging.BitmapSource Image)? _pendingSkinCrop;
    private void HandleSaveSkinCrop(JsonElement root)
    {
        var token = root.TryGetProperty("token", out var value) ? value.GetString() : null;
        try
        {
            if (_pendingSkinCrop is not { } pending || pending.Token != token) throw new InvalidOperationException("裁剪已取消或过期，请重新选择图片。");
            if (!string.Equals(pending.Directory, RequireModGameDirectory(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前游戏已切换，请重新选择图片。");
            var x = root.GetProperty("x").GetInt32(); var y = root.GetProperty("y").GetInt32(); var size = root.GetProperty("size").GetInt32();
            var image = SkinCropImage.RenderSquare(pending.Image, new System.Windows.Int32Rect(x, y, size, size));
            var updated = _skinManager.SaveCroppedCard(pending.Directory, pending.Skin, pending.File, image);
            _pendingSkinCrop = null;
            Post(new { type = "skinsCropSaved", payload = new { token, success = true } });
            Post(new { type = "skinsCardUpdated", payload = new { skinName = pending.Skin, card = updated } });
            PushSkinProfiles();
            Post(new { type = "toast", message = "裁剪卡面已保存，重启游戏后生效。" });
        }
        catch (Exception ex) { Post(new { type = "skinsCropSaved", payload = new { token, success = false, error = ex.Message } }); }
    }
    private void HandlePickAndReplaceCard(JsonElement root)
    {
        try
        {
            var skinName = root.TryGetProperty("skinName", out var sElem) ? sElem.GetString() : null;
            var fileName = root.TryGetProperty("fileName", out var fElem) ? fElem.GetString() : null;
            if (string.IsNullOrWhiteSpace(skinName) || string.IsNullOrWhiteSpace(fileName))
            {
                Post(new { type = "toast", message = "参数缺失" });
                return;
            }

            var directory = RequireModGameDirectory();
            var dialog = new OpenFileDialog
            {
                Title = $"选择替换卡面图片 ({fileName})",
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var image = SkinCropImage.Load(dialog.FileName);
                var token = Guid.NewGuid().ToString("N");
                _pendingSkinCrop = (token, directory, skinName, fileName, image);
                Post(new { type = "skinsCropSource", payload = new { token, fileName, width = image.PixelWidth, height = image.PixelHeight, previewUrl = SkinCropImage.Preview(image) } });
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"替换卡面失败：{ex.Message}" });
        }
    }

    private void HandleRevertCard(JsonElement root)
    {
        try
        {
            var skinName = root.TryGetProperty("skinName", out var sElem) ? sElem.GetString() : null;
            var fileName = root.TryGetProperty("fileName", out var fElem) ? fElem.GetString() : null;
            if (string.IsNullOrWhiteSpace(skinName) || string.IsNullOrWhiteSpace(fileName))
            {
                Post(new { type = "toast", message = "参数缺失" });
                return;
            }

            var directory = RequireModGameDirectory();
            var updated = _skinManager.RevertCardImage(directory, skinName, fileName);
            Post(new { type = "toast", message = $"已还原卡面为默认：{updated.CardName} ({fileName})" });
            Post(new { type = "skinsCardUpdated", payload = new { skinName, card = updated } });
            PushSkinProfiles();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"还原卡面失败：{ex.Message}" });
        }
    }

}
