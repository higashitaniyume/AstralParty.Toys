using AstralParty.Toys.Services.Common;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class UiMotionTests
{
    private static string Web(string path)
    {
        using var stream = EmbeddedWebStore.Open(path);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void SharedMotionLoadsBeforeConsumersAndAfterVisualOverrides()
    {
        var index = Web("index.html");
        Assert.True(index.IndexOf("css/motion.css", StringComparison.Ordinal) > index.IndexOf("css/account.css", StringComparison.Ordinal));
        Assert.True(index.IndexOf("js/core/motion.js", StringComparison.Ordinal) < index.IndexOf("js/core/router.js", StringComparison.Ordinal));
        Assert.True(index.IndexOf("js/core/motion.js", StringComparison.Ordinal) < index.IndexOf("js/account.js", StringComparison.Ordinal));
    }

    [Fact]
    public void MotionIsInterruptibleAndHonorsBothPreferenceSources()
    {
        var script = Web("js/core/motion.js");
        Assert.Contains("prefers-reduced-motion: reduce", script);
        Assert.Contains("document.body.classList.contains('reduce-motion')", script);
        Assert.Contains("running.get(element)?.cancel()", script);
        Assert.Contains("animation.finished.then(cleanup, cleanup)", script);
        Assert.Contains("if (previous === key) return", script);
        Assert.Contains("attributeFilter: ['class']", script);
        Assert.Contains("preference.addEventListener('change'", script);
        Assert.Contains("if (reduced()) cancelAll()", script);
        Assert.DoesNotContain("setInterval", script);
        Assert.DoesNotContain("document.querySelectorAll", script);
    }

    [Fact]
    public void ReducedMotionSuppressesDelaysAndPressOrHoverMovement()
    {
        var css = Web("css/motion.css");
        Assert.Contains("body.reduce-motion", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("animation-delay: 0s !important", css);
        Assert.Contains("transition-delay: 0s !important", css);
        Assert.Contains("scale: none !important", css);
        Assert.Contains("scroll-behavior: auto !important", css);
        Assert.Contains("@supports (interpolate-size: allow-keywords)", css);
        Assert.Contains("@starting-style", css);
    }

    [Fact]
    public void ClosingOverlaysImmediatelyDisablesInteractionAndKeepsFocusRestoration()
    {
        Assert.Contains("id=\"globalModal\" inert", Web("index.html"));
        var modal = Web("js/core/modal.js");
        Assert.Contains("modal.inert = false", modal);
        Assert.Contains("modal.inert = true", modal);
        Assert.Contains("if (!wasOpen) modal._restoreFocus", modal);
        Assert.Contains("!modal.classList.contains('open') && restore?.isConnected", modal);
        var shell = Web("js/v2/shell.js");
        Assert.Contains("menu.inert = true", shell);
        Assert.Contains("modal.inert = true", shell);
        Assert.Contains("box.setAttribute('aria-modal', 'true')", shell);
        Assert.Contains("if (e.key !== 'Tab') return", shell);
        Assert.Contains("if (restore?.isConnected) restore.focus()", shell);
        // Inherited visibility must not be animated by legacy transition:all controls.
        Assert.Contains("transition-property: color, background-color, border-color, box-shadow, opacity, transform, scale, translate", Web("css/motion.css"));
    }

    [Fact]
    public void NavigationAndThemeUseSystemMotionPreference()
    {
        var router = Web("js/core/router.js");
        Assert.Contains("options.immediate || window.Motion?.reduced()", router);
        Assert.Contains("window.Motion?.duration('--motion-page-in'", router);
        Assert.Contains("window.Motion?.duration('--motion-page-out'", router);
        Assert.Contains("document.startViewTransition && !window.Motion?.reduced()", Web("js/v2/shell.js"));
        var replay = Web("js/replay/lifecycle.js");
        Assert.Contains("window.Motion?.reveal(next)", replay);
        Assert.DoesNotContain("window.setTimeout", replay[(replay.IndexOf("switchReplayTab", StringComparison.Ordinal))..]);
    }

    [Theory]
    [InlineData("js/v2/settings.js")]
    [InlineData("js/v2/skins.js")]
    [InlineData("js/v2/replays.js")]
    [InlineData("js/v2/characters.js")]
    [InlineData("js/profiles.js")]
    [InlineData("js/workshop/module.js")]
    [InlineData("js/account.js")]
    public void DynamicContentUsesKeyedMotionInsteadOfPerFrameAnimation(string path)
    {
        Assert.Contains("window.Motion?.content(", Web(path));
    }
}
