using System.Xml.Linq;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void Version220_UsesMatchingAppAndInstallerVersion()
    {
        var project = XDocument.Load(Path.Combine(TestPaths.AppSourceDirectory, "AstralParty.Toys.csproj"));
        Assert.Equal("2.2.0", project.Descendants("Version").Single().Value);
        Assert.Equal("2.2.0.0", project.Descendants("AssemblyVersion").Single().Value);
        Assert.Equal("2.2.0.0", project.Descendants("FileVersion").Single().Value);
    }

    [Fact]
    public void Version220_IsPrereleaseWithoutNotesOrLatestPromotion()
    {
        var workflow = ReadWorkflow();
        Assert.Contains("generate_release_notes: false", workflow);
        Assert.Contains("prerelease: ${{ github.ref_name == 'v2.2.0' || contains(github.ref_name, '-') }}", workflow);
        Assert.Contains("make_latest: ${{ github.ref_name == 'v2.2.0' && 'false' || 'legacy' }}", workflow);
    }

    [Fact]
    public void Version220_PinsBothLoaderAndSkinsToMatchingCandidate()
    {
        var workflow = ReadWorkflow();
        Assert.Contains("CESIUM_RELEASE_PATH: ${{ github.ref_name == 'v2.2.0' && 'download/modloader-3.6.0' || 'latest/download' }}", workflow);
        Assert.Contains("releases/$env:CESIUM_RELEASE_PATH/cesium-loader.zip", workflow);
        Assert.Contains("releases/$env:CESIUM_RELEASE_PATH/cesium-default-skins.zip", workflow);
    }

    private static string ReadWorkflow() => File.ReadAllText(Path.GetFullPath(Path.Combine(
        TestPaths.AppSourceDirectory, "..", "..", ".github", "workflows", "release.yml")));
}
