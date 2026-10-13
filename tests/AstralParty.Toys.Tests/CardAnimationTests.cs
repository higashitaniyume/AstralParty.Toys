using System.Diagnostics;
using System.IO.Compression;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute() { if (CardAnimationConverter.FindFfmpeg() == null) Skip = "FFmpeg is not installed; conversion integration requires it."; }
}

public sealed class CardAnimationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ToysCardAnimation-" + Guid.NewGuid().ToString("N"));
    private readonly SkinManager _manager = new();
    public CardAnimationTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private string CreatePackage(string binding = "UT_HandCard_10001", string skin = "custom")
    {
        var directory = Path.Combine(_manager.GetSkinsDirectory(_root), skin);
        Directory.CreateDirectory(Path.Combine(directory, binding + ".frames"));
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jH1sAAAAASUVORK5CYII=");
        File.WriteAllBytes(Path.Combine(directory, binding + ".frames", "000000.png"), png);
        File.WriteAllBytes(Path.Combine(directory, binding + ".frames", "000001.png"), png);
        var manifest = Path.Combine(directory, binding + ".cardanim");
        File.WriteAllText(manifest, "version=1\nfps=15\nframeCount=2\nwidth=1\nheight=1\n");
        return manifest;
    }

    [Fact]
    public void AnimatedCardListsOnceAndExportsAllFrames()
    {
        var manifest = CreatePackage();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(manifest)!, "UT_HandCard_10001.png"), "static fallback");
        var card = Assert.Single(_manager.GetCards(_root, "custom"));
        Assert.True(card.IsAnimated);
        Assert.Equal(2, card.AnimationFrameCount);
        Assert.Contains(".frames/000000.png", card.PreviewUrl);
        Assert.Equal(1, _manager.GetProfiles(_root).Single(p => p.Name == "custom").CardCount);
        var zip = Path.Combine(_root, "skin.zip");
        _manager.ExportSkinZip(_root, "custom", zip);
        using (var archive = ZipFile.OpenRead(zip))
        {
            Assert.Contains(archive.Entries, e => e.FullName == "custom/UT_HandCard_10001.cardanim");
            Assert.Contains(archive.Entries, e => e.FullName == "custom/UT_HandCard_10001.frames/000001.png");
        }
        var other = Path.Combine(_root, "otherGame");
        Assert.Equal("custom", _manager.ImportSkinZip(other, zip));
        Assert.True(Assert.Single(_manager.GetCards(other, "custom")).IsAnimated);
    }

    [Fact]
    public void FlatZipPreservesFrameDirectories()
    {
        var manifest = CreatePackage("90001");
        var zip = Path.Combine(_root, "flat.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(manifest, "90001.cardanim");
            archive.CreateEntryFromFile(Path.Combine(Path.GetDirectoryName(manifest)!, "90001.frames/000000.png"), "90001.frames/000000.png");
            archive.CreateEntryFromFile(Path.Combine(Path.GetDirectoryName(manifest)!, "90001.frames/000001.png"), "90001.frames/000001.png");
        }
        var skin = _manager.ImportSkinZip(_root, zip);
        Assert.True(Assert.Single(_manager.GetCards(_root, skin)).IsAnimated);
    }

    [Fact]
    public void InvalidPackageFallsBackAndDoesNotListFrameImages()
    {
        var manifest = CreatePackage();
        File.WriteAllText(manifest, "version=1\nfps=15\nframeCount=301\nwidth=1\nheight=1\n");
        Assert.Throws<InvalidDataException>(() => CardAnimationPackage.Read(manifest));
        Assert.Empty(_manager.GetCards(_root, "custom"));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(manifest)!, "UT_HandCard_10001.png"), "static");
        Assert.False(Assert.Single(_manager.GetCards(_root, "custom")).IsAnimated);
    }

    [Fact]
    public void StaticReplacementRemovesCorrespondingAnimation()
    {
        var manifest = CreatePackage();
        var source = Path.Combine(_root, "static.png");
        File.WriteAllText(source, "replacement");
        _manager.ReplaceCardImage(_root, "custom", "UT_HandCard_10001.png", source);
        Assert.False(File.Exists(manifest));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(manifest)!, "UT_HandCard_10001.frames")));
        Assert.False(Assert.Single(_manager.GetCards(_root, "custom")).IsAnimated);
    }

    [Fact]
    public void NumericBindingOverlaysKnownCardAndExportsOriginalFallback()
    {
        var manifest = CreatePackage("10001");
        var defaults = Path.Combine(_manager.GetSkinsDirectory(_root), "default");
        Directory.CreateDirectory(defaults);
        File.WriteAllText(Path.Combine(defaults, "UT_HandCard_10001.png"), "default fallback");
        var card = Assert.Single(_manager.GetCards(_root, "custom"));
        Assert.True(card.IsAnimated);
        Assert.Equal("UT_HandCard_10001", card.AssetKey);
        Assert.Equal("10001", card.AnimationBinding);
        var zip = Path.Combine(_root, "numeric.zip");
        _manager.ExportSkinZip(_root, "custom", zip);
        using (var archive = ZipFile.OpenRead(zip))
        {
            Assert.Contains(archive.Entries, e => e.FullName == "custom/10001.cardanim");
            Assert.Contains(archive.Entries, e => e.FullName == "custom/UT_HandCard_10001.png");
        }
        var source = Path.Combine(_root, "replacement.png");
        File.WriteAllText(source, "static");
        _manager.ReplaceCardImage(_root, "custom", card.FileName, source);
        Assert.False(File.Exists(manifest));
    }

    [Theory]
    [InlineData("+15")]
    [InlineData("-15")]
    [InlineData("１５")]
    public void StrictNumbersMatchModParser(string fps)
    {
        var manifest = CreatePackage();
        File.WriteAllText(manifest, $"version=1\nfps={fps}\nframeCount=2\nwidth=1\nheight=1\n");
        Assert.Throws<InvalidDataException>(() => CardAnimationPackage.Read(manifest));
    }

    [Fact]
    public void WhitespaceAndPngHeaderValidationMatchModParser()
    {
        var manifest = CreatePackage();
        File.WriteAllText(manifest, "version = 1\nfps = 15\nframeCount = 2\nwidth = 1\nheight = 1\n");
        Assert.Equal(15, CardAnimationPackage.Read(manifest).Fps);
        var frame = Path.Combine(Path.GetDirectoryName(manifest)!, "UT_HandCard_10001.frames/000000.png");
        var bytes = File.ReadAllBytes(frame); bytes[11] = 12; File.WriteAllBytes(frame, bytes);
        Assert.Throws<InvalidDataException>(() => CardAnimationPackage.Read(manifest));
    }

    [Fact]
    public void CropBoundsAndAspectAreValidatedBeforeConversion()
    {
        new CardAnimationCrop(0, 0, 101, 100, 200, 200).Validate();
        Assert.Throws<ArgumentException>(() => new CardAnimationCrop(150, 0, 101, 100, 200, 200).Validate());
        Assert.Throws<ArgumentException>(() => new CardAnimationCrop(0, 0, 100, 200, 200, 200).Validate());
    }

    [Theory]
    [InlineData("../10001")]
    [InlineData("C:\\10001")]
    [InlineData("..")]
    [InlineData("10001/other")]
    [InlineData("name.")]
    public void BindingRejectsPathEscape(string binding) => Assert.Throws<ArgumentException>(() => CardAnimationConverter.ValidateBinding(binding));

    [FfmpegFact]
    public async Task VideoAndGifConvertToPlayableBoundedSequences()
    {
        var ffmpeg = CardAnimationConverter.FindFfmpeg()!;
        foreach (var extension in new[] { ".mp4", ".gif" })
        {
            var source = Path.Combine(_root, "source (测试) 'literal'" + extension);
            await Generate(ffmpeg, source, "1");
            var directory = Path.Combine(_root, extension.TrimStart('.'));
            await CardAnimationConverter.ConvertAsync(ffmpeg, source, directory, "10001");
            var package = CardAnimationPackage.Read(Path.Combine(directory, "10001.cardanim"));
            Assert.InRange(package.FrameCount, 14, 16);
            Assert.Equal(15, package.Fps);
            Assert.Equal((404, 400), (package.Width, package.Height));
            Assert.NotEqual(File.ReadAllBytes(Path.Combine(directory, "10001.frames/000000.png")), File.ReadAllBytes(Path.Combine(directory, "10001.frames/000010.png")));
        }
    }

    [FfmpegFact]
    public async Task OverlongAndCanceledConversionsPreserveOldPackage()
    {
        var ffmpeg = CardAnimationConverter.FindFfmpeg()!;
        var manifest = CreatePackage("10001");
        var original = File.ReadAllBytes(manifest);
        var source = Path.Combine(_root, "long.mp4");
        await Generate(ffmpeg, source, "21");
        var directory = Path.GetDirectoryName(manifest)!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CardAnimationConverter.ConvertAsync(ffmpeg, source, directory, "10001"));
        Assert.Equal(original, File.ReadAllBytes(manifest));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CardAnimationConverter.ConvertAsync(ffmpeg, source, directory, "10001", cancellationToken: cancellation.Token));
        Assert.Equal(original, File.ReadAllBytes(manifest));
        Assert.Empty(Directory.GetDirectories(directory, ".animation-*"));
    }

    [FfmpegFact]
    public async Task SelectedCropIsAppliedToEveryFrameAndOldBindingCanBeReplaced()
    {
        var ffmpeg = CardAnimationConverter.FindFfmpeg()!;
        var manifest = CreatePackage("old");
        var directory = Path.GetDirectoryName(manifest)!;
        var source = Path.Combine(_root, "colors.mp4");
        await Generate(ffmpeg, source, "1", "color=c=blue:size=128x64:rate=15,drawbox=x=0:y=0:w=64:h=64:color=red:t=fill");
        var poster = Path.Combine(_root, "poster.png");
        await CardAnimationConverter.ExtractPreviewAsync(ffmpeg, source, poster, CancellationToken.None);
        Assert.Equal((128, 64), CardAnimationConverter.ReadPngSize(poster));
        await CardAnimationConverter.ConvertAsync(ffmpeg, source, directory, "old", crop: new CardAnimationCrop(0, 0, 60, 59, 128, 64));
        var package = CardAnimationPackage.Read(manifest);
        Assert.Equal((404, 400), (package.Width, package.Height));
        foreach (var index in new[] { 0, package.FrameCount - 1 })
        {
            var image = SkinCropImage.Load(Path.Combine(directory, "old.frames", index.ToString("D6") + ".png"));
            var bitmap = new System.Windows.Media.Imaging.FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4]; bitmap.CopyPixels(new System.Windows.Int32Rect(100, 100, 1, 1), pixel, 4, 0);
            Assert.True(pixel[2] > 200 && pixel[0] < 30, "The selected left red region must be used for each frame.");
        }
    }

    private static async Task Generate(string ffmpeg, string output, string seconds, string filter = "testsrc2=size=64x96:rate=15")
    {
        var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", filter, "-t", seconds, output }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await errors);
    }
}
