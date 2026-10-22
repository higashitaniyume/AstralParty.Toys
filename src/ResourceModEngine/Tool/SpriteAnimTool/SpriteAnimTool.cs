using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Tool.SpriteAnimTool
{
    class SpriteAnimTool
    {

        public static void HandleSpriteAnimTool(string[] args)
        {
            if (args.Length == 0)
            {
                PrintSpriteAnimHelp();
                return;
            }

            try
            {
                var cmd = args[0].ToLowerInvariant();

                if (cmd == "export")
                {
                    if (!HasArg(args, "-in") && !HasArg(args, "-out") && !HasArg(args, "-class"))
                    {
                        RunSpriteAnimBatchExport();
                        return;
                    }

                    // 用法: tool sprite-anim export -in <bundle> -out <exportDir>
                    var inPath = GetArg(args, "-in");
                    var outDir = GetArg(args, "-out");

                    if (string.IsNullOrEmpty(inPath) || string.IsNullOrEmpty(outDir))
                    {
                        Log.Error("export 需要 -in -out 参数，或不带参数使用自动批处理模式");
                        PrintSpriteAnimHelp();
                        return;
                    }

                    Directory.CreateDirectory(outDir);
                    AbExporter.Run(inPath, outDir, string.Empty);
                }
                else if (cmd == "import")
                {
                    var atlasStr = GetArg(args, "-atlas");
                    int atlasSize = string.IsNullOrEmpty(atlasStr) ? 4096 : int.Parse(atlasStr);
                    bool preserveTimeline = HasArg(args, "-pt") || HasArg(args, "-preserveTimeline") || HasArg(args, "--preserve-timeline");

                    if (!HasArg(args, "-in") && !HasArg(args, "-jsonDir") && !HasArg(args, "-out") && !HasArg(args, "-class"))
                    {
                        RunSpriteAnimBatchImport(atlasSize, preserveTimeline);
                        return;
                    }

                    // 用法: tool sprite-anim import -in <bundle> -jsonDir <exportDir> -out <newBundle> [-atlas 4096]
                    var inPath = GetArg(args, "-in");
                    var jsonDir = GetArg(args, "-jsonDir");
                    var outPath = GetArg(args, "-out");

                    if (string.IsNullOrEmpty(inPath) || string.IsNullOrEmpty(jsonDir) || string.IsNullOrEmpty(outPath))
                    {
                        Log.Error("import 需要 -in -jsonDir -out 参数，或不带路径参数使用自动批处理模式");
                        PrintSpriteAnimHelp();
                        return;
                    }

                    AbImporter.Run(inPath, jsonDir, outPath, string.Empty, atlasSize, preserveTimeline);
                }
                else
                {
                    Log.Error($"未知的sprite-anim子命令: {cmd}");
                    PrintSpriteAnimHelp();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"sprite-anim工具执行失败: {ex.Message}");
                Log.Info(ex.StackTrace ?? string.Empty);
            }
        }

        static void RunSpriteAnimBatchExport()
        {
            var (importDir, exportDir) = EnsureSpriteAnimWorkingDirectories();
            var bundleFiles = EnumerateSpriteAnimBundles(importDir);
            if (bundleFiles.Length == 0)
            {
                Log.Warn($"[sprite-anim] 未在 {importDir} 找到任何 AB 包");
                return;
            }

            Log.Info($"[sprite-anim] 自动导出模式: import={importDir}, export={exportDir}");
            foreach (var bundlePath in bundleFiles)
            {
                string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
                string spriteDir = Path.Combine(exportDir, bundleName, "sprite");
                Directory.CreateDirectory(spriteDir);
                Log.Info($"[sprite-anim] 导出 {Path.GetFileName(bundlePath)} -> {spriteDir}");
                AbExporter.Run(bundlePath, spriteDir, string.Empty);
            }
        }

        static void RunSpriteAnimBatchImport(int atlasSize, bool preserveTimeline)
        {
            var (importDir, exportDir) = EnsureSpriteAnimWorkingDirectories();
            var bundleFiles = EnumerateSpriteAnimBundles(importDir);
            if (bundleFiles.Length == 0)
            {
                Log.Warn($"[sprite-anim] 未在 {importDir} 找到任何 AB 包");
                return;
            }

            Log.Info($"[sprite-anim] 自动回填模式: import={importDir}, export={exportDir}");
            foreach (var bundlePath in bundleFiles)
            {
                string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
                string jsonDir = Path.Combine(exportDir, bundleName, "sprite");
                if (!Directory.Exists(jsonDir))
                {
                    Log.Warn($"[sprite-anim] 跳过 {Path.GetFileName(bundlePath)}: 未找到 {jsonDir}");
                    continue;
                }

                if (!HasClipJson(jsonDir))
                {
                    Log.Warn($"[sprite-anim] 跳过 {Path.GetFileName(bundlePath)}: {jsonDir} 内未找到任何 clip.json");
                    continue;
                }

                string outBundle = Path.Combine(
                    importDir,
                    $"{Path.GetFileNameWithoutExtension(bundlePath)}_patched{Path.GetExtension(bundlePath)}");
                Log.Info($"[sprite-anim] 回填 {Path.GetFileName(bundlePath)} <- {jsonDir}");
                AbImporter.Run(bundlePath, jsonDir, outBundle, string.Empty, atlasSize, preserveTimeline);
            }
        }

        static (string importDir, string exportDir) EnsureSpriteAnimWorkingDirectories()
        {
            string importDir = Path.Combine(Program.Modder.basePath, "import");
            string exportDir = Path.Combine(Program.Modder.basePath, "export");
            Directory.CreateDirectory(importDir);
            Directory.CreateDirectory(exportDir);
            return (importDir, exportDir);
        }

        static string[] EnumerateSpriteAnimBundles(string inputDir)
        {
            if (!Directory.Exists(inputDir))
                return Array.Empty<string>();

            return Directory
                .GetFiles(inputDir, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSpriteAnimBundleCandidate)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static bool IsSpriteAnimBundleCandidate(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                return true;

            return extension == ".ab"
                || extension == ".bundle"
                || extension == ".assetbundle"
                || extension == ".unity3d";
        }

        static bool HasClipJson(string rootDir)
        {
            return Directory.EnumerateFiles(rootDir, "clip.json", SearchOption.AllDirectories).Any();
        }

        static bool HasArg(string[] args, string name)
        {
            return args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        static string? GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        static void PrintSpriteAnimHelp()
        {
            Log.Info("sprite-anim 工具 - AssetBundle动画处理");
            Log.Info("");
            Log.Info("自动批处理模式（基于 mods 同级目录的 import/export）:");
            Log.Info("  tool sprite-anim export");
            Log.Info("    从 <basePath>/import 扫描 AB 包，导出到 <basePath>/export/<bundleName>/sprite/");
            Log.Info("  tool sprite-anim import [-atlas 4096] [-pt]");
            Log.Info("    从 <basePath>/export/<bundleName>/sprite/ 扫描 clip.json+PNG，从 <basePath>/import 查找原 AB 包，生成 <bundleName>_patched.* 到 <basePath>/import/");
            Log.Info("");
            Log.Info("导出动画:");
            Log.Info("  tool sprite-anim export -in <bundle.ab> -out <exportDir>");
            Log.Info("");
            Log.Info("回填动画:");
            Log.Info("  tool sprite-anim import -in <bundle.ab> -jsonDir <exportDir> -out <newBundle.ab> [-atlas 4096] [-pt]");
            Log.Info("");
            Log.Info("参数说明:");
            Log.Info("  -in        输入bundle路径");
            Log.Info("  -out       输出目录或文件路径");
            Log.Info("  -jsonDir   导出目录（包含clip.json的根目录）");
            Log.Info("  -atlas     图集最大尺寸（默认4096）");
            Log.Info("  -pt        严格按导出时的原始时间轴回填；要求 PNG 数量与源动画槽位数一致，不允许增减帧");
            Log.Info("  -preserveTimeline / --preserve-timeline  与 -pt 等价，保留兼容");
        }

    }
}
