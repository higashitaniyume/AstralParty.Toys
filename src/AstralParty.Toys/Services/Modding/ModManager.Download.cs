using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Speedhack;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    // ============================== 联网更新 ==============================

    /// <summary>CesiumLoader 最新发布包的固定下载地址（GitHub Release latest）。</summary>
    public const string LatestPackageUrl =
        "https://github.com/higashitaniyume/CesiumLoader/releases/latest/download/cesium-loader.zip";

    /// <summary>CesiumLoader 最新 Release 的 API 地址（轻量检查：只取 tag_name，不下载整个 ZIP）。</summary>
    public const string LatestReleaseApiUrl =
        "https://api.github.com/repos/higashitaniyume/CesiumLoader/releases/latest";

    /// <summary>CesiumLoader 静态版本清单地址（与 Toys 自身 version.json 同格式，免 API 配额）。</summary>
    public const string LatestLoaderVersionManifestUrl =
        "https://github.com/higashitaniyume/CesiumLoader/releases/latest/download/version.json";

    /// <summary>CesiumLoader 最新 Release 的页面地址（界面「打开 GitHub Release」用）。</summary>
    public const string LatestReleaseUrl =
        "https://github.com/higashitaniyume/CesiumLoader/releases/latest";

    /// <summary>CesiumLoader 发布 tag 前缀（<c>modloader-&lt;版本&gt;</c>，见加载器 CHANGELOG）。</summary>
    public const string ReleaseTagPrefix = "modloader-";

    /// <summary>Toys 仓库（取仓库根 CHANGELOG.md 当作工具箱更新日志）。</summary>
    public const string AppRepo = "higashitaniyume/AstralParty.Toys";

    /// <summary>CesiumLoader 仓库（取仓库根 CHANGELOG.md 当作加载器更新日志）。</summary>
    public const string LoaderRepo = "higashitaniyume/CesiumLoader";

    /// <summary>去 GitHub 取指定仓库根目录 CHANGELOG.md（raw），优先设置中的 GitHub 镜像、失败回退直连。
    /// 全部失败抛异常，由调用方兜底内嵌。</summary>
    public static async Task<string> DownloadRepoChangelogAsync(
        string repo,
        string? mirrorType,
        string? customUrl,
        CancellationToken cancellationToken = default)
    {
        var rawUrl = $"https://raw.githubusercontent.com/{repo}/master/CHANGELOG.md";
        var mirroredUrl = GitHubMirrorHelper.ApplyMirror(rawUrl, mirrorType, customUrl);
        var candidates = new List<string> { mirroredUrl };
        if (!string.Equals(mirroredUrl, rawUrl, StringComparison.OrdinalIgnoreCase))
            candidates.Add(rawUrl);

        Exception? lastError = null;
        foreach (var url in candidates)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Accept.ParseAdd("text/markdown,text/plain;q=0.9,*/*;q=0.8");
                using var resp = await Http.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var text = await resp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                lastError = new InvalidOperationException("仓库根 CHANGELOG.md 为空。");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("无法获取仓库根 CHANGELOG.md。");
    }

    public const string InstalledManifestFileName = "cesium-loader.json";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstralParty.Toys/" + typeof(ModManager).Assembly.GetName().Version);
        return client;
    }

    /// <summary>读「已安装版本」—— 唯一来源是安装 / 更新时写下的清单
    /// <c>AstralParty_ModLoader\cesium-loader.json</c>（见 <see cref="WriteInstalledManifest"/>）。
    ///
    /// ★ 这里刻意**不**做任何推测：不问加载器日志、也不拿配置里的 sdkVersion 顶替。
    ///   猜测出来的版本号会被界面当成权威值显示给用户，而"版本"恰恰是 mod 管理器最不能含糊的信息 ——
    ///   读不到就返回空串，由界面如实显示「版本未知，重新安装一次即可记录」。
    ///   0.4.7 之前装的加载器没有这份清单，重新安装（或点「从 GitHub 更新」）一次就会补上。</summary>
    private static string ReadInstalledVersion(string gameDirectory)
    {
        try
        {
            var manifestPath = Path.Combine(gameDirectory, LoaderFolderName, InstalledManifestFileName);
            if (!File.Exists(manifestPath)) return "";
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("version", out var element) ? element.GetString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>安装清单里记录的某个文件的 SHA256（没有清单 / 没有该条目 / 清单损坏 → 空串）。
    /// 拆分后原生层有两个文件（version.dll 代理 + CesiumLoader.dll 核心），都要能按名字查。</summary>
    private static string ReadManifestFileHash(string gameDirectory, string fileName)
    {
        try
        {
            var manifestPath = Path.Combine(gameDirectory, LoaderFolderName, InstalledManifestFileName);
            if (!File.Exists(manifestPath)) return "";
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Object)
                return "";
            return files.TryGetProperty(fileName, out var hash) ? hash.GetString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>安装清单里记录的 version.dll SHA256。</summary>
    private static string ReadManifestLoaderHash(string gameDirectory) => ReadManifestFileHash(gameDirectory, LoaderDllName);

    /// <summary>把 Release tag（如 <c>modloader-3.3.2</c>）转成版本号（<c>3.3.2</c>）。
    /// 认 <c>modloader-</c> 前缀大小写与可选的 <c>v</c> 前缀；空 / null → 空串。</summary>
    public static string ParseReleaseVersion(string? tag)
    {
        var text = (tag ?? "").Trim();
        if (text.StartsWith(ReleaseTagPrefix, StringComparison.OrdinalIgnoreCase))
            text = text.Substring(ReleaseTagPrefix.Length);
        text = text.TrimStart('v', 'V').Trim();
        return text;
    }

    /// <summary>解析 GitHub API 的 Release JSON（<c>tag_name</c> / <c>html_url</c> / <c>published_at</c>）。
    /// 解析失败 → null；没有可用 tag_name 也 → null（调用方再兜底下载发布包）。</summary>
    public static LoaderLatestReleaseInfo? ParseLatestReleaseJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
            var version = ParseReleaseVersion(tag);
            if (string.IsNullOrEmpty(version)) return null;
            return new LoaderLatestReleaseInfo
            {
                Version = version,
                Tag = string.IsNullOrWhiteSpace(tag) ? ReleaseTagPrefix + version : tag.Trim(),
                PublishedAt = root.TryGetProperty("published_at", out var publishedElement) ? publishedElement.GetString() ?? "" : "",
                HtmlUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() ?? "" : ""
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>解析静态 version.json（<c>version</c> / <c>releaseTag</c> / <c>releaseDate</c>）。
    /// 与 GitHub API 字段不同名，单独解析；失败或没有可用版本 → null。</summary>
    public static LoaderLatestReleaseInfo? ParseLoaderVersionManifestJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.TryGetProperty("version", out var versionElement) ? (versionElement.GetString() ?? "").Trim() : "";
            version = version.TrimStart('v', 'V').Trim();
            if (string.IsNullOrEmpty(version)) return null;
            var tag = root.TryGetProperty("releaseTag", out var tagElement) ? (tagElement.GetString() ?? "").Trim() : "";
            if (string.IsNullOrWhiteSpace(tag)) tag = ReleaseTagPrefix + version;
            var date = root.TryGetProperty("releaseDate", out var dateElement) ? dateElement.GetString() ?? "" : "";
            return new LoaderLatestReleaseInfo
            {
                Version = version,
                Tag = tag,
                PublishedAt = date,
                HtmlUrl = LatestReleaseUrl
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>轻量查询 GitHub 最新 Release 版本（不下载整个发布包；client 仅供测试注入）。
    /// 优先走静态 version.json（免 API 配额），失败再走 GitHub API，最后下载发布包读清单兜底。
    /// 全部失败时抛 <see cref="InvalidOperationException"/>（含各通道的错误摘要）。</summary>
    public async Task<LoaderLatestReleaseInfo> GetLatestVersionAsync(
        string? mirrorType = null,
        string? customMirrorUrl = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var http = client ?? Http;
        var errors = new List<string>();

        // 先走静态 version.json（与 Toys 自身同款清单，免 API 配额）
        var mirroredManifest = string.IsNullOrEmpty(mirrorType)
            ? LatestLoaderVersionManifestUrl
            : GitHubMirrorHelper.ApplyMirror(LatestLoaderVersionManifestUrl, mirrorType, customMirrorUrl);
        var manifestCandidates = new List<string>();
        if (!string.Equals(mirroredManifest, LatestLoaderVersionManifestUrl, StringComparison.OrdinalIgnoreCase))
        {
            manifestCandidates.Add(mirroredManifest);
            manifestCandidates.Add(LatestLoaderVersionManifestUrl);
        }
        else
        {
            manifestCandidates.Add(LatestLoaderVersionManifestUrl);
        }

        foreach (var url in manifestCandidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("application/json");
                if (!request.Headers.Contains("User-Agent"))
                    request.Headers.TryAddWithoutValidation("User-Agent", "AstralParty.Toys/" + Assembly.GetName().Version);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"静态清单：HTTP {(int)response.StatusCode}");
                    continue;
                }
                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var info = ParseLoaderVersionManifestJson(text);
                if (info is not null && !string.IsNullOrEmpty(info.Version))
                    return info;
                errors.Add("静态清单：响应里没有可用的 version");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"静态清单：{ex.Message}");
            }
        }

        var mirrored = string.IsNullOrEmpty(mirrorType)
            ? LatestReleaseApiUrl
            : GitHubMirrorHelper.ApplyMirror(LatestReleaseApiUrl, mirrorType, customMirrorUrl);
        var candidates = new List<string>();
        // 用户选了镜像就先走镜像（尊重设置），再直连兜底；选了直连就只走直连。
        if (!string.Equals(mirrored, LatestReleaseApiUrl, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(mirrored);
            candidates.Add(LatestReleaseApiUrl);
        }
        else
        {
            candidates.Add(LatestReleaseApiUrl);
        }

        foreach (var url in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                if (!request.Headers.Contains("User-Agent"))
                    request.Headers.TryAddWithoutValidation("User-Agent", "AstralParty.Toys/" + Assembly.GetName().Version);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{ShortUrl(url)}：HTTP {(int)response.StatusCode}");
                    continue;
                }
                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var info = ParseLatestReleaseJson(text);
                if (info is not null && !string.IsNullOrEmpty(info.Version))
                    return info;
                errors.Add($"{ShortUrl(url)}：响应里没有可用的 tag_name");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{ShortUrl(url)}：{ex.Message}");
            }
        }

        // API 配额 / 临时故障时的兜底：下载发布包读清单（体积小，约数 MB）。
        try
        {
            var bytes = await DownloadLatestPackageAsync(mirrorType, customMirrorUrl, client, cancellationToken).ConfigureAwait(false);
            var manifest = ParsePackageManifest(bytes);
            if (manifest is not null && !string.IsNullOrEmpty(manifest.Version))
            {
                var version = ParseReleaseVersion(manifest.Version);
                if (!string.IsNullOrEmpty(version))
                {
                    return new LoaderLatestReleaseInfo
                    {
                        Version = version,
                        Tag = ReleaseTagPrefix + version,
                        PublishedAt = "",
                        HtmlUrl = LatestReleaseUrl
                    };
                }
            }
            errors.Add("发布包回退：清单里没有可用的 version");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add($"发布包回退：{ex.Message}");
        }

        throw new InvalidOperationException("无法确认 GitHub 最新版本：" + string.Join("；", errors));
    }

    /// <summary>错误归因用的短名称：api.github.com 与其镜像代理地址统一叫「GitHub API」。</summary>
    private static string ShortUrl(string url)
        => url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase) ? "GitHub API" : "GitHub";

    /// <summary>加载器下载 / 安装的进度回调：phase = download | verify | install | done，percent = 0..100（-1 = 未知）。</summary>
    public delegate void LoaderProgressCallback(string phase, int percent, string message);

    /// <summary>下载最新发布包到字节数组（不落盘）。网络异常/无 Release 时抛异常。</summary>
    public Task<byte[]> DownloadLatestPackageAsync(CancellationToken cancellationToken)
        => DownloadLatestPackageAsync(mirrorType: null, customMirrorUrl: null, client: null, cancellationToken);

    /// <summary>下载最新发布包到字节数组（不落盘），支持指定镜像加速源与测试客户端。网络异常/无 Release 时抛异常。
    /// <paramref name="progress"/> 按已接收字节回报下载进度（不知道总大小时 percent = -1）。</summary>
    public async Task<byte[]> DownloadLatestPackageAsync(
        string? mirrorType = null,
        string? customMirrorUrl = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default,
        LoaderProgressCallback? progress = null)
    {
        var url = string.IsNullOrEmpty(mirrorType)
            ? LatestPackageUrl
            : GitHubMirrorHelper.ApplyMirror(LatestPackageUrl, mirrorType, customMirrorUrl);
        progress?.Invoke("download", -1, "正在连接 GitHub 下载 CesiumLoader…");
        ToysLog.Info("modloader", $"开始下载 CesiumLoader 发布包：{url}");
        using var response = await (client ?? Http).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            ToysLog.Warn("modloader", $"下载 CesiumLoader 发布包失败：HTTP {status}");
            throw new InvalidOperationException(
                status == 404
                    ? "CesiumLoader 还没有发布过任何 Release（打 modloader-* tag 触发 GitHub Action 即可）。"
                    : $"下载失败：HTTP {(int)response.StatusCode}。");
        }

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var memory = total is > 0 and < MaxPackageBytes ? new MemoryStream((int)total.Value) : new MemoryStream();
        var buffer = new byte[81920];
        long received = 0;
        var lastPercent = -2;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            memory.Write(buffer, 0, read);
            received += read;
            if (memory.Length > MaxPackageBytes) throw new InvalidDataException("发布包超过大小限制。");
            var percent = total is > 0 ? (int)Math.Clamp(received * 100 / total.Value, 0, 100) : -1;
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Invoke("download", percent, percent >= 0
                    ? $"正在下载 CesiumLoader… {percent}%（{FormatBytes(received)} / {FormatBytes(total!.Value)}）"
                    : $"正在下载 CesiumLoader… {FormatBytes(received)}");
            }
        }
        progress?.Invoke("download", 100, $"下载完成（{FormatBytes(received)}）");
        ToysLog.Info("modloader", $"下载完成：CesiumLoader 发布包 {FormatBytes(received)}");
        return memory.ToArray();
    }

    /// <summary>发布包大小上限（与发布包校验里的总量限制一致）。</summary>
    private const long MaxPackageBytes = 128L * 1024 * 1024;

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes} B"
    };

    /// <summary>解析发布包清单（zip 内的 cesium-loader.json）：版本 + 各文件 SHA256。</summary>
    public static LoaderPackageInfo? ParsePackageManifest(byte[] zipBytes)
    {
        try
        {
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (zipBytes.Length > 64 * 1024 * 1024) return null;
            var entry = archive.GetEntry(InstalledManifestFileName);
            if (entry is null || entry.Length > 1024 * 1024) return null;
            using var reader = new StreamReader(entry.Open());
            var json = reader.ReadToEnd();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var info = new LoaderPackageInfo { Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "" };
            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in files.EnumerateObject())
                {
                    info.Files[property.Name] = property.Value.GetString() ?? "";
                }
            }
            return info;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>校验发布包内容：路径安全 / 重复条目 / 大小上限 + 必需文件齐全，
    /// 再逐个核对**清单里声明的每个文件**（存在 + SHA256 一致；老包清单没写哈希时只校验存在）。
    /// 返回清单没有覆盖到的文件数（这些文件仍会安装，但无法校验哈希，调用方记一条警告）。</summary>
    private static int ValidatePackage(byte[] zipBytes, LoaderPackageInfo manifest)
    {
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            total += entry.Length;
            if (path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(p => p is ".." or ".")
                || !paths.Add(path) || entry.Length > 64 * 1024 * 1024 || total > 128 * 1024 * 1024 || paths.Count > 4096)
                throw new InvalidDataException("发布包包含不安全路径、重复文件或超过大小限制。");
        }

        // 必需文件：加载器 / 配置模板 / SDK
        foreach (var relative in new[] { LoaderDllName, PackageConfigEntry, PackageSdkEntry })
            if (archive.GetEntry(relative) is null)
                throw new InvalidDataException($"发布包缺少文件：{relative}");
        // 包内 mod：以发布包内容为准，mods\{ModId}\ 下的文件全部安装。
        if (!archive.Entries.Any(e => IsPackageModEntry(e.FullName) && !string.IsNullOrEmpty(e.Name)))
            throw new InvalidDataException("发布包里没有任何 mod（AstralParty_ModLoader/mods/ 为空），已停止安装。");

        // 清单里声明的文件必须真的在包里，且内容一致（这是「导入发布包」唯一的内容凭证）
        foreach (var pair in manifest.Files)
        {
            var entry = archive.GetEntry(pair.Key);
            if (entry is null) throw new InvalidDataException($"发布包缺少清单里声明的文件：{pair.Key}");
            if (string.IsNullOrEmpty(pair.Value)) continue;
            using var entryStream = entry.Open();
            using var sha = SHA256.Create();
            var actual = Convert.ToHexString(sha.ComputeHash(entryStream)).ToLowerInvariant();
            if (!string.Equals(actual, pair.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"发布包校验失败：{pair.Key} 哈希不一致。");
        }

        // 清单本身不列自己；其余没被清单覆盖的文件（旧包 / 手工打包）只计数，不阻断安装
        return archive.Entries.Count(e => !string.IsNullOrEmpty(e.Name)
            && !e.FullName.Replace('\\', '/').Equals(InstalledManifestFileName, StringComparison.OrdinalIgnoreCase)
            && !manifest.Files.ContainsKey(e.FullName.Replace('\\', '/')));
    }

    /// <summary>
    /// 安装 CesiumLoader 发布包：先按包内清单逐文件校验哈希，再**按包内布局整包解压**到游戏目录
    /// —— <c>version.dll</c>（代理）/ <c>CesiumLoader.dll</c>（核心）与两者同名的 <c>.pdb</c>，
    /// 以及 <c>CesiumLoader-NOTICE.txt</c> / <c>CesiumLoader-LICENSE.txt</c> /
    /// <c>README.txt</c> / <c>CHANGELOG.md</c> 落游戏 exe 目录，
    /// <c>cesium-loader.json</c> 与 <c>AstralParty_ModLoader\**</c>（sdk / bootstrap / mods / …）
    /// 落加载器目录。「安装 / 重新安装内置版本」也走这里，包来自程序集内嵌资源。
    ///
    /// 包内版本比游戏目录已装的更旧时（降级）默认拒绝，需要 allowDowngrade 显式放行；
    /// 游戏目录里那个不属于本工具的 version.dll 需要 overwriteDll 确认。
    /// 通用名文件（README.txt / CHANGELOG.md）遇到不是本工具落的同名文件时跳过并返回给调用方，绝不覆盖玩家文件。
    /// </summary>
    public LoaderInstallResult InstallPackage(string gameDirectory, byte[] zipBytes, bool overwriteDll,
        bool allowDowngrade = false, LoaderProgressCallback? progress = null)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory}");
        if (string.IsNullOrEmpty(SpeedhackManager.ContainsGameExe(gameDirectory)))
            throw new InvalidOperationException("所选目录里没有找到游戏主程序（AstralParty*.exe），确认这是游戏 exe 所在的目录？");

        progress?.Invoke("verify", 5, "正在校验发布包…");
        var manifest = ParsePackageManifest(zipBytes)
            ?? throw new InvalidDataException("发布包缺少清单（cesium-loader.json），已停止安装。");
        var unlistedFiles = ValidatePackage(zipBytes, manifest);
        if (unlistedFiles > 0)
            ToysLog.Warn("modloader", $"发布包清单没有覆盖 {unlistedFiles} 个文件，这些文件按包内内容安装（未做哈希校验）。");
        ToysLog.Info("modloader",
            $"开始安装 CesiumLoader {manifest.Version}（发布包 {FormatBytes(zipBytes.Length)}，目标 {gameDirectory}）");

        // 降级闸门（内嵌安装与导入发布包同一条规则）：包内版本比游戏目录已装的更旧 → 默认拒绝。
        // 已装版本从游戏目录里的安装清单读，清单缺失（老安装）时退回 Toys 的安装历史。
        // ★ 但只有目录里**还留着加载器**时才谈得上「已装」：文件被手工删干净之后历史仍在数据根里，
        //   那时装回旧版本是重新安装而不是降级，不能拦（否则用户会看到「明明删干净了还说已装 3.8.10」）。
        var installedBeforePackage = HasLoaderOnDisk(gameDirectory)
            ? ReadCurrentInstallationVersion(gameDirectory)
            : "";
        var packageVersion = manifest.Version;
        if (!allowDowngrade && packageVersion.Length > 0 && installedBeforePackage.Length > 0
            && CompareSemVer(packageVersion, installedBeforePackage) < 0)
            throw new InvalidOperationException(
                $"发布包里的加载器（{packageVersion}）比游戏目录里已装的（{installedBeforePackage}）更旧 —— 这是一次降级，已阻止。" +
                "如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。");

        var targetDll = Path.Combine(gameDirectory, LoaderDllName);
        if (File.Exists(targetDll) && !overwriteDll && ClassifyLoaderOrigin(gameDirectory, targetDll).Length == 0)
            throw new InvalidOperationException(
                "游戏目录里的 version.dll 不是本工具装的（来源不明，可能是旧版独立变速器或第三方文件）——" +
                "如确定要覆盖，请勾选「允许覆盖其它 version.dll」。");

        var remainDisabled = IsRetainedDisabled(gameDirectory);
        var previousInstallation = ReadRetainedInstallation(gameDirectory);
        using var retained = BeginRetainedOperation(gameDirectory, "install-package", zipBytes);
        // Cache individual input files before any game mutation, so even a partial install is recoverable.
        using (var cacheArchive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read))
            foreach (var entry in cacheArchive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
            {
                if (entry.Length > 64 * 1024 * 1024) throw new InvalidDataException("发布包文件超过大小限制。");
                CacheBytes(ReadEntryBytes(entry));
            }

        var skipped = new List<string>();
        try
        {
            progress?.Invoke("install", 30, "正在解压发布包…");
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
            Directory.CreateDirectory(loaderRoot);
            Directory.CreateDirectory(Path.Combine(loaderRoot, ModsFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, DisabledModsFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, SdkFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, LogsFolderName));

            var configPath = Path.Combine(loaderRoot, ConfigFileName);
            var isNewConfig = !File.Exists(configPath);

            // 包内每个文件条目按布局落地；doorstop 已存在就不覆盖，mods 走「保留用户设置」的规则
            ExtractPackageEntries(archive, gameDirectory, loaderRoot, remainDisabled,
                CaptureInstalledModsEnabled(loaderRoot), previousInstallation, skipped);

            // 老配置可能停在旧 SDK 号（不覆盖用户配置）：抬到随包版本，否则新版内置 mod 会被拒绝加载
            var packageConfigEntry = archive.GetEntry(PackageConfigEntry);
            SyncLoaderConfigSdkVersion(configPath,
                packageConfigEntry is null ? "" : ReadSdkVersionFromConfigBytes(ReadEntryBytes(packageConfigEntry)));
            // 老配置里没有 Steam 绕过开关：补齐（只追加缺失的键，不动用户已有值与注释）
            SyncLoaderConfigSteamKeys(configPath);
            // 老配置里没有 profileWindowTitle：一并补齐，让配置自文档化、也防止旧版 Toys 保存时删掉它
            SyncLoaderConfigProfileWindowTitle(configPath);
            // 首次安装默认从 Steam 启动；升级/重装保留已有启动方式。
            // 模板可能默认开启绕过，须同时关闭主开关、匹配和房间列表查询。
            if (isNewConfig && !SetSteamBypassProfile(gameDirectory, enabled: false))
                throw new InvalidDataException("无法设置首次安装的 Steam 启动方式，加载器配置不完整。");

            // 安装清单由包内那份直接落盘（见 ExtractPackageEntries）；这里只确认它确实写进去了
            progress?.Invoke("install", 85, "正在写入安装清单…");
            if (!File.Exists(Path.Combine(loaderRoot, InstalledManifestFileName)))
                throw new InvalidDataException($"发布包缺少清单文件：{InstalledManifestFileName}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ToysLog.Error("modloader", "安装发布包时写入游戏目录失败", ex);
            throw new IOException(
                $"写入游戏目录失败：{ex.Message}。" +
                (SpeedhackManager.IsGameRunning() ? "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再更新。" : ""), ex);
        }

        SaveStoredGameDirectory(gameDirectory);
        using var completedZip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        retained.Complete(manifest.Version, HashBytes(ReadEntryBytes(completedZip.GetEntry(LoaderDllName)!)), remainDisabled, packageHash: HashBytes(zipBytes));
        if (skipped.Count > 0)
            ToysLog.Info("modloader", $"保留了游戏目录里原有的同名文件（未覆盖）：{string.Join("、", skipped)}");
        ToysLog.Info("modloader",
            $"CesiumLoader {manifest.Version} 安装完成" + (remainDisabled ? "（保留停用状态）" : ""));
        progress?.Invoke("done", 100, "加载器安装完成");
        return new LoaderInstallResult(manifest.Version, skipped);
    }

    /// <summary>
    /// 把发布包里的每个文件条目按包内布局落地（zip 布局 = 游戏目录布局）。
    /// <list type="bullet">
    ///   <item>根级 <c>version.dll</c> / <c>version.pdb</c> / <c>CesiumLoader.dll</c> / <c>CesiumLoader.pdb</c>
    ///         / NOTICE / LICENSE / README.txt / CHANGELOG.md → 游戏 exe 目录；
    ///         加载器处于「停用」状态时 <c>version.dll</c> 不写回；通用名文件遇到陌生同名文件时跳过并记入
    ///         <paramref name="skipped"/>，绝不覆盖玩家自己的文件；</item>
    ///   <item><c>cesium-loader.json</c> → 加载器目录根（安装清单，版本来源）；</item>
    ///   <item><c>AstralParty_ModLoader\**</c> → 加载器目录下同相对路径。其中
    ///         <c>doorstop_config.json</c> 已存在则不覆盖（保留用户配置），<c>mods\**</c> 走模组落地规则
    ///         （不覆盖已有 <c>config.json</c>、保留 <c>enabled</c>、停用的模组留在 <c>disabled_mods</c>）。</item>
    /// </list>
    /// </summary>
    private static void ExtractPackageEntries(ZipArchive archive, string gameDirectory, string loaderRoot,
        bool remainDisabled, IReadOnlyDictionary<string, bool> preservedModEnabled,
        InstallationRecord? previousInstallation, List<string> skipped)
    {
        var installedBefore = new HashSet<string>(
            previousInstallation?.Files.Keys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;   // 目录条目（logs\ 等目录已由调用方建好）
            var path = entry.FullName.Replace('\\', '/');
            if (path.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(path))
                throw new InvalidDataException($"发布包含有非法路径：{entry.FullName}");

            // ── 安装清单（包内根级文件）→ 加载器目录根，而不是游戏 exe 目录 ────────
            if (path.Equals(InstalledManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                WriteAllBytesProtected(Path.Combine(loaderRoot, InstalledManifestFileName), ReadEntryBytes(entry));
                continue;
            }

            // ── 其余根级文件：落在游戏 exe 目录 ─────────────────────────────────
            if (!path.Contains('/'))
            {
                var target = Path.Combine(gameDirectory, path);
                if (path.Equals(LoaderDllName, StringComparison.OrdinalIgnoreCase) && remainDisabled)
                    continue;   // 加载器停用中：不把 version.dll 写回去
                // 通用名文件只覆盖本工具落过的那一份：玩家自己放的 README.txt / CHANGELOG.md 一律不动
                if (IsGenericRootFileName(path) && File.Exists(target) && !installedBefore.Contains(path))
                {
                    var packageBytes = ReadEntryBytes(entry);
                    if (!MatchesHash(target, HashBytes(packageBytes)))
                    {
                        skipped.Add(path);
                        continue;
                    }
                }
                WriteAllBytesProtected(target, ReadEntryBytes(entry));
                continue;
            }

            // ── 包内加载器目录之外的内容：原样落到加载器目录下 ────────────────
            if (!path.StartsWith(PackageLoaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var otherRelative = path.Replace('/', Path.DirectorySeparatorChar);
                if (!IsSafeRelativePath(otherRelative))
                    throw new InvalidDataException($"发布包含有非法路径：{entry.FullName}");
                var otherTarget = Path.Combine(loaderRoot, otherRelative);
                Directory.CreateDirectory(Path.GetDirectoryName(otherTarget)!);
                WriteAllBytesProtected(otherTarget, ReadEntryBytes(entry));
                continue;
            }

            // ── AstralParty_ModLoader\** ─────────────────────────────────────
            var relative = path.Substring(PackageLoaderPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
            if (!IsSafeRelativePath(relative))
                throw new InvalidDataException($"发布包含有非法路径：{entry.FullName}");

            if (relative.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase))
            {
                // 已存在则保留用户配置（变速 / 开关等），随后调用方单独抬 sdkVersion、补新键
                var configPath = Path.Combine(loaderRoot, ConfigFileName);
                if (!File.Exists(configPath)) WriteAllBytesProtected(configPath, ReadEntryBytes(entry));
                continue;
            }

            if (relative.StartsWith(ModsFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var segments = relative.Split(Path.DirectorySeparatorChar);
                // 停用的 mod 已经躺在 disabled_mods 里：这次更新写回原处，不能借机把它放回 mods（等于偷偷重新启用）
                var targetRoot = segments.Length > 2
                                 && Directory.Exists(Path.Combine(loaderRoot, DisabledModsFolderName, segments[1]))
                    ? DisabledModsFolderName
                    : ModsFolderName;
                var modRelative = segments.Length > 2
                    ? Path.Combine(new[] { targetRoot, segments[1] }.Concat(segments.Skip(2)).ToArray())
                    : relative;
                var target = Path.Combine(loaderRoot, modRelative);
                var fileName = Path.GetFileName(target);

                // 1. 保留已有 mod 配置（config.json）：发布包内默认配置不覆盖用户已调整的值
                if (fileName.Equals(ModConfigFileName, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                    continue;

                var bytes = ReadEntryBytes(entry);

                // 2. sidecar：用户先前设置过启用/禁用状态就保留
                if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    && !fileName.Equals(ModConfigFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var modId = Path.GetFileNameWithoutExtension(fileName);
                    var parentDir = Path.GetFileName(Path.GetDirectoryName(target));
                    if (preservedModEnabled.TryGetValue(modId, out var previousEnabled) ||
                        (!string.IsNullOrEmpty(parentDir) && preservedModEnabled.TryGetValue(parentDir, out previousEnabled)))
                    {
                        bytes = System.Text.Encoding.UTF8.GetBytes(
                            SetJsonBool(System.Text.Encoding.UTF8.GetString(bytes), "enabled", previousEnabled));
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                WriteAllBytesProtected(target, bytes);
                continue;
            }

            // 其余加载器目录内容（sdk\、bootstrap\、NOTICE / LICENSE、日志目录里的文件等）原样落地
            var fileTarget = Path.Combine(loaderRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(fileTarget)!);
            WriteAllBytesProtected(fileTarget, ReadEntryBytes(entry));
        }
    }

    /// <summary>收集两个 mod 目录下所有已装 mod 的用户启用状态（显式设置了 enabled 的）。
    /// 键为 mod 标识符（文件夹名或文件名不含扩展名），不区分大小写。
    /// <c>disabled_mods</c> 里的 mod 一律记成 false：更新加载器不能把它们悄悄搬回 mods。</summary>
    private static Dictionary<string, bool> CaptureInstalledModsEnabled(string loaderRoot)
    {
        var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        CaptureRoot(Path.Combine(loaderRoot, ModsFolderName));
        CaptureRoot(Path.Combine(loaderRoot, DisabledModsFolderName));
        return states;

        void CaptureRoot(string modsDir)
        {
            if (!Directory.Exists(modsDir)) return;
            var disabled = string.Equals(
                Path.GetFileName(modsDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                DisabledModsFolderName, StringComparison.OrdinalIgnoreCase);

            // 1. 旧布局平铺: *.json
            try
            {
                foreach (var file in Directory.EnumerateFiles(modsDir, "*.json"))
                {
                    var fileName = Path.GetFileName(file);
                    if (string.Equals(fileName, ModConfigFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    var modId = Path.GetFileNameWithoutExtension(fileName);
                    var enabled = disabled ? false : ReadSidecarEnabled(file);
                    if (enabled.HasValue) states[modId] = enabled.Value;
                    else if (disabled) states[modId] = false;
                }
            }
            catch { }

            // 2. 新布局: {folder}/{folder}.json (新布局优先覆盖平铺)
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(modsDir))
                {
                    var folderName = Path.GetFileName(subDir);
                    var sidecar = Path.Combine(subDir, folderName + ".json");
                    if (File.Exists(sidecar))
                    {
                        var enabled = disabled ? false : ReadSidecarEnabled(sidecar);
                        if (enabled.HasValue) states[folderName] = enabled.Value;
                        else if (disabled) states[folderName] = false;
                    }
                    else
                    {
                        if (disabled) states[folderName] = false;
                        // 兼容文件夹内 sidecar 与文件夹名不完全一致但为 mod 主配置的情况
                        foreach (var file in Directory.EnumerateFiles(subDir, "*.json"))
                        {
                            var name = Path.GetFileName(file);
                            if (string.Equals(name, ModConfigFileName, StringComparison.OrdinalIgnoreCase)) continue;
                            var modId = Path.GetFileNameWithoutExtension(name);
                            var enabled = disabled ? false : ReadSidecarEnabled(file);
                            if (enabled.HasValue)
                            {
                                states[modId] = enabled.Value;
                                states[folderName] = enabled.Value;
                            }
                            else if (disabled)
                            {
                                states[modId] = false;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>发布包内的相对路径是否安全（不含 ".." 穿越、不是绝对路径）。</summary>
    private static bool IsSafeRelativePath(string relative)
        => !string.IsNullOrWhiteSpace(relative)
           && !relative.Contains("..", StringComparison.Ordinal)
           && !Path.IsPathRooted(relative);

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

}
