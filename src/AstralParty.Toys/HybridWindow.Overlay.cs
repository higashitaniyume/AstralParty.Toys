using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private OverlayConfigService? _overlayService;

    private OverlayConfigService OverlayService
    {
        get
        {
            _overlayService ??= new OverlayConfigService(_modManager);
            return _overlayService;
        }
    }

    private void PushOverlayStatus()
    {
        try
        {
            var settings = OverlayService.Load();
            var status = _modManager.GetStatus();
            // 对应使用的 Mod：OverlayFeedMod 主 + ShopRelicPeekMod 次
            var mods = new List<object>();
            foreach (var m in status.Mods)
            {
                if (m.FileName.Contains("OverlayFeed", StringComparison.OrdinalIgnoreCase) ||
                    m.FileName.Contains("ShopRelicPeek", StringComparison.OrdinalIgnoreCase))
                {
                    mods.Add(new { name = m.FileName, enabled = m.Enabled });
                }
            }
            // 若列表空，至少告诉前端接口
            if (mods.Count == 0)
            {
                mods.Add(new { name = "OverlayFeedMod.dll", enabled = false });
                mods.Add(new { name = "ShopRelicPeekMod.dll", enabled = false });
            }
            Post(new
            {
                type = "overlayStatus",
                payload = new
                {
                    config = settings,
                    configPath = OverlayService.ConfigPath,
                    mods,
                    gameDirectory = status.GameDirectory,
                    gameRunning = status.GameRunning
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "读取覆盖层配置失败：" + ex.Message });
        }
    }

    private void HandleOverlaySave(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var r = doc.RootElement;
            bool enabled = r.TryGetProperty("enabled", out var e) ? e.GetBoolean() : true;
            double scale = r.TryGetProperty("scale", out var sc) && sc.TryGetDouble(out double s) ? s : 1.0;
            int font = r.TryGetProperty("fontSize", out var f) && f.TryGetInt32(out int fs) ? fs : 14;
            double op = r.TryGetProperty("opacity", out var o) && o.TryGetDouble(out double ov) ? ov : 0.85;
            string tk = r.TryGetProperty("toggleKey", out var t) ? t.GetString() ?? "Backslash" : "Backslash";
            double ox = r.TryGetProperty("offsetX", out var oxe) && oxe.TryGetDouble(out double oxv) ? oxv : 20;
            double oy = r.TryGetProperty("offsetY", out var oye) && oye.TryGetDouble(out double oyv) ? oyv : 70;
            double pw = r.TryGetProperty("panelWidth", out var pe) && pe.TryGetDouble(out double pwv) ? pwv : 340;
            // toggleVk 由 toggleKey 派生，后端不信任前端传的 Vk
            int vk = tk.Trim().ToLowerInvariant() switch
            {
                "backslash" or "oem5" or "\\" => 0xDC,
                "insert" => 0x2D,
                "delete" => 0x2E,
                _ => 0xDC
            };
            tk = vk == 0xDC ? "Backslash" : vk == 0x2D ? "Insert" : vk == 0x2E ? "Delete" : tk;
            var settings = new OverlayConfigService.OverlaySettings(enabled, Math.Clamp(scale, 0.5, 2), Math.Clamp(font, 10, 36), Math.Clamp(op, 0.2, 1), tk, vk, ox, oy, Math.Clamp(pw, 200, 800));
            OverlayService.Save(settings);
            Post(new { type = "toast", message = "覆盖层配置已保存" });
            PushOverlayStatus();
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = "保存覆盖层配置失败：" + ex.Message });
        }
    }
}
