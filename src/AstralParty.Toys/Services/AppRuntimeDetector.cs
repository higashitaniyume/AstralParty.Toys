using System.IO;
using System.Runtime.InteropServices;

namespace AstralParty.Toys.Services;

/// <summary>
/// 应用程序运行时形态：框架依赖版（Framework-Dependent）还是自包含版（Self-Contained）。
/// </summary>
public enum AppRuntimeKind
{
    FrameworkDependent,
    SelfContained
}

/// <summary>
/// 应用程序当前运行环境与安装形态信息。
/// </summary>
public sealed record AppEnvironmentInfo
{
    public AppRuntimeKind RuntimeKind { get; init; }

    public bool IsInstaller { get; init; }

    public string RuntimeDirectory { get; init; } = "";

    public string AppDirectory { get; init; } = "";

    public string DisplayLabel => RuntimeKind switch
    {
        AppRuntimeKind.SelfContained => "Self-Contained",
        _ => "Framework-Dependent"
    };
}

/// <summary>
/// 运行时与安装形态探测器。只报事实，不凭空猜测。
/// </summary>
public static class AppRuntimeDetector
{
    public const string InnoAppId = "{8E9C6C61-5A60-4EF0-AD84-8F59E877A663}";

    /// <summary>
    /// 探测当前运行环境（可传入显式路径供单元测试注入）。
    /// </summary>
    public static AppEnvironmentInfo Detect(string? appDirectory = null, string? runtimeDirectory = null)
    {
        var appDir = (appDirectory ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var runtimeDir = (runtimeDirectory ?? RuntimeEnvironment.GetRuntimeDirectory()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. 判断是否为自包含运行时：
        //    自包含发布包中，coreclr.dll / hostfxr.dll 位于程序自身目录下，或者运行时目录位于应用根目录或其子目录中。
        var hasLocalCoreClr = File.Exists(Path.Combine(appDir, "coreclr.dll")) || File.Exists(Path.Combine(appDir, "hostfxr.dll"));
        var isUnderAppDir = runtimeDir.StartsWith(appDir, StringComparison.OrdinalIgnoreCase);

        var runtimeKind = (hasLocalCoreClr || isUnderAppDir)
            ? AppRuntimeKind.SelfContained
            : AppRuntimeKind.FrameworkDependent;

        // 2. 判断是否由安装器（Inno Setup）安装：
        //    Inno Setup 在安装根目录下会生成卸载程序 unins000.exe。
        var isInstaller = File.Exists(Path.Combine(appDir, "unins000.exe"));

        return new AppEnvironmentInfo
        {
            RuntimeKind = runtimeKind,
            IsInstaller = isInstaller,
            RuntimeDirectory = runtimeDir,
            AppDirectory = appDir
        };
    }
}
