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

    /// <summary>把内嵌的 CHANGELOG.md（Markdown 文本）推给界面，在「关于 → 更新日志」弹窗里渲染。</summary>
    private void PostChangelog()
    {
        string markdown;
        try
        {
            var asm = typeof(HybridWindow).Assembly;
            using var stream = asm.GetManifestResourceStream("AstralParty.Toys.CHANGELOG.md");
            if (stream is null)
            {
                Post(new { type = "changelog", payload = new { markdown = "" } });
                return;
            }
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            markdown = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PostChangelog failed: {ex.Message}");
            markdown = "";
        }
        Post(new { type = "changelog", payload = new { markdown } });
    }

    /// <summary>把加载器（CesiumLoader）的更新日志推给界面。策略：CI 内嵌优先（离线即时），
    /// 内嵌为占位/空、或前端要求 fresh 时，去 GitHub 拉最新 Release 发布说明兜底。</summary>
    private async Task PostLoaderChangelogAsync(bool fresh)
    {
        var embedded = ModManager.BundledLoaderChangelog;
        if (!fresh && !string.IsNullOrWhiteSpace(embedded))
        {
            Post(new { type = "loaderChangelog", payload = new { markdown = embedded, source = "bundled" } });
            return;
        }
        try
        {
            var notes = await ModManager.DownloadLatestReleaseNotesAsync().ConfigureAwait(true);
            Post(new { type = "loaderChangelog", payload = new { markdown = notes, source = "github" } });
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
