using System.Text.Json;
using AstralParty.Toys.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace AstralParty.Toys;

// 自定义 → 背景。导入的图片复制到设置文件夹的 backgrounds，原文件挪走也不受影响。
// WebM 先放进 .pending，由页面解码出中间一帧再发回来存成 PNG（程序自带的浏览器内核能解码，不用额外下载工具）。
public partial class HybridWindow
{
    private static readonly string[] BackgroundImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private const string BackgroundPendingFolder = ".pending";
    private const string BackgroundNameSeparator = "__";
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static string BackgroundDirectory => Path.Combine(SpeedhackManager.ResolveProfileDirectory(), "backgrounds");
    private static string BackgroundPendingDirectory => Path.Combine(BackgroundDirectory, BackgroundPendingFolder);

    private void MapBackgroundHost()
    {
        // 上次没处理完的 WebM 不会再有人要了
        try { if (Directory.Exists(BackgroundPendingDirectory)) Directory.Delete(BackgroundPendingDirectory, true); }
        catch (IOException) { /* 清不掉就留到下次，不能影响启动 */ }
        catch (UnauthorizedAccessException) { }
        Directory.CreateDirectory(BackgroundPendingDirectory);
        // 要用 Allow：页面在 app.astral.local，把视频画到画布再导出 PNG 需要跨域许可
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "backgrounds.astral.local", BackgroundDirectory, CoreWebView2HostResourceAccessKind.Allow);
    }

    private bool HandleBackgroundMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "backgroundList":
                PostBackgroundList();
                return true;
            case "backgroundImport":
                ImportBackgrounds();
                return true;
            case "backgroundSaveFrame":
                SaveBackgroundFrame(root.GetProperty("file").GetString() ?? "", root.GetProperty("dataUrl").GetString() ?? "");
                return true;
            case "backgroundFrameFailed":
                DeletePendingBackground(root.GetProperty("file").GetString() ?? "");
                return true;
            case "backgroundDelete":
                string file = root.GetProperty("file").GetString() ?? "";
                if (!IsPlainFileName(file) || !BackgroundImageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    throw new InvalidOperationException("背景文件名不正确。");
                string target = Path.Combine(BackgroundDirectory, file);
                if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);   // 只读文件 File.Delete 删不掉
                File.Delete(target);
                PostBackgroundList();
                return true;
            default:
                return false;
        }
    }

    private void PostBackgroundList()
    {
        Directory.CreateDirectory(BackgroundDirectory);
        var items = new DirectoryInfo(BackgroundDirectory).GetFiles()
            .Where(f => BackgroundImageExtensions.Contains(f.Extension.ToLowerInvariant()))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new { file = f.Name, name = Path.GetFileNameWithoutExtension(f.Name), bytes = f.Length })
            .ToArray();
        Post(new { type = "backgroundList", payload = new { items } });
    }

    private void ImportBackgrounds()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片或 WebM（WebM 会截取中间一帧）|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.webm",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        Directory.CreateDirectory(BackgroundPendingDirectory);
        foreach (var source in dialog.FileNames)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            string name = SafeBackgroundName(source);
            if (extension == ".webm")
            {
                string pending = Guid.NewGuid().ToString("N") + BackgroundNameSeparator + name + extension;
                CopyBackgroundFile(source, Path.Combine(BackgroundPendingDirectory, pending));
                Post(new { type = "backgroundFrameNeeded", payload = new { file = pending, name } });
            }
            else if (BackgroundImageExtensions.Contains(extension))
            {
                CopyBackgroundFile(source, UniqueBackgroundPath(name, extension));
            }
        }
        PostBackgroundList();
    }

    // File.Copy 会带上只读属性：从只读位置（光盘、只读的网络盘）导入后，副本就删不掉了
    private static void CopyBackgroundFile(string source, string target)
    {
        File.Copy(source, target);
        File.SetAttributes(target, FileAttributes.Normal);
    }

    private void SaveBackgroundFrame(string file, string dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        try
        {
            if (!IsPlainFileName(file) || !dataUrl.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("提取的画面格式不正确。");
            byte[] png = Convert.FromBase64String(dataUrl[prefix.Length..]);
            if (!png.AsSpan().StartsWith(PngSignature)) throw new InvalidOperationException("提取的画面格式不正确。");
            int separator = file.IndexOf(BackgroundNameSeparator, StringComparison.Ordinal);
            string name = separator >= 0 ? Path.GetFileNameWithoutExtension(file[(separator + BackgroundNameSeparator.Length)..]) : "背景";
            string target = UniqueBackgroundPath(name, ".png");
            File.WriteAllBytes(target, png);
            Post(new { type = "backgroundFrameSaved", payload = new { file = Path.GetFileName(target) } });
        }
        finally
        {
            DeletePendingBackground(file);
            PostBackgroundList();   // 就算这一张失败，已经存好的也要显示出来
        }
    }

    private static void DeletePendingBackground(string file)
    {
        if (!IsPlainFileName(file) || !file.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)) return;
        string path = Path.Combine(BackgroundPendingDirectory, file);
        // 临时文件删不掉不是用户的问题，下次启动会再清一次，不能因此报错
        try
        {
            if (!File.Exists(path)) return;
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsPlainFileName(string file) =>
        file.Length > 0 && !file.StartsWith('.') && file == Path.GetFileName(file) && file.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string SafeBackgroundName(string source)
    {
        var chars = Path.GetFileNameWithoutExtension(source)
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        string name = new string(chars).Trim().TrimStart('.').TrimEnd('.');
        if (name.Length > 80) name = name[..80].TrimEnd();
        return name.Length == 0 ? "背景" : name;
    }

    private static string UniqueBackgroundPath(string name, string extension)
    {
        Directory.CreateDirectory(BackgroundDirectory);
        string path = Path.Combine(BackgroundDirectory, name + extension);
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(BackgroundDirectory, $"{name}-{i}{extension}");
        return path;
    }
}
