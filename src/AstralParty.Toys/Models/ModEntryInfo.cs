namespace AstralParty.Toys.Services;

/// <summary>Mod 条目信息模型。</summary>
public sealed class ModEntryInfo
{
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";

    /// <summary>mod 文件夹名(相对 mods 目录, 新布局 mods\{DirectoryName}\{FileName})。
    /// 平铺旧布局 mod 为 null。</summary>
    public string? DirectoryName { get; set; }
    public long SizeBytes { get; set; }
    public DateTime ModifiedUtc { get; set; }

    /// <summary>sidecar 的 id（= 程序集名，依赖解析的 key）。</summary>
    public string Id { get; set; } = "";

    /// <summary>sidecar 声明的显示名(mod DLL 旁同名 .json 的 name 字段, 由 SDK 的 SdkManifest.ExportSidecar / cesium CLI 写出)。</summary>
    public string DisplayName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>需要的 SDK 最低版本（API 版本协商用）。</summary>
    public string SdkVersion { get; set; } = "";

    /// <summary>mod 是否启用（sidecar enabled 字段, 缺省 true; false = 加载器跳过）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>权限位掩码（与 C# ModPermission 枚举一致: 1=ReadGameState 2=GameActions 4=SpeedHack 8=FileWrite）。</summary>
    public int Permissions { get; set; }

    /// <summary>依赖的其他 mod（id + 可选最低版本）。</summary>
    public List<ModDependencyInfo> Dependencies { get; set; } = new();
}
