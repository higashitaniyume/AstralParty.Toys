using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    /// 自建构建也会带上提交号、构建配置、运行时等信息，不会只给一个孤零零的版本号。</summary>
    private void PostAppVersion()
    {
        try
        {
            Post(new { type = "appVersion", payload = AppInfo.Get() });
        }
        catch (Exception ex)
        {
            // 取版本信息失败不该影响任何功能，界面上就显示"版本未知"
            Debug.WriteLine($"PostAppVersion failed: {ex.Message}");
        }
    }

    /// <summary>内嵌的工具箱 CHANGELOG.md（离线兜底；随程序集编译进来的版本）。</summary>
    private static string BundledAppChangelog()
    {
        try
        {
            var asm = typeof(HybridWindow).Assembly;
            using var stream = asm.GetManifestResourceStream("AstralParty.Toys.CHANGELOG.md");
            if (stream is null) return "";
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BundledAppChangelog failed: {ex.Message}");
            return "";
        }
    }

    /// <summary>把工具箱更新日志推给界面。策略：先经设置里的 GitHub 镜像去本仓库根拉
    /// CHANGELOG.md，拉不到退回内嵌版本。</summary>
    private async Task PostChangelogAsync()
    {
        string markdown;
        try
        {
            var settings = _libraryService.Settings;
            markdown = await ModManager.DownloadRepoChangelogAsync(
                ModManager.AppRepo, settings.GitHubMirrorType, settings.CustomGitHubMirrorUrl).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PostChangelog remote failed, fallback to bundled: {ex.Message}");
            markdown = BundledAppChangelog();
        }
        Post(new { type = "changelog", payload = new { markdown } });
    }

    /// <summary>把加载器（CesiumLoader）的更新日志推给界面。策略：先经设置里的 GitHub 镜像去
    /// CesiumLoader 仓库根拉 CHANGELOG.md，拉不到退回内嵌的打包版本。</summary>
    private async Task PostLoaderChangelogAsync(bool fresh)
    {
        var embedded = ModManager.BundledLoaderChangelog;
        try
        {
            var settings = _libraryService.Settings;
            var text = await ModManager.DownloadRepoChangelogAsync(
                ModManager.LoaderRepo, settings.GitHubMirrorType, settings.CustomGitHubMirrorUrl).ConfigureAwait(true);
            Post(new { type = "loaderChangelog", payload = new { markdown = text, source = "github" } });
        }
        catch (Exception ex)
        {
            // 联网失败：还有内嵌就退回内嵌，否则如实说读不到
            if (!string.IsNullOrWhiteSpace(embedded))
                Post(new { type = "loaderChangelog", payload = new { markdown = embedded, source = "bundled" } });
            else
                Post(new { type = "loaderChangelog", payload = new { markdown = "", source = "none", error = ex.Message } });
        }
    }

    /// <summary>复制版本信息到剪贴板（用 WPF 的剪贴板，比网页的 navigator.clipboard 在 file:// 下可靠）。</summary>
    private void CopyVersionInfo()
    {
        try
        {
            Clipboard.SetText(AppInfo.Get().DetailText);
            Post(new { type = "toast", message = "版本信息已复制到剪贴板" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"复制失败：{ex.Message}" });
        }
    }

}
