using System.Text.Json;
using System.Windows.Media;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

// 深浅色的选择由网页保存（localStorage），但启动画面在网页加载之前就要显示，所以这里再记一份。
public partial class HybridWindow
{
    private static string UiThemeFile => Path.Combine(SpeedhackManager.ResolveProfileDirectory(), "ui-theme.txt");

    private void ApplyStartupTheme()
    {
        bool light;
        try { light = File.Exists(UiThemeFile) && File.ReadAllText(UiThemeFile).Trim() == "light"; }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        if (!light) return;

        var background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF5, 0xF9));
        Background = background;
        StartupOverlay.Background = background;
        WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xF6, 0xF5, 0xF9);
        StartupTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x33));
        StartupDetail.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x70, 0x84));
        StartupProgress.Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xE3, 0xEC));
    }

    private bool HandleUiThemeMessage(string? type, JsonElement root)
    {
        if (type != "uiThemeChanged") return false;
        bool light = root.TryGetProperty("light", out var value) && value.ValueKind == JsonValueKind.True;
        try
        {
            if (light) File.WriteAllText(UiThemeFile, "light");
            else if (File.Exists(UiThemeFile)) File.Delete(UiThemeFile);
        }
        catch (IOException) { /* 只影响下次启动画面的颜色 */ }
        catch (UnauthorizedAccessException) { }
        return true;
    }
}
