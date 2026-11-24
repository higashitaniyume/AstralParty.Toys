using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstralParty.Toys.Mobile.Targets;

/// <summary>Per-target installation record and original code-bundle backups, kept under the Toys data root.</summary>
public sealed class MobileInstallStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _root;
    private readonly object _gate = new();

    public MobileInstallStore(string root)
    {
        _root = root;
    }

    public string StatePath => Path.Combine(_root, "install-state.json");

    public string BackupDirectory => Path.Combine(_root, "backups");

    public string WorkDirectory => Path.Combine(_root, "work");

    public string BackupPath(string inner) => Path.Combine(BackupDirectory, inner + ".data");

    public InstallState Load()
    {
        lock (_gate)
        {
            if (!File.Exists(StatePath))
                return new InstallState();
            try
            {
                return JsonSerializer.Deserialize<InstallState>(File.ReadAllBytes(StatePath), JsonOptions)
                       ?? new InstallState();
            }
            catch (JsonException)
            {
                return new InstallState();
            }
        }
    }

    public TargetInstallState? Get(string targetId) =>
        Load().Targets.GetValueOrDefault(targetId);

    public void Update(string targetId, Action<TargetInstallState> mutate)
    {
        lock (_gate)
        {
            var state = File.Exists(StatePath)
                ? JsonSerializer.Deserialize<InstallState>(File.ReadAllBytes(StatePath), JsonOptions) ?? new InstallState()
                : new InstallState();
            if (!state.Targets.TryGetValue(targetId, out var record))
            {
                record = new TargetInstallState();
                state.Targets[targetId] = record;
            }
            mutate(record);
            WriteAtomic(StatePath, JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions));
        }
    }

    public bool HasBackup(string inner) => File.Exists(BackupPath(inner));

    public byte[] ReadBackup(string inner) => File.ReadAllBytes(BackupPath(inner));

    /// <summary>Idempotent: never overwrite an existing original snapshot with an injected bundle.</summary>
    public string SaveBackup(string inner, byte[] original)
    {
        var path = BackupPath(inner);
        Directory.CreateDirectory(BackupDirectory);
        if (!File.Exists(path))
            WriteAtomic(path, original);
        return path;
    }

    public void DeleteBackup(string inner)
    {
        var path = BackupPath(inner);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }
}

public sealed class InstallState
{
    [JsonPropertyName("targets")]
    public Dictionary<string, TargetInstallState> Targets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TargetInstallState
{
    [JsonPropertyName("bundleOuter")]
    public string BundleOuter { get; set; } = "";

    [JsonPropertyName("catalogBuildHash")]
    public string CatalogBuildHash { get; set; } = "";

    [JsonPropertyName("installed")]
    public InstalledBundleState Installed { get; set; } = new();

    [JsonPropertyName("payload")]
    public PayloadState Payload { get; set; } = new();
}

public sealed class InstalledBundleState
{
    [JsonPropertyName("inner")]
    public string Inner { get; set; } = "";

    [JsonPropertyName("originalSha256")]
    public string OriginalSha256 { get; set; } = "";

    [JsonPropertyName("patchedSha256")]
    public string PatchedSha256 { get; set; } = "";

    [JsonPropertyName("at")]
    public string At { get; set; } = "";
}

public sealed class PayloadState
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; } = "";

    [JsonPropertyName("at")]
    public string At { get; set; } = "";
}
