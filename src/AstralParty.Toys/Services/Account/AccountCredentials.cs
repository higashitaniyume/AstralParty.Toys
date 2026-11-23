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

    // The Mod captures the endpoint the running game is connected to, and the vendor resolves it from
    // its own remote config (CN: se-jump-cn-01.feimogames.com, international: game-int-01.star-engine.com).
    // Toys keeps no host list: whatever the credential names is dialed as written, and this only rejects
    // values that cannot be a host name at all.
    private static readonly Regex HostPattern = new(@"\A[A-Za-z0-9._:-]{1,255}\z", RegexOptions.CultureInvariant);

    public static bool ValidEndpoint(string? host, int port) =>
        port is >= 1 and <= 65535 && host != null && HostPattern.IsMatch(host);

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
            if (value == null || !ValidEndpoint(value.Host, value.Port) || !ValidRegion(value.Region) ||
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
