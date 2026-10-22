using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Utils
{
    using Microsoft.Win32;
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    class GameExecutableSeeker
    {
        public static (string shell, string[] args) AutoFindGameStartupShell(string gameBin)
        {
            string gameDir = Path.GetDirectoryName(gameBin);
            if (File.Exists(Path.Combine(gameDir, "AstralParty_CN_Data", "Plugins", "x86_64", "steam_api64.dll")))
            {
                var steamResult = DetectFromSteamInstall(gameBin, gameDir);
                if (steamResult.HasValue)
                    return steamResult.Value;
            }

            return (gameBin, Array.Empty<string>());
        }

        private static (string shell, string[] args)? DetectFromSteamInstall(string gameBin, string gameDir)
        {
            string normalizedGameDir = NormalizePath(gameDir);

            var result = FindSteamAppId(RegistryHive.LocalMachine, RegistryView.Registry64, normalizedGameDir);
            if (result.HasValue) return result;

            result = FindSteamAppId(RegistryHive.LocalMachine, RegistryView.Registry32, normalizedGameDir);
            return result;
        }

        private static (string shell, string[] args)? FindSteamAppId(RegistryHive hive, RegistryView view, string targetGameDir)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (uninstallKey == null)
                    return null;

                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                {
                    if (!subKeyName.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string appIdString = subKeyName.Substring("Steam App ".Length).Trim();
                    if (!int.TryParse(appIdString, out int appId))
                        continue;

                    using (RegistryKey appKey = uninstallKey.OpenSubKey(subKeyName))
                    {
                        if (appKey == null)
                            continue;

                        string installLocation = appKey.GetValue("InstallLocation") as string;
                        if (string.IsNullOrEmpty(installLocation))
                            continue;

                        string normalizedInstall = NormalizePath(installLocation);
                        if (!targetGameDir.Equals(normalizedInstall, StringComparison.OrdinalIgnoreCase) &&
                            !targetGameDir.StartsWith(normalizedInstall + "\\", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string uninstallString = appKey.GetValue("UninstallString") as string;
                        if (!string.IsNullOrEmpty(uninstallString))
                        {
                            var match = Regex.Match(uninstallString, @"^""?([^""]+?)""?\s+steam://uninstall/(\d+)");
                            if (match.Success)
                            {
                                string steamExe = match.Groups[1].Value;
                                string runUrl = $"steam://rungameid/{match.Groups[2].Value}";
                                return (steamExe, new[] { runUrl });
                            }
                        }

                        // 回退：默认 steam 路径
                        return ("steam.exe", new[] { $"steam://rungameid/{appId}" });
                    }
                }
            }

            return null;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            path = path.Trim().Trim('"').Replace('/', '\\');

            if (path.Length > 3 && path.EndsWith("\\"))
                path = path.TrimEnd('\\');

            return path;
        }
    }
}
