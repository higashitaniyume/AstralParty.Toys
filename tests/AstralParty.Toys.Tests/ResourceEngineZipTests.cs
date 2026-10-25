using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace AstralParty.Toys.Tests;

/// <summary>
/// RML 引擎内部（ResourceModEngine）的嵌套 ZIP 解压：Mod 目录里再放 ZIP 时也要保留中文件名。
/// 引擎的 Zip 类不公开，这里按类型名反射调用。
/// </summary>
public sealed class ResourceEngineZipTests
{
    [Fact]
    public void NestedTreeZipKeepsGbkEntryNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "toys-engine-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = Path.Combine(root, "嵌套模组.zip");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var gbk = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            byte[] content = { 1, 2, 3 };
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create, gbk))
            {
                using (var stream = archive.CreateEntry("预览/效果图.png").Open()) stream.Write(content);
                using (var stream = archive.CreateEntry("皮肤/替换.bundle").Open()) stream.Write(content);
            }

            var type = Type.GetType("ResourceModLoader.Module.Zip, AstralParty.Toys.ResourceEngine", throwOnError: true)!;
            var method = type.GetMethod("ExtractAndGetPath", BindingFlags.Public | BindingFlags.Static)!;
            var extracted = (string)method.Invoke(null, new object[] { zip })!;

            Assert.False(string.IsNullOrEmpty(extracted));
            Assert.True(File.Exists(Path.Combine(extracted, "预览", "效果图.png")));
            Assert.True(File.Exists(Path.Combine(extracted, "皮肤", "替换.bundle")));
            Assert.DoesNotContain(
                Directory.EnumerateFileSystemEntries(extracted, "*", SearchOption.AllDirectories),
                path => path.Contains('\uFFFD'));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
