using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class LegacyCardSkinImportTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "legacy-card-tests-" + Guid.NewGuid().ToString("N"));
    readonly SkinManager manager = new();
    string Game => Path.Combine(root, "game");
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    static byte[] Bundle(byte marker = 1)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("UnityFS\0"u8); writer.Write(new byte[] { 0, 0, 0, 8 });
        writer.Write(Encoding.ASCII.GetBytes("5.x.x\0")); writer.Write(Encoding.ASCII.GetBytes("2021.3.45f1\0"));
        int offset = (int)stream.Position; writer.Write(new byte[20]); writer.Write(marker);
        byte[] bytes = stream.ToArray(); BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(offset), bytes.Length);
        return bytes;
    }
    string Zip(params (string Name, byte[] Data)[] files)
    {
        Directory.CreateDirectory(root); string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes) in files) { using var entry = archive.CreateEntry(name).Open(); entry.Write(bytes); }
        return path;
    }
    static LegacyCharacterSkinImporter.Texture Image(string name = "UT_HandCard_10001") => new(name, 1, 2, [0, 0, 255, 255, 255, 0, 0, 255]);
    Task<LegacyCardSkinImportResult> Import(string path, LegacyCharacterSkinImporter.ReadBundle? reader = null,
        Action<CharacterExportProgress>? progress = null, CancellationToken token = default)
        => LegacyCardSkinImporter.ImportForTestingAsync(Game, path, reader ?? ((_, _) => [Image()]), progress ?? (_ => { }), token);
    void AssertRolledBack()
    {
        Assert.DoesNotContain(manager.GetProfiles(Game), x => !x.IsDefault);
        string loader = Path.Combine(Game, "AstralParty_ModLoader");
        if (Directory.Exists(loader)) Assert.DoesNotContain(Directory.GetDirectories(loader), x => Path.GetFileName(x).StartsWith(".legacy-cards-import-"));
    }

    [Theory]
    [InlineData("UT_HandCard_10001")] [InlineData("UT_Event_12704")] [InlineData("UT_Destiny_1_sfw")]
    [InlineData("UT_MapEvent_2")] [InlineData("UT_Item_AltArt_3")] [InlineData("UT_IAltArt_4")] [InlineData("Card_5")]
    public void SupportsExactCardNames(string name) => Assert.True(LegacyCardSkinImporter.IsCardAsset(name));
    [Theory]
    [InlineData("UT_Hero_Card_117_02")] [InlineData("UT_Event_0")] [InlineData("UT_Event_2147483648")]
    [InlineData("UT_Event_1/evil")] [InlineData("UT_HandCard_1.png")] [InlineData("UT_Event_1_more")]
    public void RejectsOtherOrUnsafeNames(string name) => Assert.False(LegacyCardSkinImporter.IsCardAsset(name));

    [Fact]
    public async Task ImportKeepsActiveSkinAndWritesRootPngAndMetadataPreservingSource()
    {
        manager.SetActiveSkin(Game, "existing"); string config = File.ReadAllText(manager.GetModConfigPath(Game));
        byte[] bundle = Bundle(), docs = Encoding.UTF8.GetBytes("原作者说明");
        string zip = Zip(("旧式/one.bundle", bundle), ("授权.txt", docs));
        bool checkedStaging = false;
        var result = await Import(zip, (_, _) => [Image(), Image("UT_Event_12704")], p =>
        {
            if (p.Percent >= 5 && p.Percent < 99) { Assert.DoesNotContain(manager.GetProfiles(Game), x => !x.IsDefault); checkedStaging = true; }
        });
        Assert.True(checkedStaging); Assert.Equal(2, result.Images); Assert.Empty(result.Normalizations);
        Assert.StartsWith("legacy-cards-", result.Name);
        string dir = Path.Combine(manager.GetSkinsDirectory(Game), result.Name);
        Assert.Equal(bundle, File.ReadAllBytes(Path.Combine(dir, "source", "旧式", "one.bundle")));
        Assert.Equal(docs, File.ReadAllBytes(Path.Combine(dir, "source", "授权.txt")));
        Assert.Equal(config, File.ReadAllText(manager.GetModConfigPath(Game)));
        var profile = Assert.Single(manager.GetProfiles(Game), x => x.Name == result.Name);
        Assert.Equal(2, profile.CardCount); Assert.False(profile.IsActive);
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "skin.json")));
        Assert.Equal(Path.GetFileNameWithoutExtension(zip), metadata.RootElement.GetProperty("name").GetString());
        using var image = File.OpenRead(Path.Combine(dir, "UT_HandCard_10001.png"));
        var bitmap = BitmapDecoder.Create(image, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        byte[] pixels = new byte[8]; bitmap.CopyPixels(pixels, 4, 0);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }, pixels);
    }
    [Fact]
    public async Task LaterFailureRollsBackAllCards()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("a.bundle", Bundle()), ("b.bundle", Bundle(2))),
            (path, _) => File.ReadAllBytes(path)[^1] == 1 ? [Image()] : throw new InvalidDataException("Unsupported Sprite")));
        Assert.Contains("b.bundle", error.Message); AssertRolledBack();
    }
    [Fact]
    public async Task MixedNonCardAndCaseDuplicateTexturesFailAtomically()
    {
        string zip = Zip(("one.bundle", Bundle()));
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(zip, (_, _) => [Image(), Image("UT_Hero_Card_117_02")])); AssertRolledBack();
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(zip, (_, _) => [Image(), Image("ut_handcard_10001")])); AssertRolledBack();
    }
    [Fact]
    public async Task CancellationAndProgressFailureRemoveStaging()
    {
        string zip = Zip(("one.bundle", Bundle())); using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Import(zip, progress: p => { if (p.Percent >= 95) cancellation.Cancel(); }, token: cancellation.Token));
        AssertRolledBack();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Import(zip, progress: p => { if (p.Percent >= 99) throw new InvalidOperationException("observer"); }));
        AssertRolledBack();
    }
    [Theory]
    [InlineData("../escape")] [InlineData("C:/escape")] [InlineData("x/CON")]
    public async Task InvalidZipPathsFailBeforeReadingBundle(string path)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("a.bundle", Bundle()), (path, [1])))); AssertRolledBack();
    }
    [Fact]
    public async Task EmptyAndCorruptBundlesFail()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("readme.txt", [1])))); AssertRolledBack();
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("a.bundle", [1])))); AssertRolledBack();
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Zip(("a.bundle", Bundle())), (_, _) => [])); AssertRolledBack();
    }
    [Fact]
    public async Task LegacyGbkDocumentNamesAndTailNormalizationArePreserved()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); Directory.CreateDirectory(root);
        string zip = Path.Combine(root, "GBK.zip"); byte[] bundle = Bundle();
        // A duplicated directory suffix is normalized only in the decoding copy.
        BinaryPrimitives.WriteUInt32BigEndian(bundle.AsSpan(38), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bundle.AsSpan(46), 0x80);
        byte[] duplicated = bundle.Concat(new byte[] { 1 }).ToArray();
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create, Encoding.GetEncoding(936)))
        {
            using (var entry = archive.CreateEntry("替换/a.bundle").Open()) entry.Write(duplicated);
            using (var entry = archive.CreateEntry("授权.txt").Open()) entry.Write([1, 2]);
        }
        var result = await Import(zip); Assert.Single(result.Normalizations);
        string source = Path.Combine(manager.GetSkinsDirectory(Game), result.Name, "source");
        Assert.Equal(duplicated, File.ReadAllBytes(Path.Combine(source, "替换", "a.bundle")));
        Assert.True(File.Exists(Path.Combine(source, "授权.txt")));
    }
    [Theory]
    [InlineData(1,1)] [InlineData(5,7)] [InlineData(4,5)] [InlineData(7,4)]
    public void Bc7PartialBlocksPreservePixelsAndOriginalDimensions(int width,int height)
    {
        int paddedWidth=(width+3)&~3,paddedHeight=(height+3)&~3;
        var bytes=new byte[paddedWidth*paddedHeight];
        for(int block=0;block<bytes.Length/16;block++)
        {
            bytes[block*16]=0x40; // BC7 mode 6
            for(int j=1;j<16;j++)bytes[block*16+j]=(byte)(block*37+j*11);
        }
        var texture=new AssetsTools.NET.Texture.TextureFile {m_Width=width,m_Height=height,m_TextureFormat=25};
        byte[] decoded=CharacterUnityResources.DecodeTexture(texture,bytes)!;
        Assert.Equal(width,texture.m_Width);Assert.Equal(height,texture.m_Height);Assert.Equal(width*height*4,decoded.Length);
        // Independently decode each block and compare cropped pixel coordinates, including last row/column.
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int block=(y/4)*(paddedWidth/4)+x/4;
            var aligned=new AssetsTools.NET.Texture.TextureFile {m_Width=4,m_Height=4,m_TextureFormat=25};
            byte[] expected=aligned.DecodeTextureRaw(bytes.AsSpan(block*16,16).ToArray(),true);
            Assert.Equal(expected.AsSpan(((y%4)*4+x%4)*4,4).ToArray(),decoded.AsSpan((y*width+x)*4,4).ToArray());
        }
        Assert.Throws<InvalidDataException>(()=>CharacterUnityResources.DecodeTexture(texture,bytes[..^1]));
    }
    [RealLegacyCardArchiveFact]
    public async Task RealArchiveConverts42ImagesAndPreservesInactiveProfile()
    {
        string zip = Environment.GetEnvironmentVariable("ASTRAL_TEST_LEGACY_CARD_ZIP")!;
        var result = await LegacyCardSkinImporter.ImportAsync(Game, zip, _ => { });
        Assert.Equal(42, result.Images); Assert.Empty(result.Normalizations);
        string dir = Path.Combine(manager.GetSkinsDirectory(Game), result.Name);
        Assert.Equal(36, Directory.GetFiles(dir, "UT_HandCard_*.png").Length);
        Assert.Equal(6, Directory.GetFiles(dir, "UT_Event_*.png").Length);
        Assert.Equal(42, Directory.GetFiles(Path.Combine(dir, "source"), "*.bundle", SearchOption.AllDirectories).Length);
        Assert.False(File.Exists(manager.GetModConfigPath(Game)));
        Assert.False(Assert.Single(manager.GetProfiles(Game), x => x.Name == result.Name).IsActive);
        foreach (string path in Directory.GetFiles(dir, "*.png"))
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            Assert.True(frame.PixelWidth > 0 && frame.PixelHeight > 0);
        }
    }
}
public sealed class RealLegacyCardArchiveFactAttribute : FactAttribute
{
    public RealLegacyCardArchiveFactAttribute()
    {
        string? archive = Environment.GetEnvironmentVariable("ASTRAL_TEST_LEGACY_CARD_ZIP");
        if (string.IsNullOrEmpty(archive) || !File.Exists(archive)) Skip = "设置 ASTRAL_TEST_LEGACY_CARD_ZIP 可验证真实旧式卡牌包。";
    }
}
