using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AstralParty.Toys.Services;

public sealed partial class ModManager
{
    private const int MaxSdkFileBytes = 16 * 1024 * 1024;

    /// <summary>加载器 ZIP 整包安装；SDK 工具包或单独 DLL 只更新 SDK。</summary>
    public string ImportReleaseFile(string gameDirectory, string sourcePath, bool overwriteDll, bool allowDowngrade = false)
    {
        if (new FileInfo(sourcePath).Length > 64 * 1024 * 1024)
            throw new InvalidOperationException("发布包不能超过 64 MB。");
        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                if (path.StartsWith('/') || path.Contains(':')
                    || path.Split('/').Any(segment => segment is ".." or ".")
                    || !paths.Add(path))
                    throw new InvalidOperationException("发布包包含不安全或重复的路径。");
                total += entry.Length;
                if (entry.Length > 64 * 1024 * 1024 || total > 128 * 1024 * 1024 || paths.Count > 4096)
                    throw new InvalidOperationException("发布包解压大小或文件数量超过限制。");
            }
            if (paths.Contains(LoaderDllName))
            {
                var bytes = File.ReadAllBytes(sourcePath);
                InstallPackage(gameDirectory, bytes, overwriteDll, allowDowngrade);
                return "加载器发布包 " + (ParsePackageManifest(bytes)?.Version ?? "");
            }
        }
        return "SDK " + ImportSdk(gameDirectory, sourcePath);
    }

    /// <summary>导入发布包中的 SDK，不提取或执行开发工具、脚本和其它程序集。</summary>
    public string ImportSdk(string gameDirectory, string sourcePath)
    {
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        if (!File.Exists(Path.Combine(gameDirectory, LoaderDllName))
            || !File.Exists(Path.Combine(loaderRoot, ConfigFileName)))
            throw new InvalidOperationException("请先安装加载器，再导入 SDK。");
        if (new FileInfo(sourcePath).Length > 64 * 1024 * 1024)
            throw new InvalidOperationException("SDK 导入文件不能超过 64 MB。");

        byte[] dll;
        byte[]? pdb = null;
        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                if (path.StartsWith('/') || path.Contains(':')
                    || path.Split('/').Any(segment => segment is ".." or ".")
                    || !entries.TryAdd(path, entry))
                    throw new InvalidOperationException("压缩包包含不安全或重复的路径。");
            }
            var candidates = entries.Where(pair => pair.Key.Equals(SdkDllName, StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals($"{LoaderFolderName}/{SdkFolderName}/{SdkDllName}", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("发布包必须包含唯一的 CesiumLoader.SDK.dll（SDK 工具包或加载器发布包）。");
            dll = ReadSdkEntry(candidates[0].Value);
            var pdbPath = Path.ChangeExtension(candidates[0].Key, ".pdb");
            if (entries.TryGetValue(pdbPath, out var pdbEntry)) pdb = ReadSdkEntry(pdbEntry);
        }
        else
        {
            if (!Path.GetFileName(sourcePath).Equals(SdkDllName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择 SDK 发布 ZIP 或 CesiumLoader.SDK.dll。");
            dll = File.ReadAllBytes(sourcePath);
            var sibling = Path.ChangeExtension(sourcePath, ".pdb");
            if (File.Exists(sibling))
            {
                if (new FileInfo(sibling).Length > MaxSdkFileBytes)
                    throw new InvalidOperationException("SDK 符号文件过大。");
                pdb = File.ReadAllBytes(sibling);
            }
        }

        // 只读 PE 元数据，不把用户选择的程序集加载到工具进程。
        if (dll.Length == 0 || dll.Length > MaxSdkFileBytes)
            throw new InvalidOperationException("SDK 文件为空或超过 16 MB。");
        string version;
        using (var pe = new PEReader(new MemoryStream(dll)))
        {
            if (!pe.HasMetadata) throw new InvalidOperationException("SDK 不是有效的托管程序集。");
            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly) throw new InvalidOperationException("SDK 不是有效的程序集。");
            var definition = metadata.GetAssemblyDefinition();
            if (metadata.GetString(definition.Name) != "CesiumLoader.SDK")
                throw new InvalidOperationException("程序集名称不是 CesiumLoader.SDK。");
            version = definition.Version.ToString(3);
            if (pdb is not null)
            {
                using var symbols = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pdb));
                var id = symbols.GetMetadataReader().DebugMetadataHeader!.Id;
                var symbolGuid = new Guid(id.Take(16).ToArray());
                var matches = pe.ReadDebugDirectory().Any(entry => entry.Type == DebugDirectoryEntryType.CodeView
                    && pe.ReadCodeViewDebugDirectoryData(entry).Guid == symbolGuid);
                if (!matches) throw new InvalidOperationException("SDK 的 PDB 符号与 DLL 不属于同一次构建。");
            }
        }

        var sdkRoot = Path.Combine(loaderRoot, SdkFolderName);
        Directory.CreateDirectory(sdkRoot);
        WriteAllBytesProtected(Path.Combine(sdkRoot, SdkDllName), dll);
        var targetPdb = Path.Combine(sdkRoot, "CesiumLoader.SDK.pdb");
        if (pdb is not null) WriteAllBytesProtected(targetPdb, pdb);
        else if (File.Exists(targetPdb)) File.Delete(targetPdb);
        SyncLoaderConfigSdkVersion(Path.Combine(loaderRoot, ConfigFileName), version);
        return version;
    }

    private static byte[] ReadSdkEntry(ZipArchiveEntry entry)
    {
        if (entry.Length <= 0 || entry.Length > MaxSdkFileBytes)
            throw new InvalidOperationException("SDK 压缩包中的文件为空或超过 16 MB。");
        using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (output.Length + count > MaxSdkFileBytes)
                throw new InvalidOperationException("SDK 解压后的文件超过 16 MB。");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
