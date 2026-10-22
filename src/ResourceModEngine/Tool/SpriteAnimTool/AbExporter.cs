using System;

namespace ResourceModLoader.Tool.SpriteAnimTool
{
    public static class AbExporter
    {
        public static void Run(string bundlePath, string exportRoot, string classdbPath)
        {
            Log.Info($"[AbExporter] 开始导出 {bundlePath}");

            if (!UnityPyBridge.TryExport(bundlePath, exportRoot, out var unityPyMessage))
            {
                Log.Error($"[AbExporter] UnityPy 导出失败: {unityPyMessage}");
                throw new InvalidOperationException(unityPyMessage);
            }
            Log.SuccessAll($"[AbExporter] UnityPy 导出完成到: {exportRoot}");
        }
    }
}
