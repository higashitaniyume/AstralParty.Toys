using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.LinuxHost;

internal static partial class LinuxLoader
{
    internal static LoaderBundle DescribeBundle()
    {
        // The build verifies the resource. Apply independently verifies it again before writing.
        using var archive = new ZipArchive(ReadResource(ReleaseArchive), ZipArchiveMode.Read);
        var mods = new List<string>();
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.Ordinal)) continue;
            var id = ModId(entry.FullName);
            if (id is null) continue;
            var member = entry.FullName.AsSpan(ActiveMods.Length + id.Length + 2);
            if (member.Length == id.Length + 4 && member.StartsWith(id, StringComparison.Ordinal))
                mods.Add(id);
        }
        mods.Sort(StringComparer.Ordinal);

        var config = archive.GetEntry(LoaderConfig);
        return new LoaderBundle(AvailableVersion, config is null ? "" : ValidateDefaultConfig(ReadEntry(config)).ToString(),
            archive.GetEntry(LoaderDll) is not null, config is not null, archive.GetEntry(LoaderSdk) is not null, mods);
    }

    internal static bool HasManagedManifest(string gameDirectory)
    {
        try
        {
            var path = Path.Combine(gameDirectory, ManifestPath);
            var bytes = InspectionFiles.ReadBytes(path);
            if (bytes is null) return false;
            var manifest = ReadManifest(bytes);
            return MatchesPublishedManifest(PinnedRelease.Value, manifest.Version, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed record LoaderBundle(string Version, string SdkVersion, bool LoaderPresent, bool ConfigPresent, bool SdkPresent,
    IReadOnlyList<string> Mods);
