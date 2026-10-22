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
        public static string ExtractAndGetPath(string path)
        {
            string tmpPath = Path.Combine(Path.GetDirectoryName(path), "_generated");
            if (!Path.Exists(tmpPath))
                Directory.CreateDirectory(tmpPath);
            string extractTargetPath = Path.Combine(tmpPath, Path.GetFileNameWithoutExtension(path));
            string hashPath = Path.Combine(extractTargetPath, "__hash");
            if (!Path.Exists(extractTargetPath))
                Directory.CreateDirectory(extractTargetPath);
            string incomingHash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path)));

            if (Path.Exists(hashPath) && File.ReadAllText(hashPath) == incomingHash)
                return extractTargetPath;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    ZipFile.ExtractToDirectory(fs, extractTargetPath, Encoding.UTF8);
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
    }
}
