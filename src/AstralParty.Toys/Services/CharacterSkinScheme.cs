#nullable enable
using System;
using System.Collections.Generic;

namespace CesiumLoader.CharacterSkins
{
    public sealed class CharacterSkinScheme
    {
        public int SchemaVersion { get; set; } = 1;
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public List<CharacterSkinBinding> Rules { get; set; } = new List<CharacterSkinBinding>();
    }
    public sealed class CharacterSkinBinding
    {
        public string Id { get; set; } = "";
        public int HeroId { get; set; }
        public int TargetSkinItemId { get; set; }
        public int OfficialSourceSkinItemId { get; set; }
        public string CustomPackageId { get; set; } = "";
    }
    public static class CharacterSkinSchemeRules
    {
        public static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 80) throw new InvalidOperationException("标识必须是 1～80 个字母、数字、下划线或短横线");
            foreach (char c in id)
                if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new InvalidOperationException("标识必须是 1～80 个字母、数字、下划线或短横线");
        }
        public static void Validate(CharacterSkinScheme scheme)
        {
            if (scheme == null || scheme.SchemaVersion != 1) throw new InvalidOperationException("不支持的皮肤方案版本");
            ValidateId(scheme.Id);
            if (scheme.Name == null || scheme.Author == null || scheme.Description == null || scheme.Rules == null) throw new InvalidOperationException("方案存在空字段");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targets = new HashSet<string>();
            foreach (var rule in scheme.Rules)
            {
                if (rule == null) throw new InvalidOperationException("规则为空");
                ValidateId(rule.Id);
                if (!ids.Add(rule.Id)) throw new InvalidOperationException("规则标识重复: " + rule.Id);
                if (rule.HeroId <= 0 || rule.TargetSkinItemId < 0 || rule.OfficialSourceSkinItemId < 0 || rule.CustomPackageId == null) throw new InvalidOperationException("规则角色或皮肤 ID 无效");
                if (rule.CustomPackageId.Length > 0) ValidateId(rule.CustomPackageId);
                if (rule.OfficialSourceSkinItemId == 0 && rule.CustomPackageId.Length == 0) throw new InvalidOperationException("规则至少需要一种资源来源");
                if (!targets.Add(rule.HeroId + ":" + rule.TargetSkinItemId)) throw new InvalidOperationException("相同角色和目标皮肤存在重复规则");
            }
        }
        // Hero text/map slots and asset: addresses cannot differ between skin targets.
        public static void ValidateSharedOverrides(CharacterSkinScheme scheme, IDictionary<string, CharacterSkinManifest> packages)
        {
            Validate(scheme);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var rule in scheme.Rules)
            {
                if (rule.CustomPackageId.Length == 0) continue;
                if (!packages.TryGetValue(rule.CustomPackageId, out var package)) throw new InvalidOperationException("方案缺少自定义包: " + rule.CustomPackageId);
                if (package.HeroId != rule.HeroId) throw new InvalidOperationException("自定义包与规则角色不匹配");
                foreach (var image in package.Images)
                {
                    string? key = image.Key == "CharacterMap" ? "hero:" + rule.HeroId + ":CharacterMap" : image.Key.StartsWith("asset:", StringComparison.Ordinal) ? image.Key : null;
                    if (key == null) continue;
                    string value = rule.CustomPackageId.ToLowerInvariant() + "/" + image.Value.Replace('\\', '/').ToLowerInvariant();
                    if (values.TryGetValue(key, out var old) && old != value) throw new InvalidOperationException("方案共享图片冲突: " + key);
                    values[key] = value;
                }
                foreach (var text in package.Texts)
                {
                    if (text.Key == "SkinLong" || text.Key == "SkinShort") continue;
                    string key = "hero:" + rule.HeroId + ":" + text.Key;
                    if (values.TryGetValue(key, out var old) && old != text.Value) throw new InvalidOperationException("方案共享文案冲突: " + key);
                    values[key] = text.Value;
                }
            }
        }
        // Call Validate before resolving an untrusted scheme. Exact rules take priority over wildcard rules.
        public static CharacterSkinBinding? Resolve(CharacterSkinScheme scheme, int heroId, int skinItemId)
        {
            CharacterSkinBinding? wildcard = null;
            foreach (var rule in scheme.Rules)
            {
                if (rule.HeroId != heroId) continue;
                if (rule.TargetSkinItemId == skinItemId) return rule;
                if (rule.TargetSkinItemId == 0) wildcard = rule;
            }
            return wildcard;
        }
    }
}
