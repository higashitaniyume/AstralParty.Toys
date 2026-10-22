using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ResourceModLoader.Tool.SpriteAnimTool
{
    internal static class EmbeddedUnityPyAssets
    {
        private static readonly object SyncRoot = new();
        private static readonly IReadOnlyList<(string ResourceName, string RelativePath)> ResourceMap = new[]
        {
            ("ResourceModLoader.Resources.UnityPy.sprite_ab_io_mesh.py", "sprite_ab_io_mesh.py"),
            ("ResourceModLoader.Resources.UnityPy.libs.AssetsTools.NET.dll", Path.Combine("libs", "AssetsTools.NET.dll")),
            ("ResourceModLoader.Resources.UnityPy.libs.AssetsTools.NET.Cpp2IL.dll", Path.Combine("libs", "AssetsTools.NET.Cpp2IL.dll")),
            ("ResourceModLoader.Resources.UnityPy.libs.classdata.tpk", Path.Combine("libs", "classdata.tpk")),
        };

        private static string? preparedRoot;

        public static bool TryPrepare(out string scriptPath, out string message)
        {
            scriptPath = string.Empty;
            message = string.Empty;

            try
            {
                lock (SyncRoot)
                {
                    preparedRoot ??= PrepareAssetsCore();
                    scriptPath = Path.Combine(preparedRoot, "sprite_ab_io_mesh.py");
                }

                if (!File.Exists(scriptPath))
                {
                    message = "UnityPy 脚本资源准备失败";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                message = $"准备 UnityPy 内嵌资源失败: {ex.Message}";
                return false;
            }
        }

        private static string PrepareAssetsCore()
        {
            Assembly assembly = typeof(EmbeddedUnityPyAssets).Assembly;
            string root = Path.Combine(
                Path.GetTempPath(),
                "ResourceModLoader",
                "unitypy",
                assembly.ManifestModule.ModuleVersionId.ToString("N"));

            Directory.CreateDirectory(root);

            foreach (var (resourceName, relativePath) in ResourceMap)
            {
                string outputPath = Path.Combine(root, relativePath);
                string? parent = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                using Stream resourceStream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new FileNotFoundException($"未找到程序集资源: {resourceName}");
                using FileStream fileStream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                resourceStream.CopyTo(fileStream);
            }

            return root;
        }
    }
}