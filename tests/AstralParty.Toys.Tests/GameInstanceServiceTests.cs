using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>多开实例外壳：不同 exe 路径 + 同一份数据目录。</summary>
public sealed class GameInstanceServiceTests
{
    [Fact]
    public void EnsureInstance_LinksExecutableAndSharesDataDirectories()
    {
        using var sandbox = new TestSandbox("ap-instance");
        var game = sandbox.EnsureDirectory("game");
        sandbox.WriteFile("game/AstralParty_CN.exe", "exe");
        sandbox.WriteFile("game/GameAssembly.dll", "dll");
        sandbox.WriteFile("game/AstralParty_CN_Data/marker.txt", "data");
        sandbox.WriteFile("game/AstralParty_ModLoader/doorstop_config.json", "{}");

        var shellExe = GameInstanceService.EnsureInstance(game, "P2");
        var shell = Path.GetDirectoryName(shellExe)!;

        try
        {
            Assert.True(File.Exists(shellExe));
            Assert.EndsWith("AstralParty_CN.exe", shellExe, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(shell, "GameAssembly.dll")));

            // 数据目录与加载器目录是 junction：读到的就是原目录内容。
            Assert.Equal("data", File.ReadAllText(Path.Combine(shell, "AstralParty_CN_Data", "marker.txt")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(shell, "AstralParty_ModLoader", "doorstop_config.json")));

            // 同一 profile 重复调用返回同一个外壳。
            Assert.Equal(shellExe, GameInstanceService.EnsureInstance(game, "P2"));
        }
        finally
        {
            DeleteShell(shell);
        }
    }

    private static void DeleteShell(string shell)
    {
        foreach (var name in new[] { "AstralParty_CN_Data", "AstralParty_ModLoader", "AstralParty_ResourceMods", "_generated" })
        {
            var path = Path.Combine(shell, name);
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: false);
            }
            catch
            {
                // 清理失败不影响测试结论
            }
        }

        try
        {
            if (Directory.Exists(shell)) Directory.Delete(shell, recursive: true);
            var parent = Path.GetDirectoryName(shell);
            if (parent is not null && Directory.Exists(parent)) Directory.Delete(parent, recursive: false);
        }
        catch
        {
            // 忽略清理失败
        }
    }
}
