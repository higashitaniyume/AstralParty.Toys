using System.Text.Json;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private bool GameFileOperationBusy =>
        _loaderRecoveryBusy || _modPackageImportRunning || _modUpdateRunning || _modImportRunning ||
        _modInstallRunning || _modRollbackRunning || _workshopBusy || _modProfileRunning;

    private async Task HandleSpeedModeMessageAsync(JsonElement request, bool save)
    {
        var requestId = request.GetProperty("requestId").GetInt32();
        try
        {
            if (save && GameFileOperationBusy)
                throw new InvalidOperationException("游戏文件操作正在进行，请等待完成后再保存。");

            var status = save ? _modManager.GetStatus() : await Task.Run(_modManager.GetStatus);
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
                    preserveAccountFields: true, modFileName: ModManager.SpeedModeModFileName);
                _modManager.SaveSpeedModeSettings(status, mode, speed);
                operation.Commit();
            }

            var settings = _modManager.ReadSpeedModeSettings(status);
            Post(new { type = "speedModeResult", payload = new { requestId, status.GameDirectory, settings } });
        }
        catch (Exception ex)
        {
            Post(new { type = "speedModeResult", payload = new { requestId, error = ex.Message } });
        }
    }
}
