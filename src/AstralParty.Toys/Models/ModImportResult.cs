namespace AstralParty.Toys.Services;

/// <summary>导入 mod 包（ZIP）的结果：一个包可以包含多个模组。</summary>
public sealed class ModImportResult
{
    /// <summary>本次装进 mods 目录的模组（元数据来自落盘后的 sidecar）。</summary>
    public List<ModEntryInfo> Mods { get; set; } = new();

    /// <summary>每个模组的版本变化（新装 / 升级 / 降级 / 版本未知），供界面拼一句人话。</summary>
    public List<ModPackageChange> Changes { get; set; } = new();

    /// <summary>包内没有归入任何模组、被忽略的条目（根目录说明文档、其它工具的文件等）。</summary>
    public List<string> Skipped { get; set; } = new();

    /// <summary>保留了用户已有 config.json 的模组名 —— 升级包不会覆盖玩家改过的配置。</summary>
    public List<string> PreservedConfigs { get; set; } = new();

    /// <summary>沿用了用户「已禁用」状态的模组名 —— 导入不会悄悄把停用的模组重新打开。</summary>
    public List<string> PreservedDisabled { get; set; } = new();

    /// <summary>升级时移入回收站的旧版本残留（相对 mods 目录的路径）。</summary>
    public List<string> RemovedFiles { get; set; } = new();

    /// <summary>没能移入回收站、因而已原样保留的旧版本文件（回收站不可用或被占用）。
    /// 这里刻意不退回永久删除 —— 旧文件留着不影响新版本加载。</summary>
    public List<string> KeptFiles { get; set; } = new();
}

/// <summary>一个模组在一次导入中的版本变化。</summary>
public sealed class ModPackageChange
{
    public string ModId { get; set; } = "";

    /// <summary>显示名（包内 sidecar 的 name），没有就用 ModId。</summary>
    public string Name { get; set; } = "";

    /// <summary>导入前已装的版本；新装时为空。</summary>
    public string FromVersion { get; set; } = "";

    /// <summary>包内版本；sidecar 没写时为空（不猜）。</summary>
    public string ToVersion { get; set; } = "";

    public bool WasInstalled { get; set; }
    public bool IsDowngrade { get; set; }
    public bool IsUpgrade { get; set; }
}

/// <summary>模组包的导入前预检：包里有什么、会覆盖成什么版本、会不会降级。</summary>
public sealed class ModPackageInspection
{
    public string SourcePath { get; set; } = "";
    public string SourceName { get; set; } = "";
    public List<ModPackageInspectionEntry> Mods { get; set; } = new();
    public List<string> Skipped { get; set; } = new();

    /// <summary>包里有模组比已装的旧 —— 导入前需要用户确认。</summary>
    public bool HasDowngrade => Mods.Exists(mod => mod.IsDowngrade);
}

/// <summary>预检里单个模组的情况。</summary>
public sealed class ModPackageInspectionEntry
{
    public string ModId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Installed { get; set; }
    public string InstalledVersion { get; set; } = "";
    public string PackageVersion { get; set; } = "";
    public bool IsDowngrade { get; set; }
    public bool IsUpgrade { get; set; }

    /// <summary>已装模组改过配置：导入会保留它。</summary>
    public bool PreservesConfig { get; set; }

    /// <summary>已装模组被停用：导入后会继续停用。</summary>
    public bool PreservesDisabled { get; set; }

    /// <summary>导入时会清掉的旧版本残留文件。</summary>
    public List<string> RemovedFiles { get; set; } = new();
}
