using System.IO.Compression;
using System.Text.Json;

namespace AstralParty.Toys.Services;

/// <summary>
/// 导入 mod 包（ZIP）。
///
/// 包布局与 <c>cesium package</c> 和加载器扫描规则一致：
///
/// <code>
/// &lt;ModId&gt;\&lt;ModId&gt;.dll      ← 必须有；DLL 与所在文件夹同名，加载器才认作一个模组
/// &lt;ModId&gt;\&lt;ModId&gt;.json     ← sidecar（可选，声明名字/版本/依赖/启用状态）
/// &lt;ModId&gt;\config.json        ← 模组配置（可选，mod 的 SdkConfig 读写它）
/// &lt;ModId&gt;\...                ← 模组自带的资源（可选，保持包内相对结构）
/// </code>
///
/// 也接受把 <c>&lt;ModId&gt;.dll</c> 直接放在包根目录的包（导入时自动包一层 <c>&lt;ModId&gt;\</c>）；
/// 一个包带多个 mod 文件夹时逐个安装。写入位置就是 <c>mods\&lt;ModId&gt;\</c> —— 与手动解压到 mods 目录等价。
///
/// 已装同名模组时按「升级」处理：
/// <list type="bullet">
///   <item>包内版本比已装的旧时不直接覆盖，先要调用方确认（<c>allowDowngrade</c>）——
///         与加载器安装/更新那条路径的降级闸门保持一致；</item>
///   <item>**不覆盖玩家改过的 config.json**，也保留 sidecar 里的「已禁用」状态；</item>
///   <item>把上一版安装留下、新包已经没有的文件移入 Windows 回收站（清单见 <see cref="SaveModPackageManifest"/>；
///         没有清单的旧安装只清 dll / pdb / json 这类构建产物，不碰玩家的其它文件；
///         回收站不可用时保留原文件，绝不永久删除）。</item>
/// </list>
/// </summary>
public sealed partial class ModManager
{
    /// <summary>mod 包文件（ZIP 本身）大小上限。</summary>
    private const long MaxModPackageFileBytes = 64L * 1024 * 1024;
    /// <summary>mod 包内单个条目的解压上限。</summary>
    private const long MaxModPackageEntryBytes = 64L * 1024 * 1024;
    /// <summary>mod 包解压后的总大小上限。</summary>
    private const long MaxModPackageTotalBytes = 128L * 1024 * 1024;
    /// <summary>mod 包内条目数量上限（带资源的模组可能有很多图片）。</summary>
    private const int MaxModPackageEntries = 8192;

    /// <summary>解包工具留下的、与模组无关的文件名。</summary>
    private static readonly HashSet<string> ModPackageJunkFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", "Thumbs.db", "desktop.ini"
    };

    /// <summary>
    /// 没有安装清单（手动解压装的旧版）时的清理范围：只认这些构建产物，不碰玩家的图片/文档。
    /// </summary>
    private static readonly HashSet<string> ModPackageArtifactExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".pdb", ".json", ".xml"
    };

    /// <summary>测试注入：把旧文件送进回收站的动作（默认走 <see cref="RecycleBinHelper.TryRecycleFile"/>）。
    /// 用它可以稳定验证「回收成功 / 回收站不可用」两条路径，而不往开发者真实回收站里塞临时文件。</summary>
    internal Func<string, (bool Recycled, string Error)>? RecycleFileHook { get; set; }

    /// <summary>导入前预检：包里有哪些模组、会覆盖成什么版本、是否降级、会清掉哪些旧文件。不写任何文件。</summary>
    public ModPackageInspection InspectModPackage(string gameDirectory, string sourcePath)
    {
        using var prepared = PrepareModPackage(gameDirectory, sourcePath);
        var inspection = new ModPackageInspection
        {
            SourcePath = sourcePath,
            SourceName = Path.GetFileName(sourcePath)
        };
        foreach (var plan in prepared.Plans)
        {
            inspection.Mods.Add(new ModPackageInspectionEntry
            {
                ModId = plan.ModId,
                Name = plan.DisplayName,
                Installed = plan.Installed,
                InstalledVersion = plan.InstalledVersion,
                PackageVersion = plan.PackageVersion,
                IsDowngrade = plan.IsDowngrade,
                IsUpgrade = plan.IsUpgrade,
                PreservesConfig = plan.ConfigExisted,
                PreservesDisabled = plan.DisabledBefore,
                RemovedFiles = plan.StaleFiles.ToList()
            });
        }
        inspection.Skipped.AddRange(prepared.Skipped);
        return inspection;
    }

    /// <summary>
    /// 把 mod 包（ZIP）导入游戏 <c>mods</c> 目录。整包先读入内存校验，通过后才落盘：
    /// 路径不安全、条目重复、超出大小限制或找不到模组结构时，一个文件都不会写。
    /// 包内版本比已装的旧时抛 <see cref="InvalidOperationException"/>，除非 <paramref name="allowDowngrade"/>。
    /// </summary>
    public ModImportResult ImportModPackage(string gameDirectory, string sourcePath, bool allowDowngrade = false)
    {
        using var prepared = PrepareModPackage(gameDirectory, sourcePath);
        if (!allowDowngrade)
        {
            var downgrades = prepared.Plans.Where(plan => plan.IsDowngrade).ToList();
            if (downgrades.Count > 0)
                throw new InvalidOperationException(
                    "模组包比游戏目录里已装的更旧：" +
                    string.Join("、", downgrades.Select(plan =>
                        $"{plan.DisplayName} 已装 v{plan.InstalledVersion} → 包内 v{plan.PackageVersion}")) +
                    "。确认要降级后再导入（玩家配置与停用状态仍会保留）。");
        }

        // 先把要写的字节全部读出来：包内容有问题时不会留下半个模组。
        var payloads = prepared.Plans
            .Select(plan => (Plan: plan, Files: plan.Files
                .Select(file => (Relative: plan.RelativePathOf(file), Bytes: ReadEntryBytes(file.Entry)))
                .ToList()))
            .ToList();

        var modsDir = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);
        Directory.CreateDirectory(modsDir);

        var result = new ModImportResult();
        using (var retained = BeginRetainedOperation(gameDirectory, "import-mod-package",
                   payloads.SelectMany(payload => payload.Files.Select(file => file.Bytes)).ToArray()))
        {
            foreach (var (plan, payloadFiles) in payloads)
            {
                var modFolder = Path.Combine(modsDir, plan.FolderName);
                Directory.CreateDirectory(modFolder);

                var sidecarName = plan.ModId + ".json";
                foreach (var (relative, bytes) in payloadFiles)
                {
                    var target = Path.Combine(modFolder, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (!IsInside(modFolder, target))
                        throw new InvalidDataException($"mod 包包含不安全的路径：{relative}");
                    // 玩家改过的配置优先于包内默认配置（新版本加字段时由 mod 自己取默认值）
                    if (plan.ConfigExisted && relative.Equals(ModConfigFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!result.PreservedConfigs.Contains(plan.ModId)) result.PreservedConfigs.Add(plan.ModId);
                        continue;
                    }

                    var content = bytes;
                    if (plan.DisabledBefore && relative.Equals(sidecarName, StringComparison.OrdinalIgnoreCase))
                        content = System.Text.Encoding.UTF8.GetBytes(
                            SetJsonBool(System.Text.Encoding.UTF8.GetString(bytes), "enabled", false));

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    WriteAllBytesProtected(target, content);
                }
                if (plan.DisabledBefore) result.PreservedDisabled.Add(plan.ModId);

                // 旧版本残留放在写完之后清：万一上面写失败，不会被清成半个空模组
                RemoveStaleModFiles(modFolder, plan, result);

                var modDll = Path.Combine(modFolder, plan.ModId + ".dll");
                if (!File.Exists(modDll))
                    throw new InvalidDataException($"mod 包里的 {plan.ModId}.dll 没有成功写入，已停止导入。");
                result.Mods.Add(ScanModEntry(modDll, plan.FolderName));
                result.Changes.Add(new ModPackageChange
                {
                    ModId = plan.ModId,
                    Name = plan.DisplayName,
                    FromVersion = plan.InstalledVersion,
                    ToVersion = plan.PackageVersion,
                    WasInstalled = plan.Installed,
                    IsDowngrade = plan.IsDowngrade,
                    IsUpgrade = plan.IsUpgrade
                });

                SaveModPackageManifest(gameDirectory, plan.ModId, plan.Files.Select(plan.RelativePathOf));
            }

            retained.Complete();
        }

        result.Skipped.AddRange(prepared.Skipped);
        return result;
    }

    /// <summary>预检与导入共用的准备结果：待安装模组计划（含版本/清理信息）+ 被忽略的条目。
    /// 持有打开的 zip：条目要等真正落盘时才读完，用完必须释放。</summary>
    private sealed class PreparedModPackage : IDisposable
    {
        private ZipArchive? _archive;

        public PreparedModPackage(ZipArchive archive) => _archive = archive;

        public ZipArchive Archive => _archive ?? throw new ObjectDisposedException(nameof(PreparedModPackage));
        public List<PackageModPlan> Plans { get; } = new();
        public List<string> Skipped { get; } = new();

        public void Dispose()
        {
            _archive?.Dispose();
            _archive = null;
        }
    }

    /// <summary>校验包并算好「装什么、覆盖成什么版本、清哪些旧文件」。只读，不写盘。</summary>
    private PreparedModPackage PrepareModPackage(string gameDirectory, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException("游戏目录不存在，请先安装加载器或选择游戏目录。");
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("找不到要导入的 mod 包。");
        if (!Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("mod 包必须是 .zip 文件。");
        if (new FileInfo(sourcePath).Length > MaxModPackageFileBytes)
            throw new InvalidOperationException("mod 包不能超过 64 MB。");

        var prepared = new PreparedModPackage(ZipFile.OpenRead(sourcePath));
        try
        {
            var files = ReadModPackageFiles(prepared.Archive);
            prepared.Plans.AddRange(BuildModPackagePlans(files));
            prepared.Skipped.AddRange(SkippedPackageFiles(files, prepared.Plans));
            FillPackagePlanState(gameDirectory, prepared.Plans);
            return prepared;
        }
        catch
        {
            prepared.Dispose();
            throw;
        }
    }

    /// <summary>算每个模组的目标文件夹、已装/包内版本、配置与停用状态、要清理的旧文件。</summary>
    private void FillPackagePlanState(string gameDirectory, List<PackageModPlan> plans)
    {
        var modsDir = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);
        foreach (var plan in plans)
        {
            // 已装的同名模组：沿用它的文件夹写法（大小写），并把版本 / 配置 / 停用状态读出来
            plan.FolderName = ResolveModFolderName(modsDir, plan.ModId);
            var modFolder = Path.Combine(modsDir, plan.FolderName);
            var sidecarPath = Path.Combine(modFolder, plan.ModId + ".json");
            plan.Installed = File.Exists(Path.Combine(modFolder, plan.ModId + ".dll"));
            plan.InstalledVersion = ReadJsonString(ReadTextOrEmpty(sidecarPath), "version");
            plan.ConfigExisted = File.Exists(Path.Combine(modFolder, ModConfigFileName));
            plan.DisabledBefore = ReadSidecarEnabled(sidecarPath) == false;
            // 包内版本以包里的 sidecar 为准（不拿文件名或已装版本顶替）
            var sidecarEntry = plan.Files.FirstOrDefault(file =>
                plan.RelativePathOf(file).Equals(plan.ModId + ".json", StringComparison.OrdinalIgnoreCase));
            if (sidecarEntry is not null)
            {
                var json = System.Text.Encoding.UTF8.GetString(ReadEntryBytes(sidecarEntry.Entry));
                plan.PackageVersion = ReadJsonString(json, "version");
                plan.PackageDisplayName = ReadJsonString(json, "name");
            }
            plan.IsDowngrade = plan.InstalledVersion.Length > 0 && plan.PackageVersion.Length > 0
                               && CompareSemVer(plan.PackageVersion, plan.InstalledVersion) < 0;
            plan.IsUpgrade = plan.InstalledVersion.Length > 0 && plan.PackageVersion.Length > 0
                             && CompareSemVer(plan.PackageVersion, plan.InstalledVersion) > 0;
            plan.StaleFiles = FindStaleModFiles(gameDirectory, plan, modFolder);
        }
    }

    /// <summary>包内单个文件条目：按 <c>/</c> 拆好的相对路径 + 原始 zip 条目。</summary>
    private sealed record PackageFile(string[] Segments, ZipArchiveEntry Entry)
    {
        public string FileName => Segments[^1];

        public string RelativePath => string.Join('/', Segments);
    }

    /// <summary>一个待安装的模组：待写文件 + 相对路径的裁剪基准 + 预检结果。</summary>
    private sealed class PackageModPlan
    {
        public PackageModPlan(string modId, PackageFile dll)
        {
            ModId = modId;
            Add(dll);
        }

        /// <summary>mod 名 = DLL 文件名（不含扩展名），也是目标文件夹名。</summary>
        public string ModId { get; }

        /// <summary>实际写入的文件夹名（沿用已装模组的大小写写法）。</summary>
        public string FolderName { get; set; } = "";

        /// <summary>模组在包内的目录前缀；根目录平铺的包为空（此时相对路径就是文件名）。</summary>
        public string[] FolderSegments { get; init; } = Array.Empty<string>();

        /// <summary>要写进 <c>mods\&lt;ModId&gt;\</c> 的全部条目（含主 DLL）。</summary>
        public List<PackageFile> Files { get; } = new();

        public string PackageVersion { get; set; } = "";
        public string PackageDisplayName { get; set; } = "";
        public string InstalledVersion { get; set; } = "";
        public bool Installed { get; set; }
        public bool IsDowngrade { get; set; }
        public bool IsUpgrade { get; set; }
        public bool ConfigExisted { get; set; }
        public bool DisabledBefore { get; set; }

        /// <summary>升级时要清掉的旧版本残留（相对模组目录）。</summary>
        public List<string> StaleFiles { get; set; } = new();

        /// <summary>给界面用的名字：包内 sidecar 的 name 优先，其次 mod 名。</summary>
        public string DisplayName => PackageDisplayName.Length > 0 ? PackageDisplayName : ModId;

        /// <summary>按包内相对路径去重后加入（同一文件可能被多个规则命中）。</summary>
        public void Add(PackageFile file)
        {
            if (Files.Any(existing => existing.RelativePath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase)))
                return;
            Files.Add(file);
        }

        /// <summary>条目相对模组目录的路径（用 <c>/</c> 分隔）。</summary>
        public string RelativePathOf(PackageFile file) => FolderSegments.Length == 0
            ? file.FileName
            : string.Join('/', file.Segments.Skip(FolderSegments.Length));
    }

    /// <summary>读出并校验包内文件条目（目录条目与解包垃圾文件会被跳过）。</summary>
    private static List<PackageFile> ReadModPackageFiles(ZipArchive archive)
    {
        var files = new List<PackageFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidChars = Path.GetInvalidFileNameChars();
        long total = 0;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;   // 目录条目
            var path = entry.FullName.Replace('\\', '/');
            if (path.Length == 0) continue;
            if (path.StartsWith('/') || path.Contains(':')
                || path.Split('/').Any(segment => segment is ".." or "."))
                throw new InvalidDataException($"mod 包包含不安全的路径：{entry.FullName}");
            if (!seen.Add(path))
                throw new InvalidDataException($"mod 包包含重复文件：{entry.FullName}");
            if (files.Count >= MaxModPackageEntries)
                throw new InvalidDataException("mod 包内文件数量超过限制。");

            total += entry.Length;
            if (entry.Length > MaxModPackageEntryBytes || total > MaxModPackageTotalBytes)
                throw new InvalidDataException("mod 包解压后超过大小限制。");

            var segments = path.Split('/');
            foreach (var segment in segments)
            {
                if (segment.Length == 0 || segment.IndexOfAny(invalidChars) >= 0
                    || segment.EndsWith(' ') || segment.EndsWith('.'))
                    throw new InvalidDataException($"mod 包包含非法的文件名：{entry.FullName}");
            }

            if (IsModPackageJunk(segments)) continue;
            files.Add(new PackageFile(segments, entry));
        }

        return files;
    }

    /// <summary>与模组无关的解包残留（macOS 的 <c>__MACOSX</c> / <c>._*</c>、Windows 的缩略图缓存等）。</summary>
    private static bool IsModPackageJunk(string[] segments)
        => segments.Any(segment => segment.StartsWith("._", StringComparison.Ordinal))
           || segments.Any(segment => segment.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase))
           || ModPackageJunkFileNames.Contains(segments[^1]);

    /// <summary>
    /// 从包内条目推断要安装哪些模组。
    /// 首选 <c>&lt;目录&gt;\&lt;目录&gt;.dll</c> 结构（可以带外层包装目录，可以一次带多个模组）；
    /// 没有该结构时退回「根目录平铺的 &lt;ModId&gt;.dll」，导入时补一层同名文件夹。
    /// </summary>
    private static List<PackageModPlan> BuildModPackagePlans(List<PackageFile> files)
    {
        // 加载器发布包 / SDK 包不是模组：指回「导入发布包」，别把 version.dll 塞进 mods
        if (files.Any(file => file.Segments.Length == 1 && file.FileName.Equals(LoaderDllName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("这是加载器发布包（包含 version.dll），请用「导入发布包」安装。");
        if (files.Any(file => file.Segments.Length == 1 && file.FileName.Equals(SdkDllName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("这是 SDK 包，请用「导入发布包」更新 SDK。");

        var byFolder = new Dictionary<string, PackageModPlan>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file.Segments.Length < 2) continue;
            if (!file.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            var stem = file.FileName[..^4];
            // 只有「DLL 与所在文件夹同名」才是加载器认可的模组
            if (!stem.Equals(file.Segments[^2], StringComparison.OrdinalIgnoreCase)) continue;
            var folder = file.Segments[..^1];
            var key = string.Join('/', folder);
            if (!byFolder.ContainsKey(key))
                byFolder[key] = new PackageModPlan(stem, file) { FolderSegments = folder };
        }

        var plans = new List<PackageModPlan>(byFolder.Values);
        if (plans.Count > 0)
        {
            foreach (var plan in plans)
            {
                foreach (var file in files)
                {
                    if (file.Segments.Length <= plan.FolderSegments.Length) continue;
                    if (!StartsWithSegments(file.Segments, plan.FolderSegments)) continue;
                    plan.Add(file);
                }
            }
        }
        else
        {
            // 根目录平铺：<ModId>.dll 直接放在包根
            var rootDlls = files
                .Where(file => file.Segments.Length == 1
                               && file.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (rootDlls.Count == 0)
                throw new InvalidDataException(
                    "压缩包里没有找到模组：需要 <ModId>/<ModId>.dll 这样的结构，或者把 <ModId>.dll 直接放在包根目录。");

            foreach (var dll in rootDlls)
            {
                var stem = dll.FileName[..^4];
                var plan = new PackageModPlan(stem, dll);
                foreach (var file in files)
                {
                    if (file.Segments.Length != 1 || file.RelativePath.Equals(dll.RelativePath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    // 整个包只有一个 DLL：根目录其余文件（sidecar / pdb / config.json / 资源）都归它；
                    // 有多个 DLL 时只归属同名的附属文件，避免把一个 mod 的文件塞给另一个。
                    if (rootDlls.Count == 1
                        || file.FileName.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase))
                        plan.Add(file);
                }
                plans.Add(plan);
            }
        }

        var duplicates = plans
            .GroupBy(plan => plan.ModId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
            throw new InvalidDataException($"mod 包里有两个同名模组（{duplicates.Key}），已停止导入。");

        plans.Sort((left, right) => string.Compare(left.ModId, right.ModId, StringComparison.OrdinalIgnoreCase));
        return plans;
    }

    /// <summary>没有被归入任何模组的条目（包根目录的说明文档等），用于导入结果里的提示。</summary>
    private static IEnumerable<string> SkippedPackageFiles(List<PackageFile> files, List<PackageModPlan> plans)
    {
        var used = new HashSet<string>(
            plans.SelectMany(plan => plan.Files).Select(file => file.RelativePath),
            StringComparer.OrdinalIgnoreCase);
        return files
            .Where(file => !used.Contains(file.RelativePath))
            .Select(file => file.RelativePath);
    }

    private static bool StartsWithSegments(string[] segments, string[] prefix)
    {
        if (segments.Length < prefix.Length) return false;
        for (var index = 0; index < prefix.Length; index++)
        {
            if (!segments[index].Equals(prefix[index], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    // ============================== 升级：旧版本残留 ==============================

    /// <summary>
    /// 升级前算出会被清掉的旧文件。清单（<see cref="SaveModPackageManifest"/>）存在时只清清单里记过、
    /// 新包已经没有的文件 —— 玩家后来自己放进模组文件夹的东西不动；没有清单的手动安装（本功能之前装的）
    /// 只清 dll / pdb / json / xml 这类构建产物。config.json 永远保留。
    /// </summary>
    private List<string> FindStaleModFiles(string gameDirectory, PackageModPlan plan, string modFolder)
    {
        var stale = new List<string>();
        if (!Directory.Exists(modFolder)) return stale;
        var manifest = ReadModPackageManifest(gameDirectory, plan.ModId);
        var installedFiles = new HashSet<string>(
            plan.Files.Select(plan.RelativePathOf), StringComparer.OrdinalIgnoreCase);
        foreach (var relative in EnumerateModFolderFiles(modFolder))
        {
            if (relative.Equals(ModConfigFileName, StringComparison.OrdinalIgnoreCase)) continue;
            if (installedFiles.Contains(relative)) continue;
            var owned = manifest is null
                ? ModPackageArtifactExtensions.Contains(Path.GetExtension(relative))
                : manifest.Contains(relative);
            if (owned) stale.Add(relative);
        }
        return stale;
    }

    /// <summary>
    /// 删掉计划里标记的旧版本残留。文件走 Windows 回收站（和资源管理器里删除一样，可以还原），
    /// **绝不永久删除**：卷上没有回收站或文件被占用时，旧文件原样保留并记进 <see cref="ModImportResult.KeptFiles"/>。
    /// </summary>
    private void RemoveStaleModFiles(string modFolder, PackageModPlan plan, ModImportResult result)
    {
        var recycle = RecycleFileHook ?? RecycleBinHelper.TryRecycleFile;
        foreach (var relative in plan.StaleFiles)
        {
            var target = Path.Combine(modFolder, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!IsInside(modFolder, target) || !File.Exists(target)) continue;
            var reported = Path.Combine(plan.FolderName, relative).Replace('\\', '/');
            var (recycled, _) = recycle(target);
            if (recycled) result.RemovedFiles.Add(reported);
            else result.KeptFiles.Add(reported);
        }
        RemoveEmptyDirectories(modFolder);
    }

    /// <summary>递归删掉空子目录（新版本不再带的资源目录别留一堆空文件夹）。</summary>
    private static void RemoveEmptyDirectories(string root)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
        }
        catch
        {
            // 清理空目录失败不影响使用
        }
    }

    private static IEnumerable<string> EnumerateModFolderFiles(string modFolder)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
        return files.Select(file => Path.GetRelativePath(modFolder, file).Replace('\\', '/'));
    }

    /// <summary>
    /// 上次导入写了哪些文件（放在配置根的安装历史里，不污染 mods 目录）。
    /// 升级时只清这份清单里记过、新包已经没有的条目，玩家后加的文件不受影响。
    /// </summary>
    private string ModPackageManifestPath(string gameDirectory, string modId)
        => Path.Combine(GameHistoryDirectory(gameDirectory), "packages", modId + ".json");

    private HashSet<string>? ReadModPackageManifest(string gameDirectory, string modId)
    {
        try
        {
            var path = ModPackageManifestPath(gameDirectory, modId);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                return null;
            return new HashSet<string>(
                files.EnumerateArray().Select(element => element.GetString() ?? "").Where(file => file.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private void SaveModPackageManifest(string gameDirectory, string modId, IEnumerable<string> files)
    {
        try
        {
            var path = ModPackageManifestPath(gameDirectory, modId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(
                new { files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // 清单只服务于「下次升级清残留」：写不了不影响本次安装
        }
    }

    // ============================== 小工具 ==============================

    private static string ReadTextOrEmpty(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>从 JSON 文本里读一个字符串字段（容忍注释与 BOM）；读不到返回空串，不猜。</summary>
    private static string ReadJsonString(string json, string field)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var document = JsonDocument.Parse(StripJsonComments(json.TrimStart('\uFEFF')));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(field, out var element)
                   && element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>沿用已有的文件夹写法（Windows 不区分大小写：已存在 <c>mods\handviewermod</c> 时用它），
    /// 避免同一个模组出现两个只差大小写的目录。</summary>
    private static string ResolveModFolderName(string modsDir, string modId)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(modsDir))
            {
                var name = Path.GetFileName(directory);
                if (name.Equals(modId, StringComparison.OrdinalIgnoreCase)) return name;
            }
        }
        catch
        {
            // 扫描失败就用请求的名字
        }
        return modId;
    }

    /// <summary>sidecar 里的 enabled 字段：true / false / null（没有 sidecar 或不认识）。</summary>
    private static bool? ReadSidecarEnabled(string sidecarPath)
    {
        try
        {
            if (!File.Exists(sidecarPath)) return null;
            using var document = JsonDocument.Parse(StripJsonComments(File.ReadAllText(sidecarPath)));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("enabled", out var enabled)) return null;
            return enabled.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary><paramref name="target"/> 是否确实位于 <paramref name="root"/> 之内（防 zip 路径穿越）。</summary>
    private static bool IsInside(string root, string target)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
            return Path.GetFullPath(target).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
