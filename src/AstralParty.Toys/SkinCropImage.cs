using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstralParty.Toys;

public static class SkinCropImage
{
    public static BitmapSource Load(string path)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(System.IO.Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    public static string Preview(BitmapSource source)
    {
        var scale = Math.Min(1d, 1600d / Math.Max(source.PixelWidth, source.PixelHeight));
        BitmapSource preview = scale < 1 ? new TransformedBitmap(source, new ScaleTransform(scale, scale)) : source;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(preview));
        using var stream = new System.IO.MemoryStream(); encoder.Save(stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
    public static BitmapSource RenderRegion(BitmapSource source, Int32Rect region, int width, int height, bool contain)
    {
        if (width < 1 || height < 1 || width > 4096 || height > 4096 || region.Width < 1 || region.Height < 1 || region.X < 0 || region.Y < 0 || region.Width > source.PixelWidth-region.X || region.Height > source.PixelHeight-region.Y)
            throw new ArgumentException("裁剪范围或输出尺寸无效");
        var crop = new CroppedBitmap(source,region);
        double scale = contain ? Math.Min((double)width/region.Width,(double)height/region.Height) : 0;
        var rect = contain ? new Rect((width-region.Width*scale)/2,(height-region.Height*scale)/2,region.Width*scale,region.Height*scale) : new Rect(0,0,width,height);
        var visual=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var drawing=visual.RenderOpen())drawing.DrawImage(crop,rect);
        var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
    }
    public static BitmapSource RenderSquare(BitmapSource source, Int32Rect region)
    {
        if (region.Width <= 0 || region.Width != region.Height || region.X < 0 || region.Y < 0
            || region.Width > source.PixelWidth - region.X || region.Height > source.PixelHeight - region.Y)
            throw new ArgumentException("裁剪区域必须是图片范围内的正方形");
        var crop = new CroppedBitmap(source, region);
        var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen()) drawing.DrawImage(crop, new Rect(0, 0, 512, 512));
        var result = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }
}
