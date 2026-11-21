namespace AstralParty.Toys.Services.Common;

/// <summary>
/// WebView2 运行时的位置：默认交给 WebView2 加载器去找系统安装的 Evergreen 运行时；
/// 设置了 <c>WEBVIEW2_BROWSER_EXECUTABLE_FOLDER</c> 时改用该目录里的固定版本（便携版）运行时。
///
/// 固定版本运行时的目录里直接放着 <c>msedgewebview2.exe</c>，不需要安装、不写注册表，
/// 适合没有装 Evergreen 运行时（或想钉死版本）的机器。环境变量设了却不可用时**不**硬失败：
/// 记一条原因后回退到系统运行时，免得一条别的程序留下的旧变量把工具箱卡死。
/// </summary>
public static class WebView2Runtime
{
    /// <summary>指向固定版本运行时目录的环境变量（WebView2 也认这个名字，这里显式读一遍）。</summary>
    public const string BrowserExecutableFolderVariable = "WEBVIEW2_BROWSER_EXECUTABLE_FOLDER";

    /// <summary>固定版本运行时目录里的浏览器主程序。</summary>
    public const string BrowserExecutableFileName = "msedgewebview2.exe";

    /// <summary>
    /// 解析结果。<see cref="BrowserExecutableFolder"/> 为 null 表示用系统安装的运行时；
    /// <see cref="Warning"/> 不为 null 表示环境变量设了但不可用（已回退）。
    /// </summary>
    public readonly record struct Resolution(string? BrowserExecutableFolder, string? Warning)
    {
        /// <summary>是否使用指定目录里的固定版本运行时。</summary>
        public bool UsesFixedVersion => !string.IsNullOrEmpty(BrowserExecutableFolder);
    }

    /// <summary>按当前进程的环境变量解析。</summary>
    public static Resolution ResolveFromEnvironment()
        => Resolve(Environment.GetEnvironmentVariable(BrowserExecutableFolderVariable));

    /// <summary>
    /// 解析环境变量的值。目录与文件是否存在通过委托注入，便于测试。
    /// </summary>
    public static Resolution Resolve(
        string? rawValue,
        Func<string, bool>? directoryExists = null,
        Func<string, bool>? fileExists = null)
    {
        directoryExists ??= Directory.Exists;
        fileExists ??= File.Exists;

        var value = Normalize(rawValue);
        if (value is null) return default;

        string folder;
        try
        {
            // 文档允许相对路径（相对 exe 所在目录），这里先补全再判断。
            folder = Path.IsPathFullyQualified(value)
                ? Path.GetFullPath(value)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, value));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new Resolution(null, $"{BrowserExecutableFolderVariable} 不是有效路径：{value}");
        }

        if (!directoryExists(folder))
            return new Resolution(null, $"{BrowserExecutableFolderVariable} 指向的目录不存在：{folder}");

        if (!fileExists(Path.Combine(folder, BrowserExecutableFileName)))
            return new Resolution(null,
                $"{BrowserExecutableFolderVariable} 指向的目录里没有 {BrowserExecutableFileName}：{folder}");

        return new Resolution(folder, null);
    }

    /// <summary>去掉空白、引号、环境变量与结尾分隔符；空值返回 null。</summary>
    private static string? Normalize(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        var value = Environment.ExpandEnvironmentVariables(rawValue.Trim()).Trim().Trim('"').Trim();
        if (value.Length == 0) return null;

        // 结尾的分隔符去掉（驱动器的根 "C:\" 保留）
        var root = Path.GetPathRoot(value) ?? string.Empty;
        while (value.Length > root.Length &&
               (value[^1] == Path.DirectorySeparatorChar || value[^1] == Path.AltDirectorySeparatorChar))
            value = value[..^1];

        return value.Length == 0 ? null : value;
    }
}
