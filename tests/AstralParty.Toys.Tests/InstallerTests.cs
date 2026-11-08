using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 安装器（installer/Setup.iss）的接线：安装目录必须为空（已有安装升级除外），
/// 静默安装也拦，协议注册受安装选项控制。编译本身由 CI 的 Inno Setup 完成，这里只守文本接线。
/// </summary>
public sealed class InstallerTests
{
    private static string ReadSetup() => File.ReadAllText(Path.GetFullPath(Path.Combine(
        TestPaths.AppSourceDirectory, "..", "..", "installer", "Setup.iss")));

    [Fact]
    public void Installer_RejectsNonEmptyFolderUnlessItIsExistingInstall()
    {
        var setup = ReadSetup();

        Assert.Contains("function IsDirectoryEmpty", setup);
        Assert.Contains("function IsExistingToysInstall", setup);
        Assert.Contains("unins000.exe", setup);
        Assert.Contains("The selected folder is not empty", setup);

        // 向导选目录页：空目录检查要在写权限探针之前（探针文件本身会让空目录变非空）
        var nextButton = setup.IndexOf("function NextButtonClick", StringComparison.Ordinal);
        var emptyCheck = setup.IndexOf("IsDirectoryEmpty(Directory)", nextButton, StringComparison.Ordinal);
        var probe = setup.IndexOf("ForceDirectories(Directory)", nextButton, StringComparison.Ordinal);
        Assert.True(nextButton >= 0 && emptyCheck > nextButton && probe > emptyCheck,
            "空目录检查应在 ForceDirectories 探针之前");
    }

    [Fact]
    public void Installer_SilentInstallAlsoRejectsNonEmptyFolder()
    {
        var setup = ReadSetup();

        // 静默安装不走选目录页，PrepareToInstall 要再查一次
        Assert.Contains("function PrepareToInstall", setup);
        Assert.Contains("IsDirectoryEmpty(Directory)", setup);
        Assert.Contains("IsExistingToysInstall(Directory)", setup);
    }
}
