namespace AstralParty.Toys;

public partial class HybridWindow
{
    private bool _loaderRecoveryBusy;
    private async Task HandleLoaderRecoveryAsync(bool restore, string? mirrorType = null, string? customMirrorUrl = null)
    {
        if (_loaderRecoveryBusy) return;
        _loaderRecoveryBusy = true;
        try
        {
            var directory = RequireModGameDirectory();
            var settings = _libraryService.Settings;
            var mirror = !string.IsNullOrEmpty(mirrorType) ? mirrorType : settings.GitHubMirrorType;
            var custom = customMirrorUrl ?? settings.CustomGitHubMirrorUrl;
            if (restore) Post(new { type = "modLoaderProgress", payload = new { phase = "verify", percent = -1, message = "正在恢复加载器…", active = true } });
            var message = restore
                ? await _modManager.RestoreLoaderAsync(directory, mirror, custom)
                : await Task.Run(() => _modManager.DisableLoader(directory));
            if (restore) Post(new { type = "modLoaderProgress", payload = new { phase = "done", percent = 100, message = "加载器已恢复", active = false } });
            Post(new { type = "toast", message });
        }
        catch (Exception ex)
        {
            if (restore) Post(new { type = "modLoaderProgress", payload = new { phase = "error", percent = -1, message = ex.Message, active = false } });
            Post(new { type = "error", message = ex.Message });
        }
        finally
        {
            _loaderRecoveryBusy = false;
            Post(new { type = "modRecoveryDone" });
            await PushModStatusAsync();
            _ = PushSpeedhackStatusAsync();
        }
    }
}
