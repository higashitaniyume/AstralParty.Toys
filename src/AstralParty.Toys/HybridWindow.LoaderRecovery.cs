namespace AstralParty.Toys;

public partial class HybridWindow
{
    private bool _loaderRecoveryBusy;
    private async Task HandleLoaderRecoveryAsync(bool restore)
    {
        if (_loaderRecoveryBusy) return;
        _loaderRecoveryBusy = true;
        try
        {
            var directory = RequireModGameDirectory();
            var message = restore
                ? await _modManager.RestoreLoaderAsync(directory)
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
