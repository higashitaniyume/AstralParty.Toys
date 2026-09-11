using System.Reflection;
using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests.Support;

/// <summary>
/// 测试运行环境的路径约定。
/// 测试输出目录里应当同时有 <c>Protocol\</c>（游戏协议程序集）与 <c>GameData\</c>（配表），
/// 它们由 app 项目的 Content 项经 ProjectReference 带过来——所以 appDir 就是测试输出目录本身。
/// </summary>
internal static class TestPaths
{
    /// <summary>工具运行目录（= 测试输出目录，含 Protocol/ 与 GameData/）。</summary>
    public static string AppDirectory { get; } = AppContext.BaseDirectory;

    public static string ProtocolAssembly { get; } =
        Path.Combine(AppDirectory, "Protocol", "AstralParty.Runtime.dll");

    public static string GameDataDirectory { get; } = Path.Combine(AppDirectory, "GameData");

    public static string AssetsDirectory { get; } = Path.Combine(AppDirectory, "Assets");

    /// <summary>真实游戏回放根目录（可能不存在，或存在但没有回放）。</summary>
    public static string GameReplayRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "feimo", "AstralParty_CN", "Temp", "Replay");

    /// <summary>
    /// app 项目的源码目录（由测试 csproj 用 AssemblyMetadata 注入）。
    /// 页面资源已内嵌、输出目录里没有 WebUI\，核对内嵌完整性只能对着源码目录来。
    /// </summary>
    public static string AppSourceDirectory { get; } = Path.GetFullPath(
        typeof(TestPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AppSourceDirectory")?.Value
        ?? throw new InvalidOperationException("测试程序集里没有 AppSourceDirectory 元数据。"));

    public static string WebUiSourceDirectory { get; } = Path.Combine(AppSourceDirectory, "WebUI");

    /// <summary>按 app 项目的正规方式构造解析器（内嵌素材 + 空的额外素材目录）。</summary>
    public static ReplayAnalyzer CreateAnalyzer() =>
        new(AppDirectory, AssetsDirectory, []);
}
