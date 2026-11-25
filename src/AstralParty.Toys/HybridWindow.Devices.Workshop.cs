using System.Text.Json;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    /// <summary>
    /// M6：从市场下载一个模组并直接装到设备上。走的是与本机市场同一条「下载 → 校验 SHA-256 → 预检 → 导入」链路，
    /// 只是最后一步落到设备目录；进度与结果复用市场页已有的消息类型。
    /// </summary>
    private async Task InstallMarketModToDeviceAsync(MobileTarget target, string modId, string? version)
    {
        if (!RequireWorkshopConsent())
        {
            _deviceBusy = false;
            return;
        }

        var temporary = Path.Combine(Path.GetTempPath(), $"astralparty-workshop-{Guid.NewGuid():N}.zip");
        try
        {
            Post(new { type = "workshopProgress", payload = new { id = modId, phase = "prepare", percent = (int?)null } });

            var baseUrl = WorkshopBaseUrl;
            var detail = await _workshopService.GetDetailAsync(baseUrl, modId, _libraryService.Settings.Language);
            var targetVersion = string.IsNullOrWhiteSpace(version) ? detail.LatestVersion : version!.Trim();
            var selected = detail.Versions.FirstOrDefault(item =>
                               item.Version.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException($"市场里已经没有 v{targetVersion} 这个版本。");

            var url = WorkshopService.ResolveUrl(baseUrl, selected.DownloadUrl);
            var progress = new InlineProgress<WorkshopDownloadProgress>(value => Post(new
            {
                type = "workshopProgress",
                payload = new
                {
                    id = modId,
                    phase = "download",
                    receivedBytes = value.ReceivedBytes,
                    totalBytes = value.TotalBytes,
                    percent = value.Percent,
                },
            }));

            var (sha256, _) = await _workshopService.DownloadPackageAsync(url, temporary, progress, CancellationToken.None);
            if (!sha256.Equals(selected.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "下载到的文件与市场登记的 SHA-256 不一致，已拒绝安装。可能是作者替换了同版本文件。");

            Post(new { type = "workshopProgress", payload = new { id = modId, phase = "verify", percent = (int?)null } });

            var package = MobileModPackage.Read(temporary);
            if (!package.ModId.Equals(detail.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"包里的模组是 {package.ModId}，与登记的 {detail.Id} 不一致，已停止安装。");
            if (!package.Version.Equals(selected.Version, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"包内版本（v{package.Version}）与登记的 v{selected.Version} 不一致，已停止安装。");

            Post(new { type = "workshopProgress", payload = new { id = modId, phase = "install", percent = (int?)null } });

            var result = await Task.Run(() => MobileInstall.ImportModPackageAsync(
                target, temporary, enabled: true,
                progress: (message, percent) => Post(new
                {
                    type = "workshopProgress",
                    payload = new { id = modId, phase = "device", percent, message },
                })));

            Post(new
            {
                type = "workshopResult",
                payload = new
                {
                    success = result.Ok,
                    id = modId,
                    version = selected.Version,
                    name = string.IsNullOrEmpty(package.Name) ? modId : package.Name,
                    device = target.Label,
                    message = result.Ok
                        ? $"{package.Name} v{selected.Version} 已装到 {target.Label}（{result.Detail}）；在设备上重启游戏后生效"
                        : $"装到 {target.Label} 失败：{result.Message}",
                },
            });
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "workshopResult",
                payload = new { success = false, id = modId, message = DescribeWorkshopError(ex) },
            });
        }
        finally
        {
            TryDeleteFile(temporary);
            _workshopBusy = false;
            // 不清 _deviceBusy：交给 PushModsAsync 的 finally，免得刷新列表的这段空窗里又开一个设备操作
            if (ActiveMobileTarget() is { } current)
                _ = PushModsAsync(current);
            else
                FinishDeviceBusyOp();
        }
    }

    private static List<(string Key, string Kind, string Value)> ReadModConfigChanges(JsonElement root)
    {
        var changes = new List<(string Key, string Kind, string Value)>();
        if (!root.TryGetProperty("changes", out var array) || array.ValueKind != JsonValueKind.Array)
            return changes;

        foreach (var item in array.EnumerateArray())
        {
            var key = item.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            if (string.IsNullOrEmpty(key))
                continue;
            var kind = item.TryGetProperty("kind", out var kindNode) && kindNode.ValueKind == JsonValueKind.String
                ? kindNode.GetString() ?? "string"
                : "string";
            var value = item.TryGetProperty("value", out var valueNode) && valueNode.ValueKind == JsonValueKind.String
                ? valueNode.GetString() ?? ""
                : "";
            changes.Add((key, kind, value));
        }

        return changes;
    }
}
