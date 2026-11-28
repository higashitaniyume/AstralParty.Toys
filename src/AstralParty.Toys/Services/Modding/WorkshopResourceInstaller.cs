using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.ResourceMods;
using AstralParty.Toys.Services.Skins;

namespace AstralParty.Toys.Services.Modding;

public sealed class InstalledWorkshopResource
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Version { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
}

public sealed record WorkshopResourceInstallResult(
    string ResourceId,
    string Type,
    string Version,
    string TargetId,
    string Name,
    string JumpPage,
    string Message,
    bool RequiresRestart);

/// <summary>
/// 社区资源模组本地安装器：对下载并通过校验的资源 ZIP 进行目录规范化（剥离单层顶层文件夹与
/// <c>resource.json</c>、解析 <c>contentRoot</c>），按资源类型路由到 Tools 对应皮肤/音效/背景/RML 管理器，
/// 并维护已安装映射以支持列表状态展示与原地更新。
/// </summary>
public sealed class WorkshopResourceInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SkinManager _skinManager;
    private readonly CharacterSkinManager _characterSkins;
    private readonly Func<CharacterSkinSchemeManager> _schemeManagerFactory;
    private readonly AudioSkinSchemeManager _audioSkins;
    private readonly ResourceModManager _resourceMods;
    private readonly HomeBackgroundService _homeBackground;

    public WorkshopResourceInstaller(
        SkinManager skinManager,
        CharacterSkinManager characterSkins,
        Func<CharacterSkinSchemeManager> schemeManagerFactory,
        AudioSkinSchemeManager audioSkins,
        ResourceModManager resourceMods,
        HomeBackgroundService homeBackground)
    {
        _skinManager = skinManager;
        _characterSkins = characterSkins;
        _schemeManagerFactory = schemeManagerFactory;
        _audioSkins = audioSkins;
        _resourceMods = resourceMods;
        _homeBackground = homeBackground;
    }

    public static string RegistryPath(string gameDirectory)
        => Path.Combine(gameDirectory, "AstralParty_ModLoader", "data", "workshop-resources.json");

    /// <summary>读取当前游戏目录下通过市场安装且仍在磁盘上的资源记录。</summary>
    public Dictionary<string, InstalledWorkshopResource> LoadInstalled(string? gameDirectory)
    {
        var result = new Dictionary<string, InstalledWorkshopResource>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            return result;

        var path = RegistryPath(gameDirectory);
        if (!File.Exists(path)) return result;

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, InstalledWorkshopResource>>(
                File.ReadAllText(path, Encoding.UTF8), JsonOptions);
            if (raw is null) return result;

            foreach (var (key, item) in raw)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Id)) continue;
                if (!IsInstalledTargetPresent(gameDirectory, item)) continue;
                result[item.Id] = item;
            }
        }
        catch
        {
            // 清单损坏时按空返回，不阻断市场浏览。
        }

        return result;
    }

    /// <summary>将资源模组 ZIP 安装到指定游戏目录并持久化安装记录。</summary>
    public WorkshopResourceInstallResult Install(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        string zipPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new InvalidOperationException("尚未选择有效的游戏目录，请先在「设置 → 游戏目录」添加。");
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("找不到下载的资源压缩包。", zipPath);

        var installedMap = LoadInstalledRaw(gameDirectory);
        installedMap.TryGetValue(detail.Id, out var existing);

        WorkshopResourceInstallResult result = detail.Type.Trim().ToLowerInvariant() switch
        {
            "card_skin" => InstallCardSkin(gameDirectory, detail, version, zipPath, existing, cancellationToken),
            "character_skin" => InstallCharacterSkin(gameDirectory, detail, version, zipPath, cancellationToken),
            "audio_skin" => InstallAudioSkin(gameDirectory, detail, version, zipPath, existing, cancellationToken),
            "rml_bundle" => InstallRmlBundle(gameDirectory, detail, version, zipPath, existing, cancellationToken),
            "home_background" => InstallHomeBackground(gameDirectory, detail, version, zipPath, cancellationToken),
            _ => throw new InvalidOperationException(
                $"当前版本的工具箱暂不支持直接安装此资源类型（{detail.Type}），请更新工具箱或前往网页手动下载。")
        };

        installedMap[detail.Id] = new InstalledWorkshopResource
        {
            Id = detail.Id,
            Type = detail.Type,
            Version = version.Version,
            TargetId = result.TargetId,
            Name = result.Name,
            Sha256 = version.Sha256,
            InstalledAt = DateTimeOffset.UtcNow
        };
        SaveInstalled(gameDirectory, installedMap);
        return result;
    }

    /// <summary>清理或规范化卡面 ZIP（剥离顶层外框与 resource.json、补全 skin.json）以供移动端安装。</summary>
    public static void PrepareCleanCardSkinZip(string sourceZipPath, WorkshopResourceDetail detail, string outputZipPath)
    {
        using var source = CompatZipArchive.OpenRead(sourceZipPath);
        var layout = AnalyzeLayout(source);
        var entries = CollectContentEntries(source, layout.ContentPrefix);
        var skinJsonEntry = entries.FirstOrDefault(e =>
            Path.GetFileName(e.RelativePath).Equals("skin.json", StringComparison.OrdinalIgnoreCase));
        var basePrefix = skinJsonEntry is null ? "" : GetParentPrefix(skinJsonEntry.RelativePath);

        var hasMedia = false;
        var hasSkinJson = false;
        var dirName = SanitizeFolderId(detail.Id, "card_skin");

        if (File.Exists(outputZipPath)) File.Delete(outputZipPath);
        using var output = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            if (basePrefix.Length > 0 && !item.RelativePath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var rel = item.RelativePath[basePrefix.Length..];
            if (rel.Length == 0 || IsIgnoredMetadataFile(rel)) continue;
            if (rel.Equals("skin.json", StringComparison.OrdinalIgnoreCase)) hasSkinJson = true;
            if (IsCardSkinMedia(rel)) hasMedia = true;

            var dst = output.CreateEntry($"{dirName}/{rel}", CompressionLevel.Optimal);
            using var srcStream = item.Entry.Open();
            using var dstStream = dst.Open();
            srcStream.CopyTo(dstStream);
        }

        if (!hasMedia)
            throw new InvalidOperationException("该卡面资源包中没有找到图片或动画素材。");

        if (!hasSkinJson)
        {
            var metaJson = JsonSerializer.Serialize(new
            {
                name = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name,
                author = detail.Author ?? "",
                description = detail.Summary ?? ""
            }, JsonOptions);
            using var writer = new StreamWriter(output.CreateEntry($"{dirName}/skin.json").Open(), new UTF8Encoding(false));
            writer.Write(metaJson);
        }
    }

    // ============================== 各类型安装路由 ==============================

    private WorkshopResourceInstallResult InstallCardSkin(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        string zipPath,
        InstalledWorkshopResource? existing,
        CancellationToken ct)
    {
        using var archive = CompatZipArchive.OpenRead(zipPath);
        var layout = AnalyzeLayout(archive);
        var entries = CollectContentEntries(archive, layout.ContentPrefix);
        if (entries.Count == 0)
            throw new InvalidOperationException("压缩包内容为空。");

        var skinsRoot = _skinManager.GetSkinsDirectory(gameDirectory);
        Directory.CreateDirectory(skinsRoot);
        ImportLimits.EnsureFreeSpace(skinsRoot, entries.Sum(e => e.Entry.Length));

        // 优先查找 skin.json 所在前缀；也兼容传统 AstralParty_ModLoader/skins/<name>/... 布局
        var skinJson = entries.FirstOrDefault(e =>
            Path.GetFileName(e.RelativePath).Equals("skin.json", StringComparison.OrdinalIgnoreCase));
        var innerPrefix = skinJson is not null ? GetParentPrefix(skinJson.RelativePath) : "";

        // 确定方案目录名：更新已有资源时沿用 TargetId 实现原地更新
        var targetId = existing is not null && !string.IsNullOrWhiteSpace(existing.TargetId)
            ? existing.TargetId
            : ChooseCardSkinTargetId(skinsRoot, detail.Id);

        var targetDir = Path.Combine(skinsRoot, targetId);
        var stagingDir = Path.Combine(skinsRoot, ".workshop-import-" + Guid.NewGuid().ToString("N"));
        var backupDir = Path.Combine(skinsRoot, ".workshop-old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);

        try
        {
            var hasMedia = false;
            var hasSkinJson = false;
            foreach (var item in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (innerPrefix.Length > 0 &&
                    !item.RelativePath.StartsWith(innerPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var rel = item.RelativePath[innerPrefix.Length..];
                if (rel.Length == 0 || IsIgnoredMetadataFile(rel)) continue;
                if (rel.Equals("skin.json", StringComparison.OrdinalIgnoreCase)) hasSkinJson = true;
                if (IsCardSkinMedia(rel)) hasMedia = true;

                var destPath = CharacterSkinRules.SafePath(stagingDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                item.Entry.ExtractToFile(destPath, overwrite: true);
            }

            if (!hasMedia)
                throw new InvalidOperationException("该卡面资源包中没有可识别的图片或动画文件。");

            if (!hasSkinJson)
            {
                var metaJson = JsonSerializer.Serialize(new
                {
                    name = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name,
                    author = detail.Author ?? "",
                    description = detail.Summary ?? ""
                }, JsonOptions);
                File.WriteAllText(Path.Combine(stagingDir, "skin.json"), metaJson, new UTF8Encoding(false));
            }

            if (Directory.Exists(targetDir)) Directory.Move(targetDir, backupDir);
            try
            {
                Directory.Move(stagingDir, targetDir);
                if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            }
            catch
            {
                if (!Directory.Exists(targetDir) && Directory.Exists(backupDir))
                    Directory.Move(backupDir, targetDir);
                throw;
            }
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }

        var displayName = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name;
        return new WorkshopResourceInstallResult(
            detail.Id,
            detail.Type,
            version.Version,
            targetId,
            displayName,
            "skins",
            $"{displayName} v{version.Version} 已安装到卡面方案「{targetId}」，可前往「自定义 → 卡牌皮肤」启用。",
            RequiresRestart: false);
    }

    private WorkshopResourceInstallResult InstallCharacterSkin(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        string zipPath,
        CancellationToken ct)
    {
        using var archive = CompatZipArchive.OpenRead(zipPath);
        var layout = AnalyzeLayout(archive);
        var entries = CollectContentEntries(archive, layout.ContentPrefix);
        if (entries.Count == 0)
            throw new InvalidOperationException("角色皮肤资源包为空。");

        var schemeEntry = entries.FirstOrDefault(e =>
            Path.GetFileName(e.RelativePath).Equals("character-scheme.json", StringComparison.OrdinalIgnoreCase));
        if (schemeEntry is not null)
        {
            return InstallCharacterSchemePack(gameDirectory, detail, version, entries, schemeEntry, ct);
        }

        var singleEntry = entries.FirstOrDefault(e =>
            Path.GetFileName(e.RelativePath).Equals("character-skin.json", StringComparison.OrdinalIgnoreCase));
        if (singleEntry is null)
            throw new InvalidOperationException("角色皮肤包里找不到 character-skin.json 或 character-scheme.json。");

        var prefix = GetParentPrefix(singleEntry.RelativePath);
        CharacterSkinManifest manifest;
        using (var reader = new StreamReader(singleEntry.Entry.Open()))
        {
            manifest = JsonSerializer.Deserialize<CharacterSkinManifest>(
                reader.ReadToEnd(), CharacterSkinManager.JsonOptions)
                ?? throw new InvalidOperationException("character-skin.json 清单为空。");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id) || !Regex.IsMatch(manifest.Id, "^[A-Za-z0-9_-]{1,80}$"))
            manifest.Id = SanitizeFolderId(detail.Id, "character_skin", allowDot: false);
        if (string.IsNullOrWhiteSpace(manifest.Name))
            manifest.Name = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name;

        var root = _characterSkins.Root(gameDirectory);
        Directory.CreateDirectory(root);
        ImportLimits.EnsureFreeSpace(root, entries.Sum(e => e.Entry.Length));

        var target = _characterSkins.Package(gameDirectory, manifest.Id);
        var staging = Path.Combine(root, ".workshop-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (prefix.Length > 0 && !item.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                var rel = item.RelativePath[prefix.Length..];
                if (rel.Length == 0 || IsIgnoredMetadataFile(rel)) continue;

                var dst = CharacterSkinRules.SafePath(staging, rel);
                if (!seen.Add(dst)) throw new InvalidOperationException("压缩包存在重复文件路径。");
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                item.Entry.ExtractToFile(dst, overwrite: true);
            }

            CharacterSkinRules.Validate(manifest, staging, true);
            File.WriteAllText(
                Path.Combine(staging, "character-skin.json"),
                JsonSerializer.Serialize(manifest, CharacterSkinManager.JsonOptions));

            if (Directory.Exists(target))
            {
                var trash = Path.Combine(root, ".deleted");
                Directory.CreateDirectory(trash);
                Directory.Move(target, Path.Combine(trash, manifest.Id + "-" + Guid.NewGuid().ToString("N")));
            }
            Directory.Move(staging, target);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }

        var displayName = string.IsNullOrWhiteSpace(detail.Name) ? manifest.Name : detail.Name;
        return new WorkshopResourceInstallResult(
            detail.Id,
            detail.Type,
            version.Version,
            manifest.Id,
            displayName,
            "characterSkins",
            $"{displayName} v{version.Version} 已安装到「角色皮肤」（方案 {manifest.Id}），前往「自定义 → 角色皮肤」启用即可。",
            RequiresRestart: false);
    }

    private WorkshopResourceInstallResult InstallCharacterSchemePack(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        List<NormalizedZipEntry> entries,
        NormalizedZipEntry schemeEntry,
        CancellationToken ct)
    {
        var prefix = GetParentPrefix(schemeEntry.RelativePath);
        CharacterSkinScheme parsedScheme;
        using (var reader = new StreamReader(schemeEntry.Entry.Open()))
        {
            parsedScheme = JsonSerializer.Deserialize<CharacterSkinScheme>(
                reader.ReadToEnd(), CharacterSkinManager.JsonOptions)
                ?? throw new InvalidOperationException("character-scheme.json 清单为空。");
        }

        var tempZip = Path.Combine(Path.GetTempPath(), $"ap-charscheme-{Guid.NewGuid():N}.zip");
        try
        {
            using (var cleanZip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                foreach (var item in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (prefix.Length > 0 && !item.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var rel = item.RelativePath[prefix.Length..];
                    if (rel.Length == 0 || IsIgnoredMetadataFile(rel)) continue;
                    var dst = cleanZip.CreateEntry(rel, CompressionLevel.Optimal);
                    using var srcStream = item.Entry.Open();
                    using var dstStream = dst.Open();
                    srcStream.CopyTo(dstStream);
                }
            }

            var schemeManager = _schemeManagerFactory();
            if (Directory.Exists(schemeManager.SchemeDirectory(gameDirectory, parsedScheme.Id)))
                schemeManager.Delete(gameDirectory, parsedScheme.Id);

            // 若方案引用的自定义包已存在，先移至 .deleted 以免导入报错
            var trash = Path.Combine(_characterSkins.Root(gameDirectory), ".deleted");
            foreach (var pkgId in parsedScheme.Rules.Select(r => r.CustomPackageId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pkgDir = _characterSkins.Package(gameDirectory, pkgId);
                if (Directory.Exists(pkgDir))
                {
                    Directory.CreateDirectory(trash);
                    Directory.Move(pkgDir, Path.Combine(trash, pkgId + "-" + Guid.NewGuid().ToString("N")));
                }
            }

            var importedId = schemeManager.Import(gameDirectory, tempZip);
            var displayName = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name;
            return new WorkshopResourceInstallResult(
                detail.Id,
                detail.Type,
                version.Version,
                importedId,
                displayName,
                "characterSkins",
                $"{displayName} v{version.Version} 已安装到「角色方案」（{importedId}），前往「自定义 → 角色皮肤」启用即可。",
                RequiresRestart: false);
        }
        finally
        {
            TryDeleteFile(tempZip);
        }
    }

    private WorkshopResourceInstallResult InstallAudioSkin(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        string zipPath,
        InstalledWorkshopResource? existing,
        CancellationToken ct)
    {
        using var archive = CompatZipArchive.OpenRead(zipPath);
        var layout = AnalyzeLayout(archive);
        var entries = CollectContentEntries(archive, layout.ContentPrefix);
        if (entries.Count == 0)
            throw new InvalidOperationException("音效资源包为空。");

        var manifestEntry = entries.FirstOrDefault(e =>
                                Path.GetFileName(e.RelativePath).Equals("audio-scheme.json", StringComparison.OrdinalIgnoreCase))
                            ?? entries.FirstOrDefault(e =>
                                Path.GetFileName(e.RelativePath).Equals("scheme.json", StringComparison.OrdinalIgnoreCase));
        var innerPrefix = manifestEntry is not null ? GetParentPrefix(manifestEntry.RelativePath) : "";

        AudioSkinScheme? parsed = null;
        if (manifestEntry is not null)
        {
            using var reader = new StreamReader(manifestEntry.Entry.Open());
            parsed = JsonSerializer.Deserialize<AudioSkinScheme>(reader.ReadToEnd(), JsonOptions);
        }

        var root = AudioSkinSchemeManager.Root(gameDirectory);
        Directory.CreateDirectory(root);
        ImportLimits.EnsureFreeSpace(root, entries.Sum(e => e.Entry.Length));

        var rawCandidateId = existing is not null && !string.IsNullOrWhiteSpace(existing.TargetId)
            ? existing.TargetId
            : !string.IsNullOrWhiteSpace(parsed?.Id) ? parsed!.Id : detail.Id;
        var schemeId = SanitizeFolderId(rawCandidateId, "audio_skin");
        var targetDir = AudioSkinSchemeManager.SchemeDirectory(gameDirectory, schemeId);
        schemeId = Path.GetFileName(targetDir);

        var staging = Path.Combine(root, ".workshop-import-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(root, ".workshop-old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            foreach (var item in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (innerPrefix.Length > 0 && !item.RelativePath.StartsWith(innerPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                var rel = item.RelativePath[innerPrefix.Length..];
                if (rel.Length == 0 || IsIgnoredMetadataFile(rel)) continue;
                if (rel.Equals("scheme.json", StringComparison.OrdinalIgnoreCase)) rel = "audio-scheme.json";

                var dest = CharacterSkinRules.SafePath(staging, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                item.Entry.ExtractToFile(dest, overwrite: true);
            }

            var manifest = parsed ?? new AudioSkinScheme();
            manifest.Id = schemeId;
            if (string.IsNullOrWhiteSpace(manifest.Name))
                manifest.Name = string.IsNullOrWhiteSpace(detail.Name) ? schemeId : detail.Name;
            if (string.IsNullOrWhiteSpace(manifest.Author))
                manifest.Author = detail.Author ?? "";
            if (string.IsNullOrWhiteSpace(manifest.Description))
                manifest.Description = detail.Summary ?? "";

            File.WriteAllText(
                Path.Combine(staging, "audio-scheme.json"),
                JsonSerializer.Serialize(manifest, JsonOptions));

            if (Directory.Exists(targetDir)) Directory.Move(targetDir, backup);
            try
            {
                Directory.Move(staging, targetDir);
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            catch
            {
                if (!Directory.Exists(targetDir) && Directory.Exists(backup))
                    Directory.Move(backup, targetDir);
                throw;
            }
        }
        finally
        {
            TryDeleteDirectory(staging);
        }

        var displayName = string.IsNullOrWhiteSpace(detail.Name) ? schemeId : detail.Name;
        return new WorkshopResourceInstallResult(
            detail.Id,
            detail.Type,
            version.Version,
            schemeId,
            displayName,
            "audioSkins",
            $"{displayName} v{version.Version} 已安装到「语音与音效」（方案 {schemeId}）。",
            RequiresRestart: false);
    }

    private WorkshopResourceInstallResult InstallRmlBundle(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        string zipPath,
        InstalledWorkshopResource? existing,
        CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ap-rml-workshop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var namedZip = Path.Combine(tempDir, SanitizeFolderId(detail.Id, "rml_bundle") + ".zip");

        try
        {
            using (var source = CompatZipArchive.OpenRead(zipPath))
            {
                var layout = AnalyzeLayout(source);
                var entries = CollectContentEntries(source, layout.ContentPrefix);
                if (entries.Count == 0)
                    throw new InvalidOperationException("RML 资源模组包为空。");

                using var output = ZipFile.Open(namedZip, ZipArchiveMode.Create);
                foreach (var item in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsIgnoredMetadataFile(item.RelativePath)) continue;
                    var dst = output.CreateEntry(item.RelativePath, CompressionLevel.Optimal);
                    using var srcStream = item.Entry.Open();
                    using var dstStream = dst.Open();
                    srcStream.CopyTo(dstStream);
                }
            }

            if (existing is not null && !string.IsNullOrWhiteSpace(existing.TargetId))
            {
                try
                {
                    if (!SpeedhackManager.IsGameRunning())
                        _resourceMods.Remove(gameDirectory, existing.TargetId, cancellationToken: ct);
                }
                catch
                {
                    // 旧包清理失败不影响继续导入新包
                }
            }

            var importResult = _resourceMods.Import(gameDirectory, namedZip, cancellationToken: ct);
            var displayName = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name;
            return new WorkshopResourceInstallResult(
                detail.Id,
                detail.Type,
                version.Version,
                importResult.Package.Id,
                displayName,
                "resourceMods",
                $"{displayName} v{version.Version} 已导入资源模组列表；请前往「自定义 → 资源模组」点击「应用」写入游戏目录后生效。",
                RequiresRestart: true);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private WorkshopResourceInstallResult InstallHomeBackground(
        string gameDirectory,
        WorkshopResourceDetail detail,
        WorkshopResourceVersion version,
        string zipPath,
        CancellationToken ct)
    {
        using var archive = CompatZipArchive.OpenRead(zipPath);
        var layout = AnalyzeLayout(archive);
        var entries = CollectContentEntries(archive, layout.ContentPrefix)
            .Where(e => !IsIgnoredMetadataFile(e.RelativePath))
            .ToList();
        if (entries.Count == 0)
            throw new InvalidOperationException("主页背景包内没有可用的图片或视频文件。");

        var declaredEntry = TryGetMetaString(version.Meta, "entry")
                            ?? TryGetMetaString(version.Meta, "file")
                            ?? TryGetMetaString(layout.Meta, "entry")
                            ?? TryGetMetaString(layout.Meta, "file");

        NormalizedZipEntry? media = null;
        if (!string.IsNullOrWhiteSpace(declaredEntry))
        {
            var normalizedDeclared = declaredEntry.Replace('\\', '/').TrimStart('/');
            media = entries.FirstOrDefault(e =>
                e.RelativePath.Equals(normalizedDeclared, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(e.RelativePath).Equals(Path.GetFileName(normalizedDeclared), StringComparison.OrdinalIgnoreCase));
        }

        media ??= entries.FirstOrDefault(e =>
            HomeBackgroundService.VideoExtensions.Contains(Path.GetExtension(e.RelativePath), StringComparer.OrdinalIgnoreCase));
        media ??= entries.FirstOrDefault(e =>
            HomeBackgroundService.ImageExtensions.Contains(Path.GetExtension(e.RelativePath), StringComparer.OrdinalIgnoreCase));

        if (media is null)
            throw new InvalidOperationException("主页背景包内没有找到支持的图片（PNG/JPG/WEBP）或视频（MP4/WEBM）。");

        var ext = Path.GetExtension(media.RelativePath).ToLowerInvariant();
        var tempMedia = Path.Combine(Path.GetTempPath(), $"ap-homebg-{Guid.NewGuid():N}{ext}");
        try
        {
            ct.ThrowIfCancellationRequested();
            ImportLimits.EnsureFreeSpace(Path.GetTempPath(), media.Entry.Length);
            media.Entry.ExtractToFile(tempMedia, overwrite: true);

            var isVideo = HomeBackgroundService.VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            if (isVideo) _homeBackground.SetVideo(gameDirectory, tempMedia);
            else _homeBackground.SetImage(gameDirectory, tempMedia);

            var displayName = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name;
            return new WorkshopResourceInstallResult(
                detail.Id,
                detail.Type,
                version.Version,
                HomeBackgroundService.ModId,
                displayName,
                "homeBackground",
                isVideo
                    ? $"{displayName} v{version.Version} 已设为主页动态视频背景，重启游戏后生效。"
                    : $"{displayName} v{version.Version} 已设为主页图片背景。",
                RequiresRestart: isVideo);
        }
        finally
        {
            TryDeleteFile(tempMedia);
        }
    }

    // ============================== ZIP 结构解析 & 持久化辅助 ==============================

    private sealed record ZipLayout(string ContentPrefix, Dictionary<string, JsonElement> Meta);
    private sealed record NormalizedZipEntry(ZipArchiveEntry Entry, string RelativePath);

    private static ZipLayout AnalyzeLayout(ZipArchive archive)
    {
        var files = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && !e.FullName.EndsWith('/'))
            .Select(e => (Entry: e, Path: e.FullName.Replace('\\', '/').TrimStart('/')))
            .Where(x => x.Path.Length > 0 && !IsSystemJunk(x.Path))
            .ToList();

        ValidateArchivePaths(archive);

        var resourceJson = files.FirstOrDefault(x =>
            Path.GetFileName(x.Path).Equals("resource.json", StringComparison.OrdinalIgnoreCase));

        var basePrefix = "";
        if (resourceJson.Entry is not null)
        {
            basePrefix = GetParentPrefix(resourceJson.Path);
        }
        else
        {
            var topDirs = files
                .Select(x => x.Path.Split('/')[0])
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (topDirs.Count == 1 && files.All(x => x.Path.Contains('/')))
                basePrefix = topDirs[0] + "/";
        }

        var meta = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var contentPrefix = basePrefix;
        if (resourceJson.Entry is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(resourceJson.Entry.Open());
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("contentRoot", out var cr) && cr.ValueKind == JsonValueKind.String)
                    {
                        var relRoot = NormalizeSubFolder(cr.GetString());
                        if (relRoot.Length > 0) contentPrefix = basePrefix + relRoot + "/";
                    }

                    if (doc.RootElement.TryGetProperty("meta", out var metaEl) && metaEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in metaEl.EnumerateObject())
                            meta[prop.Name] = prop.Value.Clone();
                    }
                }
            }
            catch
            {
                // 无法解析 resource.json 时按默认目录根处理
            }
        }

        return new ZipLayout(contentPrefix, meta);
    }

    private static List<NormalizedZipEntry> CollectContentEntries(ZipArchive archive, string contentPrefix)
    {
        var result = new List<NormalizedZipEntry>();
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/')) continue;
            var full = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (IsSystemJunk(full)) continue;
            if (contentPrefix.Length > 0 && !full.StartsWith(contentPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rel = full[contentPrefix.Length..];
            if (rel.Length == 0) continue;
            result.Add(new NormalizedZipEntry(entry, rel));
        }

        return result;
    }

    private static void ValidateArchivePaths(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Contains(':')
                || normalized.Split('/').Any(seg => seg is ".." or "."))
            {
                throw new InvalidOperationException("资源压缩包包含不安全的路径。");
            }
        }
    }

    private static string NormalizeSubFolder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var clean = raw.Replace('\\', '/').Trim().Trim('/');
        if (clean is "" or ".") return "";
        if (clean.Split('/').Any(s => s is "" or "." or ".."))
            throw new InvalidOperationException("resource.json 的 contentRoot 路径非法。");
        return clean;
    }

    private static bool IsSystemJunk(string path)
        => path.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
           || Path.GetFileName(path).Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
           || Path.GetFileName(path).Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase);

    private static bool IsIgnoredMetadataFile(string relativePath)
        => Path.GetFileName(relativePath).Equals("resource.json", StringComparison.OrdinalIgnoreCase)
           || IsSystemJunk(relativePath);

    private static string GetParentPrefix(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[..(slash + 1)] : "";
    }

    private static bool IsCardSkinMedia(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".cardanim";
    }

    private static string ChooseCardSkinTargetId(string skinsRoot, string resourceId)
    {
        var candidate = SanitizeFolderId(resourceId, "card_skin");
        if (!Directory.Exists(Path.Combine(skinsRoot, candidate))) return candidate;
        for (var i = 2; i < 1000; i++)
        {
            var next = $"{candidate}_{i}";
            if (!Directory.Exists(Path.Combine(skinsRoot, next))) return next;
        }
        return candidate + "_" + Guid.NewGuid().ToString("N")[..6];
    }

    private static string SanitizeFolderId(string raw, string fallback, bool allowDot = true)
    {
        var chars = (raw ?? "").Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' || (allowDot && ch == '.') ? ch : '_')
            .ToArray();
        var value = new string(chars).Trim('.');
        if (value.Length > 64) value = value[..64];
        return value.Length == 0 || value.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? fallback
            : value;
    }

    private static string? TryGetMetaString(Dictionary<string, JsonElement> meta, string key)
        => meta.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;

    private Dictionary<string, InstalledWorkshopResource> LoadInstalledRaw(string gameDirectory)
    {
        var path = RegistryPath(gameDirectory);
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, InstalledWorkshopResource>>(
                       File.ReadAllText(path, Encoding.UTF8), JsonOptions)
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveInstalled(string gameDirectory, Dictionary<string, InstalledWorkshopResource> map)
    {
        var path = RegistryPath(gameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(map, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private bool IsInstalledTargetPresent(string gameDirectory, InstalledWorkshopResource item)
    {
        try
        {
            return item.Type.Trim().ToLowerInvariant() switch
            {
                "card_skin" => !string.IsNullOrWhiteSpace(item.TargetId) &&
                               Directory.Exists(Path.Combine(_skinManager.GetSkinsDirectory(gameDirectory), item.TargetId)),
                "character_skin" => !string.IsNullOrWhiteSpace(item.TargetId) &&
                                    (Directory.Exists(Path.Combine(_characterSkins.Root(gameDirectory), item.TargetId))
                                     || Directory.Exists(_schemeManagerFactory().SchemeDirectory(gameDirectory, item.TargetId))),
                "audio_skin" => !string.IsNullOrWhiteSpace(item.TargetId) &&
                                Directory.Exists(AudioSkinSchemeManager.SchemeDirectory(gameDirectory, item.TargetId)),
                "rml_bundle" => !string.IsNullOrWhiteSpace(item.TargetId) &&
                                _resourceMods.GetStatus(gameDirectory).Mods.Any(m =>
                                    string.Equals(m.Id, item.TargetId, StringComparison.OrdinalIgnoreCase)),
                "home_background" => Directory.Exists(HomeBackgroundService.ModDirectory(gameDirectory)),
                _ => true
            };
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
