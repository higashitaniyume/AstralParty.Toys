using System.Security.Cryptography;
using AstralParty.Toys.Services.Modding;

namespace AstralParty.Toys.Tests.Support;

/// <summary>模组方案 / 回滚测试用的沙箱：一个假游戏目录（含加载器目录、mods、disabled_mods）+ 数据根。</summary>
internal sealed class ModProfileHarness : IDisposable
{
    /// <summary>带注释的加载器配置模板：回滚必须按原始字节还原，包括这些注释。</summary>
    public const string LoaderJson = """
{
  // 加载器配置（注释也要原样保留）
  "enabled": true,
  "speedhackBaseSpeed": 1.0,
  "steamBypassEnabled": true,
  "steamBypassMatchmaking": true,
  "steamBypassLobbyQuery": true,
  "profile": "P2",
  "profileWindowTitle": true,
  "sdkVersion": "3.8.5"
}
""";

    public ModProfileHarness(string name)
    {
        Sandbox = new TestSandbox(name);
        GameDirectory = Sandbox.EnsureDirectory("game");
        LoaderRoot = Sandbox.EnsureDirectory("game", ModManager.LoaderFolderName);
        ModsRoot = Sandbox.EnsureDirectory("game", ModManager.LoaderFolderName, ModManager.ModsFolderName);
        DisabledRoot = Sandbox.EnsureDirectory("game", ModManager.LoaderFolderName, ModManager.DisabledModsFolderName);
        File.WriteAllText(LoaderConfigPath, LoaderJson);

        var dataRoot = Sandbox.EnsureDirectory("data");
        Manager = new ModManager(TestPaths.AppDirectory, dataRoot);
        Manager.SaveStoredGameDirectory(GameDirectory);
        Service = new ModProfileService(Manager, GameDirectory);
    }

    public TestSandbox Sandbox { get; }
    public string GameDirectory { get; }
    public string LoaderRoot { get; }
    public string ModsRoot { get; }
    public string DisabledRoot { get; }
    public string LoaderConfigPath => Path.Combine(LoaderRoot, ModManager.ConfigFileName);
    public ModManager Manager { get; }
    public ModProfileService Service { get; }

    /// <summary>建一个模组目录（新布局），返回目录路径。</summary>
    public string AddMod(string name, bool enabled = true, string? config = null, string? sidecar = null)
    {
        var root = enabled ? ModsRoot : DisabledRoot;
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, name + ".dll"), new byte[] { 0x4D, 0x5A, 0x01, 0x00 });
        var json = sidecar ??
                   "{\"id\":\"" + name + "\",\"name\":\"" + name + "\",\"version\":\"1.0.0\",\"enabled\":" +
                   (enabled ? "true" : "false") + "}";
        File.WriteAllText(Path.Combine(dir, name + ".json"), json);
        if (config is not null) File.WriteAllText(Path.Combine(dir, "config.json"), config);
        return dir;
    }

    /// <summary>模组的玩家配置(现行: configs\{ModId}.json; 与启停状态无关, 所以忽略 enabled)。</summary>
    public string ModConfigPath(string name, bool enabled = true)
    {
        var configs = Sandbox.EnsureDirectory("game", ModManager.LoaderFolderName, "configs");
        return Path.Combine(configs, name + ".json");
    }

    public string ModSidecarPath(string name, bool enabled = true)
        => Path.Combine(enabled ? ModsRoot : DisabledRoot, name, name + ".json");

    /// <summary>另建一个游戏目录（含加载器目录与模板配置），用于验证方案全局共享。</summary>
    public string CreateGameDirectory(string folder)
    {
        var game = Sandbox.EnsureDirectory(folder);
        var loader = Sandbox.EnsureDirectory(folder, ModManager.LoaderFolderName);
        Sandbox.EnsureDirectory(folder, ModManager.LoaderFolderName, ModManager.ModsFolderName);
        Sandbox.EnsureDirectory(folder, ModManager.LoaderFolderName, ModManager.DisabledModsFolderName);
        File.WriteAllText(Path.Combine(loader, ModManager.ConfigFileName), LoaderJson);
        return game;
    }

    /// <summary>整个加载器目录的指纹：相对路径 → SHA-256。用来证明回滚是"逐字节 + 位置"完全一致。</summary>
    public SortedDictionary<string, string> Snapshot()
    {
        var result = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(LoaderRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(LoaderRoot, file).Replace('\\', '/');
            result[relative] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        }
        return result;
    }

    public void Dispose() => Sandbox.Dispose();
}
