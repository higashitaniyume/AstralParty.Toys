using System;

namespace ResourceModLoader.Tool.SpriteAnimTool
{
    public static class AbImporter
    {
        public static void Run(string inBundle, string jsonRoot, string outBundle, string classdbPath, int atlasSize, bool preserveTimeline = false)
        {
            Log.Info($"[AbImporter] 开始回填: {inBundle} -> {outBundle}");

            if (!UnityPyBridge.TryImport(inBundle, jsonRoot, outBundle, preserveTimeline, out var unityPyMessage))
            {
                Log.Error($"[AbImporter] UnityPy 回填失败: {unityPyMessage}");
                throw new InvalidOperationException(unityPyMessage);
            }
            Log.SuccessAll($"[AbImporter] UnityPy 回填完成: {unityPyMessage}");
        }
    }
}
