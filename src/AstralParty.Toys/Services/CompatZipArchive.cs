using System.IO.Compression;
using System.Text;

namespace AstralParty.Toys.Services;

/// <summary>
/// 打开玩家提供的 ZIP。部分压缩工具在中文 Windows 上按 GBK 写入条目名且不设置 UTF-8 标志位，
/// .NET 默认把这类名字当作 UTF-8 解码，中文会变成 U+FFFD 乱码；这里先按严格 UTF-8 解码，
/// 遇到非法字节再按 GBK（936）重开一次，保证解压出来的文件名与压缩包内一致。
/// </summary>
internal static class CompatZipArchive
{
    internal static ZipArchive OpenRead(string path)
    {
        // 打开阶段失败（文件不存在、不是 ZIP 等）与编码无关，直接交给调用方。
        ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Read, new UTF8Encoding(false, true));
        try
        {
            _ = archive.Entries.Count; // 条目名在读中央目录时解码，提前触发以捕获编码错误。
            return archive;
        }
        catch (DecoderFallbackException)
        {
            archive.Dispose();
        }
        catch
        {
            archive.Dispose();
            throw;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        archive = ZipFile.Open(path, ZipArchiveMode.Read,
            Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
        try
        {
            _ = archive.Entries.Count;
            return archive;
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }
}
