using System.IO.Compression;
using System.Text;
using AstralParty.Toys.Mobile;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Mobile.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// M5：设备上的卡面方案。全部走内存文件系统 —— 这些路径在真机上跟 iOS/安卓通道捆在一起，
/// 单测只验证「认不认得出方案、写没写对位置、激活有没有做文本定点替换」。
/// </summary>
public sealed class MobileSkinTests
{
    private const string Root = "Documents/";
    private const string LoaderRoot = Root + MobilePaths.LoaderRelative;

    // ============================== 包解析 ==============================

    [Fact]
    public void SkinPackage_ReadsTheLoaderSkinsLayoutAndStripsEverythingElse()
    {
        using var sandbox = new TestSandbox("ap-skin-pkg-a");
        var zip = sandbox.Combine("default.zip");
        WriteZip(zip, new Dictionary<string, string>
        {
            ["AstralParty_ModLoader/skins/default/UT_HandCard_1.png"] = "one",
            ["AstralParty_ModLoader/skins/default/UT_HandCard_2.png"] = "two",
            ["AstralParty_ModLoader/skins/default/skin.json"] = "{\"name\":\"默认\"}",
            ["AstralParty_ModLoader/skins/default/说明.txt"] = "not carried",
            ["AstralParty_ModLoader/skins/default/thumbs/UT_HandCard_1.png"] = "nested",
        });

        var package = MobileSkinPackage.Read(zip);

        Assert.Equal("default", package.ProfileName);
        Assert.Equal(4, package.Entries.Count);   // 2 图 + skin.json + 子目录里那张图
        Assert.Equal(1, package.Skipped);         // 说明.txt
        Assert.Contains(package.Entries, e => e.RelativePath == "UT_HandCard_1.png");
        Assert.Contains(package.Entries, e => e.RelativePath == "skin.json");
        Assert.Contains(package.Entries, e => e.RelativePath == "thumbs/UT_HandCard_1.png");
        Assert.DoesNotContain(package.Entries, e => e.RelativePath.EndsWith(".txt"));
    }

    [Fact]
    public void SkinPackage_ReadsASingleTopDirectoryAndUsesTheArchiveNameForFlatOnes()
    {
        using var sandbox = new TestSandbox("ap-skin-pkg-bc");

        var nested = sandbox.Combine("MyScheme.zip");
        WriteZip(nested, new Dictionary<string, string>
        {
            ["MyScheme/UT_HandCard_7.png"] = "x",
        });
        Assert.Equal("MyScheme", MobileSkinPackage.Read(nested).ProfileName);

        var flat = sandbox.Combine("cool 皮肤.zip");
        WriteZip(flat, new Dictionary<string, string>
        {
            ["UT_HandCard_8.png"] = "y",
        });
        var fromFlat = MobileSkinPackage.Read(flat);
        Assert.Equal("cool 皮肤", fromFlat.ProfileName);
        Assert.Equal("UT_HandCard_8.png", Assert.Single(fromFlat.Entries).RelativePath);
    }

    [Fact]
    public void SkinPackage_RejectsTraversalDuplicatesAndArchivesWithoutCardFiles()
    {
        using var sandbox = new TestSandbox("ap-skin-pkg-bad");

        var traversal = sandbox.Combine("traversal.zip");
        WriteZip(traversal, new Dictionary<string, string>
        {
            ["../escape/UT_HandCard_1.png"] = "x",
        });
        Assert.Throws<InvalidDataException>(() => { MobileSkinPackage.Read(traversal); });

        var noCards = sandbox.Combine("docs.zip");
        WriteZip(noCards, new Dictionary<string, string>
        {
            ["readme.md"] = "just text",
            ["notes/info.txt"] = "more text",
        });
        Assert.Throws<InvalidDataException>(() => { MobileSkinPackage.Read(noCards); });
    }

    // ============================== 设备状态 ==============================

    [Fact]
    public async Task SkinService_ListsProfilesAndReportsAMissingCardSkinMod()
    {
        var fs = new MemoryFileSystem();
        fs.AddDirectory(Root + MobilePaths.AddressablesRelative);
        fs.AddFile($"{LoaderRoot}/skins/default/UT_HandCard_1.png", "d");
        fs.AddFile($"{LoaderRoot}/skins/MyScheme/UT_HandCard_1.png", "a");
        fs.AddFile($"{LoaderRoot}/skins/MyScheme/UT_HandCard_2.png", "b");
        fs.AddFile($"{LoaderRoot}/skins/MyScheme/skin.json", """{"name":"我的方案","author":"某人"}""");

        var state = await new MobileSkinService(new StubChannel(fs)).GetStateAsync(Target());

        Assert.True(state.HasDefaultPool);
        Assert.False(state.CardSkinModPresent);
        Assert.Empty(state.ActiveSkin);
        Assert.Contains(state.Issues, issue => issue.Contains("CardSkinMod"));

        var scheme = Assert.Single(state.Profiles, p => p.Name == "MyScheme");
        Assert.Equal("我的方案", scheme.DisplayName);
        Assert.Equal("某人", scheme.Author);
        Assert.Equal(2, scheme.CardCount);
        Assert.False(scheme.Active);

        Assert.True(Assert.Single(state.Profiles, p => p.Name == "default").IsDefault);
    }

    [Fact]
    public async Task SkinService_ActivatesByTextPointReplacementAndKeepsComments()
    {
        var fs = new MemoryFileSystem();
        fs.AddDirectory(Root + MobilePaths.AddressablesRelative);
        fs.AddFile($"{LoaderRoot}/skins/MyScheme/UT_HandCard_1.png", "a");
        fs.AddFile($"{LoaderRoot}/mods/CardSkinMod/CardSkinMod.dll", "dll");
        const string original = """
            {
              // 玩家自己写的注释，改配置不能把它弄丢
              "Enabled": true,
              "ActiveSkin": "default",
              "Scale": 1.0
            }
            """;
        fs.AddFile($"{LoaderRoot}/mods/CardSkinMod/config.json", original);

        var service = new MobileSkinService(new StubChannel(fs));
        var result = await service.SetActiveAsync(Target(), "MyScheme");

        Assert.True(result.Ok, result.Message);
        var text = fs.ReadText($"{LoaderRoot}/mods/CardSkinMod/config.json");
        Assert.Contains("// 玩家自己写的注释，改配置不能把它弄丢", text);
        Assert.Contains("\"ActiveSkin\": \"MyScheme\"", text);
        Assert.Contains("\"Scale\": 1.0", text);   // 别的字段原样保留，1.0 没被改写成 1

        var state = await service.GetStateAsync(Target());
        Assert.Equal("MyScheme", state.ActiveSkin);
        Assert.True(Assert.Single(state.Profiles, p => p.Name == "MyScheme").Active);
    }

    [Fact]
    public async Task SkinService_FallsBackToGameDefaultAndRefusesWhenTheModIsAbsent()
    {
        var fs = new MemoryFileSystem();
        fs.AddDirectory(Root + MobilePaths.AddressablesRelative);
        fs.AddFile($"{LoaderRoot}/skins/MyScheme/UT_HandCard_1.png", "a");

        var service = new MobileSkinService(new StubChannel(fs));

        // 没有 CardSkinMod：不能激活，但要说清原因，不能静默成功
        var blocked = await service.SetActiveAsync(Target(), "MyScheme");
        Assert.False(blocked.Ok);
        Assert.Contains("CardSkinMod", blocked.Message);

        // 装了模组但还没有 config.json：按游戏默认起手，写入要新建文件
        fs.AddFile($"{LoaderRoot}/mods/CardSkinMod/CardSkinMod.dll", "dll");
        var created = await service.SetActiveAsync(Target(), "MyScheme");
        Assert.True(created.Ok, created.Message);
        var text = fs.ReadText($"{LoaderRoot}/mods/CardSkinMod/config.json");
        Assert.Contains("\"ActiveSkin\": \"MyScheme\"", text);
        Assert.Contains("\"Enabled\": true", text);

        // 切回游戏默认：Enabled=false，ActiveSkin 留着（下次启用回到原方案）
        var back = await service.SetActiveAsync(Target(), MobileSkinService.GameDefault);
        Assert.True(back.Ok, back.Message);
        text = fs.ReadText($"{LoaderRoot}/mods/CardSkinMod/config.json");
        Assert.Contains("\"Enabled\": false", text);
        Assert.Contains("\"ActiveSkin\": \"MyScheme\"", text);
        Assert.Equal(MobileSkinService.GameDefault, (await service.GetStateAsync(Target())).ActiveSkin);
    }

    [Fact]
    public async Task SkinService_ImportsEveryCardFileAndActivatesWhenAsked()
    {
        var fs = new MemoryFileSystem();
        fs.AddDirectory(Root + MobilePaths.AddressablesRelative);
        fs.AddFile($"{LoaderRoot}/mods/CardSkinMod/CardSkinMod.dll", "dll");

        using var sandbox = new TestSandbox("ap-skin-import");
        var zip = sandbox.Combine("Imported.zip");
        WriteZip(zip, new Dictionary<string, string>
        {
            ["Imported/UT_HandCard_1.png"] = "first",
            ["Imported/UT_HandCard_2.png"] = "second",
            ["Imported/sub/UT_HandCard_3.png"] = "third",
        });

        var service = new MobileSkinService(new StubChannel(fs));
        var result = await service.ImportAsync(Target(), zip, activate: true);

        Assert.True(result.Ok, result.Message);
        Assert.Equal("first", fs.ReadText($"{LoaderRoot}/skins/Imported/UT_HandCard_1.png"));
        Assert.Equal("third", fs.ReadText($"{LoaderRoot}/skins/Imported/sub/UT_HandCard_3.png"));

        var state = await service.GetStateAsync(Target());
        Assert.Equal("Imported", state.ActiveSkin);
        // 顶层的两张才算「方案里有几张卡」（与桌面版 GetCards 的 TopDirectoryOnly 一致）；
        // sub/ 那张已经写到设备上了，只是不进这个计数
        Assert.Equal(2, Assert.Single(state.Profiles, p => p.Name == "Imported").CardCount);
    }

    [Fact]
    public async Task SkinService_RefusesToDeleteTheActiveOrDefaultProfileButDeletesTheRest()
    {
        var fs = new MemoryFileSystem();
        fs.AddDirectory(Root + MobilePaths.AddressablesRelative);
        fs.AddFile($"{LoaderRoot}/skins/default/UT_HandCard_1.png", "d");
        fs.AddFile($"{LoaderRoot}/skins/Active/UT_HandCard_1.png", "a");
        fs.AddFile($"{LoaderRoot}/skins/Old/UT_HandCard_1.png", "o");
        fs.AddFile($"{LoaderRoot}/mods/CardSkinMod/config.json",
            "{\"Enabled\": true, \"ActiveSkin\": \"Active\"}");

        var service = new MobileSkinService(new StubChannel(fs));

        Assert.False((await service.DeleteAsync(Target(), "default")).Ok);
        var active = await service.DeleteAsync(Target(), "Active");
        Assert.False(active.Ok);
        Assert.Contains("正在生效", active.Message);

        var deleted = await service.DeleteAsync(Target(), "Old");
        Assert.True(deleted.Ok, deleted.Message);
        Assert.False(fs.Exists($"{LoaderRoot}/skins/Old"));
    }

    // ============================== 脚手架 ==============================

    private static MobileTarget Target() => new() { Id = "ios:test", Kind = "ios", Serial = "test", Label = "Test" };

    private static void WriteZip(string path, IReadOnlyDictionary<string, string> entries)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var target = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            target.Write(bytes, 0, bytes.Length);
        }
    }

    /// <summary>只认一个通道、一个容器根的最小通道实现。</summary>
    private sealed class StubChannel(IDeviceFileSystem fileSystem) : IDeviceChannel
    {
        public string Kind => "ios";

        public string BuildRoot(MobileTarget target) => Root;

        public Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeviceInfo>>([]);

        public Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(
            string serial, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MobilePackageCandidate>>([]);

        public Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(fileSystem);
    }

    /// <summary>路径 → 字节的内存设备。目录用显式的集合表示，因为服务会先探测容器根在不在。</summary>
    private sealed class MemoryFileSystem : IDeviceFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

        public void AddFile(string path, string content)
        {
            _files[path] = Encoding.UTF8.GetBytes(content);
            // 真机上「有文件」就意味着父目录存在；ExistsAsync 对目录也必须返回 true，
            // 否则服务里的「方案目录在不在」检查会被假文件系统骗成不存在。
            var parts = path.Split('/');
            for (var i = 1; i < parts.Length; i++)
                _directories.Add(string.Join('/', parts.Take(i)));
        }

        public void AddDirectory(string path) => _directories.Add(path.TrimEnd('/'));

        public string ReadText(string path) => Encoding.UTF8.GetString(_files[path]);

        public bool Exists(string path) =>
            _files.ContainsKey(path) || _directories.Contains(path.TrimEnd('/'));

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Exists(path));

        public Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files.TryGetValue(path, out var bytes)
                ? new DeviceFileInfo(bytes.Length, null)
                : (DeviceFileInfo?)null);

        public Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var prefix = path.TrimEnd('/') + "/";
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in _files.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
                names.Add(file[prefix.Length..].Split('/')[0]);
            foreach (var directory in _directories.Where(dir => dir.StartsWith(prefix, StringComparison.Ordinal)))
                names.Add(directory[prefix.Length..].Split('/')[0]);
            return Task.FromResult<IReadOnlyList<string>>(names.ToList());
        }

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files[path]);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReadText(path));

        public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            _files[path] = data;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            var prefix = path.TrimEnd('/') + "/";
            foreach (var key in _files.Keys.Where(k => k == path || k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
                _files.Remove(key);
            foreach (var directory in _directories.Where(d => d == path || d.StartsWith(prefix, StringComparison.Ordinal)).ToList())
                _directories.Remove(directory);
            _files.Remove(path);
            return Task.CompletedTask;
        }

        public Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default)
        {
            var parts = path.TrimEnd('/').Split('/');
            for (var i = 1; i <= parts.Length; i++)
                _directories.Add(string.Join('/', parts.Take(i)));
            return Task.CompletedTask;
        }

        public Task RenameAsync(string from, string to, CancellationToken cancellationToken = default)
        {
            var prefix = from.TrimEnd('/') + "/";
            foreach (var key in _files.Keys.Where(k => k == from || k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                var moved = to + key[from.Length..];
                _files[moved] = _files[key];
                _files.Remove(key);
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
