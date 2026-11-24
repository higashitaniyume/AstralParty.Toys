using System.Text.Json.Serialization;

namespace AstralParty.Toys.Mobile.Models;

/// <summary>
/// 设备上的一个卡面方案：<c>&lt;加载器根&gt;/skins/&lt;方案&gt;/</c> 里的一批图片。
/// 与桌面版同构（桌面是 <c>&lt;游戏目录&gt;\AstralParty_ModLoader\skins\</c>），只是容器路径换成设备上的。
/// </summary>
public sealed class MobileSkinProfile
{
    /// <summary>目录名，也是 <c>CardSkinMod</c> 的 <c>ActiveSkin</c> 值。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary><c>skin.json</c> 里的显示名；没有就退回目录名。</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>目录里的图片数量（只数顶层图片，与桌面版 <c>GetCards</c> 的口径一致）。</summary>
    [JsonPropertyName("cardCount")]
    public int CardCount { get; set; }

    /// <summary>是否为当前生效方案。</summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>是否是 <c>default</c>（默认回退方案，不作为可选方案展示的重复项）。</summary>
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    /// <summary>目录里是否有 <c>skin.json</c>。</summary>
    [JsonPropertyName("hasMetadata")]
    public bool HasMetadata { get; set; }
}

/// <summary>
/// 设备上卡面方案的整体状态：方案清单 + 当前生效值 + <c>CardSkinMod</c> 是否在设备上。
/// 模组不在时清单仍要能显示（用户可以先把图片放上去），只是不能激活。
/// </summary>
public sealed class MobileSkinState
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";

    [JsonPropertyName("skinsRoot")]
    public string SkinsRoot { get; set; } = "";

    [JsonPropertyName("profiles")]
    public List<MobileSkinProfile> Profiles { get; set; } = [];

    /// <summary><c>CardSkinMod</c> 的 <c>ActiveSkin</c>；<c>Enabled=false</c> 时这里是 <c>__game_default__</c>。</summary>
    [JsonPropertyName("activeSkin")]
    public string ActiveSkin { get; set; } = "";

    /// <summary>设备上有没有 <c>mods/CardSkinMod/</c>（没有就不能激活，只能在设备上装好模组后再来）。</summary>
    [JsonPropertyName("cardSkinModPresent")]
    public bool CardSkinModPresent { get; set; }

    /// <summary>设备上有没有 <c>skins/default/</c>：没有的话方案只有覆盖图，缺图仍按游戏原版显示。</summary>
    [JsonPropertyName("hasDefaultPool")]
    public bool HasDefaultPool { get; set; }

    /// <summary>读不出来的部分（目录不存在、权限不足…），界面照实提示，不假装空方案。</summary>
    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = [];
}
