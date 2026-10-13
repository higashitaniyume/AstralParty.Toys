using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private CancellationTokenSource? _cardAnimationConversion;
    private sealed record PendingCardAnimation(string Token, string Game, string Skin, string File, string Source, string Executable, int Width, int Height);
    private PendingCardAnimation? _pendingCardAnimation;

    protected override void OnClosed(EventArgs e)
    {
        _webReady = false;
        _cardAnimationConversion?.Cancel();
        _pendingCardAnimation = null;
        base.OnClosed(e);
    }

    private IProgress<string> AnimationProgress() => new InlineProgress<string>(message => Post(new { type = "skinsAnimationState", payload = new { running = true, message } }));
    private void FinishAnimationOperation()
    {
        _cardAnimationConversion = null;
        Post(new { type = "skinsAnimationState", payload = new { running = false } });
    }

    private async void HandlePickCardAnimation(JsonElement root)
    {
        if (_cardAnimationConversion != null) { Post(new { type = "toast", message = "已有卡面正在处理，请等待或取消。" }); return; }
        string? poster = null;
        try
        {
            var skin = root.GetProperty("skinName").GetString() ?? "";
            var file = root.GetProperty("fileName").GetString() ?? "";
            CardAnimationConverter.ValidateBinding(skin);
            if (skin == SkinManager.GameDefault) throw new InvalidOperationException("请先选择外部皮肤方案。");
            CardAnimationConverter.ValidateBinding(file);
            var game = RequireModGameDirectory();
            var dialog = new OpenFileDialog { Title = "选择视频 / GIF，下一步裁剪普通卡面（404×400）", Filter = "视频或 GIF|*.mp4;*.webm;*.mov;*.avi;*.mkv;*.m4v;*.gif", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            using var cancellation = new CancellationTokenSource();
            _cardAnimationConversion = cancellation;
            var progress = AnimationProgress();
            progress.Report("正在读取视频首帧，准备裁剪…");
            var executable = await CardAnimationConverter.EnsureFfmpegAsync(progress, cancellation.Token);
            poster = Path.Combine(Path.GetTempPath(), "ToysCardPreview-" + Guid.NewGuid().ToString("N") + ".png");
            await CardAnimationConverter.ExtractPreviewAsync(executable, dialog.FileName, poster, cancellation.Token);
            var image = SkinCropImage.Load(poster);
            var token = Guid.NewGuid().ToString("N");
            _pendingCardAnimation = new(token, game, skin, file, dialog.FileName, executable, image.PixelWidth, image.PixelHeight);
            FinishAnimationOperation();
            Post(new { type = "skinsAnimationCropSource", payload = new { token, fileName = Path.GetFileName(dialog.FileName), width = image.PixelWidth, height = image.PixelHeight, previewUrl = SkinCropImage.Preview(image), isAnimation = true, outputWidth = 404, outputHeight = 400 } });
        }
        catch (OperationCanceledException) { Post(new { type = "toast", message = "已取消读取，原卡面保留。" }); }
        catch (Exception ex) { Post(new { type = "toast", message = "动态卡面导入失败：" + ex.Message }); }
        finally
        {
            if (poster != null && File.Exists(poster)) File.Delete(poster);
            FinishAnimationOperation();
        }
    }

    private async void HandleSaveCardAnimationCrop(JsonElement root)
    {
        var token = root.TryGetProperty("token", out var value) ? value.GetString() : null;
        PendingCardAnimation pending;
        CardAnimationCrop crop;
        try
        {
            if (_cardAnimationConversion != null) throw new InvalidOperationException("已有卡面正在转换。");
            pending = _pendingCardAnimation is { } candidate && candidate.Token == token ? candidate : throw new InvalidOperationException("裁剪已取消或过期，请重新选择视频。");
            if (!string.Equals(pending.Game, RequireModGameDirectory(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前游戏已切换，请重新选择视频。");
            crop = new(root.GetProperty("x").GetInt32(), root.GetProperty("y").GetInt32(), root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(), pending.Width, pending.Height);
            crop.Validate();
        }
        catch (Exception ex) { Post(new { type = "skinsCropSaved", payload = new { token, success = false, error = ex.Message } }); return; }
        _pendingCardAnimation = null;
        using var cancellation = new CancellationTokenSource();
        _cardAnimationConversion = cancellation;
        try
        {
            var directory = Path.Combine(_skinManager.GetSkinsDirectory(pending.Game), pending.Skin);
            await CardAnimationConverter.ConvertAsync(pending.Executable, pending.Source, directory, Path.GetFileNameWithoutExtension(pending.File), AnimationProgress(), cancellation.Token, crop);
            if (string.Equals(pending.Game, RequireModGameDirectory(), StringComparison.OrdinalIgnoreCase))
            {
                var cards = _skinManager.GetCards(pending.Game, pending.Skin);
                Post(new { type = "skinsCards", payload = new { skinName = pending.Skin, cards } });
                PushSkinProfiles();
            }
            Post(new { type = "toast", message = "动态卡面已裁剪为 404×400。选择该方案并重启游戏后生效，需要新版 CardSkinMod。" });
        }
        catch (OperationCanceledException) { Post(new { type = "toast", message = "已取消转换，原卡面保留。" }); }
        catch (Exception ex) { Post(new { type = "toast", message = "动态卡面转换失败：" + ex.Message }); }
        finally { FinishAnimationOperation(); }
    }

    private void HandleRemoveCardAnimation(JsonElement root)
    {
        try
        {
            if (_cardAnimationConversion != null) throw new InvalidOperationException("请先等待或取消当前转换。");
            var skin = root.GetProperty("skinName").GetString() ?? "";
            var file = root.GetProperty("fileName").GetString() ?? "";
            CardAnimationConverter.ValidateBinding(skin);
            CardAnimationConverter.ValidateBinding(file);
            var game = RequireModGameDirectory();
            CardAnimationPackage.Remove(Path.Combine(_skinManager.GetSkinsDirectory(game), skin), Path.GetFileNameWithoutExtension(file));
            Post(new { type = "skinsCards", payload = new { skinName = skin, cards = _skinManager.GetCards(game, skin) } });
            PushSkinProfiles();
            Post(new { type = "toast", message = "动态覆盖已移除，重启游戏后使用原有静态卡面或游戏原图。" });
        }
        catch (Exception ex) { Post(new { type = "toast", message = "移除动态卡面失败：" + ex.Message }); }
    }
}
