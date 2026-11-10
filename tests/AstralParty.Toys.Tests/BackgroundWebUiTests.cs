using AstralParty.Toys.Services.Common;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class BackgroundWebUiTests
{
    private static string Web(string path)
    {
        using var stream = EmbeddedWebStore.Open(path);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void DeleteConfirmationCanShrinkAndWrapUnbrokenFileNames()
    {
        var css = Web("css/v2-components.css");
        Assert.Contains(".v2-confirm > :last-child { flex: 1; min-width: 0; overflow-wrap: anywhere; }", css);
        Assert.Contains("white-space: normal; overflow-wrap: anywhere;", css);
        // The filename remains escaped; wrapping must not be implemented through raw HTML.
        Assert.Contains("${esc(file)}", Web("js/v2/background.js"));
    }

    [Fact]
    public void BackgroundDeleteIconDoesNotInheritPreviewIconSize()
    {
        Assert.Contains(".v2-bg-tile .v2-bg-del svg { width: 18px; height: 18px; }", Web("css/v2-components.css"));
    }
}
