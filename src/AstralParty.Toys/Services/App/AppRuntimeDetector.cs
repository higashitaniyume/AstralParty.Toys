using System.IO;
using System.Runtime.InteropServices;

namespace AstralParty.Toys.Services.App;

/// <summary>
/// 应用程序运行时形态：框架依赖版（Framework-Dependent）还是自包含版（Self-Contained）。
/// </summary>
public enum AppRuntimeKind
{
    FrameworkDependent,
    SelfContained
}

/// <summary>
/// 发布形态：决定更新时能不能原地替换、要替换什么。
/// </summary>
public enum AppPackageForm
{
    /// <summary>安装版（Inno Setup 装的，目录里有 unins000.exe）。</summary>
    Installer,
    /// <summary>单文件版：一个 exe，运行时解压到临时目录。</summary>
    SingleFile,
    /// <summary>便携版：portable.zip 解压出来的文件夹。</summary>
    Portable,
    /// <summary>调试版或 bin 目录里的编译输出：不参与自动更新，免得覆盖开发目录。</summary>
    Development
}

/// <summary>
/// 应用程序当前运行环境与安装形态信息。
/// </summary>
public sealed record AppEnvironmentInfo
{
    public AppRuntimeKind RuntimeKind { get; init; }

    public bool IsInstaller { get; init; }

    public AppPackageForm PackageForm { get; init; }

    /// <summary>正在运行的 exe（单文件版就是用户手里那个文件，不是解压目录里的）。</summary>
    public string ExePath { get; init; } = "";

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
    public static AppEnvironmentInfo Detect(string? appDirectory = null, string? runtimeDirectory = null, string? exePath = null, bool? isDebugBuild = null)
    {
        var appDir = (appDirectory ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // 测试只传 appDirectory 时，当作 exe 就在这个目录里
        var exe = exePath ?? (appDirectory == null ? Environment.ProcessPath : null) ?? Path.Combine(appDir, "AstralParty.Toys.exe");
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
            PackageForm = DetectForm(appDir, exe, isInstaller, isDebugBuild ?? IsDebugBuild),
            ExePath = exe,
            RuntimeDirectory = runtimeDir,
            AppDirectory = appDir
        };
    }

#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    /// <summary>
    /// 单文件版（发布时带 IncludeAllContentForSelfExtract）的程序目录是临时解压目录，和 exe 所在目录不同；
    /// 便携版两者相同。安装版看 unins000.exe。
    /// </summary>
    public static AppPackageForm DetectForm(string appDirectory, string exePath, bool isInstaller, bool isDebugBuild)
    {
        if (isInstaller) return AppPackageForm.Installer;
        var exeDir = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? "");
        if (isDebugBuild || IsBuildOutput(exeDir)) return AppPackageForm.Development;
        var appDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory));
        if (!string.Equals(appDir, exeDir, StringComparison.OrdinalIgnoreCase)) return AppPackageForm.SingleFile;
        // 不带自解压的单文件版，程序目录也是 exe 所在目录；便携版旁边一定有主程序 dll
        return File.Exists(Path.Combine(exeDir, "AstralParty.Toys.dll")) ? AppPackageForm.Portable : AppPackageForm.SingleFile;
    }

    /// <summary>bin\Debug、bin\Release 下的编译输出（开发者直接运行的）。</summary>
    private static bool IsBuildOutput(string directory)
    {
        var d = directory.Replace('/', '\\') + "\\";
        return d.Contains("\\bin\\Debug\\", StringComparison.OrdinalIgnoreCase)
            || d.Contains("\\bin\\Release\\", StringComparison.OrdinalIgnoreCase);
    }
}
