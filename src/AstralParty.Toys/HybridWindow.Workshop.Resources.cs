using System.IO;
using System.Text.Json;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Modding;

namespace AstralParty.Toys;

/// <summary>
/// 市场 → 资源市场：拉取 Forge 资源类型/列表/详情、缓存封面与预览画廊、
/// 走多线程断点续传下载并通过 <see cref="WorkshopResourceInstaller"/> 落盘。
/// </summary>
public partial class HybridWindow
{
    private (string BaseUrl, DateTimeOffset Expires, List<WorkshopResourceTypeItem> Items)? _workshopResourceTypesCache;
    private WorkshopResourceInstaller? _workshopResourceInstaller;

    private WorkshopResourceInstaller ResourceInstaller => _workshopResourceInstaller ??= new WorkshopResourceInstaller(
        _skinManager,
        _characterSkins,
        CharacterSchemeManager,
        _audioSkins,
        _resourceModManager,
        HomeBackground);

    private async Task<List<WorkshopResourceTypeItem>> GetCachedResourceTypesAsync(string baseUrl)
    {
        if (_workshopResourceTypesCache is { } cached &&
            string.Equals(cached.BaseUrl, baseUrl, StringComparison.OrdinalIgnoreCase) &&
            cached.Expires > DateTimeOffset.UtcNow)
        {
            return cached.Items;
        }

        var items = await _workshopService.GetResourceTypesAsync(baseUrl);
        _workshopResourceTypesCache = (baseUrl, DateTimeOffset.UtcNow.AddMinutes(5), items);
        return items;
    }

    private string? TryResolveLocalGameDirectory()
    {
        try
        {
            var dir = _modManager.ResolveGameDirectory();
            return !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) ? dir : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, InstalledWorkshopResource>> BuildInstalledResourceMapForTargetAsync()
    {
        if (ActiveOnlineMobileTarget() is { } device)
        {
            var mobileMap = new Dictionary<string, InstalledWorkshopResource>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var state = await Task.Run(() => MobileSkins.GetStateAsync(device));
                foreach (var profile in state.Profiles)
                {
                    if (string.IsNullOrWhiteSpace(profile.Name)) continue;
                    mobileMap.TryAdd(profile.Name, new InstalledWorkshopResource
                    {
                        Id = profile.Name,
                        Type = "card_skin",
                        Version = "",
                        TargetId = profile.Name,
                        Name = profile.DisplayName
                    });
                }
            }
            catch (Exception ex)
            {
                _mobileLog?.Warn($"{device.Label} 上的卡牌皮肤读取失败：{ex.Message}");
            }
            return mobileMap;
        }

        var gameDir = TryResolveLocalGameDirectory();
        return await Task.Run(() => ResourceInstaller.LoadInstalled(gameDir));
    }

    private async Task HandleWorkshopResourceListAsync(JsonElement root)
    {
        if (!RequireWorkshopConsent()) return;

        var type = root.TryGetProperty("resourceType", out var typeEl) ? typeEl.GetString() : null;
        var query = root.TryGetProperty("query", out var queryEl) ? queryEl.GetString() : null;
        var tag = root.TryGetProperty("tag", out var tagEl) ? tagEl.GetString() : null;
        var rating = root.TryGetProperty("rating", out var ratingEl) ? ratingEl.GetString() : null;
        var sort = root.TryGetProperty("sort", out var sortEl) ? sortEl.GetString() : null;
        var page = root.TryGetProperty("page", out var pageEl) && pageEl.TryGetInt32(out var parsedPage)
            ? parsedPage
            : 1;

        var baseUrl = WorkshopBaseUrl;
        try
        {
            var listTask = _workshopService.GetResourceListAsync(
                baseUrl,
                _libraryService.Settings.Language,
                type,
                query,
                tag,
                rating,
                string.IsNullOrWhiteSpace(sort) ? "updated" : sort!,
                page,
                24);
            var typesTask = GetCachedResourceTypesAsync(baseUrl);
            await Task.WhenAll(listTask, typesTask);

            var list = listTask.Result;
            var resourceTypes = typesTask.Result.Count > 0 ? typesTask.Result : list.Types;
            var installed = await BuildInstalledResourceMapForTargetAsync();

            var covers = await Task.WhenAll(list.Items.Select(item =>
                _workshopService.CacheResourceCoverAsync(baseUrl, item.Id, item.CoverUrl)));

            var items = list.Items.Select((item, index) =>
            {
                installed.TryGetValue(item.Id, out var inst);
                return new
                {
                    id = item.Id,
                    type = item.Type,
                    typeName = item.TypeName,
                    name = item.Name,
                    summary = item.Summary,
                    author = item.Author,
                    tags = item.Tags,
                    contentRating = item.ContentRating,
                    attributes = item.Attributes,
                    coverUrl = covers[index] ?? "",
                    previewCount = item.PreviewCount,
                    latestVersion = item.LatestVersion,
                    gameVersion = item.GameVersion,
                    platforms = item.Platforms,
                    sizeBytes = item.SizeBytes,
                    fileCount = item.FileCount,
                    hosted = item.Hosted,
                    sha256 = item.Sha256,
                    updatedAt = item.UpdatedAt,
                    downloads = item.Downloads,
                    downloads7Days = item.Downloads7Days,
                    downloads30Days = item.Downloads30Days,
                    featured = item.Featured,
                    installedVersion = inst?.Version ?? "",
                    installedTargetId = inst?.TargetId ?? "",
                    isMobileSupported = string.Equals(item.Type, "card_skin", StringComparison.OrdinalIgnoreCase)
                };
            }).ToList();

            Post(new
            {
                type = "workshopResourceList",
                payload = new
                {
                    baseUrl,
                    target = DescribeWorkshopTarget(),
                    downloadThreads = _libraryService.Settings.WorkshopDownloadThreads,
                    total = list.Total,
                    page = list.Page,
                    pageSize = list.PageSize,
                    tags = list.Tags,
                    resourceTypes = resourceTypes.Select(t => new
                    {
                        type = t.Type,
                        displayName = t.DisplayName,
                        description = t.Description,
                        enabled = t.Enabled
                    }),
                    items
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopResourceList", payload = new { baseUrl, error = DescribeWorkshopError(ex) } });
        }
    }

    private async Task HandleWorkshopResourceDetailAsync(string id)
    {
        if (!RequireWorkshopConsent()) return;

        var baseUrl = WorkshopBaseUrl;
        try
        {
            var detail = await _workshopService.GetResourceDetailAsync(
                baseUrl, id, _libraryService.Settings.Language);

            var coverTask = _workshopService.CacheResourceCoverAsync(baseUrl, detail.Id, detail.CoverUrl);
            var previewSlice = detail.Previews.Take(12).ToList();
            var previewTasks = previewSlice.Select(p =>
                _workshopService.CacheResourcePreviewAsync(baseUrl, detail.Id, p.Sha256, p.Url)).ToArray();

            await Task.WhenAll(previewTasks.Prepend(coverTask));

            var installedResources = await BuildInstalledResourceMapForTargetAsync();
            var installedMods = await Task.Run(BuildInstalledModMap);
            installedResources.TryGetValue(detail.Id, out var installedRes);

            var previews = previewSlice.Select((p, idx) => new
            {
                sha256 = p.Sha256,
                url = previewTasks[idx].Result ?? "",
                width = p.Width,
                height = p.Height,
                caption = p.Caption,
                sourcePath = p.SourcePath
            }).Where(p => !string.IsNullOrEmpty(p.url)).ToList();

            Post(new
            {
                type = "workshopResourceDetail",
                payload = new
                {
                    baseUrl,
                    target = DescribeWorkshopTarget(),
                    downloadThreads = _libraryService.Settings.WorkshopDownloadThreads,
                    resource = new
                    {
                        id = detail.Id,
                        type = detail.Type,
                        typeName = detail.TypeName,
                        name = detail.Name,
                        summary = detail.Summary,
                        description = detail.Description,
                        author = detail.Author,
                        tags = detail.Tags,
                        contentRating = detail.ContentRating,
                        attributes = detail.Attributes,
                        repositoryUrl = detail.RepositoryUrl,
                        license = detail.License,
                        licenseText = detail.LicenseText,
                        readme = detail.Readme,
                        credits = detail.Credits,
                        coverUrl = coverTask.Result ?? "",
                        previews,
                        featured = detail.Featured,
                        createdAt = detail.CreatedAt,
                        updatedAt = detail.UpdatedAt,
                        latestVersion = detail.LatestVersion,
                        downloads = detail.Downloads,
                        installedVersion = installedRes?.Version ?? "",
                        installedTargetId = installedRes?.TargetId ?? "",
                        isMobileSupported = string.Equals(detail.Type, "card_skin", StringComparison.OrdinalIgnoreCase),
                        versions = detail.Versions.Select(v => new
                        {
                            version = v.Version,
                            sourceUrl = v.SourceUrl,
                            hosted = v.Hosted,
                            sha256 = v.Sha256,
                            sizeBytes = v.SizeBytes,
                            unpackedBytes = v.UnpackedBytes,
                            fileCount = v.FileCount,
                            gameVersion = v.GameVersion,
                            platforms = v.Platforms,
                            modDependencies = v.ModDependencies.Select(dep => new
                            {
                                id = dep.Id,
                                minVersion = dep.MinVersion,
                                installed = installedMods.ContainsKey(dep.Id),
                                installedVersion = installedMods.TryGetValue(dep.Id, out var modInfo) ? modInfo.Version : ""
                            }),
                            resourceDependencies = v.ResourceDependencies,
                            changelog = v.Changelog,
                            publishedAt = v.PublishedAt,
                            downloads = v.Downloads
                        })
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopResourceDetail", payload = new { id, baseUrl, error = DescribeWorkshopError(ex) } });
        }
    }

    private async Task HandleWorkshopResourceInstallAsync(string id, string? version)
    {
        if (!RequireWorkshopConsent()) return;

        if (_workshopBusy)
        {
            Post(new { type = "toast", message = "正在安装其他项目，请稍候。" });
            return;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Post(new { type = "workshopResourceResult", payload = new { success = false, id, message = "缺少资源 ID。" } });
            return;
        }

        if (ActiveMobileTarget() is { } device)
        {
            if (!device.Online)
            {
                Post(new
                {
                    type = "workshopResourceResult",
                    payload = new { success = false, id, message = $"{device.Label} 已离线，重新连上后再试。" }
                });
                return;
            }
            if (DeviceBusyGate())
            {
                Post(new
                {
                    type = "workshopResourceResult",
                    payload = new { success = false, id, message = "设备上还有操作正在进行，请稍候。" }
                });
                return;
            }

            _workshopBusy = true;
            _deviceBusy = true;
            _workshopDownloadCts = new CancellationTokenSource();
            await InstallMarketResourceToDeviceAsync(device, id, version, _workshopDownloadCts.Token);
            return;
        }

        string gameDirectory;
        try
        {
            gameDirectory = RequireModGameDirectory();
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopResourceResult", payload = new { success = false, id, message = ex.Message } });
            return;
        }

        var temporary = Path.Combine(Path.GetTempPath(), $"astralparty-res-{Guid.NewGuid():N}.zip");
        var baseUrl = WorkshopBaseUrl;
        _workshopBusy = true;
        _workshopDownloadCts = new CancellationTokenSource();
        var token = _workshopDownloadCts.Token;

        try
        {
            Post(new { type = "workshopResourceProgress", payload = new { id, phase = "prepare", percent = (int?)null } });

            var detail = await _workshopService.GetResourceDetailAsync(
                baseUrl, id, _libraryService.Settings.Language, token);

            var targetVersion = string.IsNullOrWhiteSpace(version) ? detail.LatestVersion : version!.Trim();
            var selected = detail.Versions.FirstOrDefault(v =>
                               v.Version.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
                           ?? throw new WorkshopException($"市场里已经没有 v{targetVersion} 这个版本。");

            var requiredBytes = selected.UnpackedBytes > 0 ? selected.UnpackedBytes : selected.SizeBytes;
            if (requiredBytes > 0) ImportLimits.EnsureFreeSpace(gameDirectory, requiredBytes);

            var url = WorkshopService.ResolveUrl(baseUrl, selected.DownloadUrl);
            var progress = new InlineProgress<WorkshopDownloadProgress>(value => Post(new
            {
                type = "workshopResourceProgress",
                payload = new
                {
                    id,
                    phase = "download",
                    receivedBytes = value.ReceivedBytes,
                    totalBytes = value.TotalBytes,
                    percent = value.Percent,
                    bytesPerSecond = value.BytesPerSecond,
                    activeThreads = value.ActiveThreads,
                    resumable = value.Resumable,
                    resumed = value.Resumed
                }
            }));

            var (sha256, _) = await _workshopService.DownloadPackageAsync(
                url,
                temporary,
                progress,
                token,
                WorkshopService.MaxResourcePackageBytes,
                _libraryService.Settings.WorkshopDownloadThreads,
                selected.Sha256);

            Post(new { type = "workshopResourceProgress", payload = new { id, phase = "verify", percent = (int?)null } });

            if (!string.IsNullOrWhiteSpace(selected.Sha256) &&
                !sha256.Equals(selected.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkshopException("下载到的文件与市场登记的 SHA-256 不一致，已拒绝安装。");
            }

            Post(new { type = "workshopResourceProgress", payload = new { id, phase = "install", percent = (int?)null } });

            var installResult = await Task.Run(() => ResourceInstaller.Install(gameDirectory, detail, selected, temporary, token), token);
            RefreshAfterResourceInstall(gameDirectory, installResult.Type);

            Post(new
            {
                type = "workshopResourceResult",
                payload = new
                {
                    success = true,
                    id = detail.Id,
                    type = installResult.Type,
                    version = installResult.Version,
                    targetId = installResult.TargetId,
                    name = installResult.Name,
                    jumpPage = installResult.JumpPage,
                    requiresRestart = installResult.RequiresRestart,
                    message = installResult.Message
                }
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Post(new { type = "workshopResourceResult", payload = new { success = false, id, cancelled = true, message = "已暂停/取消下载（若支持断点续传，下次点击安装会从断点继续）。" } });
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopResourceResult", payload = new { success = false, id, message = DescribeWorkshopError(ex) } });
        }
        finally
        {
            TryDeleteFile(temporary);
            _workshopBusy = false;
            _workshopDownloadCts?.Dispose();
            _workshopDownloadCts = null;
        }
    }

    private async Task InstallMarketResourceToDeviceAsync(
        MobileTarget target,
        string id,
        string? version,
        CancellationToken token)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"astralparty-res-mob-{Guid.NewGuid():N}.zip");
        var cleanZip = Path.Combine(Path.GetTempPath(), $"astralparty-res-mob-clean-{Guid.NewGuid():N}.zip");
        var baseUrl = WorkshopBaseUrl;

        try
        {
            Post(new { type = "workshopResourceProgress", payload = new { id, phase = "prepare", percent = (int?)null } });

            var detail = await _workshopService.GetResourceDetailAsync(baseUrl, id, _libraryService.Settings.Language, token);
            if (!string.Equals(detail.Type, "card_skin", StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkshopException($"移动设备目标暂只支持安装「卡牌皮肤」；「{detail.TypeName}」请把顶部目标切换回本机后再安装。");
            }

            var targetVersion = string.IsNullOrWhiteSpace(version) ? detail.LatestVersion : version!.Trim();
            var selected = detail.Versions.FirstOrDefault(v =>
                               v.Version.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
                           ?? throw new WorkshopException($"市场里已经没有 v{targetVersion} 这个版本。");

            var url = WorkshopService.ResolveUrl(baseUrl, selected.DownloadUrl);
            var progress = new InlineProgress<WorkshopDownloadProgress>(value => Post(new
            {
                type = "workshopResourceProgress",
                payload = new
                {
                    id,
                    phase = "download",
                    receivedBytes = value.ReceivedBytes,
                    totalBytes = value.TotalBytes,
                    percent = value.Percent,
                    bytesPerSecond = value.BytesPerSecond,
                    activeThreads = value.ActiveThreads,
                    resumable = value.Resumable,
                    resumed = value.Resumed
                }
            }));

            var (sha256, _) = await _workshopService.DownloadPackageAsync(
                url,
                tempZip,
                progress,
                token,
                WorkshopService.MaxResourcePackageBytes,
                _libraryService.Settings.WorkshopDownloadThreads,
                selected.Sha256);

            Post(new { type = "workshopResourceProgress", payload = new { id, phase = "verify", percent = (int?)null } });

            if (!string.IsNullOrWhiteSpace(selected.Sha256) &&
                !sha256.Equals(selected.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkshopException("下载到的文件与市场登记的 SHA-256 不一致，已拒绝安装。");
            }

            await Task.Run(() => WorkshopResourceInstaller.PrepareCleanCardSkinZip(tempZip, detail, cleanZip), token);

            Post(new { type = "workshopResourceProgress", payload = new { id, phase = "install", percent = (int?)null } });

            var result = await Task.Run(() => MobileSkins.ImportAsync(
                target,
                cleanZip,
                activate: false,
                progress: (msg, pct) => Post(new
                {
                    type = "workshopResourceProgress",
                    payload = new { id, phase = "device", percent = pct, message = msg }
                }),
                token));

            var displayName = string.IsNullOrWhiteSpace(detail.Name) ? id : detail.Name;
            Post(new
            {
                type = "workshopResourceResult",
                payload = new
                {
                    success = result.Ok,
                    id = detail.Id,
                    type = detail.Type,
                    version = selected.Version,
                    targetId = detail.Id,
                    name = displayName,
                    device = target.Label,
                    message = result.Ok
                        ? $"{displayName} v{selected.Version} 已装到 {target.Label}（{result.Detail}）"
                        : $"装到 {target.Label} 失败：{result.Message}"
                }
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Post(new { type = "workshopResourceResult", payload = new { success = false, id, cancelled = true, message = "已取消下载（支持断点续传时下次可续传）。" } });
        }
        catch (Exception ex)
        {
            Post(new { type = "workshopResourceResult", payload = new { success = false, id, message = DescribeWorkshopError(ex) } });
        }
        finally
        {
            TryDeleteFile(tempZip);
            TryDeleteFile(cleanZip);
            _workshopBusy = false;
            _workshopDownloadCts?.Dispose();
            _workshopDownloadCts = null;
            if (ActiveMobileTarget() is { } current)
                _ = PushSkinsAsync(current);
            else
                FinishDeviceBusyOp();
        }
    }

    private void RefreshAfterResourceInstall(string gameDirectory, string type)
    {
        try
        {
            switch (type.Trim().ToLowerInvariant())
            {
                case "card_skin":
                    PushSkinProfiles();
                    break;
                case "character_skin":
                    PushCharacterSkins();
                    break;
                case "audio_skin":
                    PushAudioSkinProfiles(gameDirectory);
                    break;
                case "rml_bundle":
                    Post(new { type = "resourceModsStatus", payload = _resourceModManager.GetStatus(gameDirectory) });
                    break;
                case "home_background":
                    PushHomeBgStatus(gameDirectory);
                    break;
            }
        }
        catch
        {
            // 页面尚未打开时部分 Push 可能跳过，不影响安装成功结果
        }
    }
}
