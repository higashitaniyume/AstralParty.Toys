using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AstralParty.Toys.Services.App;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void Version231_UsesMatchingAppAndInstallerVersion()
    {
        var project = XDocument.Load(Path.Combine(TestPaths.AppSourceDirectory, "AstralParty.Toys.csproj"));
        Assert.Equal("2.3.1", project.Descendants("Version").Single().Value);
        Assert.Equal("2.3.1.0", project.Descendants("AssemblyVersion").Single().Value);
        Assert.Equal("2.3.1.0", project.Descendants("FileVersion").Single().Value);
    }

    [Fact]
    public void Release_DoesNotGenerateNotesOrSpecialCaseVersion220()
    {
        var workflow = ReadWorkflow();
        Assert.Contains("generate_release_notes: false", workflow);
        Assert.Contains("prerelease: ${{ contains(github.ref_name, '-') }}", workflow);
        Assert.DoesNotContain("github.ref_name == 'v2.2.0'", workflow);
    }

    [Fact]
    public void Release_UsesLatestLoaderAndSkins()
    {
        var workflow = ReadWorkflow();
        Assert.Contains("releases/latest/download/cesium-loader.zip", workflow);
        Assert.Contains("releases/latest/download/cesium-default-skins.zip", workflow);
        Assert.Contains("if ((Test-Path $packagedChangelog) -and", workflow);
    }

    [Fact]
    public void Release_ManifestListsEveryPackageForm()
    {
        // 单文件版和便携版的原地更新靠 version.json 里的这几项
        var workflow = ReadWorkflow();
        foreach (var key in new[] { "frameworkDependentInstaller", "selfContainedInstaller", "frameworkDependentSingleFile", "selfContainedSingleFile", "frameworkDependentPortable", "selfContainedPortable" })
            Assert.Contains(key, workflow);
        Assert.Contains("*-self-contained-single-file.exe", workflow);
        Assert.Contains("*-framework-dependent-portable.zip", workflow);
    }

    /// <summary>
    /// version.json 里的字段名必须和客户端读它的属性名一致：写错一个，
    /// 旧客户端会忽略、新客户端拿到空值，所有单文件版/便携版用户就都只能手动下载了。
    /// </summary>
    [Fact]
    public void Release_ManifestKeysMatchTheClientPackageFields()
    {
        var workflow = ReadWorkflow();
        var workflowKeys = Regex.Matches(workflow, @"^\s*([A-Za-z][A-Za-z0-9]*)\s*=\s*""\*-", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToArray();
        var clientKeys = typeof(AppPackageAssets)
            .GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToArray();

        Assert.NotEmpty(workflowKeys);
        Assert.Equal(clientKeys.OrderBy(k => k), workflowKeys.OrderBy(k => k));
    }

    private static string ReadWorkflow() => File.ReadAllText(Path.GetFullPath(Path.Combine(
        TestPaths.AppSourceDirectory, "..", "..", ".github", "workflows", "release.yml")));
}
