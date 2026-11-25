using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.App;

/// <summary>
/// 更新日志弹窗的触发判定与内容头部规范化（纯逻辑，便于单元测试）。
/// 弹窗统一渲染完整 CHANGELOG.md，不做版本裁剪。
/// </summary>
public static partial class UpdateChangelogService
{
    public const string AppWelcomeParagraph =
        "欢迎您使用 **AstralParty.Toys**（吉星派对工具箱）与 **CesiumLoader** 模组加载器，衷心感谢您的支持与陪伴！如果您在使用过程中遇到任何问题、Bug 或有功能建议，非常欢迎前往 [GitHub Issues](https://github.com/higashitaniyume/AstralParty.Toys/issues) 提交反馈，也欢迎加入企鹅群 **1078464597**（[点击加群](https://qm.qq.com/cgi-bin/qm/qr?k=tYrqev4U2Xr2PPAA4EkILA0NKmP3zgeU&jump_from=webapi&authKey=jHiIqsg0jEV53dBtQ9V3qXohKKHwLDGmfWprcrg7gueM9pU9iz+ST/QHR6d1j3wa)）或 [Discord 频道](https://discord.gg/SZaYSwGX94) 和我们交流，我们会认真查看并尽力为您排查解决。";

    public const string LoaderWelcomeParagraph =
        "欢迎您使用 **CesiumLoader** 模组加载器，衷心感谢您的信任与支持！如果您在游戏或模组加载过程中遇到任何异常、兼容问题或有改进建议，非常欢迎前往 [GitHub Issues](https://github.com/higashitaniyume/CesiumLoader/issues) 反馈，也可以加入企鹅群 **1078464597**（[点击加群](https://qm.qq.com/cgi-bin/qm/qr?k=tYrqev4U2Xr2PPAA4EkILA0NKmP3zgeU&jump_from=webapi&authKey=jHiIqsg0jEV53dBtQ9V3qXohKKHwLDGmfWprcrg7gueM9pU9iz+ST/QHR6d1j3wa)）或 [Discord 频道](https://discord.gg/SZaYSwGX94) 与我们联系交流，我们会认真倾听并尽力协助处理。";

    // 匹配旧版内置包或历史缓存里携带的“本文件按版本记录 ... 在打 tag 时自动构建。”头部说明。
    [GeneratedRegex(@"^本文件按版本记录[^\r\n]*在打\s*tag\s*时自动构建。\s*$", RegexOptions.Multiline)]
    private static partial Regex LegacyHeaderIntroRegex();

    /// <summary>
    /// 保留完整日志内容，仅在遇到旧版发布说明占位句时替换为诚恳的欢迎与反馈引导语。
    /// </summary>
    public static string NormalizeHeader(string? markdown, bool isLoader)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var replacement = isLoader ? LoaderWelcomeParagraph : AppWelcomeParagraph;
        return LegacyHeaderIntroRegex().Replace(markdown, replacement, 1).Trim();
    }

    /// <summary>
    /// 判断应用启动后是否应自动弹出更新日志弹窗（全新安装或版本更新完成）。
    /// </summary>
    public static StartupChangelogDecision? EvaluateStartup(
        string? currentAppVersion,
        string? currentEmbeddedLoaderVersion,
        SelfUpdateOutcome? selfUpdateOutcome,
        UpdateConfigSection updateConfig)
    {
        if (updateConfig is null) return null;
        // 原地更新未生效（程序未退出或失败回滚）时，实际还是旧版，不当作更新完毕弹窗。
        if (selfUpdateOutcome is { Ok: false }) return null;
        if (!updateConfig.AutoChangelogPopup) return null;

        var appVer = (currentAppVersion ?? "").Trim();
        var loaderVer = (currentEmbeddedLoaderVersion ?? "").Trim();
        if (string.IsNullOrEmpty(appVer)) return null;

        var lastApp = (updateConfig.LastSeenAppVersion ?? "").Trim();
        var lastLoader = (updateConfig.LastSeenEmbeddedLoaderVersion ?? "").Trim();
        var loaderChanged = !string.IsNullOrEmpty(loaderVer)
            && !string.IsNullOrEmpty(lastLoader)
            && !string.Equals(lastLoader, loaderVer, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(lastApp))
        {
            return new StartupChangelogDecision(
                Trigger: "fresh-install",
                PrevAppVersion: "",
                CurrentAppVersion: appVer,
                PrevLoaderVersion: "",
                CurrentLoaderVersion: loaderVer,
                LoaderChanged: !string.IsNullOrEmpty(loaderVer));
        }

        if (!string.Equals(lastApp, appVer, StringComparison.OrdinalIgnoreCase)
            || selfUpdateOutcome is { Ok: true })
        {
            return new StartupChangelogDecision(
                Trigger: "app-updated",
                PrevAppVersion: lastApp,
                CurrentAppVersion: appVer,
                PrevLoaderVersion: lastLoader,
                CurrentLoaderVersion: loaderVer,
                LoaderChanged: loaderChanged);
        }

        if (loaderChanged)
        {
            return new StartupChangelogDecision(
                Trigger: "app-updated",
                PrevAppVersion: lastApp,
                CurrentAppVersion: appVer,
                PrevLoaderVersion: lastLoader,
                CurrentLoaderVersion: loaderVer,
                LoaderChanged: true);
        }

        return null;
    }
}

public sealed record StartupChangelogDecision(
    string Trigger,
    string PrevAppVersion,
    string CurrentAppVersion,
    string PrevLoaderVersion,
    string CurrentLoaderVersion,
    bool LoaderChanged);
