using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Payload;

namespace AstralParty.Toys.Mobile.Services;

/// <summary>
/// M5：设备上的卡牌皮肤。落点与桌面版同构，只是把「游戏目录\AstralParty_ModLoader」换成设备上的加载器根：
/// <code>
/// 方案目录   &lt;loaderRoot&gt;/skins/&lt;方案&gt;/            （图片按卡面资源键命名）
/// 生效开关   &lt;loaderRoot&gt;/mods/CardSkinMod/config.json 的 Enabled + ActiveSkin
/// </code>
/// 只管「图片 + 生效开关」；卡面与资源键的对应关系、裁剪、单张替换都在本机做（需要读本机的默认卡面池）。
/// </summary>
public sealed class MobileSkinService
{
    /// <summary>与桌面版 <c>SkinManager.GameDefault</c> 同值：停用外部图片，只用游戏内部卡面。</summary>
    public const string GameDefault = "__game_default__";

    private const string CardSkinMod = "CardSkinMod";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".webp",
    };

    private readonly IDeviceChannel _channel;
    private readonly MobileOperationLog? _log;

    public MobileSkinService(IDeviceChannel channel, MobileOperationLog? log = null)
    {
        _channel = channel;
        _log = log;
    }

    private MobileOperationResult FailWith(string message)
    {
        _log?.Error(message);
        return MobileOperationResult.Fail(message);
    }

    private void Step(string message, int percent, Action<string, int>? progress)
    {
        _log?.Info(message);
        progress?.Invoke(message, percent);
    }

    /// <summary>读设备上的方案清单与当前生效方案。纯只读。</summary>
    public async Task<MobileSkinState> GetStateAsync(MobileTarget target, CancellationToken cancellationToken = default)
    {
        var state = new MobileSkinState { TargetId = target.Id };
        var opened = await MobileDeviceAccess.TryOpenRootAsync(_channel, target, _log, cancellationToken);
        if (opened is null)
        {
            state.Issues.Add("找不到游戏容器（先在设备页做一次检测，或确认这台设备上的游戏包名）");
            return state;
        }

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var skinsRoot = loaderRoot + "/skins";
            state.SkinsRoot = skinsRoot;

            var modRoot = await FindCardSkinModRootAsync(fileSystem, loaderRoot, cancellationToken);
            state.CardSkinModPresent = modRoot is not null;
            if (!state.CardSkinModPresent)
                state.Issues.Add($"{CardSkinMod} 不在设备上：可以先导入方案，但要在设备上装好这个模组后才能生效");

            if (modRoot is not null)
            {
                var config = modRoot + "/config.json";
                if (await fileSystem.ExistsAsync(config, cancellationToken))
                {
                    try
                    {
                        state.ActiveSkin = ReadActiveSkin(await fileSystem.ReadAllTextAsync(config, cancellationToken));
                    }
                    catch (Exception ex)
                    {
                        state.Issues.Add($"{CardSkinMod} 的 config.json 读不出来：{ex.Message}");
                    }
                }
                else
                {
                    state.Issues.Add($"{CardSkinMod} 还没有 config.json（游戏启动过一次才会生成默认配置）");
                }
            }

            IReadOnlyList<string> directories;
            try
            {
                directories = await fileSystem.ListAsync(skinsRoot, cancellationToken);
            }
            catch (Exception ex)
            {
                state.Issues.Add($"列不出方案目录：{ex.Message}（可能是还没投放过加载器载荷）");
                return state;
            }

            _log?.Info($"列卡面方案：{skinsRoot}（{directories.Count} 项）");
            foreach (var name in directories.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (name.StartsWith('.'))
                    continue;

                var profileRoot = $"{skinsRoot}/{name}";
                var files = await SafeListAsync(fileSystem, profileRoot, cancellationToken);
                var images = files.Count(f => ImageExtensions.Contains(Path.GetExtension(f)));
                var isDefault = name.Equals("default", StringComparison.OrdinalIgnoreCase);
                if (isDefault)
                    state.HasDefaultPool = true;

                var (displayName, author, description) = await ReadMetadataAsync(fileSystem, profileRoot, name, cancellationToken);
                state.Profiles.Add(new MobileSkinProfile
                {
                    Name = name,
                    DisplayName = displayName,
                    Author = author,
                    Description = description,
                    CardCount = images,
                    Active = !isDefault && name.Equals(state.ActiveSkin, StringComparison.OrdinalIgnoreCase),
                    IsDefault = isDefault,
                    HasMetadata = !string.Equals(displayName, name, StringComparison.Ordinal),
                });
            }

            _log?.Info($"方案 {state.Profiles.Count} 个，当前生效={state.ActiveSkin}");
            return state;
        }
    }

    /// <summary>
    /// 导入一个方案包并可选地立刻激活。已存在同名方案时**覆盖**（与桌面版导入一致），
    /// 但不会顺手删掉方案目录里原有的、包里没有的图片。
    /// </summary>
    public async Task<MobileOperationResult> ImportAsync(
        MobileTarget target, string zipPath, bool activate,
        Action<string, int>? progress = null, CancellationToken cancellationToken = default)
    {
        MobileSkinPackage package;
        try
        {
            package = MobileSkinPackage.Read(zipPath);
        }
        catch (Exception ex)
        {
            return FailWith("方案包不可用：" + ex.Message);
        }

        Step($"方案 {package.ProfileName}：{package.Entries.Count} 个文件" +
             (package.Skipped > 0 ? $"（跳过 {package.Skipped} 个非卡面文件）" : ""), 4, progress);

        var opened = await MobileDeviceAccess.TryOpenRootAsync(_channel, target, _log, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先在设备页做一次检测，或确认这台设备上的游戏包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var profileRoot = $"{loaderRoot}/skins/{package.ProfileName}";

            // 只有「新建方案目录」会被游戏自建的 2750 目录挡住；覆盖已有方案照常能写
            if (!await fileSystem.ExistsAsync(profileRoot, cancellationToken) &&
                await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                    is { } blocked)
                return FailWith(blocked);

            await fileSystem.MakeDirectoriesAsync(profileRoot, cancellationToken);

            var written = 0;
            foreach (var entry in package.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remote = $"{profileRoot}/{entry.RelativePath}";
                var parent = remote[..remote.LastIndexOf('/')];
                await fileSystem.MakeDirectoriesAsync(parent, cancellationToken);
                await fileSystem.WriteAllBytesAsync(remote, entry.Bytes, cancellationToken);
                written++;
                progress?.Invoke($"写入 {written}/{package.Entries.Count}",
                    5 + (int)(85.0 * written / package.Entries.Count));
            }

            Step("回读校验…", 92, progress);
            var mismatched = new List<string>();
            foreach (var entry in package.Entries)
            {
                var info = await fileSystem.StatAsync($"{profileRoot}/{entry.RelativePath}", cancellationToken);
                if (info is null || info.Size != entry.Bytes.Length)
                    mismatched.Add($"{entry.RelativePath}({info?.Size.ToString() ?? "缺"}/{entry.Bytes.Length})");
            }

            if (mismatched.Count > 0)
                return FailWith("回读校验失败（设备上的文件大小不符）：" + string.Join("、", mismatched.Take(8)));

            _log?.Info($"方案导入完成：{package.ProfileName} → {profileRoot}（{written} 个文件）");

            if (!activate)
            {
                Step("导入完成", 100, progress);
                return new MobileOperationResult
                {
                    Ok = true,
                    Message = $"已导入方案 {package.ProfileName}（{written} 个文件）",
                    Detail = profileRoot,
                };
            }

            Step("激活方案…", 95, progress);
            var activation = await SetActiveCoreAsync(fileSystem, loaderRoot, package.ProfileName, cancellationToken);
            if (!activation.Ok)
            {
                return new MobileOperationResult
                {
                    Ok = false,
                    Message = $"方案已导入，但激活失败：{activation.Message}",
                    Detail = activation.Detail,
                };
            }

            Step("导入并激活完成", 100, progress);
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已导入并激活方案 {package.ProfileName}（{written} 个文件）",
                Detail = profileRoot,
            };
        }
    }

    /// <summary>切换生效方案；<see cref="GameDefault"/> 表示停用外部图片。</summary>
    public async Task<MobileOperationResult> SetActiveAsync(
        MobileTarget target, string profileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return FailWith("方案名为空");

        var opened = await MobileDeviceAccess.TryOpenRootAsync(_channel, target, _log, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先在设备页做一次检测，或确认这台设备上的游戏包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;

            if (!string.Equals(profileName, GameDefault, StringComparison.Ordinal))
            {
                if (!await fileSystem.ExistsAsync($"{loaderRoot}/skins/{profileName}", cancellationToken))
                    return FailWith($"设备上没有方案 {profileName}");

                var files = await SafeListAsync(fileSystem, $"{loaderRoot}/skins/{profileName}", cancellationToken);
                if (!files.Any(f => ImageExtensions.Contains(Path.GetExtension(f))))
                    return FailWith($"方案 {profileName} 里没有卡面图片");
            }

            var result = await SetActiveCoreAsync(fileSystem, loaderRoot, profileName, cancellationToken);
            if (result.Ok)
                _log?.Info($"卡面方案切到 {profileName}");
            return result;
        }
    }

    /// <summary>删掉设备上的一个方案目录。当前生效的方案与 <c>default</c> 拒绝删除。</summary>
    public async Task<MobileOperationResult> DeleteAsync(
        MobileTarget target, string profileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return FailWith("方案名为空");
        if (profileName.Contains('/') || profileName.Contains('\\') || profileName.Contains(".."))
            return FailWith($"方案名非法：{profileName}");
        if (profileName.Equals("default", StringComparison.OrdinalIgnoreCase))
            return FailWith("default 是默认回退卡面池，不能删除");
        if (string.Equals(profileName, GameDefault, StringComparison.Ordinal))
            return FailWith("这不是一个方案目录");

        var opened = await MobileDeviceAccess.TryOpenRootAsync(_channel, target, _log, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先在设备页做一次检测，或确认这台设备上的游戏包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var profileRoot = $"{loaderRoot}/skins/{profileName}";
            if (!await fileSystem.ExistsAsync(profileRoot, cancellationToken))
                return FailWith($"设备上没有方案 {profileName}");

            var modRoot = await FindCardSkinModRootAsync(fileSystem, loaderRoot, cancellationToken);
            if (modRoot is not null)
            {
                var config = modRoot + "/config.json";
                if (await fileSystem.ExistsAsync(config, cancellationToken))
                {
                    var active = ReadActiveSkin(await fileSystem.ReadAllTextAsync(config, cancellationToken));
                    if (profileName.Equals(active, StringComparison.OrdinalIgnoreCase))
                        return FailWith($"{profileName} 正在生效，先切到别的方案或「游戏默认」再删除");
                }
            }

            // 删目录要写父目录：游戏自建的 skins/ 会拒绝
            if (await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                    is { } blocked)
                return FailWith(blocked);

            await fileSystem.DeleteAsync(profileRoot, cancellationToken);
            if (await fileSystem.ExistsAsync(profileRoot, cancellationToken))
                return FailWith($"删除后目录仍在：{profileRoot}");

            _log?.Info($"已删除设备上的卡面方案 {profileName}");
            return new MobileOperationResult { Ok = true, Message = $"已删除方案 {profileName}", Detail = profileRoot };
        }
    }

    // ============================== 内部 ==============================

    private async Task<MobileOperationResult> SetActiveCoreAsync(
        IDeviceFileSystem fileSystem, string loaderRoot, string profileName, CancellationToken cancellationToken)
    {
        var modRoot = await FindCardSkinModRootAsync(fileSystem, loaderRoot, cancellationToken);
        if (modRoot is null)
            return MobileOperationResult.Fail(
                $"设备上没有 {CardSkinMod}，装了它之后卡面方案才会生效（先在设备上或市场里装好这个模组）");

        var configPath = modRoot + "/config.json";
        var enabled = !string.Equals(profileName, GameDefault, StringComparison.Ordinal);

        string text;
        var created = false;
        if (await fileSystem.ExistsAsync(configPath, cancellationToken))
        {
            text = await fileSystem.ReadAllTextAsync(configPath, cancellationToken);
        }
        else
        {
            // 还没有 config.json：要新建文件，同样会被游戏自建的 2750 目录挡住
            if (await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                    is { } blocked)
                return MobileOperationResult.Fail(blocked);
            text = "{\n}\n";
            created = true;
        }

        var merged = false;
        try
        {
            text = MobileModConfig.SetScalar(text, "Enabled", "bool", enabled ? "true" : "false");
            // 停用时保留原来的 ActiveSkin 值不动（下次启用回到原方案），只在启用时写
            if (enabled)
                text = MobileModConfig.SetScalar(text, "ActiveSkin", "string", profileName);
        }
        catch (InvalidOperationException)
        {
            // 字段不存在（老配置 / 新建的文件）：只能合并写入。这一步会丢掉该文件里的注释，所以单独记一条日志。
            merged = true;
            text = MergeSettings(text, enabled, enabled ? profileName : null);
        }

        if (!CanParseObject(text))
            return MobileOperationResult.Fail("改完的 config.json 解析不成 JSON 对象，已放弃写入（设备上的文件没动）");

        var bytes = Encoding.UTF8.GetBytes(text);
        await fileSystem.WriteAllBytesAsync(configPath, bytes, cancellationToken);

        var readback = await fileSystem.ReadAllBytesAsync(configPath, cancellationToken);
        if (!readback.AsSpan().SequenceEqual(bytes))
            return MobileOperationResult.Fail("回读校验失败：设备上的 config.json 与写入内容不一致");

        var verify = ReadActiveSkin(Encoding.UTF8.GetString(readback));
        if (!string.Equals(verify, profileName, StringComparison.Ordinal))
            return MobileOperationResult.Fail($"写入后回读的生效方案是 {verify}，与预期 {profileName} 不一致");

        if (merged)
            _log?.Warn($"{configPath} 里原本没有 Enabled / ActiveSkin 字段，已合并写入（该文件里的注释未保留）");

        return new MobileOperationResult
        {
            Ok = true,
            Message = enabled ? $"已切到方案 {profileName}" : "已切回游戏默认卡面",
            Detail = configPath + (created ? "（新建）" : ""),
        };
    }

    /// <summary>
    /// 字段缺失时的兜底：把原文本按 JSON 解析（去注释）后补上两个字段再序列化。
    /// 只在「文本定点替换做不到」的时候用，且一定会记日志。
    /// </summary>
    private static string MergeSettings(string text, bool enabled, string? activeSkin)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(MobileModConfig.StripComments(text));
        }
        catch (JsonException)
        {
            node = null;
        }

        var root = node as JsonObject ?? new JsonObject();
        root["Enabled"] = enabled;
        if (activeSkin is not null)
            root["ActiveSkin"] = activeSkin;

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n";
    }

    private static string ReadActiveSkin(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(MobileModConfig.StripComments(text));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return "";
            if (root.TryGetProperty("Enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False)
                return GameDefault;
            return root.TryGetProperty("ActiveSkin", out var skin) && skin.ValueKind == JsonValueKind.String
                ? skin.GetString() ?? ""
                : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    /// <summary>CardSkinMod 的目录：启用或停用位置都可能（目录即状态）。</summary>
    private static async Task<string?> FindCardSkinModRootAsync(
        IDeviceFileSystem fileSystem, string loaderRoot, CancellationToken cancellationToken)
    {
        foreach (var folder in new[] { MobilePaths.ModsRelative, MobilePaths.DisabledModsRelative })
        {
            var candidate = $"{loaderRoot}/{folder}/{CardSkinMod}";
            if (await fileSystem.ExistsAsync(candidate, cancellationToken))
                return candidate;
        }

        return null;
    }

    private static async Task<IReadOnlyList<string>> SafeListAsync(
        IDeviceFileSystem fileSystem, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await fileSystem.ListAsync(path, cancellationToken);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<(string Name, string Author, string Description)> ReadMetadataAsync(
        IDeviceFileSystem fileSystem, string profileRoot, string fallbackName, CancellationToken cancellationToken)
    {
        var path = profileRoot + "/skin.json";
        try
        {
            if (!await fileSystem.ExistsAsync(path, cancellationToken))
                return (fallbackName, "", "");

            using var document = JsonDocument.Parse(await fileSystem.ReadAllBytesAsync(path, cancellationToken));
            var root = document.RootElement;
            var name = GetString(root, "name");
            return (string.IsNullOrWhiteSpace(name) ? fallbackName : name,
                GetString(root, "author"), GetString(root, "description"));
        }
        catch (Exception)
        {
            return (fallbackName, "", "");
        }
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool CanParseObject(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(MobileModConfig.StripComments(text));
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
