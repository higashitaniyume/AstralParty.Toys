// 第三方 RML 代码未按可空引用标注，单独关闭该文件的分析。
#nullable disable
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Module
{
    class Zip
    {
        // 名称解码规则变化后要让旧缓存失效：__hash 里带上版本前缀。
        private const string CacheVersion = "v2:";

        public static string ExtractAndGetPath(string path)
        {
            string tmpPath = Path.Combine(Path.GetDirectoryName(path), "_generated");
            if (!Path.Exists(tmpPath))
                Directory.CreateDirectory(tmpPath);
            string extractTargetPath = Path.Combine(tmpPath, Path.GetFileNameWithoutExtension(path));
            string hashPath = Path.Combine(extractTargetPath, "__hash");
            string incomingHash = CacheVersion + Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path)));

            if (Path.Exists(hashPath) && File.ReadAllText(hashPath) == incomingHash)
                return extractTargetPath;

            try
            {
                // 包内容或解码规则变了：清掉上次解压结果，避免旧文件名残留。
                if (Directory.Exists(extractTargetPath))
                    Directory.Delete(extractTargetPath, true);
                Directory.CreateDirectory(extractTargetPath);
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = OpenArchive(fs))
                {
                    archive.ExtractToDirectory(extractTargetPath, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{path}解压失败{ex}");
                return "";
            }
            File.WriteAllText(hashPath, incomingHash);
            return extractTargetPath;
        }

        /// <summary>
        /// 部分压缩工具在中文 Windows 上按 GBK 写入条目名且不设置 UTF-8 标志位，
        /// 直接按 UTF-8 解码会把中文变成 U+FFFD；先严格 UTF-8，失败再按 GBK(936) 重开。
        /// </summary>
        private static ZipArchive OpenArchive(Stream stream)
        {
            ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true, new UTF8Encoding(false, true));
            try
            {
                _ = archive.Entries.Count;
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

            stream.Position = 0;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true,
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
}
