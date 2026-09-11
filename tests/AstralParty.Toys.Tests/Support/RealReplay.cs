namespace AstralParty.Toys.Tests.Support;

/// <summary>
/// 真实回放取样：优先环境变量 <c>ASTRAL_TEST_REPLAY</c>，其次游戏目录里最新的一局。
/// 需要真实数据的测试靠它决定「跑」还是「跳过」——绝不在没有数据时假装通过。
/// </summary>
internal static class RealReplay
{
    public const string EnvironmentVariable = "ASTRAL_TEST_REPLAY";

    public static string? FindFile()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return Path.GetFullPath(fromEnvironment);

        var root = TestPaths.GameReplayRoot;
        if (!Directory.Exists(root)) return null;

        return Directory.EnumerateDirectories(root)
            .Select(dir => Path.Combine(dir, Path.GetFileName(dir)))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static bool IsAvailable => FindFile() is not null;

    public static string SkipReason =>
        $"没有可用的真实回放文件：把一局录像放进 {TestPaths.GameReplayRoot}，" +
        $"或设置环境变量 {EnvironmentVariable} 指向回放文件。";
}

/// <summary>需要真实回放的测试：没有数据时报告为「已跳过」，而不是失败。</summary>
public sealed class RealReplayFactAttribute : FactAttribute
{
    public RealReplayFactAttribute()
    {
        if (!RealReplay.IsAvailable) Skip = RealReplay.SkipReason;
    }
}
