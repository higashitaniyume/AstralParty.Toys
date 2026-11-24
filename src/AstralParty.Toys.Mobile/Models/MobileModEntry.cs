namespace AstralParty.Toys.Mobile.Models;

/// <summary>
/// 设备上的一个模组。启用状态**只看目录**：在 <c>mods/</c> 里就是启用，在 <c>disabled_mods/</c> 里就是停用。
/// sidecar 里的 <c>enabled</c> 是打包期默认值，安卓引导运行期不读它 —— 拿它当状态就会出现「显示停用、实际在加载」。
/// </summary>
public sealed class MobileModEntry
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "";

    public string SdkVersion { get; set; } = "";

    public string Description { get; set; } = "";

    /// <summary>与 <see cref="Directory"/> 一致：在 <c>mods</c> 下才是启用。</summary>
    public bool Enabled { get; set; }

    /// <summary><c>mods</c> 或 <c>disabled_mods</c>。</summary>
    public string Directory { get; set; } = "";

    public bool HasConfig { get; set; }
}
