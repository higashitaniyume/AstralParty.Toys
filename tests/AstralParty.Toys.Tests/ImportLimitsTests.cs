using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 导入限制只保留引擎/环境约束：Unity 纹理边长、单个 bundle 能读入内存、目标磁盘放得下。
/// 有意不设文件数量或总大小上限 —— 这几条边界也必须真的是「做不到」，而不是随手拍的数字。
/// </summary>
public sealed class ImportLimitsTests
{
    [Fact]
    public void TextureDimensionUsesUnityLimit()
    {
        // Unity Texture2D / Sprite 的边长硬上限
        Assert.Equal(16384, ImportLimits.MaxTextureDimension);
    }

    [Fact]
    public void BundleLimitMatchesManagedArrayRange()
    {
        // UnityFS 头里的长度会转成 int，整包还要读进 byte[]
        Assert.Equal(int.MaxValue, ImportLimits.MaxBundleBytes);
    }

    [Fact]
    public void EnsureFreeSpace_AllowsRequirementThatFits()
    {
        ImportLimits.EnsureFreeSpace(Path.GetTempPath(), 1024);
    }

    [Fact]
    public void EnsureFreeSpace_RejectsRequirementBeyondDisk()
    {
        // 需要 4 EiB：任何磁盘都放不下 → 在解压前就明确报错
        var error = Assert.Throws<InvalidDataException>(
            () => ImportLimits.EnsureFreeSpace(Path.GetTempPath(), long.MaxValue / 2));
        Assert.Contains("磁盘剩余空间不足", error.Message);
    }

    [Theory]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2 KiB")]
    [InlineData(3L * 1024 * 1024, "3 MiB")]
    [InlineData(2L * 1024 * 1024 * 1024, "2 GiB")]
    public void FormatSize_UsesReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, ImportLimits.FormatSize(bytes));
    }
}
