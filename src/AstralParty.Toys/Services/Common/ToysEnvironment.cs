using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace AstralParty.Toys.Services.Common;

/// <summary>
/// 每次启动往运行日志里写一条环境信息。
///
/// 出问题时日志自带诊断上下文，不用让用户再去翻系统版本 / .NET 版本 / 是不是管理员 / WebView2 版本。
/// 只报事实：任何一项取不到就跳过，整条失败也不能影响启动。
/// </summary>
public static class ToysEnvironment
{
    private static string? _cached;

    /// <summary>环境摘要（分号分隔；启动时算一次，之后直接复用）。</summary>
    public static string Describe()
    {
        if (_cached is not null) return _cached;
        var text = Build();
        _cached = text;
        return text;
    }

    private static string Build()
    {
        var builder = new StringBuilder();
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
            if (builder.Length > 0) builder.Append('；');
            builder.Append(label).Append('：').Append(value);
        }

        Add("系统", () => RuntimeInformation.OSDescription);
        Add("系统架构", () => RuntimeInformation.OSArchitecture.ToString());
        Add("进程架构", () => RuntimeInformation.ProcessArchitecture.ToString());
        Add(".NET", () => RuntimeInformation.FrameworkDescription);
        Add("程序目录", () => TrimTrailingSeparators(AppContext.BaseDirectory));
        Add("工作目录", () => TrimTrailingSeparators(Environment.CurrentDirectory));
        Add("机器名", () => Environment.MachineName);
        Add("CPU 核心", () => Environment.ProcessorCount.ToString());
        Add("区域", () => $"{CultureInfo.CurrentCulture.Name} / UI {CultureInfo.CurrentUICulture.Name}");
        Add("管理员", () => elevated ? "是" : "否");
        Add("WebView2", () => CoreWebView2Environment.GetAvailableBrowserVersionString());

        return builder.ToString();
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
