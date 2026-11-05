using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace AstralParty.Toys;

/// <summary>
/// 自绘标题栏：系统标题栏已隐藏（见 HybridWindow.xaml 的 WindowChrome），网页顶栏发 windowXxx 消息，这里执行对应的窗口操作。
/// </summary>
public partial class HybridWindow
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 2;

    // 网页上的拖边名称 → 系统的边缘编号（WM_NCHITTEST 的 HT 值）
    private static readonly Dictionary<string, int> ResizeEdges = new()
    {
        ["left"] = 10, ["right"] = 11, ["top"] = 12, ["topLeft"] = 13,
        ["topRight"] = 14, ["bottom"] = 15, ["bottomLeft"] = 16, ["bottomRight"] = 17,
    };

    /// <summary>处理窗口类消息；不是窗口消息时返回 false，交给原来的分发逻辑。</summary>
    private bool HandleWindowChromeMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "windowDrag":
                // 鼠标此时被 WebView2 子窗口捕获着：先释放，再让系统当作按在标题栏上处理，
                // 这样拖动、贴边分屏、从最大化拖出来还原都由系统完成
                ReleaseCapture();
                SendMessage(new WindowInteropHelper(this).Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
                return true;
            case "windowResize":
                // 和拖动同样的做法：让系统当作按在窗口边缘上，改大小、贴边都由系统完成
                if (WindowState == WindowState.Normal &&
                    root.TryGetProperty("edge", out var edge) && edge.GetString() is { } name &&
                    ResizeEdges.TryGetValue(name, out var hitTest))
                {
                    ReleaseCapture();
                    SendMessage(new WindowInteropHelper(this).Handle, WmNcLButtonDown, (IntPtr)hitTest, IntPtr.Zero);
                }
                return true;
            case "windowMinimize":
                WindowState = WindowState.Minimized;
                return true;
            case "windowToggleMaximize":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return true;
            case "windowClose":
                Close();
                return true;
            case "windowStateGet":
                PostWindowState();
                return true;
            default:
                return false;
        }
    }

    private void PostWindowState() => Post(new { type = "windowState", maximized = WindowState == WindowState.Maximized });

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // 去掉系统标题栏后，最大化的窗口会超出屏幕边缘一圈，要把内容收回来；普通状态铺满窗口
        var border = SystemParameters.WindowResizeBorderThickness;
        RootGrid.Margin = WindowState == WindowState.Maximized
            ? new Thickness(border.Left + 4, border.Top + 4, border.Right + 4, border.Bottom + 4)
            : new Thickness(0);
        PostWindowState();
    }
}
