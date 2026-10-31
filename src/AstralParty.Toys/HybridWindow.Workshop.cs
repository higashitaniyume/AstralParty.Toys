using System.IO;
using System.Text.Json;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

/// <summary>
/// 创意工坊：把 Forge 公共目录搬进玩具箱，并把作者的发布包按「下载 → 校验 →
/// 预检 → 导入」的同一套模组安装链路落盘。
/// </summary>
public partial class HybridWindow
{
    private readonly WorkshopService _workshopService = new();

    /// <summary>下载中的取消源；同一时刻只允许一个工坊安装。</summary>
    private CancellationTokenSource? _workshopDownloadCts;

    private bool _workshopBusy;

    /// <summary>等待用户回答「包内版本比已装的旧，仍要安装吗」的待装包。</summary>
    private PendingWorkshopInstall? _pendingWorkshopInstall;

    private sealed record PendingWorkshopInstall(
        string Id, string Version, string TempPath, string GameDirectory);

    private string WorkshopBaseUrl => WorkshopService.NormalizeBaseUrl(_libraryService.Settings.WorkshopUrl);

    private bool WorkshopConsentGranted => WorkshopConsent.IsGranted(_libraryService.Settings);

    /// <summary>没有同意时一律拒绝：任何工坊请求（含封面缓存）都不会发出去。</summary>
    private bool RequireWorkshopConsent()
    {
        if (WorkshopConsentGranted) return true;

        Post(new
        {
            type = "workshopConsentRequired",
            payload = new { version = WorkshopConsent.CurrentVersion }
        });
        return false;
    }

    /// <summary>「同意」持久化；「本次不用」什么都不写，下次进入会再问。</summary>
    private void HandleWorkshopConsent(bool accept)
    {
        if (!accept) return;

        var settings = _libraryService.Settings;
        settings.WorkshopConsentVersion = WorkshopConsent.CurrentVersion;
        settings.WorkshopConsentAt = DateTimeOffset.UtcNow;
        _libraryService.SaveSettings(settings);

        Post(new
        {
            type = "workshopConsent",
            payload = new
            {
                granted = true,
                version = settings.WorkshopConsentVersion,
                at = settings.WorkshopConsentAt
            }
        });
    }

    // ============================== 目录与详情 ==============================

    private async Task HandleWorkshopListAsync(JsonElement root)
    {
        if (!RequireWorkshopConsent()) return;

        var query = root.TryGetProperty("query", out var queryElement) ? queryElement.GetString() : null;
        var tag = root.TryGetProperty("tag", out var tagElement) ? tagElement.GetString() : null;
        var sort = root.TryGetProperty("sort", out var sortElement) ? sortElement.GetString() : null;
        var page = root.TryGetProperty("page", out var pageElement) && pageElement.TryGetInt32(out var parsedPage)
            ? parsedPage
            : 1;

        var baseUrl = WorkshopBaseUrl;
        try
        {
            var list = await _workshopService.GetListAsync(
                baseUrl,
                _libraryService.Settings.Language,
                query,
                tag,
                string.IsNullOrWhiteSpace(sort) ? "updated" : sort!,
                page,
                24);

            var installed = await Task.Run(() => BuildInstalledModMap());
            var builtIn = new HashSet<string>(ModManager.BuiltInModIds, StringComparer.OrdinalIgnoreCase);

            var covers = await Task.WhenAll(list.Items.Select(item =>
                _workshopService.CacheCoverAsync(baseUrl, item.Id, item.CoverUrl)));

            var items = list.Items.Select((item, index) => new
            {
                id = item.Id,
                name = item.Name,
                summary = item.Summary,
                author = item.Author,
                tags = item.Tags,
                coverUrl = covers[index] ?? "",
                latestVersion = item.LatestVersion,
                sdkVersion = item.SdkVersion,
                sizeBytes = item.SizeBytes,
                sha256 = item.Sha256,
                updatedAt = item.UpdatedAt,
                downloads = item.Downloads,
                downloads7Days = item.Downloads7Days,
                downloads30Days = item.Downloads30Days,
                featured = item.Featured,
                builtIn = builtIn.Contains(item.Id),
                installedVersion = installed.TryGetValue(item.Id, out var entry) ? entry.Version : "",
                enabled = !installed.TryGetValue(item.Id, out var installedEntry) || installedEntry.Enabled
            }).ToList();

            Post(new
            {
                type = "workshopList",
                payload = new
                {
                    baseUrl,
                    total = list.Total,
                    page = list.Page,
                    pageSize = list.PageSize,
                    tags = list.Tags,
                    items
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopList", payload = new { baseUrl, error = DescribeWorkshopError(ex) } });
        }
    }

    private async Task HandleWorkshopDetailAsync(string id)
    {
        if (!RequireWorkshopConsent()) return;

        var baseUrl = WorkshopBaseUrl;
        try
        {
            var detail = await _workshopService.GetDetailAsync(
                baseUrl, id, _libraryService.Settings.Language);
            var cover = await _workshopService.CacheCoverAsync(baseUrl, detail.Id, detail.CoverUrl);
            var installed = await Task.Run(() => BuildInstalledModMap());
            var builtIn = ModManager.BuiltInModIds.Contains(detail.Id, StringComparer.OrdinalIgnoreCase);
            installed.TryGetValue(detail.Id, out var entry);

            Post(new
            {
                type = "workshopDetail",
                payload = new
                {
                    baseUrl,
                    mod = new
                    {
                        id = detail.Id,
                        name = detail.Name,
                        summary = detail.Summary,
                        description = detail.Description,
                        author = detail.Author,
                        tags = detail.Tags,
                        coverUrl = cover ?? "",
                        featured = detail.Featured,
                        updatedAt = detail.UpdatedAt,
                        latestVersion = detail.LatestVersion,
                        downloads = detail.Downloads,
                        builtIn,
                        installedVersion = entry?.Version ?? "",
                        enabled = entry?.Enabled ?? true,
                        versions = detail.Versions.Select(version => new
                        {
                            version = version.Version,
                            sha256 = version.Sha256,
                            sizeBytes = version.SizeBytes,
                            sdkVersion = version.SdkVersion,
                            permissions = version.Permissions,
                            dependencies = version.Dependencies,
                            changelog = version.Changelog,
                            publishedAt = version.PublishedAt,
                            downloads = version.Downloads
                        })
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopDetail", payload = new { id, baseUrl, error = DescribeWorkshopError(ex) } });
        }
    }

    private Dictionary<string, ModEntryInfo> BuildInstalledModMap()
    {
        var map = new Dictionary<string, ModEntryInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var entry in _modManager.GetStatus().Mods)
            {
                if (entry.Id.Length > 0) map.TryAdd(entry.Id, entry);
                if (entry.Name.Length > 0) map.TryAdd(entry.Name, entry);
            }
        }
        catch
        {
            // 没选游戏目录时读不到已装模组：页面按「未安装」显示即可。
        }

        return map;
    }

    // ============================== 安装 ==============================

    private async Task HandleWorkshopInstallAsync(string id, string? version)
    {
        if (!RequireWorkshopConsent()) return;

        if (_workshopBusy)
        {
            Post(new { type = "toast", message = "正在安装模组，请稍候。" });
            return;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Post(new { type = "workshopResult", payload = new { success = false, id, message = "缺少模组 ID。" } });
            return;
        }

        if (ModManager.BuiltInModIds.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            Post(new { type = "workshopResult", payload = new { success = false, id, message = "这是随玩具箱内置的模组，不能从创意工坊安装。" } });
            return;
        }

        string gameDirectory;
        try
        {
            gameDirectory = RequireModGameDirectory();
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopResult", payload = new { success = false, id, message = ex.Message } });
            return;
        }

        var temporary = Path.Combine(Path.GetTempPath(), $"astralparty-workshop-{Guid.NewGuid():N}.zip");
        var baseUrl = WorkshopBaseUrl;
        _workshopBusy = true;
        _workshopDownloadCts = new CancellationTokenSource();
        var token = _workshopDownloadCts.Token;

        try
        {
            Post(new { type = "workshopProgress", payload = new { id, phase = "prepare", percent = (int?)null } });

            var detail = await _workshopService.GetDetailAsync(
                baseUrl, id, _libraryService.Settings.Language, token);

            var targetVersion = string.IsNullOrWhiteSpace(version)
                ? detail.LatestVersion
                : version!.Trim();
            var selected = detail.Versions.FirstOrDefault(item =>
                               item.Version.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
                           ?? throw new WorkshopException($"工坊里已经没有 v{targetVersion} 这个版本。");

            var url = WorkshopService.ResolveUrl(baseUrl, selected.DownloadUrl);

            var lastPercent = -1;
            var progress = new InlineProgress<WorkshopDownloadProgress>(value =>
            {
                // 每 128 KB 都发一条消息太吵：百分比变化才回推。
                var percent = value.Percent;
                if (percent is not null && percent == lastPercent) return;
                if (percent is null && value.ReceivedBytes / (512 * 1024) == lastPercent) return;
                lastPercent = percent ?? (int)(value.ReceivedBytes / (512 * 1024));

                Post(new
                {
                    type = "workshopProgress",
                    payload = new
                    {
                        id,
                        phase = "download",
                        receivedBytes = value.ReceivedBytes,
                        totalBytes = value.TotalBytes,
                        percent
                    }
                });
            });

            var (sha256, sizeBytes) = await _workshopService.DownloadPackageAsync(url, temporary, progress, token);

            if (!sha256.Equals(selected.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new WorkshopException(
                    "下载到的文件与工坊登记的 SHA-256 不一致，已拒绝安装。可能是作者替换了同版本文件，请联系登记人重新校验。");

            Post(new { type = "workshopProgress", payload = new { id, phase = "verify", percent = (int?)null } });

            var inspection = await Task.Run(() => _modManager.InspectModPackage(gameDirectory, temporary));
            if (inspection.Mods.Count != 1)
                throw new WorkshopException("压缩包里应当只有一个模组，已停止安装。");

            var package = inspection.Mods[0];
            if (!package.ModId.Equals(detail.Id, StringComparison.OrdinalIgnoreCase))
                throw new WorkshopException($"包里的模组是 {package.ModId}，与登记的 {detail.Id} 不一致，已停止安装。");
            if (!package.PackageVersion.Equals(selected.Version, StringComparison.OrdinalIgnoreCase))
                throw new WorkshopException(
                    $"包内版本（v{package.PackageVersion}）与登记的 v{selected.Version} 不一致，已停止安装。");

            if (inspection.HasDowngrade)
            {
                _pendingWorkshopInstall = new PendingWorkshopInstall(id, selected.Version, temporary, gameDirectory);
                Post(new
                {
                    type = "workshopInstallConfirm",
                    payload = new
                    {
                        id,
                        version = selected.Version,
                        packages = inspection.Mods.Where(mod => mod.IsDowngrade).Select(mod => new
                        {
                            name = mod.Name,
                            from = mod.InstalledVersion,
                            to = mod.PackageVersion
                        })
                    }
                });
                return;
            }

            await ImportWorkshopPackageAsync(gameDirectory, id, selected.Version, temporary, allowDowngrade: false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            TryDeleteFile(temporary);
            Post(new { type = "workshopResult", payload = new { success = false, id, cancelled = true, message = "已取消下载。" } });
        }
        catch (Exception ex)
        {
            TryDeleteFile(temporary);
            Post(new { type = "workshopResult", payload = new { success = false, id, message = DescribeWorkshopError(ex) } });
        }
        finally
        {
            if (_pendingWorkshopInstall is null)
            {
                _workshopBusy = false;
                _workshopDownloadCts?.Dispose();
                _workshopDownloadCts = null;
            }
        }
    }

    /// <summary>降级确认弹窗的回答。</summary>
    private void HandleWorkshopInstallConfirmAnswer(bool accept)
    {
        var pending = _pendingWorkshopInstall;
        _pendingWorkshopInstall = null;
        if (pending is null) return;

        if (!accept)
        {
            TryDeleteFile(pending.TempPath);
            _workshopBusy = false;
            _workshopDownloadCts?.Dispose();
            _workshopDownloadCts = null;
            Post(new { type = "workshopResult", payload = new { success = false, id = pending.Id, cancelled = true, message = "已取消安装。" } });
            return;
        }

        _ = ImportWorkshopPackageAsync(
            pending.GameDirectory, pending.Id, pending.Version, pending.TempPath, allowDowngrade: true);
    }

    private async Task ImportWorkshopPackageAsync(
        string gameDirectory,
        string id,
        string version,
        string packagePath,
        bool allowDowngrade)
    {
        try
        {
            Post(new { type = "workshopProgress", payload = new { id, phase = "install", percent = (int?)null } });

            var result = await Task.Run(() => _modManager.ImportModPackage(gameDirectory, packagePath, allowDowngrade));
            var change = result.Changes.FirstOrDefault();
            var name = change is null
                ? id
                : change.Name.Length > 0 ? change.Name : id;
            var notes = new List<string>();
            if (result.PreservedConfigs.Count > 0)
                notes.Add($"保留了 {string.Join("、", result.PreservedConfigs)} 的已有配置");
            if (result.PreservedDisabled.Count > 0)
                notes.Add($"{string.Join("、", result.PreservedDisabled)} 仍为禁用状态");

            var message = $"{name} v{version} 已安装" +
                          (SpeedhackManager.IsGameRunning() ? "；重启游戏后生效" : "，重启游戏后生效");
            if (notes.Count > 0) message += "。" + string.Join("；", notes);

            Post(new
            {
                type = "workshopResult",
                payload = new
                {
                    success = true,
                    id,
                    version,
                    name,
                    preservedConfigs = result.PreservedConfigs,
                    preservedDisabled = result.PreservedDisabled,
                    message
                }
            });
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "workshopResult",
                payload = new { success = false, id, version, message = $"安装失败：{ex.Message}" }
            });
        }
        finally
        {
            TryDeleteFile(packagePath);
            _workshopBusy = false;
            _workshopDownloadCts?.Dispose();
            _workshopDownloadCts = null;
            _ = PushModStatusAsync();
        }
    }

    private void CancelWorkshopDownload()
    {
        try
        {
            _workshopDownloadCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已经结束了，忽略。
        }
    }

    private static string DescribeWorkshopError(Exception ex)
        => ex is WorkshopException or InvalidOperationException or IOException
            ? ex.Message
            : $"创意工坊请求失败：{ex.Message}";

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // 临时文件删不掉不影响安装结果，系统会自己清理。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
