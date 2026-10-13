using System.IO;
using AstralParty.Toys.Services;
using Microsoft.Win32;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private void HandleLegacyCardSkinImport()
    {
        try
        {
            string game = RequireModGameDirectory();
            var dialog = new OpenFileDialog
            {
                Title = "导入旧式卡牌皮肤包（自动转换 bundle）",
                Filter = "旧式卡牌皮肤包 (*.zip)|*.zip",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;
            string zipPath = dialog.FileName;
            EnsureCharacterExports();
            _characterExports.Enqueue(game, "legacy-card-import:" + Guid.NewGuid().ToString("N"),
                Path.GetFileNameWithoutExtension(zipPath) + " · 卡面转换导入", async (progress, cancellation) =>
                {
                    var result = await LegacyCardSkinImporter.ImportAsync(game, zipPath, progress, cancellation);
                    string message = $"已转换并导入 {result.Images} 张卡面。预览后点击“设为当前”启用。"
                        + (result.Normalizations.Length > 0 ? $"已处理 {result.Normalizations.Length} 个资源包的重复尾部数据，原文件已保留。" : "");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (game != RequireModGameDirectory()) return;
                        Post(new { type = "skinsLegacyImported", payload = new { name = result.Name } });
                        PushSkinProfiles();
                        Post(new { type = "toast", message });
                    });
                    return message;
                });
            Post(new { type = "toast", message = "已加入后台卡面转换队列，右下角可查看进度或终止。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "导入旧式卡牌皮肤包失败：" + ex.Message });
        }
    }
}
