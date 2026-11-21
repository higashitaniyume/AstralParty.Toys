namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 发布包解压安装时「落在游戏 exe 目录」的那几个根级文件的规则。
///
/// 发布包根目录是游戏目录布局：<c>version.dll</c> / <c>version.pdb</c> 是加载器本体，
/// <c>CesiumLoader-NOTICE.txt</c> / <c>CesiumLoader-LICENSE.txt</c> 是许可文件，
/// <c>README.txt</c> / <c>CHANGELOG.md</c> 是通用名说明文件。
/// 通用名文件可能和玩家自己放在游戏目录里的文件撞名，所以**只覆盖本工具落过的那一份**：
/// 安装记录（<see cref="InstallationRecord.Files"/>）里有它 = 本工具写过 → 可以覆盖 / 删除；
/// 没有 = 陌生文件 → 安装时跳过并提示，卸载时也不碰。
/// </summary>
public sealed partial class ModManager
{
    /// <summary>发布包在游戏 exe 目录落下的版权声明文件名。</summary>
    public const string NoticeFileName = "CesiumLoader-NOTICE.txt";

    /// <summary>发布包在游戏 exe 目录落下的许可证文件名。</summary>
    public const string LicenseFileName = "CesiumLoader-LICENSE.txt";

    /// <summary>发布包根目录里的通用名文件：可能和玩家的文件撞名，需要保护。</summary>
    private static readonly string[] GenericRootFileNames = { "README.txt", "CHANGELOG.md" };

    /// <summary>是否是通用名根级文件（大小写不敏感）。</summary>
    private static bool IsGenericRootFileName(string fileName)
        => GenericRootFileNames.Any(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 发布包落在游戏 exe 目录、且属于本工具的文件名（相对名）。用于卸载与安装快照：
    /// <list type="bullet">
    ///   <item><c>version.dll</c> / <c>version.pdb</c> 与 <c>CesiumLoader-*</c> → 按名字直接认定；</item>
    ///   <item>通用名（README.txt / CHANGELOG.md）→ 只有安装记录里出现过（本工具落过）才算。</item>
    /// </list>
    /// </summary>
    private static IEnumerable<string> RootFilesOwnedByLoader(InstallationRecord? record)
    {
        yield return LoaderDllName;
        yield return Path.ChangeExtension(LoaderDllName, ".pdb");
        yield return NoticeFileName;
        yield return LicenseFileName;
        if (record is null) yield break;
        foreach (var relative in record.Files.Keys)
        {
            if (relative.Contains('/') || relative.Contains('\\')) continue;
            if (IsGenericRootFileName(relative)) yield return relative;
        }
    }
}
