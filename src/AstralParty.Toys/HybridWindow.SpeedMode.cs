using System.Text.Json;
using AstralParty.Toys.Models;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private bool GameFileOperationBusy =>
        _loaderRecoveryBusy || _modPackageImportRunning || _modUpdateRunning || _modImportRunning ||
        _modInstallRunning || _modRollbackRunning || _workshopBusy || _modProfileRunning;

    private void PrepareGameSpeedForLaunch()
    {
        if (GameFileOperationBusy) return;
        try
        {
            var status = _modManager.GetStatus();
            if (ModManager.CanConfigureSpeedMode(status))
                _modManager.ReadSpeedModeSettings(status, migrate: true);
        }
        catch (Exception ex)
        {
            ToysLog.Warn("mods", $"初始化游戏变速设置失败：{ex.Message}");
        }
    }

    private void PushGameSpeedSync(ModStatus status)
    {
        LoaderConfig? config = null;
        SpeedModeSettings? settings = null;
        string? error = null;
        try
        {
            if (ModManager.CanConfigureSpeedMode(status))
                settings = _modManager.ReadSpeedModeSettings(status, migrate: !GameFileOperationBusy);
            if (status.Installed && !string.IsNullOrWhiteSpace(status.GameDirectory))
                config = _modManager.ReadLoaderConfig(status.GameDirectory);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        Post(new { type = "modSpeedhackSync", payload = new { config, settings, status.GameDirectory, error } });
    }

    private void HandleGameSpeedSave(JsonElement request)
    {
        if (!request.TryGetProperty("speedhackBaseSpeed", out var value)) return;
        var status = _modManager.GetStatus();
        try
        {
            if (GameFileOperationBusy)
                throw new InvalidOperationException("游戏文件操作正在进行，请等待完成后再保存。");
            if (!status.Installed || status.LoaderDisabled || status.CoreMissing)
                throw new InvalidOperationException("请先安装或启用可用的 CesiumLoader。");
            if (request.TryGetProperty("gameDirectory", out var directory) &&
                !string.Equals(directory.GetString(), status.GameDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("当前游戏目录已变更，请重新调整倍速。");

            var speed = value.GetDouble();
            var linked = ModManager.CanConfigureSpeedMode(status);
            using var operation = CreateRollbackJournal().Begin(
                "speedhackSave", $"设置变速 {speed:0.##}x", status.GameDirectory,
                includeMods: linked, includeLoader: true, includeProfiles: false,
                preserveAccountFields: true, modFileNames: [ModManager.SpeedModeModFileName, ModManager.NativeSpeedModFileName]);
            _modManager.SaveGameSpeed(status, speed);
            operation.Commit();
            Post(new { type = "toast", message = speed > 1.0
                ? $"已启用变速：{speed:0.##}x（重启游戏生效）"
                : "已禁用变速（恢复 1.0 正常速度，重启游戏生效）" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"设置变速失败：{ex.Message}" });
        }
        finally
        {
            PushGameSpeedSync(status);
        }
    }

    private async Task HandleSpeedModeMessageAsync(JsonElement request, bool save)
    {
        var requestId = request.GetProperty("requestId").GetInt32();
        try
        {
            if (save && GameFileOperationBusy)
                throw new InvalidOperationException("游戏文件操作正在进行，请等待完成后再保存。");

            var status = save ? _modManager.GetStatus() : await Task.Run(_modManager.GetStatus);
            var modStateChanged = false;
            if (save)
            {
                var gameDirectory = request.GetProperty("gameDirectory").GetString();
                if (!string.Equals(gameDirectory, status.GameDirectory, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("当前游戏目录已变更，请重新打开变速页面后再保存。");

                var mode = request.GetProperty("mode").GetString() ?? "";
                var speed = request.GetProperty("speed").GetDouble();
                using var operation = CreateRollbackJournal().Begin(
                    "speedModeSave", "设置变速模式和倍速", status.GameDirectory,
                    includeMods: true, includeLoader: true, includeProfiles: false,
                    preserveAccountFields: true, modFileNames: [ModManager.SpeedModeModFileName, ModManager.NativeSpeedModFileName]);
                modStateChanged = _modManager.SaveSpeedModeSettings(status, mode, speed);
                operation.Commit();
            }

            var settings = _modManager.ReadSpeedModeSettings(status, migrate: !save && !GameFileOperationBusy);
            Post(new { type = "speedModeResult", payload = new { requestId, status.GameDirectory, settings } });
            if (modStateChanged) _ = PushModStatusAsync();
            else if (save) PushGameSpeedSync(status);
        }
        catch (Exception ex)
        {
            Post(new { type = "speedModeResult", payload = new { requestId, error = ex.Message } });
        }
    }
}
