using System.Diagnostics;
using System.Windows;

namespace AstralParty.Toys;

/// <summary>
/// WebView2 运行时缺失时的引导窗口。
///
/// 走到这里就说明主界面没能起来——界面本身是靠 Microsoft Edge WebView2 渲染的，
/// 运行时没装或版本太旧时就只能退到这页，提示用户去装运行时。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>微软官方的 WebView2 Runtime 下载页。</summary>
    private const string DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "等待安装 WebView2 运行时";
    }

    /// <summary>把启动失败的原因显示出来（由 <see cref="HybridWindow"/> 传进来）。</summary>
    public void ShowFailure(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return;
        ErrorText.Text = $"启动失败的原因：{detail}";
        ErrorText.Visibility = Visibility.Visible;
        StatusText.Text = "缺少 WebView2 运行时，界面无法显示";
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true });
            StatusText.Text = "已在浏览器打开官方下载页，装完重新打开本程序即可";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"打不开下载页：{ex.Message}";
            ErrorText.Text = $"可以手动在浏览器里打开：{DownloadUrl}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    /// <summary>重新打开本程序：装好运行时后不用自己去翻 exe。</summary>
    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable)) throw new InvalidOperationException("找不到当前程序的位置。");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"重新启动失败：{ex.Message}";
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
