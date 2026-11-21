namespace AstralParty.Toys.Models;

/// <summary>一次「安装 / 更新 CesiumLoader 发布包」的结果。</summary>
/// <param name="Version">包内清单声明的加载器版本号。</param>
/// <param name="SkippedFiles">因为游戏目录里已有「不是本工具落的」同名通用名文件而跳过的文件名（相对游戏 exe 目录）。</param>
public sealed record LoaderInstallResult(string Version, IReadOnlyList<string> SkippedFiles)
{
    /// <summary>是否有被保留（未覆盖）的玩家文件。</summary>
    public bool HasSkippedFiles => SkippedFiles.Count > 0;
}
