using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AstralParty.Toys.Services;

/// <summary>
/// WPF 覆盖层配置（overlay_config.json），与 Mod 的 config.json 是两套：
/// OverlayChat：scale/fontSize/opacity/toggleKey/offsetX/Y/panelWidth，
/// Toys 里的新页只改这份文件，游戏内拖动也写回同一份文件。
/// </summary>
public sealed class OverlayConfigService
{
    private readonly string? _overlayConfigPath;
    private readonly ModManager _modManager;

    public sealed record OverlaySettings(
        bool Enabled,
        double Scale,
        int FontSize,
        double Opacity,
        string ToggleKey,
        int ToggleVk,
        double OffsetX,
        double OffsetY,
        double PanelWidth
    );

    public OverlayConfigService(ModManager modManager)
    {
        _modManager = modManager;
        string? gameDir = modManager.GetStatus().GameDirectory;
        if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir))
        {
            _overlayConfigPath = Path.Combine(gameDir, ModManager.LoaderFolderName, "overlay", "overlay_config.json");
        }
    }

    public OverlaySettings Load()
    {
        var def = new OverlaySettings(true, 1.0, 14, 0.85, "Backslash", 0xDC, 20, 70, 340);
        if (_overlayConfigPath == null || !File.Exists(_overlayConfigPath)) return def;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_overlayConfigPath));
            var r = doc.RootElement;
            bool enabled = r.TryGetProperty("enabled", out var e) ? e.GetBoolean() : true;
            double scale = r.TryGetProperty("scale", out var sc) && sc.TryGetDouble(out double s) ? s : 1.0;
            int font = r.TryGetProperty("fontSize", out var f) && f.TryGetInt32(out int fs) ? fs : 14;
            double op = r.TryGetProperty("opacity", out var o) && o.TryGetDouble(out double ov) ? ov : 0.85;
            string tk = r.TryGetProperty("toggleKey", out var t) ? t.GetString() ?? "Backslash" : "Backslash";
            int vk = r.TryGetProperty("toggleVk", out var v) && v.TryGetInt32(out int vv) ? vv : 0xDC;
            double ox = r.TryGetProperty("offsetX", out var oxe) && oxe.TryGetDouble(out double oxv) ? oxv : 20;
            double oy = r.TryGetProperty("offsetY", out var oye) && oye.TryGetDouble(out double oyv) ? oyv : 70;
            double pw = r.TryGetProperty("panelWidth", out var pe) && pe.TryGetDouble(out double pwv) ? pwv : 340;
            // toggleKey 优先派生 VK
            if (!string.IsNullOrEmpty(tk))
            {
                vk = tk.Trim().ToLowerInvariant() switch
                {
                    "backslash" or "oem5" or "\\" => 0xDC,
                    "insert" => 0x2D,
                    "delete" => 0x2E,
                    _ => vk
                };
                tk = vk == 0xDC ? "Backslash" : vk == 0x2D ? "Insert" : vk == 0x2E ? "Delete" : tk;
            }
            return new OverlaySettings(enabled, Math.Clamp(scale, 0.5, 2), Math.Clamp(font, 10, 36), Math.Clamp(op, 0.2, 1), tk, vk, ox, oy, Math.Clamp(pw, 200, 800));
        }
        catch { return def; }
    }

    public void Save(OverlaySettings s)
    {
        if (_overlayConfigPath == null) throw new InvalidOperationException("未选择游戏目录");
        Directory.CreateDirectory(Path.GetDirectoryName(_overlayConfigPath)!);
        var obj = new JsonObject
        {
            ["enabled"] = s.Enabled,
            ["scale"] = s.Scale,
            ["fontSize"] = s.FontSize,
            ["opacity"] = s.Opacity,
            ["toggleKey"] = s.ToggleKey,
            ["toggleVk"] = s.ToggleVk,
            ["offsetX"] = s.OffsetX,
            ["offsetY"] = s.OffsetY,
            ["panelWidth"] = s.PanelWidth
        };
        string tmp = _overlayConfigPath + ".tmp";
        File.WriteAllText(tmp, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _overlayConfigPath, true);
    }

    public string? ConfigPath => _overlayConfigPath;
}
