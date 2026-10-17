using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class LegacyCharacterSkinImportTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "legacy-skin-tests-" + Guid.NewGuid().ToString("N"));
    readonly CharacterSkinManager manager = new();
    string Game => Path.Combine(root, "game");
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    static LegacyCharacterSkinImporter.Target Target(int hero = 117, int skin = 100117004, string asset = "UT_Hero_Card_117_02")
        => new(hero, skin, "fixture", new() { ["Character"] = asset }, new(StringComparer.Ordinal) { asset });
    static readonly byte[] Pixels = [0, 0, 255, 255, 255, 0, 0, 255]; // bottom red, top blue
    static byte[] Bundle(byte marker = 1, byte[]? suffix = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write("UnityFS\0"u8); writer.Write(new byte[] { 0, 0, 0, 8 });
        writer.Write(Encoding.ASCII.GetBytes("5.x.x\0")); writer.Write(Encoding.ASCII.GetBytes("2021.3.45f1\0"));
        int sizeOffset = (int)stream.Position; writer.Write(new byte[20]); writer.Write(new byte[] { 9, 8, 7, marker });
        byte[] bytes = stream.ToArray(); BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(sizeOffset, 8), bytes.Length);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(sizeOffset + 8, 4), 4);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(sizeOffset + 12, 4), 4);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(sizeOffset + 16, 4), 0x80);
        return suffix == null ? bytes : bytes.Concat(suffix).ToArray();
    }
    string Zip(params (string Name, byte[] Data)[] entries)
    {
        Directory.CreateDirectory(root); string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries) { using var stream = zip.CreateEntry(name).Open(); stream.Write(bytes); }
        return path;
    }
    Task<LegacyCharacterSkinImportResult> Import(string zip, LegacyCharacterSkinImporter.ReadBundle? reader = null,
        Action<CharacterExportProgress>? progress = null, CancellationToken token = default)
        => LegacyCharacterSkinImporter.ImportForTestingAsync(Game, zip, [Target()], new(StringComparer.Ordinal) { "UT_Hero_Card_117_02", "UT_AccountBackground_117_02" },
            reader ?? ((_, _) => [new("UT_Hero_Card_117_02", 1, 2, Pixels)]), progress ?? (_ => { }), token);
    void AssertRolledBack()
    {
        Assert.False(File.Exists(manager.Config(Game)));
        Assert.Empty(manager.Profiles(Game));
        if (Directory.Exists(manager.Root(Game))) Assert.Empty(Directory.GetDirectories(manager.Root(Game)));
    }

    [Fact]
    public async Task ImportPreservesDocumentsOriginalBundlesAndPngPixelsWithoutActivating()
    {
        var bundle = Bundle(); var document = Encoding.UTF8.GetBytes("Foxe（狐e）\n原作者授权声明");
        string zip = Zip(("替换/one.bundle", bundle), ("授权声明.txt", document));
        var progress = new List<int>();
        var result = await Import(zip, (_, _) => [new("UT_Hero_Card_117_02", 1, 2, Pixels), new("UT_AccountBackground_117_02", 1, 2, Pixels)], p => progress.Add(p.Percent));
        Assert.Equal(117, result.HeroId); Assert.Equal(100117004, result.SkinItemId); Assert.Equal(2, result.Images);
        Assert.Empty(result.Normalizations); Assert.Equal(progress.Order(), progress);
        var manifest = manager.Load(Game, result.Id); string package = manager.Package(Game, result.Id);
        Assert.Equal(100117004, manifest.SkinItemId); Assert.True(manifest.UseOriginalSpriteLayout);
        Assert.Contains("Character", manifest.Images.Keys); Assert.Contains("asset:UT_AccountBackground_117_02", manifest.Images.Keys);
        Assert.Equal(document, File.ReadAllBytes(Path.Combine(package, "source", "授权声明.txt")));
        Assert.Equal(bundle, File.ReadAllBytes(Path.Combine(package, "source", "替换", "one.bundle")));
        using var stream = File.OpenRead(Path.Combine(package, manifest.Images["Character"]));
        var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        byte[] pixels = new byte[8]; bitmap.CopyPixels(pixels, 4, 0);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }, pixels); // PNG is top-down
        Assert.False(File.Exists(manager.Config(Game))); Assert.Empty(manager.Settings(Game).ActivePackages);
        Assert.False(Directory.Exists(Path.Combine(package, ".work")));
    }

    [Fact]
    public async Task GbkZipPreservesChineseDocumentAndBundlePaths()
    {
        Directory.CreateDirectory(root);
        string zipPath = Path.Combine(root, "gbk.zip");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        byte[] document = Encoding.UTF8.GetBytes("原作者授权声明");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create, encoding))
        {
            using (var stream = zip.CreateEntry("飞鸟马时替换/one.bundle").Open()) stream.Write(Bundle());
            using (var stream = zip.CreateEntry("授权声明.txt").Open()) stream.Write(document);
        }
        var result = await Import(zipPath);
        string source = Path.Combine(manager.Package(Game, result.Id), "source");
        Assert.Equal(document, File.ReadAllBytes(Path.Combine(source, "授权声明.txt")));
        Assert.Equal(Bundle(), File.ReadAllBytes(Path.Combine(source, "飞鸟马时替换", "one.bundle")));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories), x => x.Contains('\uFFFD'));
    }

    [Fact]
    public void TargetUsesExactConfiguredNamesAndItemIdRatherThanSuffix()
    {
        var candidates = new[] { Target(), Target(117, 100117002, "UT_Hero_Card_117_01") };
        Assert.Equal(100117004, LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02"]).SkinItemId);
        Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02_extra"]));
        Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02", "UT_Hero_Card_117_01"]));
        Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02"], 117, 2));
    }

    [Fact]
    public void AmbiguousSharedAssetRequiresAnExactCandidateSelection()
    {
        var candidates = new[] { Target(), Target(118, 100118009) };
        var error = Assert.Throws<LegacyCharacterSkinTargetException>(() => LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02"]));
        Assert.Equal(2, error.Candidates.Length);
        Assert.Equal(100118009, LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02"], 118, 100118009).SkinItemId);
        Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.SelectTarget(candidates, ["UT_Hero_Card_117_02"], 118, 100118001));
    }

    [Theory]
    [InlineData("../escape.txt")] [InlineData("C:/escape.txt")] [InlineData("/escape.txt")]
    [InlineData("x\\..\\escape.txt")] [InlineData("x/CON.txt")] [InlineData("x/file. ")] [InlineData("x//file.txt")]
    public async Task RejectsInvalidPathsBeforeExtraction(string path)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Import(Zip(("one.bundle", Bundle()), (path, [1])))); AssertRolledBack();
    }

    [Theory]
    [InlineData("ONE.bundle")] [InlineData("x\\one.bundle")]
    public async Task RejectsCaseAndSeparatorDuplicatePaths(string duplicate)
    {
        string first = duplicate.Contains('\\') ? "x/one.bundle" : "one.bundle";
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip((first, Bundle()), (duplicate, Bundle())))); AssertRolledBack();
    }

    [Fact]
    public async Task RejectsFileDirectoryCollisionBeforeExtraction()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("one.bundle", Bundle()), ("docs", [1]), ("docs/file.txt", [2])))); AssertRolledBack();
    }

    [Fact]
    public async Task LaterUnsupportedBundleRollsBackEarlierConvertedImage()
    {
        string zip = Zip(("good.bundle", Bundle(1)), ("unsupported.bundle", Bundle(2)));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Import(zip, (path, _) => File.ReadAllBytes(path)[^1] == 1
            ? [new("UT_Hero_Card_117_02", 1, 2, Pixels)] : throw new NotSupportedException("AnimationClip unsupported")));
        Assert.Contains("unsupported.bundle", error.Message); AssertRolledBack();
    }

    [Fact]
    public async Task DuplicateOrUnknownTextureCannotProducePartialPackage()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("one.bundle", Bundle())), (_, _) =>
            [new("UT_Hero_Card_117_02", 1, 2, Pixels), new("UT_Hero_Card_117_02", 1, 2, Pixels)])); AssertRolledBack();
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("one.bundle", Bundle())), (_, _) =>
            [new("unknown", 1, 2, Pixels)])); AssertRolledBack();
    }

    [Fact]
    public async Task AuxiliaryTextureAbsentFromBundledConfigRetainsExactAssetName()
    {
        var result = await Import(Zip(("one.bundle", Bundle())), (_, _) =>
            [new("UT_Hero_Card_117_02", 1, 2, Pixels), new("UT_AccountBackground_117_02_not_in_subset", 1, 2, Pixels)]);
        Assert.Equal(2, result.Images);
        Assert.Contains("asset:UT_AccountBackground_117_02_not_in_subset", manager.Load(Game, result.Id).Images.Keys);
    }

    [Fact]
    public async Task CancellationAfterConversionRemovesStagingAndNeverActivates()
    {
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Import(Zip(("one.bundle", Bundle())), progress: p =>
            { if (p.Percent >= 80) cancellation.Cancel(); }, token: cancellation.Token)); AssertRolledBack();
    }

    [Fact]
    public async Task CorruptBundleFailsWithNoPartialPackage()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("bad.bundle", [1, 2, 3])))); AssertRolledBack();
    }

    [Fact]
    public void ZipEntryCountIsNotArtificiallyCapped()
    {
        // 不再按文件数量挡包：结构校验通过即接受（数量只受内存/磁盘约束）
        var entries = Enumerable.Range(0, 12001).Select(i => (i + ".txt", Array.Empty<byte>())).ToArray();
        using var archive = LegacyCharacterSkinImporter.OpenArchive(Zip(entries));
        Assert.Equal(entries.Length, LegacyCharacterSkinImporter.ValidateArchive(archive).Count);
    }

    [Fact]
    public void NormalizationOnlyTrimsExactDuplicateDirectorySuffix()
    {
        var original = Bundle(); var duplicated = Bundle(suffix: [7, 1]);
        Assert.Equal(original, LegacyCharacterSkinImporter.NormalizeBundle(duplicated, "bundle", out string? notice)); Assert.Contains("2", notice);
        Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.NormalizeBundle(Bundle(suffix: [7, 2]), "bundle", out _));
        byte[] truncated = original[..^1]; Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.NormalizeBundle(truncated, "bundle", out _));
        // 只拒绝「超过能读入内存的 2 GiB」：块信息未压缩大小写 2 GiB 应被拒绝
        byte[] hugeMetadata = original.ToArray(); int sizeOffset = 8 + 4 + 6 + 12;
        BinaryPrimitives.WriteUInt32BigEndian(hugeMetadata.AsSpan(sizeOffset + 12, 4), 0x80000000);
        Assert.Throws<InvalidDataException>(() => LegacyCharacterSkinImporter.NormalizeBundle(hugeMetadata, "bundle", out _));
    }

    [RealLegacyArchiveFact]
    public async Task RealTokiArchiveConvertsAllNineTexturesWithOriginalDocuments()
    {
        string archive = Environment.GetEnvironmentVariable("ASTRAL_TEST_LEGACY_SKIN_ZIP")!;
        var result = await LegacyCharacterSkinImporter.ImportAsync(Game, archive, Path.Combine(AppContext.BaseDirectory, "GameData"), Path.Combine(AppContext.BaseDirectory, "Protocol"), _ => { });
        Assert.Equal(117, result.HeroId); Assert.Equal(100117004, result.SkinItemId); Assert.Equal(9, result.Images);
        Assert.Single(result.Normalizations); Assert.Contains("59", result.Normalizations[0]);
        var manifest = manager.Load(Game, result.Id); string package = manager.Package(Game, result.Id);
        Assert.Equal(9, manifest.Images.Count); Assert.Equal(9, Directory.GetFiles(Path.Combine(package, "source"), "*.bundle", SearchOption.AllDirectories).Length);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(package, "source"), "*.pdf", SearchOption.AllDirectories));
        foreach (string path in manifest.Images.Values)
        {
            using var stream = File.OpenRead(Path.Combine(package, path));
            var image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            Assert.True(image.PixelWidth > 0); Assert.True(image.PixelHeight > 0);
        }
        Assert.False(File.Exists(manager.Config(Game)));
    }
}

public sealed class RealLegacyArchiveFactAttribute : FactAttribute
{
    public RealLegacyArchiveFactAttribute()
    {
        string? archive = Environment.GetEnvironmentVariable("ASTRAL_TEST_LEGACY_SKIN_ZIP");
        if (string.IsNullOrEmpty(archive) || !File.Exists(archive)) Skip = "设置 ASTRAL_TEST_LEGACY_SKIN_ZIP 可验证真实旧式皮肤包。";
    }
}
