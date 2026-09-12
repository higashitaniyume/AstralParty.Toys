using System.IO.Compression;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// Mod 加载器：内嵌资源、安装 / 卸载 / 覆盖保护、mod 列表 / 导入 / 删除。
/// 一律使用临时沙箱里的假游戏目录与假 profile，不碰真实游戏。
/// </summary>
public sealed class ModManagerTests
{
    [Fact]
    public void EmbeddedResources_AreComplete()
    {
        Assert.True(ModManager.HasEmbeddedLoader, "内置 winmm.dll 资源缺失");
        Assert.True(ModManager.HasEmbeddedSdk, "内置 SDK 资源缺失");
        Assert.True(ModManager.HasEmbeddedSampleMod, "内置示例 mod 资源缺失");
    }

    [Fact]
    public void Install_CopiesLoaderAndCreatesStructure()
    {
        using var harness = new ModHarness("ap-mod-install");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var status = harness.Manager.GetStatus();
        Assert.Equal(harness.GameDirectory, status.GameDirectory);
        Assert.True(status.GameExeFound, "未识别出假 exe 目录");
        Assert.False(status.Installed);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);

        status = harness.Manager.GetStatus();
        Assert.True(status.Installed);
        Assert.True(status.LoaderMatchesBundle, "安装后 winmm.dll 哈希不一致");
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "winmm.dll")), "winmm.dll 未复制");

        // 目录结构 + SDK + 示例 mod
        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Assert.True(Directory.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName)), "mods 目录未创建");
        Assert.True(Directory.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName)), "sdk 目录未创建");
        Assert.True(Directory.Exists(Path.Combine(loaderRoot, ModManager.LogsFolderName)), "logs 目录未创建");
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, ModManager.SdkDllName)), "SDK 未复制");
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName, ModManager.SampleModDllName)), "示例 mod 未复制");

        // 状态里的列表
        Assert.Single(status.Sdk);
        Assert.Single(status.Mods);
        Assert.Equal(ModManager.SampleModDllName, status.Mods[0].FileName);
    }

    [Fact]
    public void Install_WithoutSample_LeavesModsEmpty()
    {
        using var harness = new ModHarness("ap-mod-install-nosample");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var status = harness.Manager.GetStatus();
        Assert.Empty(status.Mods);
        Assert.Single(status.Sdk); // SDK 始终安装
    }

    [Fact]
    public void Install_RefusesToOverwriteForeignDll_UnlessForced()
    {
        using var harness = new ModHarness("ap-mod-overwrite");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "winmm.dll"), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<InvalidOperationException>(
            () => harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true));

        harness.Manager.Install(harness.GameDirectory, overwriteDll: true, includeSampleMod: true);
        Assert.True(harness.Manager.GetStatus().LoaderMatchesBundle, "覆盖安装后哈希应一致");
    }

    [Fact]
    public void Uninstall_RefusesForeignDll_UnlessForced()
    {
        using var harness = new ModHarness("ap-mod-uninstall-guard");
        File.WriteAllBytes(Path.Combine(harness.GameDirectory, "winmm.dll"), [9, 9, 9]);

        Assert.Throws<InvalidOperationException>(() => harness.Manager.Uninstall(harness.GameDirectory, force: false));

        var result = harness.Manager.Uninstall(harness.GameDirectory, force: true);
        Assert.True(result.RemovedDll, "强制卸载应删除 DLL");
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "winmm.dll")));
    }

    [Fact]
    public void Uninstall_RemovesLoaderAndFolder()
    {
        using var harness = new ModHarness("ap-mod-uninstall");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);
        Assert.True(harness.Manager.GetStatus().Installed);

        var result = harness.Manager.Uninstall(harness.GameDirectory, force: false);
        Assert.True(result.RemovedDll);
        Assert.True(result.RemovedConfig);
        Assert.False(File.Exists(Path.Combine(harness.GameDirectory, "winmm.dll")));
        Assert.False(Directory.Exists(Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName)));

        var status = harness.Manager.GetStatus();
        Assert.False(status.Installed);
        Assert.Empty(status.Mods);
    }

    [Fact]
    public void ImportMod_CopiesIntoModsAndAppearsInList()
    {
        using var harness = new ModHarness("ap-mod-import");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: false);

        var source = harness.Sandbox.WriteFile("my-custom-mod.dll", new byte[] { 0x4D, 0x5A, 0x00, 0x01, 0x02, 0x03 });
        var entry = harness.Manager.ImportMod(harness.GameDirectory, source);

        Assert.Equal("my-custom-mod", entry.Name);
        Assert.Equal("my-custom-mod.dll", entry.FileName);
        Assert.True(File.Exists(Path.Combine(
            harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName, "my-custom-mod.dll")));

        var status = harness.Manager.GetStatus();
        Assert.Contains(status.Mods, m => m.FileName == "my-custom-mod.dll");
    }

    [Fact]
    public void DeleteMod_RemovesFromMods()
    {
        using var harness = new ModHarness("ap-mod-delete");
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);
        Assert.Single(harness.Manager.GetStatus().Mods);

        var removed = harness.Manager.DeleteMod(harness.GameDirectory, ModManager.SampleModDllName);
        Assert.NotNull(removed);
        Assert.Equal(ModManager.SampleModDllName, removed.FileName);
        Assert.Empty(harness.Manager.GetStatus().Mods);
    }

    [Fact]
    public void Install_RejectsNonGameDirectory()
    {
        using var harness = new ModHarness("ap-mod-reject");
        // 没有 exe 的目录
        Assert.Throws<InvalidOperationException>(
            () => harness.Manager.Install(harness.Sandbox.EnsureDirectory("empty"), overwriteDll: false, includeSampleMod: true));
    }

    [Fact]
    public void GetStatus_WithoutInstall_ReportsNotInstalled()
    {
        using var harness = new ModHarness("ap-mod-status");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var status = harness.Manager.GetStatus();
        Assert.True(status.BundleLoaderPresent);
        Assert.False(status.Installed);
        Assert.False(status.LoaderPresent);
        Assert.Equal(harness.GameDirectory, status.GameDirectory);
    }

    [Fact]
    public void ScanMods_ReadsSidecarManifest()
    {
        using var harness = new ModHarness("ap-mod-manifest");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);
        harness.Manager.Install(harness.GameDirectory, overwriteDll: false, includeSampleMod: true);

        // 给示例 mod 放一个同名 .json manifest(模拟 SDK SdkManifest.ExportSidecar 的输出)
        var modsDir = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        var sidecar = Path.Combine(modsDir, ModManager.SampleModDllName.Replace(".dll", ".json"));
        File.WriteAllText(sidecar,
            "{\"name\":\"行为日志\",\"version\":\"1.1.0\",\"author\":\"CesiumLoader\",\"description\":\"把对局内的行为输出到控制台\"}");

        var status = harness.Manager.GetStatus();
        var entry = Assert.Single(status.Mods);
        Assert.Equal("行为日志", entry.DisplayName);
        Assert.Equal("1.1.0", entry.Version);
        Assert.Equal("CesiumLoader", entry.Author);
        Assert.Contains("控制台", entry.Description);
        Assert.Equal(ModManager.SampleModDllName, entry.FileName);
    }

    // ============================== 联网更新：包解析 / 校验 / 安装 ==============================

    /// <summary>构造一个符合发布布局的内存 zip（含清单，可带/不带哈希校验）。</summary>
    private static byte[] BuildFakePackage(string version, bool includeHashes = true)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, byte[] content)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }

            var loader = new byte[] { 0x4D, 0x5A, 0x01, 0x02, 0x03 }; // 假 winmm
            var sdk = new byte[] { 0x53, 0x44, 0x4B, 0x01 };          // 假 SDK
            var mod = new byte[] { 0x4D, 0x4F, 0x44, 0x01 };          // 假 mod

            Add("winmm.dll", loader);
            Add("AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll", sdk);
            Add("AstralParty_ModLoader/mods/ActivityLogMod.dll", mod);

            string Sha(byte[] data) => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();

            var manifest = new System.Text.StringBuilder();
            manifest.Append("{\"name\":\"CesiumLoader\",\"version\":\"").Append(version).Append("\",");
            manifest.Append("\"files\":{");
            if (includeHashes)
            {
                manifest.Append("\"winmm.dll\":\"").Append(Sha(loader)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll\":\"").Append(Sha(sdk)).Append("\",");
                manifest.Append("\"AstralParty_ModLoader/mods/ActivityLogMod.dll\":\"").Append(Sha(mod)).Append("\"");
            }
            manifest.Append("}}");
            Add(ModManager.InstalledManifestFileName, System.Text.Encoding.UTF8.GetBytes(manifest.ToString()));
        }
        return memory.ToArray();
    }

    [Fact]
    public void ParsePackageManifest_ReadsVersionAndFiles()
    {
        var bytes = BuildFakePackage("1.2.3");
        var info = ModManager.ParsePackageManifest(bytes);

        Assert.NotNull(info);
        Assert.Equal("1.2.3", info!.Version);
        Assert.Equal(3, info.Files.Count);
        Assert.Contains("winmm.dll", info.Files.Keys);
    }

    [Fact]
    public void InstallPackage_CopiesContentsAndWritesInstalledManifest()
    {
        using var harness = new ModHarness("ap-mod-pkg-install");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("2.0.0");
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: false);

        var loaderRoot = Path.Combine(harness.GameDirectory, ModManager.LoaderFolderName);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "winmm.dll")));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.SdkFolderName, ModManager.SdkDllName)));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.ModsFolderName, ModManager.SampleModDllName)));
        Assert.True(File.Exists(Path.Combine(loaderRoot, ModManager.InstalledManifestFileName)));

        // 已装版本被 GetStatus 读出
        var status = harness.Manager.GetStatus();
        Assert.Equal("2.0.0", status.InstalledVersion);
    }

    [Fact]
    public void InstallPackage_RejectsTamperedFile()
    {
        using var harness = new ModHarness("ap-mod-pkg-tamper");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("3.0.0");
        // 篡改 winmm.dll 内容（保持清单里的哈希不变 → 校验应失败）
        var tampered = TamperZipEntry(bytes, "winmm.dll");

        Assert.Throws<InvalidDataException>(
            () => harness.Manager.InstallPackage(harness.GameDirectory, tampered, overwriteDll: false));
    }

    [Fact]
    public void InstallPackage_WithoutHashes_StillInstalls()
    {
        using var harness = new ModHarness("ap-mod-pkg-nohash");
        harness.Manager.SaveStoredGameDirectory(harness.GameDirectory);

        var bytes = BuildFakePackage("4.0.0", includeHashes: false);
        harness.Manager.InstallPackage(harness.GameDirectory, bytes, overwriteDll: false);

        var status = harness.Manager.GetStatus();
        Assert.Equal("4.0.0", status.InstalledVersion);
        Assert.True(File.Exists(Path.Combine(harness.GameDirectory, "winmm.dll")));
    }

    /// <summary>重建 zip 并把指定条目内容换成别的字节（清单哈希不变 → 校验必失败）。</summary>
    private static byte[] TamperZipEntry(byte[] zipBytes, string entryName)
    {
        using var input = new MemoryStream(zipBytes);
        using (var archive = new ZipArchive(input, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(entryName);
            Assert.NotNull(entry);
            using var stream = entry.Open();
            stream.SetLength(0);
            stream.Write(new byte[] { 0x00, 0x01, 0x02 }, 0, 3);
        }
        input.Position = 0;
        return input.ToArray();
    }

    private sealed class ModHarness : IDisposable
    {
        public ModHarness(string sandboxName)
        {
            Sandbox = new TestSandbox(sandboxName);
            GameDirectory = Sandbox.EnsureDirectory("game");
            File.WriteAllText(Path.Combine(GameDirectory, "AstralParty_CN.exe"), "dummy exe");
            Manager = new ModManager(TestPaths.AppDirectory, Sandbox.EnsureDirectory("profile"));
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public ModManager Manager { get; }

        public void Dispose() => Sandbox.Dispose();
    }
}
