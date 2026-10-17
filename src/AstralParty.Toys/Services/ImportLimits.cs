namespace AstralParty.Toys.Services;

/// <summary>
/// 皮肤/资源导入只按「引擎或运行环境真的做不到」的边界限制，不设人为的文件数量或总大小上限。
///
/// 保留的三条边界：
/// <list type="number">
///   <item>Unity Texture2D / Sprite 的边长上限 —— GPU 纹理尺寸硬上限，超过引擎本身也用不了；</item>
///   <item>单个 UnityFS bundle 能整块读进托管 <c>byte[]</c>、且头里的长度字段会转成 <c>int</c>；</item>
///   <item>目标磁盘放不放得下 —— 解压前先问一次可用空间，真放不下才拒绝，而不是拿一个固定数字挡包。</item>
/// </list>
///
/// 压缩炸弹仍受「实际字节必须与 ZIP 声明一致」约束：声明多大就写多大，不会静默多解压。
/// </summary>
internal static class ImportLimits
{
    /// <summary>Unity Texture2D / Sprite 单边最大 16384 像素（引擎/GPU 上限）。</summary>
    public const int MaxTextureDimension = 16384;

    /// <summary>单个 UnityFS 包上限：头里的长度会转成 <c>int</c>，整包也要读进 <c>byte[]</c>（约 2 GiB）。</summary>
    public const long MaxBundleBytes = int.MaxValue;

    /// <summary>解压前确认目标卷放得下；拿不到可用空间（例如 UNC 路径）时不拦，交给写盘时的真实失败。</summary>
    public static void EnsureFreeSpace(string directory, long requiredBytes)
    {
        if (requiredBytes <= 0) return;
        string root;
        long available;
        try
        {
            root = Path.GetPathRoot(Path.GetFullPath(directory)) ?? "";
            if (root.Length == 0) return;
            available = new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return;
        }
        if (available < requiredBytes)
            throw new InvalidDataException(
                $"磁盘剩余空间不足：需要 {FormatSize(requiredBytes)}，{root} 只剩 {FormatSize(available)}。");
    }

    /// <summary>把字节数写成好读的单位（导入失败提示用）。</summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.#") + " GiB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0.#") + " MiB",
        >= 1L << 10 => (bytes / (double)(1L << 10)).ToString("0.#") + " KiB",
        _ => bytes + " B"
    };
}
