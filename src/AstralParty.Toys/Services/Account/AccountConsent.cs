namespace AstralParty.Toys.Services.Account;

/// <summary>
/// 「我的账号」免责说明的同意记录。存在统一配置 <c>AstralParty.Toys.Config.json</c> 的
/// <c>app.myAccountConsent</c> 里，不再单独维护一个 txt。
/// </summary>
public sealed class AccountConsent
{
    private readonly ToysConfigStore _store;

    public AccountConsent(string profileDirectory) => _store = ToysConfigStore.ForRoot(profileDirectory);

    public bool Granted
    {
        get
        {
            try { return _store.Load().App.MyAccountConsent; }
            catch { return false; }
        }
    }

    public void Accept() => _store.Update(config => config.App.MyAccountConsent = true);
}
