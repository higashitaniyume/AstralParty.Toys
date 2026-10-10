using AstralParty.Toys;
using AstralParty.Toys.Services;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class SkinCropTests
{
    [Fact]
    public void CropSquare_SavesSelectedPixelsAt512WithoutChangingSource()
    {
        Exception? failure = null;
        var thread = new Thread(() => {
            var root = Path.Combine(Path.GetTempPath(), "SkinCropTests_" + Guid.NewGuid().ToString("N"));
            try {
                var bytes = new byte[8 * 4 * 4];
                for (var y = 0; y < 4; y++) for (var x = 0; x < 8; x++) {
                    var i = (y * 8 + x) * 4;
                    bytes[i + (x < 4 ? 2 : 0)] = 255; bytes[i + 3] = 255;
                }
                var source = BitmapSource.Create(8, 4, 96, 96, PixelFormats.Bgra32, null, bytes, 32);
                var cropped = SkinCropImage.RenderSquare(source, new Int32Rect(4, 0, 4, 4));
                Assert.Equal(512, cropped.PixelWidth); Assert.Equal(512, cropped.PixelHeight);
                var manager = new SkinManager();
                var result = manager.SaveCroppedCard(root, "custom", "UT_HandCard_10001.png", cropped);
                var saved = new BitmapImage(); saved.BeginInit(); saved.CacheOption = BitmapCacheOption.OnLoad;
                saved.UriSource = new Uri(Path.Combine(manager.GetSkinsDirectory(root), "custom", result.FileName)); saved.EndInit();
                Assert.Equal(512, saved.PixelWidth); Assert.Equal(512, saved.PixelHeight);
                var pixel = new byte[4]; saved.CopyPixels(new Int32Rect(256, 256, 1, 1), pixel, 4, 0);
                Assert.Equal(255, pixel[0]); Assert.Equal(0, pixel[2]);
                Assert.Equal(8, source.PixelWidth);
                Assert.Throws<ArgumentException>(() => SkinCropImage.RenderSquare(source, new Int32Rect(0, 0, 4, 3)));
                Assert.Throws<InvalidOperationException>(() => manager.SaveCroppedCard(root, SkinManager.GameDefault, result.FileName, cropped));
            }
            catch (Exception ex) { failure = ex; }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
