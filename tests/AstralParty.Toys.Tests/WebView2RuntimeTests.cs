using AstralParty.Toys.Services.Common;
using Xunit;

namespace AstralParty.Toys.Tests;

/// <summary>
/// WEBVIEW2_BROWSER_EXECUTABLE_FOLDER 的解析：设了合法目录就用固定版本运行时，
/// 设了但不能用（目录不存在 / 没有 msedgewebview2.exe）就说明原因并回退到系统运行时。
/// </summary>
public sealed class WebView2RuntimeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyValue_UsesInstalledRuntime(string? value)
    {
        var resolution = WebView2Runtime.Resolve(value);

        Assert.Null(resolution.BrowserExecutableFolder);
        Assert.Null(resolution.Warning);
        Assert.False(resolution.UsesFixedVersion);
    }

    [Fact]
    public void Resolve_ValidFolder_UsesItAndStripsQuotesAndTrailingSeparator()
    {
        var folder = @"C:\WebView2\FixedVersion.157.0.4341.60.x64";

        var resolution = WebView2Runtime.Resolve(
            $"  \"{folder}\\\"  ",
            DirExists(folder),
            FileExists(Path.Combine(folder, WebView2Runtime.BrowserExecutableFileName)));

        Assert.Null(resolution.Warning);
        Assert.True(resolution.UsesFixedVersion);
        Assert.Equal(folder, resolution.BrowserExecutableFolder);
    }

    [Fact]
    public void Resolve_MissingDirectory_FallsBackWithWarning()
    {
        var folder = @"C:\WebView2\DoesNotExist";

        var resolution = WebView2Runtime.Resolve(folder, DirExists(), FileExists());

        Assert.Null(resolution.BrowserExecutableFolder);
        Assert.False(resolution.UsesFixedVersion);
        Assert.NotNull(resolution.Warning);
        Assert.Contains(WebView2Runtime.BrowserExecutableFolderVariable, resolution.Warning);
        Assert.Contains(folder, resolution.Warning);
    }

    [Fact]
    public void Resolve_DirectoryWithoutBrowserExecutable_FallsBackWithWarning()
    {
        var folder = @"C:\WebView2\NotARuntime";

        var resolution = WebView2Runtime.Resolve(folder, DirExists(folder), FileExists());

        Assert.Null(resolution.BrowserExecutableFolder);
        Assert.NotNull(resolution.Warning);
        Assert.Contains(WebView2Runtime.BrowserExecutableFileName, resolution.Warning);
    }

    [Fact]
    public void Resolve_RelativePath_ResolvesAgainstApplicationDirectory()
    {
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "WebView2Runtime"));

        var resolution = WebView2Runtime.Resolve(
            "WebView2Runtime",
            DirExists(expected),
            FileExists(Path.Combine(expected, WebView2Runtime.BrowserExecutableFileName)));

        Assert.Null(resolution.Warning);
        Assert.Equal(expected, resolution.BrowserExecutableFolder);
    }

    [Fact]
    public void Resolve_ExpandsEnvironmentVariables()
    {
        const string variable = "ASTRAL_TOYS_TEST_WEBVIEW2_DIR";
        var folder = @"C:\WebView2\Expanded";
        Environment.SetEnvironmentVariable(variable, @"C:\WebView2\Expanded");
        try
        {
            var resolution = WebView2Runtime.Resolve(
                $"%{variable}%",
                DirExists(folder),
                FileExists(Path.Combine(folder, WebView2Runtime.BrowserExecutableFileName)));

            Assert.Null(resolution.Warning);
            Assert.Equal(folder, resolution.BrowserExecutableFolder);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void ResolveFromEnvironment_ReadsTheDocumentedVariableName()
    {
        Assert.Equal("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", WebView2Runtime.BrowserExecutableFolderVariable);

        var folder = Path.Combine(Path.GetTempPath(), "astral-toys-webview2-env-test");
        var previous = Environment.GetEnvironmentVariable(WebView2Runtime.BrowserExecutableFolderVariable);
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, WebView2Runtime.BrowserExecutableFileName), string.Empty);

            Environment.SetEnvironmentVariable(WebView2Runtime.BrowserExecutableFolderVariable, folder);
            var resolution = WebView2Runtime.ResolveFromEnvironment();

            Assert.Equal(Path.GetFullPath(folder), resolution.BrowserExecutableFolder);
            Assert.Null(resolution.Warning);
        }
        finally
        {
            Environment.SetEnvironmentVariable(WebView2Runtime.BrowserExecutableFolderVariable, previous);
            try { Directory.Delete(folder, recursive: true); } catch { /* 清理失败不影响断言 */ }
        }
    }

    private static Func<string, bool> DirExists(params string[] paths)
        => path => paths.Any(candidate => Same(candidate, path));

    private static Func<string, bool> FileExists(params string[] paths)
        => path => paths.Any(candidate => Same(candidate, path));

    private static bool Same(string candidate, string path)
        => string.Equals(Path.GetFullPath(candidate), path, StringComparison.OrdinalIgnoreCase);
}
