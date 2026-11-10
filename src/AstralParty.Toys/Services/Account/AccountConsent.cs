namespace AstralParty.Toys.Services.Account;

public sealed class AccountConsent(string profileDirectory)
{
    private string PathName => Path.Combine(profileDirectory, "account-consent.txt");
    public bool Granted
    {
        get { try { return File.ReadAllText(PathName) == "1"; } catch { return false; } }
    }
    public void Accept()
    {
        Directory.CreateDirectory(profileDirectory);
        File.WriteAllText(PathName, "1");
    }
}
