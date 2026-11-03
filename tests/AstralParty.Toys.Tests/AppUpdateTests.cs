using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class AppUpdateTests
{
    // ============================== 运行时与安装包形态探测 ==============================

    [Fact]
    public void AppRuntimeDetector_DetectsSelfContainedWhenCoreClrPresent()
    {
        using var sandbox = new TestSandbox("update-detect-sc");
        var appDir = sandbox.EnsureDirectory("app");
        File.WriteAllText(Path.Combine(appDir, "coreclr.dll"), "dummy clr");

        var info = AppRuntimeDetector.Detect(appDirectory: appDir, runtimeDirectory: @"C:\dotnet\shared\Microsoft.NETCore.App\8.0.0");
        Assert.Equal(AppRuntimeKind.SelfContained, info.RuntimeKind);
        Assert.False(info.IsInstaller);
    }

    [Fact]
    public void AppRuntimeDetector_DetectsFrameworkDependentWhenExternalRuntime()
    {
        using var sandbox = new TestSandbox("update-detect-fd");
        var appDir = sandbox.EnsureDirectory("app");

        var info = AppRuntimeDetector.Detect(appDirectory: appDir, runtimeDirectory: @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.0");
        Assert.Equal(AppRuntimeKind.FrameworkDependent, info.RuntimeKind);
        Assert.False(info.IsInstaller);
    }

    [Fact]
    public void AppRuntimeDetector_DetectsInstallerWhenUninsPresent()
    {
        using var sandbox = new TestSandbox("update-detect-installer");
        var appDir = sandbox.EnsureDirectory("app");
        File.WriteAllText(Path.Combine(appDir, "unins000.exe"), "dummy uninstaller");

        var info = AppRuntimeDetector.Detect(appDirectory: appDir, runtimeDirectory: @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.0");
        Assert.True(info.IsInstaller);
    }

    // ============================== GitHub 加速镜像 ==============================

    [Fact]
    public void GitHubMirrorHelper_AppliesGhProxyByDefault()
    {
        var raw = "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe";
        var proxied = GitHubMirrorHelper.ApplyMirror(raw, "gh-proxy");
        Assert.Equal("https://gh-proxy.com/https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe", proxied);
    }

    [Fact]
    public void GitHubMirrorHelper_AppliesGhFast()
    {
        var raw = "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe";
        var proxied = GitHubMirrorHelper.ApplyMirror(raw, "ghfast");
        Assert.Equal("https://ghfast.top/https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe", proxied);
    }

    [Fact]
    public void GitHubMirrorHelper_LeavesDirectUnchanged()
    {
        var raw = "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe";
        var proxied = GitHubMirrorHelper.ApplyMirror(raw, "direct");
        Assert.Equal(raw, proxied);
    }

    [Fact]
    public void GitHubMirrorHelper_AppliesCustomPrefix()
    {
        var raw = "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe";
        var proxied = GitHubMirrorHelper.ApplyMirror(raw, "custom", "https://custom-proxy.example.com");
        Assert.Equal("https://custom-proxy.example.com/https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/setup.exe", proxied);
    }

    [Fact]
    public void GitHubMirrorHelper_DoesNotDuplicatePrefix()
    {
        var alreadyProxied = "https://gh-proxy.com/https://github.com/higashitaniyume/AstralParty.Toys/releases/latest/download/version.json";
        var res = GitHubMirrorHelper.ApplyMirror(alreadyProxied, "gh-proxy");
        Assert.Equal(alreadyProxied, res);
    }

    [Fact]
    public void GitHubMirrorHelper_DoesNotProxyNonGitHubUrls()
    {
        var forge = "https://forge.hiynet.com/api/mods";
        var res = GitHubMirrorHelper.ApplyMirror(forge, "gh-proxy");
        Assert.Equal(forge, res);
    }

    // ============================== 版本比对与资产选择 ==============================

    [Theory]
    [InlineData("1.2.0", "1.3.0", true)]
    [InlineData("1.2.0", "1.2.1", true)]
    [InlineData("v1.2.0", "v1.3.0", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.3.0", "1.2.0", false)]
    [InlineData("1.2.0", "1.2.0-rc1", false)]
    [InlineData("1.2.0", "2.0.0", true)]
    public void AppUpdateService_IsNewerVersion_ComparesCorrectly(string cur, string candidate, bool expected)
    {
        Assert.Equal(expected, AppUpdateService.IsNewerVersion(cur, candidate));
    }

    [Fact]
    public void AppUpdateService_SelectsTargetPackageBasedOnRuntime()
    {
        var manifest = new AppVersionManifest
        {
            Version = "1.3.0",
            Packages = new AppPackageAssets
            {
                FrameworkDependentInstaller = new AppAssetPackageInfo { FileName = "fw-setup.exe" },
                SelfContainedInstaller = new AppAssetPackageInfo { FileName = "sc-setup.exe" }
            }
        };

        var fwPackage = AppUpdateService.SelectTargetPackage(manifest, AppRuntimeKind.FrameworkDependent);
        Assert.NotNull(fwPackage);
        Assert.Equal("fw-setup.exe", fwPackage.FileName);

        var scPackage = AppUpdateService.SelectTargetPackage(manifest, AppRuntimeKind.SelfContained);
        Assert.NotNull(scPackage);
        Assert.Equal("sc-setup.exe", scPackage.FileName);
    }

    // ============================== Mock HTTP 检查更新与下载校验 ==============================

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task AppUpdateService_CheckUpdateAsync_ParsesManifestCorrectly()
    {
        var jsonManifest = """
        {
          "version": "1.3.0",
          "releaseTag": "v1.3.0",
          "packages": {
            "frameworkDependentInstaller": {
              "fileName": "AstralParty.Toys-1.3.0-win-x64-framework-dependent-installer.exe",
              "url": "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/fw.exe",
              "sha256": "aabbcc",
              "size": 12345
            }
          }
        }
        """;

        var fakeHandler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonManifest, Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(fakeHandler);
        var service = new AppUpdateService(client);

        var (hasUpdate, manifest, targetPackage, error) = await service.CheckUpdateAsync(
            currentVersion: "1.2.0",
            mirrorType: "direct");

        Assert.Null(error);
        Assert.True(hasUpdate);
        Assert.NotNull(manifest);
        Assert.Equal("1.3.0", manifest.Version);
        Assert.NotNull(targetPackage);
        Assert.Equal("AstralParty.Toys-1.3.0-win-x64-framework-dependent-installer.exe", targetPackage.FileName);
    }

    [Fact]
    public async Task AppUpdateService_DownloadPackageAsync_VerifiesSha256()
    {
        var dummyData = "Test update package content for Toys updater";
        var dummyBytes = Encoding.UTF8.GetBytes(dummyData);
        var expectedHash = Convert.ToHexString(SHA256.HashData(dummyBytes)).ToLowerInvariant();

        var fakeHandler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dummyBytes)
            };
        });

        using var client = new HttpClient(fakeHandler);
        var service = new AppUpdateService(client);

        var package = new AppAssetPackageInfo
        {
            FileName = "test-installer.exe",
            Url = "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/test-installer.exe",
            Sha256 = expectedHash,
            Size = dummyBytes.Length
        };

        var downloadedPath = await service.DownloadPackageAsync(package, mirrorType: "direct");
        try
        {
            Assert.True(File.Exists(downloadedPath));
            var downloadedData = await File.ReadAllTextAsync(downloadedPath);
            Assert.Equal(dummyData, downloadedData);
        }
        finally
        {
            if (File.Exists(downloadedPath)) File.Delete(downloadedPath);
        }
    }

    [Fact]
    public async Task AppUpdateService_DownloadPackageAsync_ThrowsWhenSha256Mismatch()
    {
        var dummyBytes = Encoding.UTF8.GetBytes("Data");
        var fakeHandler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dummyBytes)
            };
        });

        using var client = new HttpClient(fakeHandler);
        var service = new AppUpdateService(client);

        var package = new AppAssetPackageInfo
        {
            FileName = "test-bad-installer.exe",
            Url = "https://github.com/higashitaniyume/AstralParty.Toys/releases/download/v1.3.0/test-bad-installer.exe",
            Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
            Size = dummyBytes.Length
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DownloadPackageAsync(package, mirrorType: "direct"));

        Assert.Contains("SHA256", ex.Message);
    }
}
