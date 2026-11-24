using System.Text.Json;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Mobile.Services;
using AstralParty.Toys.Mobile.Targets;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Mobile;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private MobileDeviceService? _mobileDevices;
    private MobileInstallService? _mobileInstall;
    private MobileSkinService? _mobileSkins;
    private DeviceChannelMultiplexer? _mobileChannels;
    private MobileOperationLog? _mobileLog;
    private MobileInstallStore? _mobileInstallState;
    private bool _deviceBusy;
    private int _deviceRequest;

    /// <summary>
    /// 设备操作的全程日志：一次运行一个文件，落在应用的日志目录里；
    /// 每写一条就通过 deviceLog 推给设备页实时渲染（「打开日志文件夹」复用已有的 openLogsFolder）。
    /// </summary>
    private MobileOperationLog MobileLog => _mobileLog ??= CreateMobileLog();

    private MobileOperationLog CreateMobileLog()
    {
        var log = new MobileOperationLog(ToysPaths.LogsDirectory,
            "mobile-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        log.Written += entry => Post(new { type = "deviceLog", payload = entry });
        return log;
    }

    /// <summary>安装记录（按「设备 + 包」存）与检测共用同一份，切当前游戏时才能找回该包的状态。</summary>
    private MobileInstallStore MobileInstallState =>
        _mobileInstallState ??= new MobileInstallStore(Path.Combine(ToysPaths.Root, "mobile"));

    private MobileDeviceService MobileDevices =>
        _mobileDevices ??= new MobileDeviceService(ToysPaths.StateDirectory, [new IosChannel(), new AndroidChannel()],
            MobileLog, MobileInstallState);

    /// <summary>写操作按目标类型分派到 iOS / 安卓通道；只接一条会让安卓目标全走错通道。</summary>
    private DeviceChannelMultiplexer MobileChannels =>
        _mobileChannels ??= new DeviceChannelMultiplexer([new IosChannel(), new AndroidChannel()]);

    private MobileInstallService MobileInstall => _mobileInstall ??= new MobileInstallService(
        MobileChannels,
        MobileInstallState,
        MobileDevices.Store,
        Path.Combine(AppContext.BaseDirectory, "mobile-patcher", "CesiumLoader.Mobile.Patcher.dll"),
        MobileLog);

    private MobileSkinService MobileSkins =>
        _mobileSkins ??= new MobileSkinService(MobileChannels, MobileLog);

    /// <summary>
    /// 当前目标是设备时返回那台设备，否则 <c>null</c>（界面按本机处理）。
    /// 设备掉线时**仍然返回它**：调用方据此给出「设备已离线」而不是静默改本机。
    /// </summary>
    private MobileTarget? ActiveMobileTarget()
    {
        var active = MobileDevices.GetActive();
        if (!string.Equals(active.Kind, "mobile", StringComparison.OrdinalIgnoreCase))
            return null;
        return MobileDevices.GetTargets().FirstOrDefault(t =>
            string.Equals(t.Id, active.TargetId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>当前设备且在线；否则返回 <c>null</c>（用于需要真连设备的操作）。</summary>
    private MobileTarget? ActiveOnlineMobileTarget() => ActiveMobileTarget() is { Online: true } target ? target : null;

    /// <summary>
    /// Early-return handler wired in HybridWindow.Dispatcher.cs. All device work is read-only in M1.
    /// </summary>
    private bool HandleDeviceMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "devicesGet":
                _ = PushDevicesAsync(discover: true);
                return true;

            case "devicesRefresh":
                _ = PushDevicesAsync(discover: true);
                return true;

            case "deviceDetect":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }

                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有检测正在进行，请稍候" });
                    return true;
                }

                _deviceBusy = true;
                _ = DetectAsync(targetId);
                return true;
            }

            case "deviceSetActive":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                    return true;
                MobileDevices.SetActiveMobile(targetId);
                Post(new { type = "deviceActive", payload = new { targetId } });
                return true;
            }

            case "devicePatch":
            case "deviceRestore":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                _deviceBusy = true;
                if (type == "devicePatch")
                    _ = WriteAsync(() => MobileInstall.PatchAsync(target, DeviceProgress), targetId);
                else
                    _ = WriteAsync(
                        () => MobileInstall.RestoreAsync(
                            target, ReadDeviceBool(root, "removeLoader"), DeviceProgress),
                        targetId);
                return true;
            }

            case "deviceScanPackages":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                _deviceBusy = true;
                _ = ScanPackagesAsync(targetId);
                return true;
            }

            case "deviceSetApp":
            {
                var targetId = ReadDeviceString(root, "targetId");
                var appId = ReadDeviceString(root, "appId");
                if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(appId))
                    return true;
                MobileDevices.SetApp(targetId, appId);
                _ = PushDevicesAsync(discover: false);
                return true;
            }

            case "deviceDeployPayload":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }

                var payloadPath = ReadDeviceString(root, "payloadPath") ?? MobilePayloadPath;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.DeployPayloadAsync(target, payloadPath, DeviceProgress), targetId);
                return true;
            }

            case "deviceMods":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                _deviceBusy = true;
                _ = PushModsAsync(target);
                return true;
            }

            case "deviceSetMod":
            {
                var targetId = ReadDeviceString(root, "targetId");
                var modId = ReadDeviceString(root, "modId");
                if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(modId))
                    return true;
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                var enabled = ReadDeviceBool(root, "enabled");
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.SetModEnabledAsync(target, modId, enabled, DeviceProgress), targetId);
                return true;
            }

            case "deviceImportMod":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }

                var picker = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择模组包",
                    Filter = "模组包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                    CheckFileExists = true,
                };
                if (picker.ShowDialog() != true)
                    return true;

                var importEnabled = !ReadDeviceBool(root, "disabled");
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.ImportModPackageAsync(target, picker.FileName, importEnabled),
                    targetId);
                return true;
            }

            case "deviceModConfig":
            {
                var targetId = ReadDeviceString(root, "targetId");
                var modId = ReadDeviceString(root, "modId");
                if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(modId))
                    return true;
                if (_deviceBusy)
                {
                    Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
                    return true;
                }
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }

                var language = ReadDeviceString(root, "language") ?? "zh-Hans";
                _deviceBusy = true;
                _ = PushModConfigAsync(target, modId, language);
                return true;
            }

            case "deviceSetModConfig":
            {
                var targetId = ReadDeviceString(root, "targetId");
                var modId = ReadDeviceString(root, "modId");
                if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(modId) || _deviceBusy)
                    return true;
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Post(new { type = "error", message = "请先选择设备" });
                    return true;
                }

                var changes = ReadModConfigChanges(root);
                if (changes.Count == 0)
                {
                    Post(new { type = "toast", message = "没有改动" });
                    return true;
                }

                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.WriteModConfigValuesAsync(target, modId, changes), targetId);
                return true;
            }

            // ---- M4：统一「当前目标」切换（本机档案 / 移动设备）----
            case "targetSetActive":
            {
                var id = ReadDeviceString(root, "id");
                if (!string.IsNullOrEmpty(id))
                    SetActiveTarget(id);
                return true;
            }

            // ---- M5：设备上的卡牌皮肤 ----
            case "deviceSkins":
            {
                var target = ResolveDeviceTargetArg(root);
                if (target is null || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = PushSkinsAsync(target);
                return true;
            }

            case "deviceImportSkin":
            {
                var target = ResolveDeviceTargetArg(root);
                if (target is null || DeviceBusyGate()) return true;

                var picker = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择卡面方案包",
                    Filter = "卡面方案包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                    CheckFileExists = true,
                };
                if (picker.ShowDialog() != true)
                    return true;

                var activate = !ReadDeviceBool(root, "noActivate");
                _deviceBusy = true;
                _ = WriteAsync(() => MobileSkins.ImportAsync(target, picker.FileName, activate), target.Id);
                return true;
            }

            case "deviceSetSkin":
            {
                var target = ResolveDeviceTargetArg(root);
                var profile = ReadDeviceString(root, "profile");
                if (target is null || string.IsNullOrEmpty(profile) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileSkins.SetActiveAsync(target, profile!), target.Id);
                return true;
            }

            case "deviceDeleteSkin":
            {
                var target = ResolveDeviceTargetArg(root);
                var profile = ReadDeviceString(root, "profile");
                if (target is null || string.IsNullOrEmpty(profile) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileSkins.DeleteAsync(target, profile!), target.Id);
                return true;
            }

            // ---- M6：市场直接装到设备上 ----
            case "deviceMarketInstall":
            {
                var target = ResolveDeviceTargetArg(root);
                var modId = ReadDeviceString(root, "id");
                if (target is null || string.IsNullOrEmpty(modId) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = InstallMarketModToDeviceAsync(target, modId!, ReadDeviceString(root, "version"));
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>把消息里的 targetId 解析成目标；缺参数或目标不存在时回一条错误并返回 <c>null</c>。</summary>
    private MobileTarget? ResolveDeviceTargetArg(JsonElement root)
    {
        var targetId = ReadDeviceString(root, "targetId");
        if (string.IsNullOrEmpty(targetId))
        {
            Post(new { type = "error", message = "请先选择设备" });
            return null;
        }

        var target = MobileDevices.GetTargets().FirstOrDefault(t =>
            string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            Post(new { type = "error", message = "请先选择设备" });
            return null;
        }

        return target;
    }

    /// <summary>同一时刻只允许一个设备操作；占着就回一条 toast 并返回 <c>true</c>（调用方直接 return）。</summary>
    private bool DeviceBusyGate()
    {
        if (!_deviceBusy)
            return false;
        Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
        return true;
    }

    /// <summary>
    /// M4：切「当前目标」。本机档案走既有的 <c>gameProfileSetActive</c> 链路（会同步模组 / 变速器状态），
    /// 设备只写 <c>active-target.json</c>；两边都广播 <c>targetChanged</c> 让各页重新取数。
    /// </summary>
    private void SetActiveTarget(string id)
    {
        const string localPrefix = "local:";
        if (id.StartsWith(localPrefix, StringComparison.Ordinal))
        {
            var profileId = id[localPrefix.Length..];
            MobileDevices.SetActiveLocal(profileId);
            HandleGameProfileSetActive(profileId);
        }
        else
        {
            MobileDevices.SetActiveMobile(id);
        }

        _ = PushGameProfilesAsync();
        PostTargetChanged();
    }

    private void PostTargetChanged()
    {
        var mobile = ActiveMobileTarget();
        Post(new
        {
            type = "targetChanged",
            payload = new
            {
                kind = mobile is null ? "local" : "mobile",
                targetId = mobile?.Id ?? "",
                deviceLabel = mobile?.Label ?? "",
                deviceKind = mobile?.Kind ?? "",
                serial = mobile?.Serial ?? "",
                appId = mobile?.AppId ?? "",
                online = mobile?.Online ?? false,
                // 设备端做不到的功能，界面直接禁用并给原因（§8.4），不要让用户点到失败
                unsupported = new[]
                {
                    "characterSkins", "audioSkins", "fonts", "resourceMods", "modProfiles", "historyRollback", "launchGame",
                },
            },
        });
    }

    /// <summary>读设备上的卡面方案清单（M5）。</summary>
    private async Task PushSkinsAsync(MobileTarget target)
    {
        try
        {
            var state = await Task.Run(() => MobileSkins.GetStateAsync(target));
            Post(new { type = "deviceSkins", payload = state });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "读取卡面方案失败：" + ex.Message });
        }
        finally
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "done", percent = 100, message = "", active = false },
            });
            _deviceBusy = false;
        }
    }

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

            // 设备侧导入用设备自己的预检；这里先核对身份，避免把别人的包写进设备的 mods 目录
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
            // 不清 _deviceBusy：交给 PushModsAsync 的 finally，免得刷新列表的这段空窗里又开一个设备操作
            if (ActiveMobileTarget() is { } current)
                _ = PushModsAsync(current);
            else
                _deviceBusy = false;
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

    /// <summary>读设备上某个模组的 config.json（原文 + 标量字段）给页面弹窗。</summary>
    private async Task PushModConfigAsync(MobileTarget target, string modId, string language)
    {
        try
        {
            var (text, fields, path) = await Task.Run(() => MobileInstall.ReadModConfigAsync(target, modId, language));
            Post(new { type = "deviceModConfig", payload = new { targetId = target.Id, modId, path, text, fields } });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "读取模组配置失败：" + ex.Message });
        }
        finally
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "done", percent = 100, message = "", active = false },
            });
            _deviceBusy = false;
        }
    }

    /// <summary>读设备上的模组清单（目录即状态：mods/ 启用、disabled_mods/ 停用）。</summary>
    private async Task PushModsAsync(MobileTarget target)
    {
        try
        {
            var mods = await Task.Run(() => MobileInstall.ListModsAsync(target));
            Post(new { type = "deviceMods", payload = new { targetId = target.Id, mods } });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "读取模组列表失败：" + ex.Message });
        }
        finally
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "done", percent = 100, message = "", active = false },
            });
            _deviceBusy = false;
        }
    }

    /// <summary>
    /// 移动端载荷默认位置：内嵌在应用里的那份解到 &lt;数据根&gt;/payload/CesiumLoader。
    /// 解包失败时退回该路径（界面会提示载荷缺失，而不是把设备页整个弄崩）。
    /// </summary>
    private static string MobilePayloadPath
    {
        get
        {
            if (_mobilePayloadPath is not null)
                return _mobilePayloadPath;

            try
            {
                _mobilePayloadPath = MobilePayloadProvisioner.Ensure(ToysPaths.Root);
            }
            catch (Exception)
            {
                _mobilePayloadPath = MobilePayloadProvisioner.PayloadDirectory(ToysPaths.Root);
            }

            return _mobilePayloadPath;
        }
    }

    private static string? _mobilePayloadPath;

    /// <summary>扫描设备上所有可能的游戏包（渠道服很多），交给页面让玩家选定当前游戏。</summary>
    private async Task ScanPackagesAsync(string targetId)
    {
        try
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "scan", percent = 0, message = "正在扫描已安装的游戏包…", active = true },
            });
            var packages = await Task.Run(() => MobileDevices.FindPackagesAsync(targetId));
            Post(new { type = "devicePackages", payload = new { targetId, packages } });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "扫描游戏包失败：" + ex.Message });
        }
        finally
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "done", percent = 100, message = "", active = false },
            });
            _deviceBusy = false;
        }
    }

    private async Task PushDevicesAsync(bool discover)
    {
        var request = ++_deviceRequest;
        try
        {
            if (discover)
                await Task.Run(() => MobileDevices.DiscoverAsync());
            if (request != _deviceRequest)
                return;

            var payloadPath = MobilePayloadPath;
            var payload = new
            {
                targets = MobileDevices.GetTargets(),
                active = MobileDevices.GetActive(),
                logPath = MobileLog.FilePath,
                logLines = MobileLog.Recent.TakeLast(300),
                payloadPath,
                payloadPresent = Directory.Exists(payloadPath),
            };
            Post(new { type = "devices", payload });
        }
        catch (Exception ex)
        {
            if (request == _deviceRequest)
                Post(new { type = "error", message = "读取设备列表失败：" + ex.Message });
        }
    }

    private async Task DetectAsync(string targetId)
    {
        try
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "start", percent = 0, message = "开始检测…", active = true },
            });
            var report = await Task.Run(() => MobileDevices.DetectAsync(targetId, (message, percent) =>
                Post(new
                {
                    type = "deviceDetectProgress",
                    payload = new { phase = "detect", percent, message, active = true },
                })));
            Post(new { type = "deviceDetectReport", payload = report });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "设备检测失败：" + ex.Message });
        }
        finally
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "done", percent = 100, message = "", active = false },
            });
            _deviceBusy = false;
        }
    }

    private void DeviceProgress(string message, int percent) =>
        Post(new
        {
            type = "deviceDetectProgress",
            payload = new { phase = "write", percent, message, active = true },
        });

    private async Task WriteAsync(Func<Task<MobileOperationResult>> operation, string targetId)
    {
        try
        {
            var result = await Task.Run(operation);
            Post(new { type = "deviceOperationResult", payload = result, targetId });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "设备操作失败：" + ex.Message });
        }
        finally
        {
            Post(new
            {
                type = "deviceDetectProgress",
                payload = new { phase = "done", percent = 100, message = "", active = false },
            });
            _deviceBusy = true;
            _ = DetectAsync(targetId);
        }
    }

    private static string? ReadDeviceString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadDeviceBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
