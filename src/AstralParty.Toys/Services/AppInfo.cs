using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AstralParty.Toys.Services;

/// <summary>
/// 本程序自己的版本信息 —— 界面上的版本胶囊（详情页顶部栏）与底部状态栏都取这里，
/// 点开还能看完整清单、一键复制。
///
/// 原则：**只报事实，不猜**。
///   * 版本号来自程序集属性（csproj 的 &lt;Version&gt;）；
///   * 提交号来自 .NET SDK 写进 InformationalVersion 的 SourceRevisionId，形如 "0.4.11+18962b1…"；
///   * 运行时 / 架构 / 操作系统直接问运行时。
/// 这样即使是自己 dotnet build 出来的（没有发布 tag、甚至改过源码），
/// 界面也能说清"这是哪一版、哪个提交、什么配置构建的"，而不是给一个脱离上下文的版本号。
/// </summary>
public static class AppInfo
{
    /// <summary>采集一次版本信息（都是只读的静态事实，可反复调用）。</summary>
    public static AppVersionInfo Get()
    {
        var assembly = typeof(AppInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "";
        var (version, commit) = SplitInformationalVersion(informational);
        if (version.Length == 0)
        {
            // 极端兜底：InformationalVersion 被裁掉时，用程序集版本号（0.4.11.0 → 0.4.11）
            version = assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";
        }

        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        // 提交信息缺失（例如从源码 zip 构建、或构建机没有 git）时，把它当"本地构建"如实说明
        var isLocalBuild = commit.Length == 0 || configuration == "Debug";

        var info = new AppVersionInfo
        {
            Version = version,
            FileVersion = fileVersion,
            Commit = commit.Length >= 7 ? commit[..7] : commit,
            CommitFull = commit,
            Configuration = configuration,
            IsLocalBuild = isLocalBuild,
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            EmbeddedLoaderVersion = ModManager.BundleLoaderVersion,
            AssemblyTime = GetAssemblyFileTime(assembly),
            AppDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)
        };
        info.ShortLabel = BuildShortLabel(info);
        info.DetailText = BuildDetailText(info);
        return info;
    }

    /// <summary>界面上直接显示的一行：`v0.4.11 · 18962b1`（没有提交信息就写"本地构建"）。</summary>
    private static string BuildShortLabel(AppVersionInfo info)
    {
        var label = info.Version.Length > 0 ? $"v{info.Version}" : "版本未知";
        label += info.Commit.Length > 0 ? $" · {info.Commit}" : " · 本地构建";
        return label;
    }

    /// <summary>一键复制用的完整文本（报 issue 时贴这一段就够了）。</summary>
    private static string BuildDetailText(AppVersionInfo info)
    {
        var text = new StringBuilder();
        text.Append("AstralParty.Toys ").Append(info.Version.Length > 0 ? "v" + info.Version : "（版本未知）").AppendLine();
        Append(text, "程序集版本", info.FileVersion);
        Append(text, "源码提交", info.CommitFull.Length > 0 ? info.CommitFull : "（无：不是从 git 仓库构建的）");
        Append(text, "构建配置", info.IsLocalBuild ? $"{info.Configuration}（本地构建）" : info.Configuration);
        Append(text, "内置加载器", info.EmbeddedLoaderVersion.Length > 0 ? info.EmbeddedLoaderVersion : "（未内嵌发布包）");
        Append(text, "运行时", $"{info.Runtime} · {info.Architecture}");
        Append(text, "操作系统", info.OperatingSystem);
        Append(text, "程序集文件时间", info.AssemblyTime);
        Append(text, "程序目录", info.AppDirectory);
        return text.ToString().TrimEnd();

        static void Append(StringBuilder builder, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) builder.Append(name).Append(": ").Append(value).AppendLine();
        }
    }

    /// <summary>
    /// 把 `0.4.11+18962b107a9c…` 拆成 (0.4.11, 18962b107a9c…)。
    /// 只接受纯十六进制的提交号：别的注入内容（后缀、分支名等）一律当没有 —— 宁可显示"本地构建"，
    /// 也不要把一段看不懂的字符串当成提交号显示出去。
    /// </summary>
    private static (string Version, string Commit) SplitInformationalVersion(string informational)
    {
        if (string.IsNullOrWhiteSpace(informational)) return ("", "");

        var plus = informational.IndexOf('+');
        var version = (plus >= 0 ? informational[..plus] : informational).Trim();
        if (plus < 0) return (version, "");

        var hex = new string(informational[(plus + 1)..].TakeWhile(Uri.IsHexDigit).ToArray());
        return (version, hex.Length >= 7 ? hex : "");
    }

    /// <summary>程序集文件时间：本地构建时就是构建时间（发布包里则等于打包/解包时间，所以界面写"文件时间"）。</summary>
    private static string GetAssemblyFileTime(Assembly assembly)
    {
        try
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return File.GetLastWriteTime(location).ToString("yyyy-MM-dd HH:mm");
            }
        }
        catch
        {
            // 单文件发布等取不到路径的情况：不显示这一行就好，不值得为它报错
        }
        return "";
    }
}

/// <summary>版本信息（字段名会以 camelCase 序列化给前端）。</summary>
public sealed class AppVersionInfo
{
    /// <summary>语义版本，如 0.4.11。</summary>
    public string Version { get; set; } = "";

    /// <summary>程序集文件版本，如 0.4.11.0。</summary>
    public string FileVersion { get; set; } = "";

    /// <summary>短提交号（7 位）；不是从 git 工作区构建的则为空串。</summary>
    public string Commit { get; set; } = "";

    /// <summary>完整提交号。</summary>
    public string CommitFull { get; set; } = "";

    /// <summary>构建配置：Release / Debug。</summary>
    public string Configuration { get; set; } = "";

    /// <summary>是否本地构建（Debug 构建、或没有提交信息）。</summary>
    public bool IsLocalBuild { get; set; }

    /// <summary>.NET 运行时描述，如 .NET 8.0.11。</summary>
    public string Runtime { get; set; } = "";

    /// <summary>进程架构，如 X64。</summary>
    public string Architecture { get; set; } = "";

    /// <summary>操作系统描述。</summary>
    public string OperatingSystem { get; set; } = "";

    /// <summary>随程序内置的加载器版本，如 2.2.1（本地开发构建可能是 dev-local）。</summary>
    public string EmbeddedLoaderVersion { get; set; } = "";

    /// <summary>程序集文件时间（yyyy-MM-dd HH:mm）。</summary>
    public string AssemblyTime { get; set; } = "";

    /// <summary>程序所在目录。</summary>
    public string AppDirectory { get; set; } = "";

    /// <summary>一行短标签，如 `v0.4.11 · 18962b1`。</summary>
    public string ShortLabel { get; set; } = "";

    /// <summary>可复制的完整版本文本。</summary>
    public string DetailText { get; set; } = "";
}
