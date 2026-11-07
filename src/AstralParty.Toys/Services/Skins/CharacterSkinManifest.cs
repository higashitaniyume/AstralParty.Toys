using System;
using System.Collections.Generic;
using System.IO;

namespace CesiumLoader.CharacterSkins
{
    public sealed class CharacterSkinManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public int HeroId { get; set; }
        public int SkinItemId { get; set; }
        public Dictionary<string, string> Images { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Texts { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, CharacterAnimation> Animations { get; set; } = new Dictionary<string, CharacterAnimation>();
        public Dictionary<string, string> SpriteMappings { get; set; } = new Dictionary<string, string>();
        public bool UseOriginalSpriteLayout { get; set; }
        public CharacterSkinLayout Layout { get; set; } = new CharacterSkinLayout();
        public Dictionary<string, string> Media { get; set; } = new Dictionary<string, string>();
    }
    public sealed class CharacterSkinLayout
    {
        // Sprite.Create uses normalized pivot measured from bottom-left.
        public float PivotX { get; set; } = 0.5f;
        public float PivotY { get; set; } = 0.1f;
        public float PixelsPerUnit { get; set; } = 100f;
        public float Scale { get; set; } = 1f;
        public float HudScale { get; set; } = 1f;
        public float HudOffsetX { get; set; }
        public float HudOffsetY { get; set; }
    }
    public sealed class CharacterAnimation
    {
        public List<string> Frames { get; set; } = new List<string>();
        public List<int> DurationsMs { get; set; } = new List<int>();
        public float Fps { get; set; } = 15f;
        public bool Loop { get; set; } = true;
    }
    public sealed class CharacterSkinSettings
    {
        public bool Enabled { get; set; } = true;
        public string ActiveSchemeId { get; set; } = "";
        public List<string> ActivePackages { get; set; } = new List<string>();
    }
    public static class CharacterSkinRules
    {
        public static readonly string[] ImageSlots = { "Character", "SfwCharacter", "ProfilePhoto", "CharacterMap", "InGameCharacter", "InGameCharacterSFW", "RolePhoto", "SfwRolePhoto", "CharacterReady", "SfwCharacterReady", "CharacterThin", "SfwCharacterThin", "LandIcon", "Bust0", "Bust1", "SfwBust0", "SfwBust1" };
        public static readonly string[] TextSlots = { "Name", "Nick", "Biography", "Lines", "SkinLong", "SkinShort" };
        public static string SafePath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0) throw new Exception("资源路径必须位于皮肤包内");
            string normalized = relative.Replace('\\', '/');
            foreach (var part in normalized.Split('/'))
                if (part == ".." || part == "." || part.Length == 0) throw new Exception("资源路径包含无效目录");
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new Exception("资源路径越界");
            return path;
        }
        public static void Validate(CharacterSkinManifest m, string root, bool requireFiles)
        {
            if (m == null || m.SchemaVersion != 1) throw new Exception("不支持的角色皮肤格式版本");
            if (m.HeroId <= 0 || m.SkinItemId < 0) throw new Exception("角色或原皮肤 ID 无效");
            if (m.Images == null || m.Texts == null || m.Animations == null || m.SpriteMappings == null || m.Layout == null || m.Media == null) throw new Exception("皮肤清单存在空字段");
            var l = m.Layout;
            if (!Finite(l.PivotX) || !Finite(l.PivotY) || l.PivotX < 0 || l.PivotX > 1 || l.PivotY < 0 || l.PivotY > 1 || !Finite(l.PixelsPerUnit) || l.PixelsPerUnit < 1 || l.PixelsPerUnit > 10000 || !Finite(l.Scale) || l.Scale < 0.01f || l.Scale > 20 || !Finite(l.HudScale) || l.HudScale < 0.01f || l.HudScale > 20 || !Finite(l.HudOffsetX) || !Finite(l.HudOffsetY)) throw new Exception("锚点、缩放或 PPU 超出允许范围");
            foreach (var pair in m.Images)
            {
                if (Array.IndexOf(ImageSlots, pair.Key) < 0 && !pair.Key.StartsWith("asset:", StringComparison.Ordinal)) throw new Exception("未知图片槽位: " + pair.Key);
                CheckImage(root, pair.Value, requireFiles);
            }
            foreach (var pair in m.Texts) if (Array.IndexOf(TextSlots, pair.Key) < 0 || pair.Value == null || pair.Value.Length > 32000) throw new Exception("文案槽位或长度无效: " + pair.Key);
            int totalFrames = 0;
            foreach (var pair in m.Animations)
            {
                var a = pair.Value;
                if (string.IsNullOrWhiteSpace(pair.Key) || a == null || a.Frames == null || a.DurationsMs == null || a.Frames.Count == 0 || a.Frames.Count > 2000 || !Finite(a.Fps) || a.Fps < 1 || a.Fps > 120) throw new Exception("动作帧或帧率无效: " + pair.Key);
                if (a.DurationsMs.Count != 0 && a.DurationsMs.Count != a.Frames.Count) throw new Exception("每帧时长数量与帧数不一致");
                foreach (int duration in a.DurationsMs) if (duration < 1 || duration > 60000) throw new Exception("帧时长无效");
                foreach (string frame in a.Frames) CheckImage(root, frame, requireFiles);
                totalFrames += a.Frames.Count;
            }
            if (totalFrames > 10000) throw new Exception("皮肤动画总帧数超过 10000");
            foreach (var pair in m.SpriteMappings) CheckImage(root, pair.Value, requireFiles);
            foreach (var pair in m.Media) { string path = SafePath(root, pair.Value); if (requireFiles && !File.Exists(path)) throw new Exception("媒体文件不存在: " + pair.Value); }
        }
        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        private static void CheckImage(string root, string relative, bool requireFiles)
        {
            string path = SafePath(root, relative);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") throw new Exception("图片必须是 PNG/JPG，GIF 请先转换为帧序列");
            if (requireFiles && !File.Exists(path)) throw new Exception("图片不存在: " + relative);
        }
    }
}
