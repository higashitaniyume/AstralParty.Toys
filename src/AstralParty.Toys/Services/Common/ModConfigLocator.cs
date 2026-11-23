using System.Text;

namespace AstralParty.Toys.Services.Common;

/// <summary>
/// mod 玩家配置的位置。与加载器 SDK 的 <c>ModConfig.ResolvePath</c> 保持同一套规则：
///
///  - 唯一位置就是模组目录里的 <c>mods\{ModId}\config.json</c>；停用的模组写在它当前所在的
///    <c>disabled_mods\{ModId}\config.json</c>（不把模组搬回 <c>mods</c>）。
///  - 旧版集中放的 <c>AstralParty_ModLoader\configs\{ModId}.json</c> 已在 4.0.0 废弃
///    （加载器启动时把该目录改名 <c>configs.bak</c>），这里既不再读也不再写：写到那里
///    模组根本不会读，表现就是"界面改了游戏里不生效"。
/// </summary>
public static class ModConfigLocator
{
    public const string LoaderFolderName = "AstralParty_ModLoader";

    /// <summary>加载器目录（AstralParty_ModLoader）。</summary>
    public static string LoaderDirectory(string gameDirectory) =>
        Path.Combine(gameDirectory, LoaderFolderName);

    /// <summary>启用的模组目录（mods）。</summary>
    public static string ModsDirectory(string gameDirectory) =>
        Path.Combine(LoaderDirectory(gameDirectory), "mods");

    /// <summary>停用的模组目录（disabled_mods）。</summary>
    public static string DisabledModsDirectory(string gameDirectory) =>
        Path.Combine(LoaderDirectory(gameDirectory), "disabled_mods");

    /// <summary>
    /// 模组配置的唯一位置（只拼接，不要求文件或模组目录已存在）。
    /// 模组当前在 <c>disabled_mods</c> 里就写那份，否则写 <c>mods\{ModId}\config.json</c>。
    /// </summary>
    public static string Canonical(string gameDirectory, string modId)
    {
        var disabled = Path.Combine(DisabledModsDirectory(gameDirectory), modId);
        if (Directory.Exists(disabled)) return Path.Combine(disabled, "config.json");
        return Path.Combine(ModsDirectory(gameDirectory), modId, "config.json");
    }

    /// <summary>读取用：4.0.0 起与 <see cref="Canonical"/> 是同一个位置（只有一处）。</summary>
    public static string ResolveForRead(string gameDirectory, string modId) =>
        Canonical(gameDirectory, modId);

    /// <summary>写入目标（4.0.0 起只有一个）。保留列表形式，调用方按多目标遍历也不会写错地方。</summary>
    public static IReadOnlyList<string> WriteTargets(string gameDirectory, string modId) =>
        new[] { Canonical(gameDirectory, modId) };

    /// <summary>按字节写入（临时文件 + 替换）；返回写入的路径。</summary>
    public static string WriteAllBytes(string gameDirectory, string modId, byte[] bytes)
    {
        var path = Canonical(gameDirectory, modId);
        WriteAtomic(path, bytes);
        return path;
    }

    /// <summary>按文本写入（默认 UTF-8 无 BOM；<paramref name="bom"/> 为 true 时带 BOM）；返回写入的路径。</summary>
    public static string WriteText(string gameDirectory, string modId, string text, bool bom = false)
        => WriteAllBytes(gameDirectory, modId, new UTF8Encoding(bom).GetBytes(text));

    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            File.Move(temp, path, true);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            throw;
        }
    }
}
