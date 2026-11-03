using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AstralParty.Toys.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private readonly AudioSkinSchemeManager _audioSkins = new();

    private void HandleAudioSkinMessage(string type, JsonElement root)
    {
        var game = ResolveAudioGameDirectory();

        switch (type)
        {
            case "audioSkinsGetProfiles":
                PushAudioSkinProfiles(game);
                break;

            case "audioSkinsSetActive":
                if (root.TryGetProperty("id", out var idElem))
                {
                    var id = idElem.GetString() ?? "default";
                    _audioSkins.SetActiveScheme(game, id);
                    PushAudioSkinProfiles(game);
                    PushAudioSkinItems(game, id);
                    Post(new { type = "toast", message = $"已切换音效方案为: {id}" });
                }
                break;

            case "audioSkinsCreate":
                if (root.TryGetProperty("id", out var newIdElem))
                {
                    var id = newIdElem.GetString() ?? "";
                    var name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? id : id;
                    var author = root.TryGetProperty("author", out var authorElem) ? authorElem.GetString() ?? "" : "";
                    var desc = root.TryGetProperty("description", out var descElem) ? descElem.GetString() ?? "" : "";

                    _audioSkins.CreateScheme(game, id, name, author, desc);
                    _audioSkins.SetActiveScheme(game, id);
                    PushAudioSkinProfiles(game);
                    PushAudioSkinItems(game, id);
                    Post(new { type = "toast", message = $"成功创建音效方案: {name}" });
                }
                break;

            case "audioSkinsDuplicate":
                if (root.TryGetProperty("sourceId", out var srcElem) && root.TryGetProperty("newId", out var targetElem))
                {
                    var src = srcElem.GetString() ?? "";
                    var target = targetElem.GetString() ?? "";
                    var name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? target : target;
                    _audioSkins.DuplicateScheme(game, src, target, name);
                    _audioSkins.SetActiveScheme(game, target);
                    PushAudioSkinProfiles(game);
                    PushAudioSkinItems(game, target);
                    Post(new { type = "toast", message = $"已复制方案为: {name}" });
                }
                break;

            case "audioSkinsDelete":
                if (root.TryGetProperty("id", out var delElem))
                {
                    var id = delElem.GetString() ?? "";
                    _audioSkins.DeleteScheme(game, id);
                    PushAudioSkinProfiles(game);
                    var active = _audioSkins.GetActiveSchemeId(game);
                    PushAudioSkinItems(game, active);
                    Post(new { type = "toast", message = $"已删除方案: {id}" });
                }
                break;

            case "audioSkinsUpdateMeta":
                if (root.TryGetProperty("id", out var metaIdElem))
                {
                    var id = metaIdElem.GetString() ?? "";
                    var name = root.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? id : id;
                    var author = root.TryGetProperty("author", out var aEl) ? aEl.GetString() ?? "" : "";
                    var desc = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
                    _audioSkins.UpdateMetadata(game, id, name, author, desc);
                    PushAudioSkinProfiles(game);
                    Post(new { type = "toast", message = "方案信息已更新" });
                }
                break;

            case "audioSkinsOpenFolder":
                {
                    var active = _audioSkins.GetActiveSchemeId(game);
                    var dir = AudioSkinSchemeManager.SchemeDirectory(game, active);
                    Directory.CreateDirectory(dir);
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
                break;

            case "audioSkinsGetItems":
                {
                    var schemeId = root.TryGetProperty("schemeId", out var sElem) ? sElem.GetString() ?? "default" : _audioSkins.GetActiveSchemeId(game);
                    PushAudioSkinItems(game, schemeId);
                }
                break;

            case "audioSkinsPickAndReplace":
                if (root.TryGetProperty("schemeId", out var scElem) && root.TryGetProperty("configId", out var cfgElem))
                {
                    var schemeId = scElem.GetString() ?? "default";
                    var configId = cfgElem.GetInt32();
                    var eventName = root.TryGetProperty("eventName", out var enElem) ? enElem.GetString() ?? "" : "";
                    var volume = root.TryGetProperty("volume", out var vElem) ? (float)vElem.GetDouble() : 1.0f;

                    var dialog = new OpenFileDialog
                    {
                        Title = $"选择替换音频 ({eventName})",
                        Filter = "音频文件 (*.wav;*.ogg;*.mp3)|*.wav;*.ogg;*.mp3|WAV 音频 (*.wav)|*.wav|所有文件 (*.*)|*.*"
                    };

                    if (dialog.ShowDialog(this) == true)
                    {
                        var bytes = File.ReadAllBytes(dialog.FileName);
                        var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                        var fileName = $"{configId}_{Path.GetFileName(dialog.FileName)}";
                        _audioSkins.SaveCustomAudio(game, schemeId, configId, eventName, bytes, fileName, volume);
                        PushAudioSkinItems(game, schemeId);
                        PushAudioSkinProfiles(game);
                        Post(new { type = "toast", message = $"已替换音频: {eventName}" });
                    }
                }
                break;

            case "audioSkinsRevertAudio":
                if (root.TryGetProperty("schemeId", out var rscElem) && root.TryGetProperty("configId", out var rcfgElem))
                {
                    var schemeId = rscElem.GetString() ?? "default";
                    var configId = rcfgElem.GetInt32();
                    var eventName = root.TryGetProperty("eventName", out var renElem) ? renElem.GetString() ?? "" : "";
                    _audioSkins.RevertAudio(game, schemeId, configId, eventName);
                    PushAudioSkinItems(game, schemeId);
                    PushAudioSkinProfiles(game);
                    Post(new { type = "toast", message = $"已恢复原版音效: {eventName}" });
                }
                break;

            case "audioSkinsSetMute":
                if (root.TryGetProperty("schemeId", out var mscElem) && root.TryGetProperty("configId", out var mcfgElem))
                {
                    var schemeId = mscElem.GetString() ?? "default";
                    var configId = mcfgElem.GetInt32();
                    var eventName = root.TryGetProperty("eventName", out var menElem) ? menElem.GetString() ?? "" : "";
                    var mute = root.TryGetProperty("mute", out var muteElem) && muteElem.GetBoolean();
                    _audioSkins.SetMute(game, schemeId, configId, eventName, mute);
                    PushAudioSkinItems(game, schemeId);
                    PushAudioSkinProfiles(game);
                    Post(new { type = "toast", message = mute ? $"已静音: {eventName}" : $"已取消静音: {eventName}" });
                }
                break;

            case "audioSkinsSetVolume":
                if (root.TryGetProperty("schemeId", out var vscElem) && root.TryGetProperty("configId", out var vcfgElem))
                {
                    var schemeId = vscElem.GetString() ?? "default";
                    var configId = vcfgElem.GetInt32();
                    var eventName = root.TryGetProperty("eventName", out var venElem) ? venElem.GetString() ?? "" : "";
                    var volume = root.TryGetProperty("volume", out var volElem) ? (float)volElem.GetDouble() : 1.0f;
                    _audioSkins.SetVolume(game, schemeId, configId, eventName, volume);
                }
                break;

            case "audioSkinsExportZip":
                if (root.TryGetProperty("id", out var expElem))
                {
                    var id = expElem.GetString() ?? "default";
                    var dialog = new SaveFileDialog
                    {
                        Title = "导出音效方案",
                        Filter = "音效方案 ZIP (*.zip)|*.zip",
                        FileName = $"AudioSkin_{id}.zip"
                    };
                    if (dialog.ShowDialog(this) == true)
                    {
                        _audioSkins.ExportZip(game, id, dialog.FileName);
                        Post(new { type = "toast", message = "音效方案已成功导出" });
                    }
                }
                break;

            case "audioSkinsImportZip":
                {
                    var dialog = new OpenFileDialog
                    {
                        Title = "导入音效方案 ZIP",
                        Filter = "音效方案 ZIP (*.zip)|*.zip|所有文件 (*.*)|*.*"
                    };
                    if (dialog.ShowDialog(this) == true)
                    {
                        _audioSkins.ImportZip(game, dialog.FileName);
                        PushAudioSkinProfiles(game);
                        var active = _audioSkins.GetActiveSchemeId(game);
                        PushAudioSkinItems(game, active);
                        Post(new { type = "toast", message = "音效方案导入成功" });
                    }
                }
                break;

            case "audioSkinsExtractGameAudio":
                _ = Task.Run(() =>
                {
                    try
                    {
                        Post(new { type = "audioSkinExtractProgress", payload = new { message = "正在准备提取游戏音频…", progress = 0.05 } });
                        int extracted = _audioSkins.ExtractGameAudio(game, (msg, prog) =>
                        {
                            Post(new { type = "audioSkinExtractProgress", payload = new { message = msg, progress = prog } });
                        });
                        var active = _audioSkins.GetActiveSchemeId(game);
                        PushAudioSkinItems(game, active);
                        Post(new { type = "toast", message = $"已同步并就绪 {extracted} 个原版音频！" });
                        Post(new { type = "audioSkinExtractDone" });
                    }
                    catch (Exception ex)
                    {
                        Post(new { type = "error", message = "提取游戏音频失败: " + ex.Message });
                        Post(new { type = "audioSkinExtractDone" });
                    }
                });
                break;

            case "audioSkinsImportOriginalZip":
                {
                    var dialog = new OpenFileDialog
                    {
                        Title = "导入原版音频压缩包",
                        Filter = "ZIP 压缩文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
                    };
                    if (dialog.ShowDialog(this) == true)
                    {
                        var zipPath = dialog.FileName;
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                Post(new { type = "audioSkinExtractProgress", payload = new { message = "正在准备导入原版音频…", progress = 0.05 } });
                                int count = _audioSkins.ImportOriginalAudioZip(game, zipPath, (msg, prog) =>
                                {
                                    Post(new { type = "audioSkinExtractProgress", payload = new { message = msg, progress = prog } });
                                });
                                var active = _audioSkins.GetActiveSchemeId(game);
                                PushAudioSkinItems(game, active);
                                Post(new { type = "toast", message = $"已成功导入 {count} 首原版音频至试听缓存！" });
                                Post(new { type = "audioSkinExtractDone" });
                            }
                            catch (Exception ex)
                            {
                                Post(new { type = "error", message = "导入原版音频失败: " + ex.Message });
                                Post(new { type = "audioSkinExtractDone" });
                            }
                        });
                    }
                }
                break;

            case "audioSkinsExportOriginalZip":
                {
                    var dialog = new SaveFileDialog
                    {
                        Title = "导出全部原版音频为 ZIP",
                        Filter = "ZIP 压缩文件 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                        FileName = "AstralParty_Original_Audio.zip"
                    };
                    if (dialog.ShowDialog(this) == true)
                    {
                        var zipPath = dialog.FileName;
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                Post(new { type = "audioSkinExtractProgress", payload = new { message = "正在准备打包原版音频…", progress = 0.05 } });
                                int count = _audioSkins.ExportOriginalAudioZip(game, zipPath, (msg, prog) =>
                                {
                                    Post(new { type = "audioSkinExtractProgress", payload = new { message = msg, progress = prog } });
                                });
                                Post(new { type = "toast", message = $"已成功导出 {count} 首原版音频至 ZIP！" });
                                Post(new { type = "audioSkinExtractDone" });
                            }
                            catch (Exception ex)
                            {
                                Post(new { type = "error", message = "导出原版音频失败: " + ex.Message });
                                Post(new { type = "audioSkinExtractDone" });
                            }
                        });
                    }
                }
                break;

            case "audioSkinsExportSingleOriginal":
                {
                    var id = root.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                    var name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                    var cacheDir = AudioSkinSchemeManager.AudioCacheDirectory(game);
                    var targetFile = Path.Combine(cacheDir, $"{id}.ogg");
                    if (!File.Exists(targetFile)) targetFile = Path.Combine(cacheDir, $"{id}.wav");
                    if (!File.Exists(targetFile))
                    {
                        var catItem = _audioSkins.LoadCatalog().FirstOrDefault(x => x.Id == id);
                        var exportRoot = AudioSkinSchemeManager.FindExportRoot(game);
                        if (catItem != null && !string.IsNullOrEmpty(catItem.RelOgg) && exportRoot != null)
                        {
                            var src = Path.Combine(exportRoot, catItem.RelOgg);
                            if (File.Exists(src)) targetFile = src;
                        }
                    }

                    if (File.Exists(targetFile))
                    {
                        var ext = Path.GetExtension(targetFile);
                        if (string.IsNullOrEmpty(ext)) ext = ".ogg";
                        var filter = ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                            ? "WAV 音频 (*.wav)|*.wav|所有文件 (*.*)|*.*"
                            : "OGG 音频 (*.ogg)|*.ogg|所有文件 (*.*)|*.*";

                        var cleanName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
                        var fileName = string.IsNullOrEmpty(cleanName) ? $"{id}{ext}" : $"{id}_{cleanName}{ext}";

                        var dialog = new SaveFileDialog
                        {
                            Title = "保存原版音频",
                            Filter = filter,
                            FileName = fileName
                        };
                        if (dialog.ShowDialog(this) == true)
                        {
                            File.Copy(targetFile, dialog.FileName, true);
                            Post(new { type = "toast", message = $"已保存音频: {Path.GetFileName(dialog.FileName)}" });
                        }
                    }
                    else
                    {
                        Post(new { type = "error", message = "未找到该音频的原版文件" });
                    }
                }
                break;
        }
    }

    private void PushAudioSkinProfiles(string gameDirectory)
    {
        try
        {
            var profiles = _audioSkins.GetProfiles(gameDirectory);
            var activeId = _audioSkins.GetActiveSchemeId(gameDirectory);
            Post(new
            {
                type = "audioSkinProfiles",
                payload = new
                {
                    profiles,
                    activeScheme = activeId
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "获取音效方案失败: " + ex.Message });
        }
    }

    private void PushAudioSkinItems(string gameDirectory, string schemeId)
    {
        try
        {
            var items = _audioSkins.GetCatalogItems(gameDirectory, schemeId);
            Post(new
            {
                type = "audioSkinItems",
                payload = new
                {
                    schemeId,
                    items
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "获取音效列表失败: " + ex.Message });
        }
    }

    private string ResolveAudioGameDirectory()
    {
        try
        {
            var dir = _modManager.GetStatus().GameDirectory;
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) return dir;
        }
        catch { }

        try
        {
            var detected = SpeedhackManager.DetectGameDirectory();
            if (!string.IsNullOrWhiteSpace(detected) && Directory.Exists(detected)) return detected;
        }
        catch { }

        return AppContext.BaseDirectory;
    }

    private CoreWebView2WebResourceResponse CreateAudioResponse(Uri uri)
    {
        try
        {
            var gameDirectory = ResolveAudioGameDirectory();
            var path = uri.AbsolutePath.TrimStart('/');
            var parts = path.Split(['/'], 3);

            // 1. /scheme/{schemeId}/{fileName} or /preview/{schemeId}/{fileName}
            if (parts.Length == 3 && (string.Equals(parts[0], "scheme", StringComparison.OrdinalIgnoreCase) || string.Equals(parts[0], "preview", StringComparison.OrdinalIgnoreCase)))
            {
                var schemeId = Uri.UnescapeDataString(parts[1]);
                var fileName = Uri.UnescapeDataString(parts[2]);
                var dir = AudioSkinSchemeManager.SchemeDirectory(gameDirectory, schemeId);
                var fullPath = Path.GetFullPath(Path.Combine(dir, fileName));
                if (File.Exists(fullPath) && fullPath.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                    var mime = ext switch
                    {
                        ".ogg" => "audio/ogg",
                        ".mp3" => "audio/mpeg",
                        _ => "audio/wav"
                    };
                    var fi = new FileInfo(fullPath);
                    return WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                        new WebResourceStream(File.OpenRead(fullPath)), 200, "OK",
                        $"Content-Type: {mime}\r\nContent-Length: {fi.Length}\r\nAccept-Ranges: bytes\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
                }
            }
            // 2. /original/{id}
            else if (parts.Length >= 2 && string.Equals(parts[0], "original", StringComparison.OrdinalIgnoreCase))
            {
                var id = Uri.UnescapeDataString(parts[1]);
                var cacheDir = AudioSkinSchemeManager.AudioCacheDirectory(gameDirectory);
                var oggPath = Path.Combine(cacheDir, $"{id}.ogg");
                var wavPath = Path.Combine(cacheDir, $"{id}.wav");
                var targetFile = File.Exists(oggPath) ? oggPath : (File.Exists(wavPath) ? wavPath : null);

                if (targetFile == null && int.TryParse(id, out var numId))
                {
                    var catItem = _audioSkins.LoadCatalog().FirstOrDefault(x => x.Id == numId);
                    if (catItem != null && !string.IsNullOrEmpty(catItem.RelOgg))
                    {
                        var exportRoot = AudioSkinSchemeManager.FindExportRoot(gameDirectory);
                        if (exportRoot != null)
                        {
                            var candidate = Path.Combine(exportRoot, catItem.RelOgg);
                            if (File.Exists(candidate)) targetFile = candidate;
                        }
                    }
                }

                if (targetFile != null)
                {
                    var ext = Path.GetExtension(targetFile).ToLowerInvariant();
                    var mime = ext == ".ogg" ? "audio/ogg" : "audio/wav";
                    var fi = new FileInfo(targetFile);
                    return WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                        new WebResourceStream(File.OpenRead(targetFile)), 200, "OK",
                        $"Content-Type: {mime}\r\nContent-Length: {fi.Length}\r\nAccept-Ranges: bytes\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
                }
            }

            return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "Content-Type: text/plain");
        }
        catch
        {
            return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 500, "Internal Error", "Content-Type: text/plain");
        }
    }
}
