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
            var message = restore
                ? await _modManager.RestoreLoaderAsync(directory, mirror, custom)
                : await Task.Run(() => _modManager.DisableLoader(directory));
            Post(new { type = "toast", message });
        }
        catch (Exception ex) { Post(new { type = "error", message = ex.Message }); }
        finally
        {
            _loaderRecoveryBusy = false;
            Post(new { type = "modRecoveryDone" });
            await PushModStatusAsync();
            _ = PushSpeedhackStatusAsync();
        }
    }
}
