using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Account;

// This record must never be posted to WebView, logged, or serialized in diagnostics.
public sealed class AccountCredentials
{
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string ClientVersion { get; init; } = "";
    public string ConnectRequestBase64 { get; init; } = "";
    public int ProtocolVersion1 { get; init; } = 1;
    public int ProtocolVersion2 { get; init; }
    public int ProtocolVersion3 { get; init; }
    // Declared by the Mod ("china" / "abroad"); empty in credentials written by earlier Mod versions.
    public string Region { get; init; } = "";

    public const int MaxFileBytes = 64 * 1024;
    public static bool ValidUid(string? uid) => uid != null && Regex.IsMatch(uid, "^[1-9][0-9]{0,18}$") && long.TryParse(uid, out _);

    // The game resolves its login endpoint from its own remote config, so only the vendor's own
    // jump hosts on the game port are accepted. Never trust an endpoint supplied by a credential file:
    // the CN client uses se-jump-cn-01, the international client another se-jump-<area>-<n> host.
    private static readonly Regex JumpHostPattern =
        new(@"^se-jump-[a-z0-9]{2,4}-[0-9]{2}\.feimogames\.com$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsOfficialEndpoint(string? host, int port) =>
        port == 8800 && host != null && JumpHostPattern.IsMatch(host);

    public static bool ValidRegion(string? region) => region is "" or "china" or "abroad";

    public static string Root(string gameDirectory) => Path.Combine(gameDirectory, ModManager.LoaderFolderName, "usercredentials");

    public static string[] Discover(string gameDirectory)
    {
        var root = Root(gameDirectory);
        if (!Directory.Exists(root) || IsLink(root)) return [];
        return Directory.EnumerateDirectories(root).Where(p => !IsLink(p) && ValidUid(Path.GetFileName(p)))
            .Where(p => File.Exists(Path.Combine(p, "account.json")) && !IsLink(Path.Combine(p, "account.json")))
            .Select(p => Path.GetFileName(p)).OrderBy(p => p, StringComparer.Ordinal).Take(100).ToArray();
    }

    public static AccountCredentials Read(string gameDirectory, string uid)
    {
        if (!ValidUid(uid)) throw new InvalidDataException("invalid_credentials");
        var root = Root(gameDirectory);
        var directory = Path.Combine(root, uid);
        var path = Path.Combine(directory, "account.json");
        if (IsLink(root) || IsLink(directory) || IsLink(path)) throw new InvalidDataException("invalid_credentials");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaxFileBytes) throw new InvalidDataException("invalid_credentials");
        try
        {
            var value = JsonSerializer.Deserialize<AccountCredentials>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (value == null || !IsOfficialEndpoint(value.Host, value.Port) || !ValidRegion(value.Region) ||
                string.IsNullOrWhiteSpace(value.ClientVersion) || value.ConnectRequestBase64.Length > MaxFileBytes ||
                value.ProtocolVersion1 is < 0 or > 255 || value.ProtocolVersion2 is < 0 or > 255 || value.ProtocolVersion3 is < 0 or > 255 ||
                Convert.FromBase64String(value.ConnectRequestBase64).Length == 0)
                throw new InvalidDataException();
            return value;
        }
        catch { throw new InvalidDataException("invalid_credentials"); }
    }

    public static void Delete(string gameDirectory, string uid)
    {
        if (!ValidUid(uid)) throw new InvalidDataException("invalid_credentials");
        var loader = Path.Combine(gameDirectory, ModManager.LoaderFolderName);
        var root = Root(gameDirectory);
        var directory = Path.Combine(root, uid);
        var path = Path.Combine(directory, "account.json");
        // Remove only this exact file, never the UID directory or unrelated exports.
        foreach (var candidate in new[] { gameDirectory, loader, root, directory, path })
            if ((File.Exists(candidate) || Directory.Exists(candidate)) && IsLink(candidate))
                throw new InvalidDataException("invalid_credentials");
        File.Delete(path);
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
