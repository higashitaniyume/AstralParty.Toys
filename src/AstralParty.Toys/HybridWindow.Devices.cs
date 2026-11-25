using System.Text.Json;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
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
    private MobileFileService? _mobileFiles;
    private DeviceChannelMultiplexer? _mobileChannels;
    private MobileOperationLog? _mobileLog;
    private MobileInstallStore? _mobileInstallState;
    private bool _deviceBusy;
    private int _deviceRequest;
    private static string? _mobilePayloadPath;

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

    private MobileDeviceService MobileDevices
    {
        get
        {
            if (_mobileDevices is null)
            {
                AndroidChannel.CustomAdbPath = AppConfigStore.Load().Mobile.AdbPath;
                _mobileDevices = new MobileDeviceService(ToysPaths.StateDirectory, [new IosChannel(), new AndroidChannel()],
                    MobileLog, MobileInstallState);
            }
            return _mobileDevices;
        }
    }

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

    private MobileFileService MobileFiles =>
        _mobileFiles ??= new MobileFileService(MobileChannels, MobileLog);

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

    /// <summary>当前目标是设备时返回那台设备，否则 <c>null</c>（界面按本机处理）。</summary>
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

    private bool HandleDeviceMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "devicesGet":
            case "devicesRefresh":
                _ = PushDevicesAsync(discover: true);
                return true;

            case "deviceDetect":
            {
                var target = ResolveDeviceTargetArg(root, out var targetId);
                if (string.IsNullOrEmpty(targetId) || DeviceBusyGate()) return true;
                _ = DetectAsync(target?.Id ?? targetId);
                return true;
            }

            case "deviceSetActive":
            {
                var targetId = ReadDeviceString(root, "targetId");
                if (string.IsNullOrEmpty(targetId))
                    return true;
                SetActiveTarget(targetId);
                Post(new { type = "deviceActive", payload = new { targetId } });
                return true;
            }

            case "deviceInstall":
            case "devicePatch":
            case "deviceRestore":
            {
                var target = ResolveOnlineDeviceTarget(root, "执行写操作");
                if (target is null || DeviceBusyGate()) return true;
                _deviceBusy = true;
                if (type == "deviceInstall")
                {
                    var payloadPath = ReadDeviceString(root, "payloadPath") ?? MobilePayloadPath;
                    _ = WriteAsync(() => InstallAllAsync(target, payloadPath), target.Id, refresh: "mods");
                }
                else if (type == "devicePatch")
                {
                    _ = WriteAsync(() => MobileInstall.PatchAsync(target, DeviceProgress), target.Id);
                }
                else
                {
                    var removeLoader = ReadDeviceBool(root, "removeLoader");
                    _ = WriteAsync(
                        () => MobileInstall.RestoreAsync(target, removeLoader, DeviceProgress),
                        target.Id,
                        refresh: removeLoader ? "mods" : null);
                }
                return true;
            }

            case "devicePullPayload":
                if (DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = PullLatestPayloadAsync();
                return true;

            case "deviceScanPackages":
            {
                if (ResolveDeviceTargetArg(root, out var targetId) is null && string.IsNullOrEmpty(targetId))
                    return true;
                if (DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = ScanPackagesAsync(targetId!);
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
                if (ActiveMobileTarget() is { } active &&
                    string.Equals(active.Id, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    _ = PushGameProfilesAsync();
                    PostTargetChanged();
                }
                return true;
            }

            case "deviceDeployPayload":
            {
                var target = ResolveOnlineDeviceTarget(root, "投放载荷");
                if (target is null || DeviceBusyGate()) return true;
                var payloadPath = ReadDeviceString(root, "payloadPath") ?? MobilePayloadPath;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.DeployPayloadAsync(target, payloadPath, DeviceProgress), target.Id, refresh: "mods");
                return true;
            }

            case "deviceMods":
            {
                var target = ResolveDeviceTargetArg(root);
                if (target is null || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = PushModsAsync(target);
                return true;
            }

            case "deviceSetMod":
            {
                var target = ResolveDeviceTargetArg(root);
                var modId = ReadDeviceString(root, "modId");
                if (target is null || string.IsNullOrEmpty(modId) || DeviceBusyGate()) return true;
                var enabled = ReadDeviceBool(root, "enabled");
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.SetModEnabledAsync(target, modId, enabled, DeviceProgress),
                    target.Id, refresh: "mods");
                return true;
            }

            case "deviceDeleteMod":
            {
                var target = ResolveOnlineDeviceTarget(root, "删除模组");
                var modId = ReadDeviceString(root, "modId");
                if (target is null || string.IsNullOrEmpty(modId) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.DeleteModAsync(target, modId, DeviceProgress),
                    target.Id, refresh: "mods");
                return true;
            }

            case "deviceImportMod":
            {
                var target = ResolveDeviceTargetArg(root);
                if (target is null || DeviceBusyGate()) return true;
                var importEnabled = !ReadDeviceBool(root, "disabled");
                RunWithDeviceDialog("选择模组包失败", () =>
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "选择模组包",
                        Filter = "模组包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                        CheckFileExists = true,
                    };
                    if (!ShowOwnedDialog(picker))
                        return false;
                    _deviceBusy = true;
                    _ = WriteAsync(() => MobileInstall.ImportModPackageAsync(target, picker.FileName, importEnabled, DeviceProgress),
                        target.Id, refresh: "mods");
                    return true;
                });
                return true;
            }

            case "deviceModConfig":
            {
                var target = ResolveDeviceTargetArg(root);
                var modId = ReadDeviceString(root, "modId");
                if (target is null || string.IsNullOrEmpty(modId) || DeviceBusyGate()) return true;
                var language = ReadDeviceString(root, "language") ?? "zh-Hans";
                _deviceBusy = true;
                _ = PushModConfigAsync(target, modId, language);
                return true;
            }

            case "deviceSetModConfig":
            {
                var target = ResolveDeviceTargetArg(root);
                var modId = ReadDeviceString(root, "modId");
                if (target is null || string.IsNullOrEmpty(modId) || _deviceBusy) return true;
                var changes = ReadModConfigChanges(root);
                if (changes.Count == 0)
                {
                    Post(new { type = "toast", message = "没有改动" });
                    return true;
                }
                _deviceBusy = true;
                _ = WriteAsync(() => MobileInstall.WriteModConfigValuesAsync(target, modId, changes), target.Id);
                return true;
            }

            case "targetSetActive":
            {
                var id = ReadDeviceString(root, "id");
                if (!string.IsNullOrEmpty(id))
                    SetActiveTarget(id);
                return true;
            }

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
                var activate = !ReadDeviceBool(root, "noActivate");
                RunWithDeviceDialog("选择卡面方案包失败", () =>
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "选择卡面方案包",
                        Filter = "卡面方案包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                        CheckFileExists = true,
                    };
                    if (!ShowOwnedDialog(picker))
                        return false;
                    _deviceBusy = true;
                    _ = WriteAsync(() => MobileSkins.ImportAsync(target, picker.FileName, activate, DeviceProgress),
                        target.Id, refresh: "skins");
                    return true;
                });
                return true;
            }

            case "deviceSetSkin":
            {
                var target = ResolveDeviceTargetArg(root);
                var profile = ReadDeviceString(root, "profile");
                if (target is null || string.IsNullOrEmpty(profile) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileSkins.SetActiveAsync(target, profile!), target.Id, refresh: "skins");
                return true;
            }

            case "deviceDeleteSkin":
            {
                var target = ResolveDeviceTargetArg(root);
                var profile = ReadDeviceString(root, "profile");
                if (target is null || string.IsNullOrEmpty(profile) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = WriteAsync(() => MobileSkins.DeleteAsync(target, profile!), target.Id, refresh: "skins");
                return true;
            }

            case "deviceMarketInstall":
            {
                var target = ResolveDeviceTargetArg(root);
                var modId = ReadDeviceString(root, "id");
                if (target is null || string.IsNullOrEmpty(modId) || DeviceBusyGate()) return true;
                _workshopBusy = true;
                _deviceBusy = true;
                _ = InstallMarketModToDeviceAsync(target, modId!, ReadDeviceString(root, "version"));
                return true;
            }

            case "deviceCheckEnv":
                if (DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = PushEnvironmentAsync();
                return true;

            case "deviceScanAdb":
                if (DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = ScanAdbFromProcessesAsync();
                return true;

            case "deviceBrowseAdb":
                if (DeviceBusyGate()) return true;
                RunWithDeviceDialog("选择 adb.exe 失败", () =>
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "选择 adb.exe",
                        Filter = "ADB 可执行文件 (adb.exe)|adb.exe|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                        CheckFileExists = true,
                        RestoreDirectory = true,
                    };
                    if (!ShowOwnedDialog(picker) || string.IsNullOrWhiteSpace(picker.FileName))
                        return false;
                    _deviceBusy = true;
                    _ = ApplyCustomAdbPathAsync(picker.FileName);
                    return true;
                });
                return true;

            case "deviceSetAdbPath":
            {
                if (DeviceBusyGate()) return true;
                var adbPath = (ReadDeviceString(root, "path") ?? "").Trim().Trim('"');
                _deviceBusy = true;
                _ = ApplyCustomAdbPathAsync(adbPath);
                return true;
            }

            case "deviceClearAdbPath":
                if (DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = ApplyCustomAdbPathAsync("");
                return true;

            default:
                return HandleDeviceFileMessage(type, root);
        }
    }

    private MobileTarget? ResolveDeviceTargetArg(JsonElement root) =>
        ResolveDeviceTargetArg(root, out _);

    private MobileTarget? ResolveDeviceTargetArg(JsonElement root, out string? targetId)
    {
        targetId = ReadDeviceString(root, "targetId");
        if (string.IsNullOrEmpty(targetId))
        {
            Post(new { type = "error", message = "请先选择设备" });
            return null;
        }

        var id = targetId;
        var target = MobileDevices.GetTargets().FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            Post(new { type = "error", message = "请先选择设备" });
            return null;
        }

        return target;
    }

    private MobileTarget? ResolveOnlineDeviceTarget(JsonElement root, string actionLabel)
    {
        var target = ResolveDeviceTargetArg(root);
        if (target is null)
            return null;
        if (!target.Online)
        {
            Post(new { type = "error", message = $"{target.Label} 已离线，无法{actionLabel}" });
            return null;
        }
        return target;
    }

    private bool DeviceBusyGate()
    {
        if (!_deviceBusy)
            return false;
        Post(new { type = "toast", message = "设备上还有操作正在进行，请稍候" });
        return true;
    }

    /// <summary>切「当前目标」：本机档案走 gameProfileSetActive，设备写 active-target.json，并统一广播。</summary>
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
                unsupported = new[]
                {
                    "characterSkins", "audioSkins", "fonts", "resourceMods", "modProfiles", "historyRollback", "launchGame",
                },
            },
        });
    }

    private void RunWithDeviceDialog(string errorPrefix, Func<bool> pickAndStart)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            try
            {
                EnsureValidCurrentDirectory();
                if (!pickAndStart())
                    ClearDeviceBusyProgress();
            }
            catch (Exception ex)
            {
                ClearDeviceBusyProgress();
                Post(new { type = "error", message = $"{errorPrefix}：{ex.Message}" });
            }
        });
    }

    private bool ShowOwnedDialog(Microsoft.Win32.CommonDialog dialog) =>
        IsVisible ? dialog.ShowDialog(this) == true : dialog.ShowDialog() == true;

    private async Task RunBusyDeviceOpAsync(string errorPrefix, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"{errorPrefix}：{ex.Message}" });
        }
        finally
        {
            FinishDeviceBusyOp();
        }
    }

    private void FinishDeviceBusyOp()
    {
        ClearDeviceBusyProgress();
        _deviceBusy = false;
    }

    private Task PushSkinsAsync(MobileTarget target) =>
        RunBusyDeviceOpAsync("读取卡面方案失败", async () =>
        {
            DeviceFileProgress("正在读取设备卡面方案…", 25, true);
            var state = await Task.Run(() => MobileSkins.GetStateAsync(target));
            Post(new { type = "deviceSkins", payload = state });
        });

    private Task PushModConfigAsync(MobileTarget target, string modId, string language) =>
        RunBusyDeviceOpAsync("读取模组配置失败", async () =>
        {
            DeviceFileProgress($"正在读取模组配置：{modId}…", 25, true);
            var (text, fields, path) = await Task.Run(() => MobileInstall.ReadModConfigAsync(target, modId, language));
            Post(new { type = "deviceModConfig", payload = new { targetId = target.Id, modId, path, text, fields } });
        });

    private Task PushModsAsync(MobileTarget target) =>
        RunBusyDeviceOpAsync("读取模组列表失败", async () =>
        {
            DeviceFileProgress("正在读取设备模组列表…", 25, true);
            var mods = await Task.Run(() => MobileInstall.ListModsAsync(target));
            Post(new { type = "deviceMods", payload = new { targetId = target.Id, mods } });
        });

    private Task ScanPackagesAsync(string targetId) =>
        RunBusyDeviceOpAsync("扫描游戏包失败", async () =>
        {
            DeviceFileProgress("正在扫描已安装的游戏包…", 20, true);
            var packages = await Task.Run(() => MobileDevices.FindPackagesAsync(targetId));
            Post(new { type = "devicePackages", payload = new { targetId, packages } });
        });

    private async Task<MobileOperationResult> InstallAllAsync(MobileTarget target, string payloadPath)
    {
        var deploy = await MobileInstall.DeployPayloadAsync(
            target, payloadPath, (msg, pct) => DeviceProgress(msg, pct / 2));
        if (!deploy.Ok)
            return deploy;

        var patch = await MobileInstall.PatchAsync(
            target, (msg, pct) => DeviceProgress(msg, 50 + pct / 2));
        return patch.Ok
            ? new MobileOperationResult
            {
                Ok = true,
                Message = "已完成：投放载荷并注入代码包",
                Detail = string.Join(" · ", new[] { deploy.Detail, patch.Detail }.Where(s => !string.IsNullOrEmpty(s))),
            }
            : patch;
    }

    private async Task PullLatestPayloadAsync()
    {
        try
        {
            var settings = _libraryService.Settings;
            var result = await MobilePayloadProvisioner.PullLatestAsync(
                ToysPaths.Root, settings.GitHubMirrorType, settings.CustomGitHubMirrorUrl, DeviceProgress);
            _mobilePayloadPath = result.ArchivePath;
            MobileLog.Info($"已拉取最新移动端载荷 {result.Tag}（v{result.Version}, SDK {result.SdkVersion}）");
            Post(new
            {
                type = "devicePullResult",
                payload = new
                {
                    ok = true,
                    tag = result.Tag,
                    version = result.Version,
                    sdkVersion = result.SdkVersion,
                    sha256 = result.Sha256,
                },
            });
            await PushDevicesAsync(discover: false);
        }
        catch (Exception ex)
        {
            Post(new { type = "devicePullResult", payload = new { ok = false, message = ex.Message } });
            Post(new { type = "error", message = "拉取最新载荷失败：" + ex.Message });
        }
        finally
        {
            FinishDeviceBusyOp();
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
            var summary = MobilePayloadProvisioner.ReadSummary(ToysPaths.Root);
            var active = MobileDevices.GetActive();
            var activeTargetId = string.Equals(active.Kind, "mobile", StringComparison.OrdinalIgnoreCase)
                ? active.TargetId
                : string.IsNullOrEmpty(active.ProfileId) ? "" : "local:" + active.ProfileId;
            Post(new
            {
                type = "devices",
                payload = new
                {
                    targets = MobileDevices.GetTargets(),
                    active,
                    activeTargetId,
                    logPath = MobileLog.FilePath,
                    logLines = MobileLog.Recent.TakeLast(300),
                    payloadPath,
                    payloadPresent = summary.Present,
                    payloadVersion = summary.Version,
                    payloadSdkVersion = summary.SdkVersion,
                    payloadFileCount = summary.FileCount,
                },
            });
        }
        catch (Exception ex)
        {
            if (request == _deviceRequest)
                Post(new { type = "error", message = "读取设备列表失败：" + ex.Message });
        }
        finally
        {
            if (request == _deviceRequest && !_deviceBusy)
                ClearDeviceBusyProgress();
            if (request == _deviceRequest && discover)
                _ = PushEnvironmentCoreAsync();
        }
    }

    private Task PushEnvironmentAsync() =>
        RunBusyDeviceOpAsync("检测运行环境失败", async () =>
        {
            DeviceFileProgress("正在检测 ADB 与 iTunes 运行环境…", 25, true);
            await PushEnvironmentCoreAsync();
        });

    private async Task PushEnvironmentCoreAsync()
    {
        var configuredAdb = AppConfigStore.Load().Mobile.AdbPath;
        var report = await MobileEnvironmentService.CheckAsync(configuredAdb);
        Post(new { type = "deviceEnvReport", payload = report });
    }

    private Task ScanAdbFromProcessesAsync() =>
        RunBusyDeviceOpAsync("扫描运行中 ADB 进程失败", async () =>
        {
            DeviceFileProgress("正在扫描运行中的进程列表…", 30, true);
            var scan = await Task.Run(() => MobileEnvironmentService.ScanAdbFromProcesses());
            if (scan.Found && !string.IsNullOrWhiteSpace(scan.Path))
            {
                SaveAdbPath(scan.Path);
                await PushEnvironmentCoreAsync();
                await PushDevicesAsync(discover: true);
            }
            Post(new { type = "deviceAdbScanResult", payload = scan });
        });

    private Task ApplyCustomAdbPathAsync(string adbPath) =>
        RunBusyDeviceOpAsync("更新 ADB 路径失败", async () =>
        {
            var trimmed = (adbPath ?? "").Trim().Trim('"');
            if (!string.IsNullOrEmpty(trimmed) && !File.Exists(trimmed))
            {
                Post(new
                {
                    type = "deviceAdbScanResult",
                    payload = new AdbProcessScanResult { Found = false, Message = $"找不到指定的 adb 文件：{trimmed}" },
                });
                return;
            }

            DeviceFileProgress(string.IsNullOrEmpty(trimmed) ? "正在清除自定义 ADB 路径…" : "正在保存并检测 ADB 路径…", 35, true);
            SaveAdbPath(trimmed);
            await PushEnvironmentCoreAsync();
            await PushDevicesAsync(discover: true);
            Post(new
            {
                type = "deviceAdbScanResult",
                payload = new AdbProcessScanResult
                {
                    Found = true,
                    Path = trimmed,
                    Message = string.IsNullOrEmpty(trimmed) ? "已清除自定义 ADB 路径，切回自动查找" : $"已指定 ADB 路径：{trimmed}",
                },
            });
        });

    private static void SaveAdbPath(string path)
    {
        AppConfigStore.Update(cfg => cfg.Mobile.AdbPath = path);
        AndroidChannel.CustomAdbPath = string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private Task DetectAsync(string targetId)
    {
        _deviceBusy = true;
        return RunBusyDeviceOpAsync("设备检测失败", () => DetectCoreAsync(targetId));
    }

    private async Task DetectCoreAsync(string targetId)
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

    private void DeviceProgress(string message, int percent) =>
        Post(new
        {
            type = "deviceDetectProgress",
            payload = new { phase = "write", percent, message, active = true },
        });

    private void DeviceFileProgress(string message, int percent, bool indeterminate = false) =>
        Post(new
        {
            type = "deviceDetectProgress",
            payload = new { phase = "work", percent, message, active = true, indeterminate },
        });

    private Task WriteAsync(Func<Task<MobileOperationResult>> operation, string targetId, string? refresh = null) =>
        RunBusyDeviceOpAsync("设备操作失败", async () =>
        {
            var result = await Task.Run(operation);
            Post(new { type = "deviceOperationResult", payload = result, targetId });
            try
            {
                await DetectCoreAsync(targetId);
            }
            catch (Exception ex)
            {
                MobileLog.Warn($"写操作后重检测失败：{ex.Message}");
            }

            if (result.Ok && (refresh == "mods" || refresh == "skins"))
            {
                var target = MobileDevices.GetTargets().FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
                if (target is not null)
                {
                    try
                    {
                        if (refresh == "mods")
                        {
                            var mods = await Task.Run(() => MobileInstall.ListModsAsync(target));
                            Post(new { type = "deviceMods", payload = new { targetId = target.Id, mods } });
                        }
                        else if (refresh == "skins")
                        {
                            var skins = await Task.Run(() => MobileSkins.GetStateAsync(target));
                            Post(new { type = "deviceSkins", payload = skins });
                        }
                    }
                    catch (Exception ex)
                    {
                        MobileLog.Warn($"写操作后刷新列表失败：{ex.Message}");
                    }
                }
            }

            await PushDevicesAsync(discover: false);
            if (ActiveMobileTarget() is { } active &&
                string.Equals(active.Id, targetId, StringComparison.OrdinalIgnoreCase))
            {
                PostTargetChanged();
            }
        });

    private void ClearDeviceBusyProgress() =>
        Post(new
        {
            type = "deviceDetectProgress",
            payload = new { phase = "done", percent = 100, message = "", active = false },
        });

    private static void EnsureValidCurrentDirectory()
    {
        try
        {
            var cwd = Environment.CurrentDirectory;
            if (string.IsNullOrEmpty(cwd) || !Directory.Exists(cwd))
                Environment.CurrentDirectory = AppContext.BaseDirectory;
        }
        catch
        {
        }
    }

    private static string? ReadDeviceString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadDeviceBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
