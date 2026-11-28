using AstralParty.Toys.Mobile.Targets;
using AstralParty.Toys.Services.Common;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class TargetSwitchWebUiTests
{
    private static string ReadWeb(string path)
    {
        using var stream = EmbeddedWebStore.Open(path);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    [Fact]
    public void TargetGuardNotice_AvoidsSelfRetriggeringObserverLoopAndExposesLocalReset()
    {
        var devicesJs = ReadWeb("js/v2/devices.js");

        Assert.Contains("guardObserver.takeRecords()", devicesJs);
        Assert.Contains("!el?.closest?.('#targetNotice')", devicesJs);
        Assert.Contains("switchToLocal: switchBackToLocal", devicesJs);
        Assert.Contains("id: localTarget ? localTarget.id : 'local:'", devicesJs);

        var stripFnStart = devicesJs.IndexOf("function renderNoticeStrip()", StringComparison.Ordinal);
        var blockFnStart = devicesJs.IndexOf("function blockElement(", StringComparison.Ordinal);
        Assert.True(stripFnStart >= 0 && blockFnStart > stripFnStart);
        var stripBody = devicesJs[stripFnStart..blockFnStart];

        // strip.innerHTML 只在初次创建 strip 时设置，后续只定点更新 textEl / btnEl，不再重建重置按钮
        var ifNotStripIndex = stripBody.IndexOf("if (!strip)", StringComparison.Ordinal);
        var innerHtmlIndex = stripBody.IndexOf("strip.innerHTML =", StringComparison.Ordinal);
        var afterIfNotStripClose = stripBody.IndexOf("if (textEl", StringComparison.Ordinal);
        Assert.True(ifNotStripIndex >= 0 && innerHtmlIndex > ifNotStripIndex && afterIfNotStripClose > innerHtmlIndex);
    }

    [Fact]
    public void SettingsGameTab_ContainsFallbackTargetSwitcherAndHandlesTargetChanges()
    {
        var settingsJs = ReadWeb("js/v2/settings.js");

        Assert.Contains("id=\"v2gActiveTarget\"", settingsJs);
        Assert.Contains("data-v2g=\"switchToLocal\"", settingsJs);
        Assert.Contains("if (t.id === 'v2gActiveTarget' && t.value) return post({ type: 'targetSetActive', id: t.value });", settingsJs);
        Assert.Contains("if (e.data?.type === 'targetChanged')", settingsJs);
        Assert.Contains("const isCurrentLocal = !isMobileTarget && g.active;", settingsJs);
        Assert.Contains("id: `local:${el.dataset.v2gActive}`", settingsJs);
    }

    [Fact]
    public void ShellTargetMenu_SupportsToggleCloseAndProfileFallback()
    {
        var shellJs = ReadWeb("js/v2/shell.js");

        Assert.Contains("if (m && m.classList.contains('show') && m._restoreFocus === anchor)", shellJs);
        Assert.Contains("function resolveTargets()", shellJs);
        Assert.Contains("window.V2SettingsModule?.openTab?.('game')", shellJs);
    }

    [Fact]
    public void MobileTargetStore_SetActiveLocal_ClearsMobileTargetState()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aptoys-target-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new MobileTargetStore(tempDir);
            store.SetActiveMobile("ios:test-udid");
            Assert.Equal("mobile", store.LoadActive().Kind);
            Assert.Equal("ios:test-udid", store.LoadActive().TargetId);

            store.SetActiveLocal("");
            var active = store.LoadActive();
            Assert.Equal("local", active.Kind);
            Assert.Equal("", active.ProfileId);
            Assert.Equal("", active.TargetId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
