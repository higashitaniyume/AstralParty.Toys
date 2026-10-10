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
    private void EmbeddedRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        if (_webRootIsEmbedded && uri.Host.Equals("app.astral.local", StringComparison.OrdinalIgnoreCase))
            e.Response = CreatePageResponse(uri);
        else if (uri.Host.Equals("assets.astral.local", StringComparison.OrdinalIgnoreCase))
            e.Response = CreateAssetResponse(uri);
        else if (uri.Host.Equals("skins.astral.local", StringComparison.OrdinalIgnoreCase))
            e.Response = CreateSkinResponse(uri);
    }

    private CoreWebView2WebResourceResponse CreateSkinResponse(Uri uri)
    {
        try
        {
            var gameDirectory = _modManager.GetStatus().GameDirectory;
            if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
                return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "Content-Type: text/plain");

            var parts = uri.AbsolutePath.TrimStart('/').Split(['/'], 2);
            if (parts.Length != 2)
                return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "Content-Type: text/plain");

            var skinName = Uri.UnescapeDataString(parts[0]);
            var fileName = Uri.UnescapeDataString(parts[1]);
            var skinsDir = _skinManager.GetSkinsDirectory(gameDirectory);
            var filePath = Path.Combine(skinsDir, skinName, fileName);

            if (!File.Exists(filePath))
            {
                var fallbackPath = Path.Combine(skinsDir, "default", fileName);
                if (File.Exists(fallbackPath))
                {
                    filePath = fallbackPath;
                }
                else
                {
                    return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "Content-Type: text/plain");
                }
            }

            var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };

            var stream = File.OpenRead(filePath);
            return WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                new WebResourceStream(stream), 200, "OK",
                $"Content-Type: {contentType}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
        }
        catch
        {
            return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 500, "Internal Error", "Content-Type: text/plain");
        }
    }

    /// <summary>提供页面本体（Document、CSS、JS、图片、fetch 的 JSON）——全部来自程序集资源。</summary>
    private CoreWebView2WebResourceResponse CreatePageResponse(Uri uri)
    {
        var relativePath = uri.AbsolutePath.TrimStart('/');
        var stream = EmbeddedWebStore.Open(relativePath);
        return stream is null
            ? WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 404, "Not Found", "Content-Type: text/plain")
            : WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                new WebResourceStream(stream), 200, "OK",
                $"Content-Type: {EmbeddedWebStore.ContentType(relativePath)}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
    }

    /// <summary>提供内嵌素材（PackedAssets）。</summary>
    private CoreWebView2WebResourceResponse CreateAssetResponse(Uri uri)
    {
        var stream = EmbeddedAssetStore.OpenWebPath(uri.AbsolutePath);
        var contentType = Path.GetExtension(uri.AbsolutePath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/webp";
        return stream is null
            ? WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 404, "Not Found", "Content-Type: text/plain")
            : WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                new WebResourceStream(stream), 200, "OK",
                $"Content-Type: {contentType}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
    }


    private string AssetUrl(string path)
    {
        if (EmbeddedAssetStore.TryGetRelativePath(path, out var embeddedPath))
            return "https://assets.astral.local/" + EncodePath(embeddedPath);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
        var relative = Path.GetRelativePath(_assetDirectory, path).Replace('\\', '/');
        if (!relative.StartsWith("../", StringComparison.Ordinal))
            return "https://assets.astral.local/" + EncodePath(relative);

        for (var index = 0; index < _materialDirectories.Count; index++)
        {
            relative = Path.GetRelativePath(_materialDirectories[index], path).Replace('\\', '/');
            if (!relative.StartsWith("../", StringComparison.Ordinal))
                return $"https://materials{index}.astral.local/" + EncodePath(relative);
        }
        return "";
    }

    private static string EncodePath(string relative) => string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));

}
