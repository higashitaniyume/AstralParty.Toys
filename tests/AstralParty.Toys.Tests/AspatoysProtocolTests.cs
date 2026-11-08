using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// <c>aspatoys://</c> 协议的解析、生成、对外链接来源标识，以及安装器/前端接线。
/// 协议注册只在安装器里做（受安装选项控制），单文件版和便携版不注册、启动也不自注册。
/// </summary>
public sealed class AspatoysProtocolTests
{
    [Theory]
    [InlineData("aspatoys://install-mod?id=HandViewerMod", "HandViewerMod", null)]
    [InlineData("aspatoys://install-mod?id=HandViewerMod&version=1.2.0", "HandViewerMod", "1.2.0")]
    [InlineData("aspatoys://install-mod?id=HandViewerMod&version=1.0.0-beta.1", "HandViewerMod", "1.0.0-beta.1")]
    [InlineData("ASPATOYS://INSTALL-MOD?id=HandViewerMod&version=2", "HandViewerMod", "2")]
    [InlineData("aspatoys://install-mod/HandViewerMod?version=1.2.0", "HandViewerMod", "1.2.0")]
    [InlineData("aspatoys://install/HandViewerMod", "HandViewerMod", null)]
    [InlineData("aspatoys://install-mod?id=My%20Mod", null, null)]
    public void TryParseInstallMod_AcceptsExpectedForms(string input, string? expectedId, string? expectedVersion)
    {
        var parsed = AspatoysProtocol.TryParseInstallMod(input, out var modId, out var version);

        if (expectedId is null)
        {
            Assert.False(parsed);
            return;
        }

        Assert.True(parsed, input);
        Assert.Equal(expectedId, modId);
        Assert.Equal(expectedVersion, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/?id=HandViewerMod")]
    [InlineData("aspatoys://open?id=HandViewerMod")]
    [InlineData("aspatoys://install-mod")]
    [InlineData("aspatoys://install-mod?id=")]
    [InlineData("aspatoys://install-mod?id=../evil")]
    [InlineData("aspatoys://install-mod?id=a/b")]
    [InlineData("aspatoys://install-mod?id=a\\b")]
    [InlineData("aspatoys://install-mod?id=HandViewerMod&version=1.2.0/evil")]
    public void TryParseInstallMod_RejectsInvalidLinks(string? input)
    {
        Assert.False(AspatoysProtocol.TryParseInstallMod(input, out _, out _));
    }

    [Fact]
    public void BuildInstallModUrl_RoundTripsThroughParser()
    {
        var url = AspatoysProtocol.BuildInstallModUrl("HandViewerMod", "1.2.0");

        Assert.Equal("aspatoys://install-mod?id=HandViewerMod&version=1.2.0", url);
        Assert.True(AspatoysProtocol.TryParseInstallMod(url, out var modId, out var version));
        Assert.Equal("HandViewerMod", modId);
        Assert.Equal("1.2.0", version);
        Assert.Equal("aspatoys://install-mod?id=HandViewerMod", AspatoysProtocol.BuildInstallModUrl("HandViewerMod"));
    }

    [Theory]
    [InlineData("https://github.com/higashitaniyume/AstralParty.Toys",
        "https://github.com/higashitaniyume/AstralParty.Toys?from=app.aspatoys.hiynet.localhost")]
    [InlineData("https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5",
        "https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5?from=app.aspatoys.hiynet.localhost")]
    [InlineData("https://example.com/page?a=1#part",
        "https://example.com/page?a=1&from=app.aspatoys.hiynet.localhost#part")]
    public void WithInternalOrigin_AddsFromParameter(string input, string expected)
    {
        Assert.Equal(expected, AspatoysProtocol.WithInternalOrigin(input));
    }

    [Theory]
    [InlineData("https://example.com/?from=other.example")]
    [InlineData("https://app.aspatoys.hiynet.localhost/index.html")]
    [InlineData("steam://rungameid/2622000")]
    [InlineData("taptap://taptap.com/app?app_id=524022")]
    [InlineData("")]
    [InlineData(null)]
    public void WithInternalOrigin_LeavesIneligibleUrlsAlone(string? input)
    {
        Assert.Equal(input, AspatoysProtocol.WithInternalOrigin(input));
    }

    [Fact]
    public void WithInternalOrigin_PreservesExistingQueryValues()
    {
        var input = "https://qm.qq.com/cgi-bin/qm/qr?k=abc123&jump_from=webapi&authKey=xyz";

        var result = AspatoysProtocol.WithInternalOrigin(input);

        Assert.StartsWith(input + "&from=app.aspatoys.hiynet.localhost", result, StringComparison.Ordinal);
    }

    [Fact]
    public void IsProtocolUrl_OnlyMatchesAspatoysScheme()
    {
        Assert.True(AspatoysProtocol.IsProtocolUrl("aspatoys://install-mod?id=x"));
        Assert.False(AspatoysProtocol.IsProtocolUrl("https://example.com/"));
        Assert.False(AspatoysProtocol.IsProtocolUrl(@"C:\Game\replay.apr"));
        Assert.False(AspatoysProtocol.IsProtocolUrl(null));
    }

    [Fact]
    public void Installer_RegistersAspatoysProtocolForCurrentUserWhenTaskSelected()
    {
        var setup = File.ReadAllText(Path.GetFullPath(Path.Combine(
            TestPaths.AppSourceDirectory, "..", "..", "installer", "Setup.iss")));

        // 默认勾选：任务行不能带 unchecked
        Assert.Contains("Name: \"protocolregistration\"", setup);
        var taskLine = setup.Split('\n').First(line => line.Contains("\"protocolregistration\""));
        Assert.DoesNotContain("unchecked", taskLine);

        Assert.Contains("[Registry]", setup);
        Assert.Contains(@"Software\Classes\aspatoys", setup);
        Assert.Contains("URL Protocol", setup);
        Assert.Contains(@"{app}\AstralParty.Toys.exe", setup);
        Assert.Contains("%1", setup);
        Assert.Contains("uninsdeletekey", setup);
        Assert.Contains("Check: RegisterProtocol", setup);
        Assert.Contains("WizardIsTaskSelected('protocolregistration')", setup);
    }

    [Fact]
    public void Host_HandlesProtocolArgumentsAndTagsExternalUrls()
    {
        var dispatcher = File.ReadAllText(Path.Combine(TestPaths.AppSourceDirectory, "HybridWindow.Dispatcher.cs"));
        Assert.Contains("HandleProtocolArguments", dispatcher);
        Assert.Contains("workshopOpenMod", dispatcher);
        Assert.Contains("AspatoysProtocol.WithInternalOrigin", dispatcher);
        Assert.Contains("AspatoysProtocol.TryParseInstallMod", dispatcher);
    }

    [Fact]
    public void PortableAndSingleFile_DoNotSelfRegisterProtocol()
    {
        // 协议注册只在安装器里做，程序启动时不能自注册（否则会盖掉用户在安装器里的取消勾选）
        var app = File.ReadAllText(Path.Combine(TestPaths.AppSourceDirectory, "App.xaml.cs"));
        Assert.DoesNotContain("EnsureRegistered", app);
        Assert.DoesNotContain("AspatoysProtocol", app);

        var protocol = File.ReadAllText(Path.Combine(
            TestPaths.AppSourceDirectory, "Services", "App", "AspatoysProtocol.cs"));
        Assert.DoesNotContain("EnsureRegistered", protocol);
        Assert.DoesNotContain("Registry.CurrentUser", protocol);
    }

    [Fact]
    public void WebUi_OpensExternalModDetailFromProtocolMessage()
    {
        using var stream = EmbeddedWebStore.Open("js/core.js");
        Assert.NotNull(stream);
        var core = new StreamReader(stream!).ReadToEnd();
        Assert.Contains("case 'workshopOpenMod':", core);
        Assert.Contains("openExternalMod", core);

        using var workshopStream = EmbeddedWebStore.Open("js/workshop.js");
        Assert.NotNull(workshopStream);
        var workshop = new StreamReader(workshopStream!).ReadToEnd();
        Assert.Contains("openExternalMod", workshop);
        Assert.Contains("openExternalModNow", workshop);
        Assert.Contains("state.externalMod", workshop);
    }
}
