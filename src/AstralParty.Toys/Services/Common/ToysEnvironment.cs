using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Web.WebView2.Core;

namespace AstralParty.Toys.Services.Common;

/// <summary>
/// 每次启动往运行日志里写一份环境信息，一条一行。
///
/// 出问题时日志自带诊断上下文，不用让用户再去翻系统版本 / .NET 版本 / 是不是管理员 / WebView2 版本。
/// 只报事实：任何一项取不到就跳过，整条失败也不能影响启动。
/// 不记机器名和 CPU 核心数——对排查没用，还平白多暴露本机信息。
/// </summary>
public static class ToysEnvironment
{
    private static IReadOnlyList<string>? _lines;

    /// <summary>本次运行的环境信息，一条一行（启动时写一次，之后复用）。</summary>
    public static IReadOnlyList<string> Lines()
    {
        if (_lines is not null) return _lines;
        var lines = Build();
        _lines = lines;
        return lines;
    }

    private static IReadOnlyList<string> Build()
    {
        var lines = new List<string>();
        var elevated = IsElevated();

        void Add(string label, Func<string> read)
        {
            string value;
            try
            {
                value = read().Trim();
            }
            catch
            {
                return;
            }
            if (value.Length == 0) return;
            lines.Add($"{label}：{value}");
        }

        Add("系统", () => RuntimeInformation.OSDescription);
        Add("系统架构", () => RuntimeInformation.OSArchitecture.ToString());
        Add("进程架构", () => RuntimeInformation.ProcessArchitecture.ToString());
        Add(".NET", () => RuntimeInformation.FrameworkDescription);
        Add("程序目录", () => TrimTrailingSeparators(AppContext.BaseDirectory));
        Add("工作目录", () => TrimTrailingSeparators(Environment.CurrentDirectory));
        Add("区域", () => $"{CultureInfo.CurrentCulture.Name} / UI {CultureInfo.CurrentUICulture.Name}");
        Add("管理员", () => elevated ? "是" : "否");
        Add("WebView2", () => CoreWebView2Environment.GetAvailableBrowserVersionString());

        return lines;
    }

    private static string TrimTrailingSeparators(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsElevated()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
