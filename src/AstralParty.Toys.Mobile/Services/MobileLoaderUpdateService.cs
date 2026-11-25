using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Mobile.Targets;

namespace AstralParty.Toys.Mobile.Services;

public sealed record PreparedLoaderPackage(
    string Version,
    IReadOnlyDictionary<string, byte[]> Files,
    IReadOnlyList<string> Mods);

/// <summary>
/// 与安卓端 <c>ManagedLoaderUpdate</c> 同口径的移动端加载器更新服务：
/// 直接从官方 <c>cesium-loader.zip</c> 校验并提取托管 SDK 与内置模组，
/// 保留移动端专属模组（悬浮窗、变速、自由相机）、移动端引导、游戏注入状态、停用目录与玩家配置。
/// </summary>
public static partial class MobileLoaderUpdateService
{
    public const string OfficialSourceUrl = "https://github.com/higashitaniyume/CesiumLoader";

    public static readonly HashSet<string> ExcludedMods = new(StringComparer.OrdinalIgnoreCase)
    {
        "CesiumOverlayMod",
        "SpeedHackMod",
        "FreeCameraMod",
        "LanguageUnlockMod",
    };

    private static readonly HashSet<string> LicenseFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "CesiumLoader-LICENSE.txt",
        "CesiumLoader-NOTICE.txt",
    };

    private static readonly HashSet<string> PreservedModConfigNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "config.json",
        "mod.cfg",
    };

    private const int MaxEntries = 10_000;
    private const long MaxManifestBytes = 2L * 1024 * 1024;
    private const long MaxEntryBytes = 128L * 1024 * 1024;
    private const long MaxTotalUncompressedBytes = 1024L * 1024 * 1024;

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,128}$")]
    private static partial Regex SafeModIdRegex();

    public static PreparedLoaderPackage PreparePackage(
        byte[] zipBytes,
        string? expectedVersion = null,
        Action<string, int>? progress = null)
    {
        if (zipBytes is null || zipBytes.Length == 0)
            throw new InvalidDataException("加载器发布包为空。");

        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var fileEntries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        if (fileEntries.Count > MaxEntries)
            throw new InvalidDataException("加载器 ZIP 文件数过多。");

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var entry in fileEntries)
        {
            var normalized = NormalizeSafeZipPath(entry.FullName);
            if (!seenPaths.Add(normalized))
                throw new InvalidDataException($"加载器 ZIP 有重名路径：{normalized}");
            if (entry.Length is < 0 or > MaxEntryBytes)
                throw new InvalidDataException($"加载器 ZIP 文件大小无效：{normalized}");
            totalBytes += entry.Length;
            if (totalBytes > MaxTotalUncompressedBytes)
                throw new InvalidDataException("加载器 ZIP 展开体积过大。");
        }

        var manifestEntry = archive.GetEntry("cesium-loader.json")
                            ?? throw new InvalidDataException("加载器 ZIP 缺少发行清单（cesium-loader.json）。");
        if (manifestEntry.Length > MaxManifestBytes)
            throw new InvalidDataException("发行清单过大。");

        string version;
        Dictionary<string, string> hashes;
        using (var manifestStream = manifestEntry.Open())
        using (var doc = JsonDocument.Parse(manifestStream))
        {
            var root = doc.RootElement;
            version = (root.TryGetProperty("version", out var vEl) ? vEl.GetString() : null)?.Trim() ?? "";
            if (string.IsNullOrEmpty(version) || version.Length > 64)
                throw new InvalidDataException("发行清单缺少版本号。");
            if (!string.IsNullOrWhiteSpace(expectedVersion) &&
                !string.Equals(version, expectedVersion.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"加载器 ZIP 版本（{version}）与检查到的版本（{expectedVersion}）不一致。");
            }

            if (!root.TryGetProperty("files", out var filesEl) || filesEl.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("发行清单缺少文件校验。");

            hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in filesEl.EnumerateObject())
            {
                hashes[prop.Name.Replace('\\', '/')] = (prop.Value.GetString() ?? "").Trim().ToLowerInvariant();
            }
        }

        var extracted = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var mods = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < fileEntries.Count; i++)
        {
            var entry = fileEntries[i];
            if (fileEntries.Count > 0)
            {
                var pct = 10 + (int)((i + 1) * 80.0 / fileEntries.Count);
                progress?.Invoke($"正在校验并解压发布包 {i + 1} / {fileEntries.Count}", pct);
            }

            var entryPath = entry.FullName.Replace('\\', '/');
            if (!entryPath.StartsWith("AstralParty_ModLoader/", StringComparison.Ordinal))
                continue;

            var relative = entryPath["AstralParty_ModLoader/".Length..];
            if (relative.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = relative.Split('/');
            var isSdk = string.Equals(relative, MobilePayload.SdkRelative, StringComparison.Ordinal);
            var isLicense = LicenseFiles.Contains(relative);
            var isMod = parts.Length >= 3 &&
                        string.Equals(parts[0], MobilePaths.ModsRelative, StringComparison.Ordinal) &&
                        SafeModIdRegex().IsMatch(parts[1]) &&
                        !ExcludedMods.Contains(parts[1]);

            if (!isSdk && !isLicense && !isMod)
                continue;
            if (isMod && PreservedModConfigNames.Contains(parts[^1]))
                continue;

            if (!hashes.TryGetValue(entryPath, out var expectedHash) ||
                expectedHash.Length != 64 ||
                !IsLowerHex(expectedHash))
            {
                throw new InvalidDataException($"清单未覆盖或校验值无效：{entryPath}");
            }

            byte[] bytes;
            using (var es = entry.Open())
            using (var ms = new MemoryStream())
            {
                es.CopyTo(ms);
                bytes = ms.ToArray();
            }

            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException($"加载器文件校验失败：{relative}");

            extracted[relative] = bytes;
            if (isMod)
                mods.Add(parts[1]);
        }

        if (!extracted.ContainsKey(MobilePayload.SdkRelative) || mods.Count == 0)
            throw new InvalidDataException("加载器包缺少托管 SDK 或内置模组。");

        foreach (var modId in mods)
        {
            if (!extracted.ContainsKey($"{MobilePaths.ModsRelative}/{modId}/{modId}.dll"))
                throw new InvalidDataException($"模组 {modId} 缺少主体 DLL。");
        }

        progress?.Invoke("发布包校验完成", 95);
        return new PreparedLoaderPackage(version, extracted, mods.ToList());
    }

    public static async Task<MobileOperationResult> ApplyToDeviceAsync(
        IDeviceChannel channel,
        MobileTarget target,
        PreparedLoaderPackage package,
        string? localMobilePayloadPath = null,
        string source = OfficialSourceUrl,
        MobileOperationLog? log = null,
        Action<string, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var opened = await MobileDeviceAccess.TryOpenRootAsync(channel, target, log, cancellationToken)
            .ConfigureAwait(false);
        if (opened is null)
            return Fail(log, "找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, appId) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var android = !string.Equals(target.Kind, "ios", StringComparison.OrdinalIgnoreCase);

            progress?.Invoke($"在设备上准备 {loaderRoot}…", 5);
            await fileSystem.MakeDirectoriesAsync(loaderRoot, cancellationToken).ConfigureAwait(false);
            foreach (var dir in new[] { "bootstrap", "sdk", "mods", "disabled_mods", "skins", "logs", "data" })
                await fileSystem.MakeDirectoriesAsync($"{loaderRoot}/{dir}", cancellationToken).ConfigureAwait(false);

            var canCreate = await ProbeWritableAsync(fileSystem, loaderRoot, cancellationToken).ConfigureAwait(false);

            var enabledSet = await ListModEntryNamesAsync(fileSystem, $"{loaderRoot}/{MobilePaths.ModsRelative}", cancellationToken)
                .ConfigureAwait(false);
            var disabledSet = await ListModEntryNamesAsync(fileSystem, $"{loaderRoot}/{MobilePaths.DisabledModsRelative}", cancellationToken)
                .ConfigureAwait(false);

            var skippedMods = package.Mods
                .Where(id => HasModPresence(enabledSet, id) && HasModPresence(disabledSet, id))
                .ToList();
            var skippedSet = skippedMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var activeDisabled = disabledSet
                .Where(n => !n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(StripDllExtension)
                .Where(id => !skippedSet.Contains(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var planned = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var updatedMods = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (relative, bytes) in package.Files)
            {
                if (relative.StartsWith(MobilePaths.ModsRelative + "/", StringComparison.Ordinal))
                {
                    var rest = relative[(MobilePaths.ModsRelative.Length + 1)..];
                    var slash = rest.IndexOf('/');
                    var modId = slash >= 0 ? rest[..slash] : rest;
                    if (skippedSet.Contains(modId))
                        continue;

                    updatedMods.Add(modId);
                    var targetRelative = activeDisabled.Contains(modId)
                        ? $"{MobilePaths.DisabledModsRelative}/{rest}"
                        : relative;
                    planned[targetRelative] = bytes;
                }
                else
                {
                    planned[relative] = bytes;
                }
            }

            if (!string.IsNullOrEmpty(localMobilePayloadPath))
            {
                try
                {
                    var mobilePayload = MobilePayload.Read(localMobilePayloadPath);
                    var bootFile = mobilePayload.Files.FirstOrDefault(f =>
                        f.RelativePath.EndsWith(MobilePayload.BootstrapFile, StringComparison.Ordinal));
                    if (bootFile is not null)
                    {
                        var bootRelative = MobilePayload.DeviceRelativePath(bootFile.RelativePath, android);
                        planned[bootRelative] = mobilePayload.BytesOf(bootFile);
                    }
                }
                catch
                {
                    // 本地移动端载荷不存在或损坏时保留设备上既有引导，不阻断在线 SDK/模组更新
                }
            }

            if (!canCreate)
            {
                foreach (var relative in planned.Keys)
                {
                    if (!await fileSystem.ExistsAsync($"{loaderRoot}/{relative}", cancellationToken).ConfigureAwait(false))
                    {
                        if (await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                                .ConfigureAwait(false) is { } blocked)
                        {
                            return Fail(log, blocked);
                        }
                        break;
                    }
                }

                var manifestPathProbe = $"{loaderRoot}/installed.json";
                if (!await fileSystem.ExistsAsync(manifestPathProbe, cancellationToken).ConfigureAwait(false) &&
                    await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                        .ConfigureAwait(false) is { } manifestBlocked)
                {
                    return Fail(log, manifestBlocked);
                }
            }

            if (canCreate)
            {
                foreach (var modId in updatedMods)
                {
                    foreach (var flatRelative in new[]
                             {
                                 $"{MobilePaths.ModsRelative}/{modId}.dll",
                                 $"{MobilePaths.ModsRelative}/{modId}.json",
                                 $"{MobilePaths.DisabledModsRelative}/{modId}.dll",
                                 $"{MobilePaths.DisabledModsRelative}/{modId}.json",
                             })
                    {
                        var flatPath = $"{loaderRoot}/{flatRelative}";
                        if (await fileSystem.ExistsAsync(flatPath, cancellationToken).ConfigureAwait(false))
                            await fileSystem.DeleteAsync(flatPath, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            var ensuredDirs = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var (relative, bytes) in planned)
            {
                index++;
                var targetPath = $"{loaderRoot}/{relative}";
                var lastSlash = targetPath.LastIndexOf('/');
                if (lastSlash > loaderRoot.Length)
                {
                    var parentDir = targetPath[..lastSlash];
                    if (ensuredDirs.Add(parentDir))
                        await fileSystem.MakeDirectoriesAsync(parentDir, cancellationToken).ConfigureAwait(false);
                }

                await fileSystem.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
                var pct = 10 + (int)(index * 78.0 / Math.Max(1, planned.Count));
                progress?.Invoke($"正在更新加载器 {index} / {planned.Count}：{relative}", pct);
            }

            var manifestPath = $"{loaderRoot}/installed.json";
            var manifestBytes = await BuildUpdatedManifestAsync(
                fileSystem, manifestPath, package.Version, source, android, updatedMods, cancellationToken)
                .ConfigureAwait(false);
            progress?.Invoke("写入 installed.json…", 92);
            await fileSystem.WriteAllBytesAsync(manifestPath, manifestBytes, cancellationToken).ConfigureAwait(false);

            progress?.Invoke("回读校验…", 96);
            var mismatched = new List<string>();
            foreach (var (relative, bytes) in planned)
            {
                var info = await fileSystem.StatAsync($"{loaderRoot}/{relative}", cancellationToken).ConfigureAwait(false);
                if (info is null || info.Size != bytes.Length)
                    mismatched.Add($"{relative}({info?.Size.ToString() ?? "缺"}/{bytes.Length})");
            }

            var manifestStat = await fileSystem.StatAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            if (manifestStat is null || manifestStat.Size != manifestBytes.Length)
                mismatched.Add($"installed.json({manifestStat?.Size.ToString() ?? "缺"}/{manifestBytes.Length})");

            if (mismatched.Count > 0)
                return Fail(log, "回读校验失败（设备上的文件大小不符）：" + string.Join("、", mismatched.Take(8)));

            progress?.Invoke("加载器更新完成", 100);
            log?.Info($"移动端加载器已更新到 {package.Version}：包名={appId} 文件={planned.Count} " +
                      $"更新模组={updatedMods.Count} 跳过冲突={skippedMods.Count}");

            var msg = new StringBuilder($"加载器 {package.Version} 已更新（SDK + {updatedMods.Count} 个模组）；请完全重启游戏生效。移动端悬浮窗、变速与自由相机已保留。");
            if (skippedMods.Count > 0)
                msg.Append($"以下模组在启用与停用目录同时存在，已保留本地文件并跳过：{string.Join("、", skippedMods)}。");

            return new MobileOperationResult
            {
                Ok = true,
                Message = msg.ToString(),
                Detail = $"version={package.Version} files={planned.Count} mods={updatedMods.Count}" +
                         (skippedMods.Count > 0 ? $" skipped={string.Join(",", skippedMods)}" : ""),
            };
        }
    }

    private static async Task<byte[]> BuildUpdatedManifestAsync(
        IDeviceFileSystem fileSystem,
        string manifestPath,
        string version,
        string source,
        bool android,
        IEnumerable<string> updatedMods,
        CancellationToken cancellationToken)
    {
        JsonObject root;
        if (await fileSystem.ExistsAsync(manifestPath, cancellationToken).ConfigureAwait(false))
        {
            var raw = await fileSystem.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    root = JsonNode.Parse(raw) as JsonObject
                           ?? throw new InvalidDataException("installed.json 不是有效 JSON 对象。");
                }
                catch (JsonException)
                {
                    throw new InvalidDataException("installed.json 解析失败，已保留原文件。");
                }
            }
            else
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        root["sdkVersion"] = version;
        root["loaderVersion"] = version;
        if (!android || root["version"] is not null)
            root["version"] = version;
        root["source"] = source;
        root["updatedAt"] = DateTimeOffset.Now.ToString("o");

        var listKey = android ? "builtInMods" : "mods";
        var existingMods = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root[listKey] is JsonArray existingArr)
        {
            foreach (var item in existingArr)
            {
                if (item?.GetValue<string>() is { Length: > 0 } id)
                    existingMods.Add(id);
            }
        }
        foreach (var id in updatedMods)
            existingMods.Add(id);

        var nextArr = new JsonArray();
        foreach (var id in existingMods)
            nextArr.Add(id);
        root[listKey] = nextArr;

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            root.WriteTo(writer);
        }
        return buffer.ToArray();
    }

    private static async Task<bool> ProbeWritableAsync(
        IDeviceFileSystem fileSystem, string loaderRoot, CancellationToken cancellationToken)
    {
        var probe = $"{loaderRoot}/.aspatoys-write-probe";
        try
        {
            await fileSystem.WriteAllBytesAsync(probe, [], cancellationToken).ConfigureAwait(false);
            if (await fileSystem.StatAsync(probe, cancellationToken).ConfigureAwait(false) is not null)
            {
                await fileSystem.DeleteAsync(probe, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }
        catch
        {
            // Expected on game-owned 2750 directories.
        }
        return false;
    }

    private static async Task<HashSet<string>> ListModEntryNamesAsync(
        IDeviceFileSystem fileSystem, string dirPath, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var item in await fileSystem.ListAsync(dirPath, cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrWhiteSpace(item))
                    set.Add(item.Trim());
            }
        }
        catch
        {
            // Missing mods/ or disabled_mods/ is valid on clean devices.
        }
        return set;
    }

    private static bool HasModPresence(HashSet<string> entries, string modId) =>
        entries.Contains(modId) || entries.Contains(modId + ".dll");

    private static string StripDllExtension(string name) =>
        name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    private static string NormalizeSafeZipPath(string rawPath)
    {
        var normalized = rawPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.StartsWith('/') ||
            normalized.Any(c => c == ':' || c < 32) ||
            normalized.Split('/').Any(seg => seg is "" or "." or ".."))
        {
            throw new InvalidDataException($"加载器 ZIP 含不安全路径：{rawPath}");
        }
        return normalized;
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var ch in value)
        {
            if (ch is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }
        return true;
    }

    private static MobileOperationResult Fail(MobileOperationLog? log, string message)
    {
        log?.Error(message);
        return MobileOperationResult.Fail(message);
    }
}
