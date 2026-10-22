using System.IO;
using System.Text.Json;
using AstralParty.Toys.Services;
using Microsoft.Win32;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private readonly ResourceModManager _resourceModManager = new();
    private int _resourceModStatusRequest;
    private bool _resourceModBusy;
    private CancellationTokenSource? _resourceModCts;
    private bool _resourceModPaused;

    private string ResourceModGameDirectory() => _modManager.ResolveGameDirectory() ?? "";

    private void PostResourceModProgressFinished()
    {
        Post(new { type = "resourceModsProgress", payload = new { phase = "done", percent = 100, task = "已结束", paused = false, canPause = false, canCancel = false } });
        PostResourceModOperation(false, "");
    }

    private void PostResourceModOperation(bool active, string title, string detail = "", bool canCancel = false, int? percent = null) =>
        Post(new { type = "resourceModsOperation", payload = new { active, title, detail, percent, canCancel } });

    private IProgress<ResourceModProgress> CreateResourceModProgress(string title) =>
        new Progress<ResourceModProgress>(value =>
        {
            Post(new { type = "resourceModsProgress", payload = value });
            Post(new
            {
                type = "resourceModsOperation",
                payload = new
                {
                    active = value.Phase is "import" or "apply",
                    title,
                    detail = value.Task,
                    percent = value.Percent,
                    canCancel = value.CanCancel
                }
            });
        });

    private void HandleResourceModPause(JsonElement root)
    {
        if (!_resourceModBusy) return;
        var paused = root.TryGetProperty("paused", out var value) && value.GetBoolean();
        _resourceModPaused = paused;
        ResourceModLoader.EngineControl.SetPaused(paused);
        Post(new { type = "resourceModsPaused", paused });
    }

    private void HandleResourceModCancel()
    {
        if (!_resourceModBusy) return;
        ResourceModLoader.EngineControl.SetPaused(false);
        _resourceModPaused = false;
        Post(new { type = "resourceModsPaused", paused = false });
        _resourceModCts?.Cancel();
    }

    private async Task PushResourceModStatusAsync()
    {
        var request = ++_resourceModStatusRequest;
        try
        {
            var directory = ResourceModGameDirectory();
            var status = await Task.Run(() => _resourceModManager.GetStatus(directory));
            if (request == _resourceModStatusRequest)
                Post(new { type = "resourceModsStatus", payload = status });
        }
        catch (Exception ex)
        {
            if (request == _resourceModStatusRequest)
                Post(new { type = "error", message = "读取资源模组状态失败：" + ex.Message });
        }
    }

    private async void HandleResourceModImport()
    {
        if (_resourceModBusy)
        {
            Post(new { type = "toast", message = "资源模组操作正在进行，请稍候。" });
            return;
        }

        var directory = ResourceModGameDirectory();
        if (directory.Length == 0)
        {
            Post(new { type = "toast", message = "尚未选择有效的游戏目录，请先在「模组管理」里选择。" });
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "导入资源模组（RML 格式 ZIP）",
            Filter = "资源模组包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        _resourceModBusy = true;
        _resourceModPaused = false;
        _resourceModCts = new CancellationTokenSource();
        var progress = CreateResourceModProgress("正在导入资源模组");
        PostResourceModOperation(true, "正在导入资源模组", Path.GetFileName(dialog.FileName), canCancel: true);
        try
        {
            var zipPath = dialog.FileName;
            var result = await Task.Run(() => _resourceModManager.Import(directory, zipPath, progress, _resourceModCts.Token));
            var package = result.Package;
            var matched = result.Applied
                ? $"已应用 {result.Redirects} 处资源替换。"
                : result.ApplyError is { Length: > 0 }
                    ? "自动应用失败：" + result.ApplyError
                    : result.PendingReason ?? "安装完成，等待应用。";
            var unsupported = package.Unsupported.Count > 0 ? $" {package.Unsupported.Count} 项暂不支持。" : "";
            var issues = result.ApplyErrors > 0 ? $" {result.ApplyErrors} 条引擎错误未生效。" : result.ApplyWarnings > 0 ? $" {result.ApplyWarnings} 条警告。" : "";
            Post(new
            {
                type = "toast",
                message = $"已安装「{package.Name}」：{package.FileCount} 个文件。{matched}{issues}{unsupported}"
            });
        }
        catch (OperationCanceledException)
        {
            Post(new { type = "toast", message = "已取消导入，游戏资源已恢复原状。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = "导入资源模组失败：" + ex.Message });
        }
        finally
        {
            _resourceModBusy = false;
            _resourceModPaused = false;
            _resourceModCts.Dispose();
            _resourceModCts = null;
            ResourceModLoader.EngineControl.SetPaused(false);
            PostResourceModProgressFinished();
            await PushResourceModStatusAsync();
        }
    }

    private async void HandleResourceModApply()
    {
        if (_resourceModBusy) return;
        var directory = ResourceModGameDirectory();
        if (directory.Length == 0)
        {
            Post(new { type = "toast", message = "尚未选择有效的游戏目录。" });
            return;
        }

        _resourceModBusy = true;
        _resourceModPaused = false;
        _resourceModCts = new CancellationTokenSource();
        var progress = CreateResourceModProgress("正在应用资源模组");
        PostResourceModOperation(true, "正在应用资源模组", "准备 catalog 备份", canCancel: true);
        try
        {
            var result = await Task.Run(() => _resourceModManager.Apply(directory, progress, _resourceModCts.Token));
            var issues = result.Errors > 0 ? $" {result.Errors} 条引擎错误未生效。" : result.Warnings > 0 ? $" {result.Warnings} 条警告。" : "";
            Post(new { type = "toast", message = $"已应用资源模组：{result.Bundles} 个 bundle，{result.Redirects} 处重定向（catalog {result.CatalogVersion}）。{issues}" });
        }
        catch (OperationCanceledException)
        {
            Post(new { type = "toast", message = "已取消应用，游戏资源已恢复原状。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = "应用失败：" + ex.Message });
        }
        finally
        {
            _resourceModBusy = false;
            _resourceModPaused = false;
            _resourceModCts.Dispose();
            _resourceModCts = null;
            ResourceModLoader.EngineControl.SetPaused(false);
            PostResourceModProgressFinished();
            await PushResourceModStatusAsync();
        }
    }

    private async void HandleResourceModRestore()
    {
        if (_resourceModBusy) return;
        var directory = ResourceModGameDirectory();
        if (directory.Length == 0) return;

        _resourceModBusy = true;
        PostResourceModOperation(true, "正在恢复原始资源", "写回 catalog 备份");
        try
        {
            await Task.Run(() => _resourceModManager.Restore(directory));
            Post(new { type = "toast", message = "已恢复游戏原始资源目录，重启游戏后生效。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = "恢复失败：" + ex.Message });
        }
        finally
        {
            _resourceModBusy = false;
            PostResourceModOperation(false, "");
            await PushResourceModStatusAsync();
        }
    }

    private async void HandleResourceModToggle(JsonElement root)
    {
        if (_resourceModBusy) return;
        if (!root.TryGetProperty("id", out var idElement) || idElement.GetString() is not { Length: > 0 } id) return;
        var enabled = root.TryGetProperty("enabled", out var enabledElement) && enabledElement.GetBoolean();
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
        var directory = ResourceModGameDirectory();
        if (directory.Length == 0) return;

        _resourceModBusy = true;
        _resourceModPaused = false;
        _resourceModCts = new CancellationTokenSource();
        var title = enabled ? "正在启用资源模组" : "正在停用资源模组";
        var progress = CreateResourceModProgress(title);
        PostResourceModOperation(true, title, name, canCancel: true);
        try
        {
            await Task.Run(() => _resourceModManager.SetEnabled(directory, id, enabled, progress, _resourceModCts.Token));
            Post(new { type = "toast", message = enabled ? "已启用该资源模组。" : "已停用该资源模组。" });
        }
        catch (OperationCanceledException)
        {
            Post(new { type = "toast", message = "已取消，资源模组保持原状态。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = "切换失败：" + ex.Message });
        }
        finally
        {
            _resourceModBusy = false;
            _resourceModPaused = false;
            _resourceModCts.Dispose();
            _resourceModCts = null;
            ResourceModLoader.EngineControl.SetPaused(false);
            PostResourceModOperation(false, "");
            await PushResourceModStatusAsync();
        }
    }

    private async void HandleResourceModDelete(JsonElement root)
    {
        if (_resourceModBusy) return;
        if (!root.TryGetProperty("id", out var idElement) || idElement.GetString() is not { Length: > 0 } id) return;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
        var directory = ResourceModGameDirectory();
        if (directory.Length == 0) return;

        _resourceModBusy = true;
        _resourceModPaused = false;
        _resourceModCts = new CancellationTokenSource();
        var progress = CreateResourceModProgress("正在删除资源模组");
        PostResourceModOperation(true, "正在删除资源模组", name, canCancel: true);
        try
        {
            await Task.Run(() => _resourceModManager.Remove(directory, id, progress, _resourceModCts.Token));
            Post(new { type = "toast", message = "已删除该资源模组。" });
        }
        catch (OperationCanceledException)
        {
            Post(new { type = "toast", message = "已取消删除，资源模组保持原状态。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = "删除失败：" + ex.Message });
        }
        finally
        {
            _resourceModBusy = false;
            _resourceModPaused = false;
            _resourceModCts.Dispose();
            _resourceModCts = null;
            ResourceModLoader.EngineControl.SetPaused(false);
            PostResourceModOperation(false, "");
            await PushResourceModStatusAsync();
        }
    }

    private void HandleResourceModOpenFolder()
    {
        var directory = ResourceModGameDirectory();
        if (directory.Length == 0) return;
        try
        {
            var store = _resourceModManager.GetStoreDirectory(directory);
            Directory.CreateDirectory(store);
            ModManager.OpenInExplorer(store);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = "打开目录失败：" + ex.Message });
        }
    }
}
