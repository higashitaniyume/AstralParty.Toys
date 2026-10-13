using System.IO;
using AstralParty.Toys.Services;
using Microsoft.Win32;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private void ImportLegacyCharacterSkin(string game)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入旧式角色皮肤包（自动转换 bundle）",
            Filter = "旧式皮肤包 (*.zip)|*.zip",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        EnsureCharacterExports();
        string zipPath = dialog.FileName;
        string taskKey = "legacy-import:" + Guid.NewGuid().ToString("N");
        _characterExports.Enqueue(game, taskKey, Path.GetFileNameWithoutExtension(zipPath) + " · 转换导入", async (progress, cancellation) =>
        {
            var result = await LegacyCharacterSkinImporter.ImportAsync(game, zipPath,
                Path.Combine(_appDirectory, "GameData"), Path.Combine(_appDirectory, "Protocol"), progress, cancellation);
            string message = $"已转换并导入 {result.Images} 张图片"
                + (result.Animations > 0 ? $"、{result.Animations} 个动作（{result.Sprites} 张精灵帧）" : "")
                + "。可将此皮肤包添加到人物方案。"
                + (result.PreservedMedia > 0 ? $"已保留 {result.PreservedMedia} 个视频源资源，视频演出仍使用原版。" : "")
                + (result.Normalizations.Length > 0 ? $"已处理 {result.Normalizations.Length} 个资源包的重复尾部数据，原文件已保留。" : "");
            await Dispatcher.InvokeAsync(() =>
            {
                // A background task can finish after the player switches games.
                if (game == RequireModGameDirectory())
                    Post(new { type = "characterTemplateState", payload = new { success = true, id = result.Id, message } });
            });
            return message;
        });
        Post(new { type = "toast", message = "已加入后台转换队列，右下角可查看进度或终止。" });
    }
}
