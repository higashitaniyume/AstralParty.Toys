using System;
using System.IO;
using System.Threading;
using ResourceModLoader.Module;

namespace ResourceModLoader;

/// <summary>
/// 把 RML 的 Mod 引擎以库的形式交给 Toys 调用；不执行 RML 的自安装、快捷方式与交互等待。
/// </summary>
public static class RmlEngine
{
    public static string Version => Program.VERSION;

    public static (int Errors, int Warnings, string[] Messages) LastReport { get; private set; }

    public static void Apply(string gameDirectory, string modsDirectory, Action<string>? log = null)
    {
        if (!Directory.Exists(gameDirectory))
            throw new InvalidOperationException("游戏目录不存在：" + gameDirectory);
        if (!Directory.Exists(modsDirectory))
            Directory.CreateDirectory(modsDirectory);

        var previousSink = Log.Sink;
        var previousHeadless = Log.Headless;
        try
        {
            Log.Sink = log;
            Log.Headless = true;

            Report.Reset();
            LastReport = (0, 0, Array.Empty<string>());
            var modder = new GameModder(gameDirectory, modsDirectory);
            if (!modder.isValid)
                throw new InvalidOperationException("未能识别游戏目录结构（缺少 *_Data / app.info）。");
            if (modder.addressableMgr.Loaded() == 0)
                throw new InvalidOperationException("未能加载 Addressables catalog，请先启动一次游戏下载资源。");

            modder.ProcessMods(resetAfterDone: false);
            LastReport = Report.GetSummary();
        }
        finally
        {
            Log.Sink = previousSink;
            Log.Headless = previousHeadless;
        }
    }

    /// <summary>以空 mods 目录运行一次，让引擎按原始 catalog 重写，达到还原效果。</summary>
    public static void Restore(string gameDirectory, Action<string>? log = null)
    {
        var empty = Path.Combine(Path.GetTempPath(), "astral-empty-mods-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try { Apply(gameDirectory, empty, log); }
        finally { try { Directory.Delete(empty, true); } catch { } }
    }
}
