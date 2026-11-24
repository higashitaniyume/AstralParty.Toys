using System.Text.Json;
using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Targets;

/// <summary>
/// Persists discovered devices and the active target. Deliberately separate from
/// game-profiles.json / modloader-state.json so a mobile target can never be mistaken for a
/// local game directory by the rest of Toys.
/// </summary>
public sealed class MobileTargetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _stateDirectory;
    private readonly object _gate = new();

    public MobileTargetStore(string stateDirectory)
    {
        _stateDirectory = stateDirectory;
    }

    public string TargetsPath => Path.Combine(_stateDirectory, "mobile-targets.json");

    public string ActivePath => Path.Combine(_stateDirectory, "active-target.json");

    public IReadOnlyList<MobileTarget> LoadTargets()
    {
        lock (_gate)
        {
            return Read<MobileTargetList>(TargetsPath)?.Targets ?? [];
        }
    }

    public void SaveTargets(IEnumerable<MobileTarget> targets)
    {
        lock (_gate)
        {
            Write(TargetsPath, new MobileTargetList { Targets = targets.ToList() });
        }
    }

    public ActiveTargetState LoadActive()
    {
        lock (_gate)
        {
            return Read<ActiveTargetState>(ActivePath) ?? new ActiveTargetState();
        }
    }

    public void SetActiveLocal(string profileId)
    {
        lock (_gate)
        {
            Write(ActivePath, new ActiveTargetState { Kind = "local", ProfileId = profileId });
        }
    }

    public void SetActiveMobile(string targetId)
    {
        lock (_gate)
        {
            Write(ActivePath, new ActiveTargetState { Kind = "mobile", TargetId = targetId });
        }
    }

    public void Upsert(MobileTarget target)
    {
        lock (_gate)
        {
            var current = Read<MobileTargetList>(TargetsPath)?.Targets ?? [];
            var index = current.FindIndex(t =>
                string.Equals(t.Id, target.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                current[index] = target;
            else
                current.Add(target);
            Write(TargetsPath, new MobileTargetList { Targets = current });
        }
    }

    public void MarkOffline(string targetId)
    {
        lock (_gate)
        {
            var current = Read<MobileTargetList>(TargetsPath)?.Targets ?? [];
            var target = current.FirstOrDefault(t =>
                string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
                return;
            target.Online = false;
            Write(TargetsPath, new MobileTargetList { Targets = current });
        }
    }

    private static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Write<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private sealed class MobileTargetList
    {
        public List<MobileTarget> Targets { get; set; } = [];
    }
}
