using System.Drawing.Text;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using AstralParty.Toys.Models;

namespace AstralParty.Toys;

/// <summary>
/// 新界面「自定义 → 字体」：列出系统已安装字体，读写 FontMod 的配置。
///
/// 页面只负责挑字体；真正替换在游戏内的 FontMod 里完成。写配置走
/// <see cref="ModManager.SetFontModConfig"/>（文本定点替换 + 回滚日志），
/// 游戏的 SDK 会热重载配置，所以不需要重启游戏。
/// </summary>
public partial class HybridWindow
{
    private const string FontModId = "FontMod";
    private const string FontModDll = "FontMod.dll";

    private sealed class SystemFontVariant
    {
        public string Label { get; set; } = "";
        /// <summary>游戏侧实际传给 Font.CreateDynamicFontFromOSFont 的家族名（可能带字重后缀）。</summary>
        public string Family { get; set; } = "";
        public int Weight { get; set; } = 400;
        public string Style { get; set; } = "normal";
        /// <summary>系统是否把这个字重注册成了独立家族名；false 时游戏里选不了。</summary>
        public bool Available { get; set; } = true;
    }

    private sealed class SystemFontCard
    {
        public string Family { get; set; } = "";
        public string Display { get; set; } = "";
        public bool Cjk { get; set; }
        public List<SystemFontVariant> Variants { get; set; } = new();
    }

    private static List<SystemFontCard>? _systemFontCards;

    private bool HandleFontMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "systemFonts":
                PushSystemFonts();
                return true;
            case "setFontModFamily":
                HandleSetFontModFamily(root);
                return true;
            default:
                return false;
        }
    }

    private void PushSystemFonts()
    {
        try
        {
            var gameDirectory = RequireModGameDirectory();
            var mod = FindFontMod(gameDirectory);
            var installed = mod != null;
            var disabled = mod?.DisabledFolder == true;

            var fields = installed
                ? _modManager.ReadModConfigFields(gameDirectory, FontModDll, _libraryService.Settings.Language)
                : new List<ModConfigField>();

            var family = fields.FirstOrDefault(f => f.Name == "fontFamily")?.StringValue ?? "";
            var applyToAll = fields.FirstOrDefault(f => f.Name == "applyToAllFonts")?.BoolValue ?? true;
            var configEnabled = fields.FirstOrDefault(f => f.Name == "enabled")?.BoolValue ?? true;
            // sidecar 的 enabled=false 与「整个目录在 disabled_mods 里」都会让加载器跳过这个模组。
            var loaderEnabled = installed && mod!.Enabled && !disabled;

            Post(new
            {
                type = "systemFonts",
                payload = new
                {
                    installed,
                    loaderEnabled,
                    enabled = loaderEnabled && configEnabled,
                    disabled,
                    fontFamily = family,
                    applyToAllFonts = applyToAll,
                    fonts = GetSystemFontCards()
                }
            });
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "systemFonts",
                payload = new
                {
                    installed = false,
                    enabled = false,
                    disabled = false,
                    fontFamily = "",
                    applyToAllFonts = true,
                    error = ex.Message,
                    fonts = Array.Empty<SystemFontCard>()
                }
            });
        }
    }

    private void HandleSetFontModFamily(JsonElement root)
    {
        try
        {
            var gameDirectory = RequireModGameDirectory();
            var family = root.TryGetProperty("fontFamily", out var familyElement)
                         && familyElement.ValueKind == JsonValueKind.String
                ? familyElement.GetString() ?? ""
                : "";
            var applyToAll = !root.TryGetProperty("applyToAllFonts", out var applyElement)
                             || applyElement.ValueKind != JsonValueKind.False;

            if (FindFontMod(gameDirectory) == null)
            {
                Post(new
                {
                    type = "fontModApplied",
                    payload = new { ok = false, message = "没找到 FontMod：请先在「模组」页安装或启用加载器。" }
                });
                return;
            }

            using var op = CreateRollbackJournal().Begin(
                "fontModApply",
                string.IsNullOrEmpty(family) ? "恢复游戏默认字体" : $"设置游戏字体 {family}",
                gameDirectory,
                includeMods: true,
                includeLoader: false,
                includeProfiles: false);

            _modManager.SetFontModConfig(gameDirectory, family, applyToAll);
            op.Commit();

            var running = _modManager.GetStatus().GameRunning == true;
            Post(new
            {
                type = "fontModApplied",
                payload = new { ok = true, fontFamily = family, applyToAllFonts = applyToAll }
            });
            Post(new
            {
                type = "toast",
                message = string.IsNullOrEmpty(family)
                    ? (running ? "已恢复游戏默认字体（游戏内即时生效）" : "已恢复游戏默认字体（启动游戏后生效）")
                    : (running
                        ? $"已切换字体：{family}（游戏内即时生效）"
                        : $"已切换字体：{family}（启动游戏后生效）")
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "fontModApplied", payload = new { ok = false, message = ex.Message } });
        }
    }

    private ModEntryInfo? FindFontMod(string gameDirectory)
    {
        return _modManager.GetInstalledMods(gameDirectory).FirstOrDefault(m =>
            string.Equals(string.IsNullOrEmpty(m.Id) ? m.Name : m.Id, FontModId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 系统字体目录。第一次枚举要读每个字体的字形表（判断是否含中文），
    /// 大概一两秒；结果缓存到进程结束（运行中装字体的场景很少，重开 TOYS 即可刷新）。
    /// </summary>
    private static List<SystemFontCard> GetSystemFontCards()
    {
        var cached = _systemFontCards;
        if (cached != null) return cached;

        // GDI 家族名比 WPF 多（WPF 把字重收进同一个 family），变体能不能在游戏里选，
        // 取决于系统有没有把它注册成独立家族名。
        var gdi = GetGdiFamilyNames();
        var cards = new List<SystemFontCard>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in Fonts.SystemFontFamilies)
        {
            string source;
            try { source = family.Source; }
            catch { continue; }
            if (string.IsNullOrWhiteSpace(source) || !seen.Add(source)) continue;

            var cjk = false;
            try { cjk = SupportsChinese(family); }
            catch { }

            cards.Add(new SystemFontCard
            {
                Family = source,
                Display = DisplayName(family, source),
                Cjk = cjk,
                Variants = BuildVariants(family, source, gdi)
            });
        }

        cards.Sort((a, b) =>
        {
            if (a.Cjk != b.Cjk) return a.Cjk ? -1 : 1;
            return string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase);
        });

        _systemFontCards = cards;
        return cards;
    }

    /// <summary>
    /// 枚举一个家族在 WPF 里的字重/字形，并尽量映射成系统里可用的家族名。
    /// 只保留系统真正注册成独立名字的变体（以及家族本身就是一个字重的情况），
    /// 否则游戏侧传过去会静默回退成常规体。
    /// </summary>
    private static List<SystemFontVariant> BuildVariants(FontFamily family, string baseName, HashSet<string> gdi)
    {
        var variants = new List<SystemFontVariant>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseIsWeightFamily = EndsWithWeightWord(baseName);

        IEnumerable<Typeface> typefaces;
        try { typefaces = family.GetTypefaces(); }
        catch { typefaces = Array.Empty<Typeface>(); }

        foreach (var typeface in typefaces)
        {
            int weight;
            string style;
            try
            {
                weight = NearestWeight(typeface.Weight.ToOpenTypeWeight());
                style = typeface.Style == FontStyles.Italic || typeface.Style == FontStyles.Oblique
                    ? "italic"
                    : "normal";
                // 压缩/展开体在各平台的家族名差异太大，先不列。
                if (typeface.Stretch != FontStretches.Normal) continue;
            }
            catch { continue; }

            bool regular = weight == 400 && style == "normal";
            string? resolved = baseName;
            bool available = true;

            if (!regular && !baseIsWeightFamily)
            {
                string word = WeightWord(weight);
                string[] candidates = style == "italic"
                    ? new[] { baseName + " " + word + " Italic", baseName + " Italic" }
                    : new[] { baseName + " " + word, baseName + " " + word + " Italic" };

                resolved = null;
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (gdi.Contains(NormalizeFontName(candidates[i]))) { resolved = candidates[i]; break; }
                }

                if (resolved == null)
                {
                    // 系统没给这个字重独立名字：游戏里的旧版动态字体按家族名取字体，
                    // 传 baseName 只会得到常规体，所以不列出来误导用户。
                    continue;
                }
            }

            string key = resolved + "|" + weight + "|" + style;
            if (!seen.Add(key)) continue;

            variants.Add(new SystemFontVariant
            {
                Label = WeightLabel(weight) + (style == "italic" ? " 斜体" : ""),
                Family = resolved ?? baseName,
                Weight = weight,
                Style = style,
                Available = available
            });
        }

        if (variants.Count == 0)
        {
            variants.Add(new SystemFontVariant
            {
                Label = "常规",
                Family = baseName,
                Weight = 400,
                Style = "normal",
                Available = true
            });
        }

        variants.Sort((a, b) =>
        {
            bool ar = a.Weight == 400 && a.Style == "normal";
            bool br = b.Weight == 400 && b.Style == "normal";
            if (ar != br) return ar ? -1 : 1;
            if (a.Weight != b.Weight) return a.Weight.CompareTo(b.Weight);
            return string.Compare(a.Style, b.Style, StringComparison.Ordinal);
        });

        if (variants.Count > 8) variants.RemoveRange(8, variants.Count - 8);
        return variants;
    }

    private static HashSet<string> GetGdiFamilyNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var collection = new InstalledFontCollection();
            foreach (var family in collection.Families)
            {
                set.Add(NormalizeFontName(family.Name));
            }
        }
        catch { }
        return set;
    }

    private static string NormalizeFontName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (!char.IsWhiteSpace(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static int NearestWeight(int weight)
    {
        int[] standards = { 100, 200, 300, 350, 400, 500, 600, 700, 800, 900 };
        int best = 400;
        int bestDiff = int.MaxValue;
        for (int i = 0; i < standards.Length; i++)
        {
            int diff = Math.Abs(standards[i] - weight);
            if (diff < bestDiff) { best = standards[i]; bestDiff = diff; }
        }
        return best;
    }

    private static string WeightWord(int weight)
    {
        switch (weight)
        {
            case 100: return "Thin";
            case 200: return "ExtraLight";
            case 300: return "Light";
            case 350: return "SemiLight";
            case 500: return "Medium";
            case 600: return "SemiBold";
            case 700: return "Bold";
            case 800: return "ExtraBold";
            case 900: return "Black";
            default: return "Regular";
        }
    }

    private static string WeightLabel(int weight)
    {
        switch (weight)
        {
            case 100: return "极细";
            case 200: return "特细";
            case 300: return "细体";
            case 350: return "半细";
            case 500: return "中等";
            case 600: return "半粗";
            case 700: return "粗体";
            case 800: return "特粗";
            case 900: return "黑体";
            default: return "常规";
        }
    }

    private static bool EndsWithWeightWord(string name)
    {
        string[] words =
        {
            "Thin", "ExtraLight", "Light", "SemiLight", "Medium",
            "SemiBold", "Bold", "ExtraBold", "Black", "Regular"
        };
        for (int i = 0; i < words.Length; i++)
        {
            if (name.EndsWith(" " + words[i], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string DisplayName(FontFamily family, string fallback)
    {
        try
        {
            var names = family.FamilyNames;
            if (names != null)
            {
                foreach (var language in new[] { "zh-cn", "zh-hans", "en-us" })
                {
                    if (names.TryGetValue(XmlLanguage.GetLanguage(language), out var value)
                        && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }
        catch { }
        return fallback;
    }

    private static bool SupportsChinese(FontFamily family)
    {
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyph)) return false;
        return glyph.CharacterToGlyphMap.ContainsKey(0x4E2D)   // 中
            && glyph.CharacterToGlyphMap.ContainsKey(0x6587);  // 文
    }
}
