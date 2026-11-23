using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Modding;

namespace AstralParty.Toys.Services.Skins;

/// <summary>
/// 卡牌默认皮肤包（CesiumLoader Release 资产 <c>cesium-default-skins.zip</c>）：解析最新版本、
/// 按 GitHub 镜像设置挑下载地址、下载与内容预检，并记录上次下载的版本。
///
/// 与加载器下载同一套口径：先走 Release 里的静态 <c>version.json</c>（免 API 配额）拿 tag，
/// 再用 tag 拼包地址；镜像失败一律回退直连。放在这里而不是 <see cref="SkinManager"/> 里，
/// 是因为它只做「拿包」，落地仍由 <see cref="SkinManager.ImportSkinZip"/> 负责。
/// </summary>
public static class DefaultSkinPack
{
    /// <summary>STABLE 资产名（每个 Release 都有，指向该版本的那份包）。</summary>
    public const string AssetName = "cesium-default-skins.zip";

    /// <summary>版本化资产名前缀（<c>cesium-default-skins-3.8.10.zip</c>）。</summary>
    public const string VersionedAssetPrefix = "cesium-default-skins-";

    /// <summary>包内卡面必须落在这个前缀下。</summary>
    public const string PackPrefix = "AstralParty_ModLoader/skins/default/";

    /// <summary>包内方案元数据（缺少它说明包不是我们认为的那种）。</summary>
    public const string MetadataEntryName = PackPrefix + "skin.json";

    /// <summary>「上次下载的是哪个版本」的记录文件名，存在数据根 <c>state\</c> 下。</summary>
    public const string RecordFileName = "skin-pack.json";

    public const long MaxDownloadBytes = 256L * 1024 * 1024;
    public const long MaxEntryBytes = 32L * 1024 * 1024;
    public const long MaxExpandedBytes = 512L * 1024 * 1024;

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        // 30 MB 的包在慢镜像上可能超过默认 30 秒，这里放宽到 5 分钟（与改造前的行为一致）。
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstralParty.Toys/" + typeof(DefaultSkinPack).Assembly.GetName().Version);
        return client;
    }

    /// <summary>本地记录：上次成功下载并安装的默认包版本。缺字段的旧记录按空串处理。</summary>
    public sealed class Record
    {
        public string Version { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Url { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string DownloadedAt { get; set; } = "";
    }

    /// <summary>一次下载的计划：本地已装版本、GitHub 上的最新版本、按优先级排好的候选地址。</summary>
    public sealed class Plan
    {
        public Record? Local { get; set; }

        /// <summary>GitHub 上的最新版本号；读不到 <c>version.json</c> 时为空。</summary>
        public string LatestVersion { get; set; } = "";

        /// <summary>最新版本对应的 Release tag（<c>modloader-3.8.10</c>）。</summary>
        public string LatestTag { get; set; } = "";

        /// <summary>确认最新版本失败的原因（可空；有值时仍会退到 latest 别名下载）。</summary>
        public string Error { get; set; } = "";

        /// <summary>候选下载地址（GitHub 原始地址，未套镜像），按优先级排列。</summary>
        public IReadOnlyList<string> Candidates { get; set; } = Array.Empty<string>();
    }

    /// <summary>下载结果：真正生效的地址、内容 SHA256、字节数。</summary>
    public sealed class DownloadResult
    {
        public string Url { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public long Bytes { get; set; }
    }

    // ============================== 版本记录 ==============================

    public static string RecordPath(string dataRoot) =>
        Path.Combine(dataRoot, ToysPaths.StateFolderName, RecordFileName);

    /// <summary>读上次下载的版本记录；文件不存在 / 内容损坏一律返回 null（不猜版本）。</summary>
    public static Record? ReadRecord(string dataRoot)
    {
        try
        {
            var path = RecordPath(dataRoot);
            if (!File.Exists(path)) return null;
            var record = JsonSerializer.Deserialize<Record>(File.ReadAllText(path, Encoding.UTF8),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return string.IsNullOrWhiteSpace(record?.Version) ? null : record;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>写版本记录。失败不抛（记录只是提示，不该让安装失败）。</summary>
    public static bool TryWriteRecord(string dataRoot, Record record)
    {
        try
        {
            var path = RecordPath(dataRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ============================== 地址解析 ==============================

    /// <summary>Release 资产地址（tag 已做转义）。</summary>
    public static string ReleaseAssetUrl(string tag, string assetName) =>
        $"https://github.com/{ModManager.LoaderRepo}/releases/download/{Uri.EscapeDataString(tag)}/{assetName}";

    /// <summary>按优先级给出候选地址：版本化资产 → 同 tag 的稳定名 → latest 别名。
    /// 拿不到 tag（读不到 version.json）时只剩 latest 别名，功能照旧可用。</summary>
    public static IReadOnlyList<string> CandidateUrls(string? tag, string? version)
    {
        var list = new List<string>();
        var tagText = (tag ?? "").Trim();
        var versionText = (version ?? "").Trim();
        if (tagText.Length > 0 && versionText.Length > 0)
            list.Add(ReleaseAssetUrl(tagText, VersionedAssetPrefix + versionText + ".zip"));
        if (tagText.Length > 0)
            list.Add(ReleaseAssetUrl(tagText, AssetName));
        list.Add($"https://github.com/{ModManager.LoaderRepo}/releases/latest/download/{AssetName}");
        return list;
    }

    /// <summary>把原始 GitHub 地址展开成「镜像优先、直连兜底」的尝试顺序。
    /// 明确选了直连（空 / direct）时只有原始地址一项——注意 <see cref="GitHubMirrorHelper.GetPrefix"/>
    /// 对空值会回落到 gh-proxy，所以这里必须先判断空值。</summary>
    public static IReadOnlyList<string> UrlAttempts(string originalUrl, string? mirrorType, string? customMirrorUrl)
    {
        if (string.IsNullOrEmpty(mirrorType)) return new[] { originalUrl };
        var mirrored = GitHubMirrorHelper.ApplyMirror(originalUrl, mirrorType, customMirrorUrl);
        if (string.Equals(mirrored, originalUrl, StringComparison.OrdinalIgnoreCase)) return new[] { originalUrl };
        return new[] { mirrored, originalUrl };
    }

    /// <summary>读 Release 里的 <c>version.json</c> 确认最新版本，并给出候选地址（先镜像、失败回退直连）。</summary>
    public static async Task<Plan> PlanAsync(string? mirrorType, string? customMirrorUrl, string dataRoot,
        HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        var plan = new Plan { Local = ReadRecord(dataRoot) };
        var http = client ?? Http;
        var errors = new List<string>();
        foreach (var url in UrlAttempts(ModManager.LatestLoaderVersionManifestUrl, mirrorType, customMirrorUrl))
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"HTTP {(int)response.StatusCode}");
                    continue;
                }
                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var info = ModManager.ParseLoaderVersionManifestJson(text);
                if (info is null || string.IsNullOrEmpty(info.Version))
                {
                    errors.Add("清单里没有版本号");
                    continue;
                }
                plan.LatestVersion = info.Version;
                plan.LatestTag = info.Tag;
                plan.Candidates = CandidateUrls(info.Tag, info.Version);
                return plan;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 只有调用方真的按了取消才往上抛；HttpClient 超时也是 OperationCanceledException，
                // 那种情况按「这条通道失败」处理，继续试直连 / 下一个候选。
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        // 版本清单不可用不该挡住下载：退到 latest 别名，界面另行提示「查不到最新版本」。
        plan.Error = string.Join("；", errors);
        plan.Candidates = CandidateUrls(null, null);
        return plan;
    }

    // ============================== 下载 ==============================

    /// <summary>按候选地址下载到 <paramref name="temporaryPath"/>（不落游戏目录）。
    /// 每个候选先镜像、再直连；全部失败时抛出带各通道原因的异常。取消时抛
    /// <see cref="OperationCanceledException"/>，此时游戏目录没有被改动过。</summary>
    public static async Task<DownloadResult> DownloadAsync(Plan plan, string temporaryPath, string? mirrorType,
        string? customMirrorUrl, Action<int>? percent = null, HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var http = client ?? Http;
        var errors = new List<string>();
        foreach (var candidate in plan.Candidates)
        {
            foreach (var url in UrlAttempts(candidate, mirrorType, customMirrorUrl))
            {
                try
                {
                    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        errors.Add($"{ShortUrl(url)}：HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    var total = response.Content.Headers.ContentLength;
                    if (total > MaxDownloadBytes) throw new InvalidDataException("默认皮肤包超过 256 MB。");

                    long received = 0;
                    var lastPercent = -2;
                    await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    await using (var target = File.Create(temporaryPath))
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                                   .ConfigureAwait(false)) > 0)
                        {
                            received += read;
                            if (received > MaxDownloadBytes) throw new InvalidDataException("默认皮肤包超过 256 MB。");
                            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            var value = total is > 0 ? (int)Math.Clamp(received * 100 / total.Value, 0, 100) : -1;
                            if (value != lastPercent)
                            {
                                lastPercent = value;
                                percent?.Invoke(value);
                            }
                        }
                    }

                    var hash = await HashFileAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
                    return new DownloadResult { Url = url, Sha256 = hash, Bytes = received };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // 用户取消：删掉半个文件并如实上报；HttpClient 超时不算取消，下面按通道失败处理。
                    TryDelete(temporaryPath);
                    throw;
                }
                catch (InvalidDataException)
                {
                    TryDelete(temporaryPath);
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add($"{ShortUrl(url)}：{ex.Message}");
                    TryDelete(temporaryPath);
                }
            }
        }

        throw new InvalidOperationException("下载默认皮肤包失败：" + string.Join("；", errors));
    }

    /// <summary>错误归因用的短名称：GitHub 与镜像地址统一按「GitHub」汇报，避免把镜像域名摊到提示里。</summary>
    private static string ShortUrl(string url) =>
        GitHubMirrorHelper.IsGitHubUrl(url) ? "GitHub" : "镜像";

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(path);
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 删除失败无所谓：临时文件在系统临时目录，下次下载用新名字。
        }
    }

    // ============================== 内容预检 ==============================

    /// <summary>下载完整后、导入之前校验包内容：必须是 <c>AstralParty_ModLoader/skins/default/</c>
    /// 下的卡面与元数据，且有图片、有 <c>skin.json</c>、没超过体积上限。
    /// 不通过时抛 <see cref="InvalidDataException"/>，此时游戏目录没有被改动过。</summary>
    public static void ValidateZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
        if (entries.Length == 0) throw new InvalidDataException("默认包是空的");
        if (!entries.Any(e => Path.GetExtension(e.Name).Equals(".png", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("默认包中没有卡面图片");

        long total = 0;
        var hasMetadata = false;
        foreach (var entry in entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (!relative.StartsWith(PackPrefix, StringComparison.Ordinal)
                || entry.Length > MaxEntryBytes) throw new InvalidDataException("默认包内容不符合预期");
            total += entry.Length;
            if (relative.Equals(MetadataEntryName, StringComparison.OrdinalIgnoreCase)) hasMetadata = true;
        }

        if (total > MaxExpandedBytes) throw new InvalidDataException("默认包解压体积过大");
        if (!hasMetadata) throw new InvalidDataException("默认包缺少 skin.json，无法作为卡面方案导入");
    }
}
