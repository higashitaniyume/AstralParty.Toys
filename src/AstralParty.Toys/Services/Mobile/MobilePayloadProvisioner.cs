using System.Reflection;
using System.Security.Cryptography;

namespace AstralParty.Toys.Services.Mobile;

/// <summary>
/// 把随包内嵌的移动端载荷包解到数据根，供「投放载荷」直接用。
///
/// 内嵌的是**一个 zip**（<c>Resources\ModLoader\cesium-loader-android-managed.zip</c>，安卓仓库发版产出），
/// 与桌面那包同一条路：不在代码里写死版本或哈希，版本从包内 <c>loader.json</c> 读，
/// CI 打 tag 时按安卓仓库最新 Release 的 <c>version.json → packages.loaderManaged</c> 覆盖这个文件。
/// </summary>
public static class MobilePayloadProvisioner
{
    /// <summary>内嵌资源名，由 csproj 的 <c>LogicalName</c> 固定。</summary>
    public const string ResourceName = "AstralParty.Toys.ModLoader.cesium-loader-android-managed.zip";

    /// <summary>落到数据根后的文件名。</summary>
    public const string FileName = "cesium-loader-android-managed.zip";

    /// <summary>版本标记：应用版本 + 内嵌包内容哈希，任一变化都重解。</summary>
    private const string MarkerName = "cesium-loader-android-managed.sha256";

    /// <summary>载荷所在目录（数据根下）。</summary>
    public static string PayloadDirectory(string dataRoot) => Path.Combine(dataRoot, "payload");

    /// <summary>载荷包路径（不存在时按需落盘）。</summary>
    public static string PayloadArchive(string dataRoot) =>
        Path.Combine(PayloadDirectory(dataRoot), FileName);

    /// <summary>
    /// 确保载荷包已落盘且是最新内容，返回包路径；内容没变时不做任何写入。
    /// </summary>
    public static string Ensure(string dataRoot)
    {
        var directory = PayloadDirectory(dataRoot);
        var target = Path.Combine(directory, FileName);
        var marker = Path.Combine(directory, MarkerName);

        // 直接读资源流算哈希：标记同时覆盖「应用换了包」和「同一版本里换了包」两种情况
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"应用里没有内嵌移动端载荷包（{ResourceName}）");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var stamp = (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0") + " " + hash;
        if (File.Exists(target) && File.Exists(marker) &&
            string.Equals(File.ReadAllText(marker).Trim(), stamp, StringComparison.Ordinal))
            return target;

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(target, bytes);
        File.WriteAllText(marker, stamp);
        return target;
    }

    /// <summary>内嵌载荷包的 SHA-256（十六进制小写），用于日志与自检。</summary>
    public static string EmbeddedHash()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"应用里没有内嵌移动端载荷包（{ResourceName}）");
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}
