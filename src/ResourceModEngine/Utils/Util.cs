using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Utils
{
    internal class Util
    {
        public static int versionCompare(string a, string b)
        {
            string[] curVer = a.Split(".");
            string[] installedVer = b.Split(".");
            int result = 0;
            for (int i = 0; i < curVer.Length + 1 && i < installedVer.Length + 1; i++)
            {
                if (i == curVer.Length || i == installedVer.Length) break;
                if (int.Parse(curVer[i]) == int.Parse(installedVer[i])) continue;
                if (int.Parse(curVer[i]) < int.Parse(installedVer[i]))
                    result = -1;
                else
                    result = 1;
                break;
            }
            return result;
        }
        public static int TailEqualLen(string s1, string s2)
        {
            for (int i = 0; i < s1.Length && i < s2.Length; i++)
            {
                if (s1.ElementAt(s1.Length - i - 1) != s2.ElementAt(s2.Length - i - 1))
                    return i;
            }
            if (s1.Length > s2.Length)
                return s2.Length;
            return s1.Length;
        }
        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
