using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AstralParty.Toys.Services.Skins;

public sealed partial class AudioSkinSchemeManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static List<AudioSkinCatalogItem>? _cachedCatalog;

    public static string Root(string gameDirectory) =>
        Path.Combine(gameDirectory, "AstralParty_ModLoader", "audio-schemes");

    public static string ModConfigPath(string gameDirectory) =>
        Path.Combine(gameDirectory, "AstralParty_ModLoader", "mods", "AudioSkinMod", "config.json");

    public static string AudioCacheDirectory(string gameDirectory) =>
        Path.Combine(gameDirectory, "AstralParty_ModLoader", "audio-cache");

    public static string? FindExportRoot(string gameDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "audio-export"),
            @"C:\src\Study\astralparty\audio-export",
            Path.Combine(gameDirectory, "audio-export"),
            Path.Combine(gameDirectory, "AstralParty_ModLoader", "audio-export")
        };
        foreach (var c in candidates)
        {
            try
            {
                if (Directory.Exists(c) && Directory.Exists(Path.Combine(c, "voice"))) return Path.GetFullPath(c);
            }
            catch { }
        }
        return null;
    }

    public static string SchemeDirectory(string gameDirectory, string schemeId)
    {
        if (string.IsNullOrWhiteSpace(schemeId)) schemeId = "default";
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(schemeId.Where(c => !invalid.Contains(c) && c != '/' && c != '\\').ToArray());
        if (string.IsNullOrEmpty(safe)) safe = "default";
        return Path.Combine(Root(gameDirectory), safe);
    }

    public List<AudioSkinCatalogItem> LoadCatalog()
    {
        if (_cachedCatalog != null) return _cachedCatalog;

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("AstralParty.Toys.AudioCatalog.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                _cachedCatalog = JsonSerializer.Deserialize<List<AudioSkinCatalogItem>>(json, JsonOptions) ?? new();
                return _cachedCatalog;
            }
        }
        catch { }

        // Fallback from disk if in dev environment
        var fallbackPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AudioCatalog.json");
        if (File.Exists(fallbackPath))
        {
            try
            {
                var json = File.ReadAllText(fallbackPath);
                _cachedCatalog = JsonSerializer.Deserialize<List<AudioSkinCatalogItem>>(json, JsonOptions) ?? new();
                return _cachedCatalog;
            }
            catch { }
        }

        _cachedCatalog = new();
        return _cachedCatalog;
    }

    public string GetActiveSchemeId(string gameDirectory)
    {
        var cfgPath = ModConfigPath(gameDirectory);
        if (File.Exists(cfgPath))
        {
            try
            {
                var json = File.ReadAllText(cfgPath);
                var node = JsonNode.Parse(json);
                if (node?["activeScheme"] != null)
                {
                    return node["activeScheme"]?.ToString() ?? "default";
                }
            }
            catch { }
        }
        return "default";
    }

    public void SetActiveScheme(string gameDirectory, string schemeId)
    {
        if (string.IsNullOrWhiteSpace(schemeId)) schemeId = "default";
        var cfgPath = ModConfigPath(gameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(cfgPath)!);

        JsonObject root;
        if (File.Exists(cfgPath))
        {
            try
            {
                var json = File.ReadAllText(cfgPath);
                root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        root["activeScheme"] = schemeId;
        if (!root.ContainsKey("enabled")) root["enabled"] = true;
        if (!root.ContainsKey("sfxVolume")) root["sfxVolume"] = 1.0;
        if (!root.ContainsKey("bgmVolume")) root["bgmVolume"] = 1.0;
        if (!root.ContainsKey("voiceVolume")) root["voiceVolume"] = 1.0;

        File.WriteAllText(cfgPath, root.ToJsonString(JsonOptions));
    }

    public List<AudioSkinProfile> GetProfiles(string gameDirectory)
    {
        var root = Root(gameDirectory);
        var result = new List<AudioSkinProfile>();
        var activeId = GetActiveSchemeId(gameDirectory);

        // Ensure default scheme exists
        var defaultDir = SchemeDirectory(gameDirectory, "default");
        if (!Directory.Exists(defaultDir))
        {
            Directory.CreateDirectory(defaultDir);
            var defaultScheme = new AudioSkinScheme
            {
                Id = "default",
                Name = "默认官方原版",
                Author = "AstralParty",
                Description = "未作任何替换的官方默认音效方案"
            };
            File.WriteAllText(Path.Combine(defaultDir, "audio-scheme.json"), JsonSerializer.Serialize(defaultScheme, JsonOptions));
        }

        if (!Directory.Exists(root)) return result;

        foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d))
        {
            var id = Path.GetFileName(dir);
            if (id.StartsWith('.')) continue;

            var manifestPath = Path.Combine(dir, "audio-scheme.json");
            if (!File.Exists(manifestPath))
                manifestPath = Path.Combine(dir, "scheme.json");

            if (File.Exists(manifestPath))
            {
                try
                {
                    var scheme = JsonSerializer.Deserialize<AudioSkinScheme>(File.ReadAllText(manifestPath), JsonOptions);
                    if (scheme != null)
                    {
                        var lastModified = File.GetLastWriteTime(manifestPath);
                        result.Add(new AudioSkinProfile
                        {
                            Id = id,
                            Name = string.IsNullOrWhiteSpace(scheme.Name) ? id : scheme.Name,
                            Author = scheme.Author,
                            Description = scheme.Description,
                            DirectoryPath = dir,
                            RuleCount = scheme.Rules?.Count ?? 0,
                            IsActive = string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase),
                            IsDefault = string.Equals(id, "default", StringComparison.OrdinalIgnoreCase),
                            LastModified = lastModified
                        });
                        continue;
                    }
                }
                catch { }
            }

            // Fallback for directory without valid json
            result.Add(new AudioSkinProfile
            {
                Id = id,
                Name = id,
                DirectoryPath = dir,
                IsActive = string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase),
                IsDefault = string.Equals(id, "default", StringComparison.OrdinalIgnoreCase),
                LastModified = Directory.GetLastWriteTime(dir)
            });
        }

        return result;
    }

    public AudioSkinScheme LoadScheme(string gameDirectory, string schemeId)
    {
        var dir = SchemeDirectory(gameDirectory, schemeId);
        var manifestPath = Path.Combine(dir, "audio-scheme.json");
        if (!File.Exists(manifestPath))
            manifestPath = Path.Combine(dir, "scheme.json");

        if (File.Exists(manifestPath))
        {
            try
            {
                var scheme = JsonSerializer.Deserialize<AudioSkinScheme>(File.ReadAllText(manifestPath), JsonOptions);
                if (scheme != null)
                {
                    scheme.Id = schemeId;
                    return scheme;
                }
            }
            catch { }
        }

        return new AudioSkinScheme { Id = schemeId, Name = schemeId };
    }

    public void SaveScheme(string gameDirectory, string schemeId, AudioSkinScheme scheme)
    {
        var dir = SchemeDirectory(gameDirectory, schemeId);
        Directory.CreateDirectory(dir);
        scheme.Id = schemeId;
        var manifestPath = Path.Combine(dir, "audio-scheme.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(scheme, JsonOptions));
    }

    public AudioSkinScheme CreateScheme(string gameDirectory, string id, string name, string author = "", string description = "")
    {
        id = id.Trim();
        if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("方案标识不能为空");
        var dir = SchemeDirectory(gameDirectory, id);
        if (Directory.Exists(dir)) throw new InvalidOperationException($"方案标识 [{id}] 已存在");

        var scheme = new AudioSkinScheme
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            Author = author.Trim(),
            Description = description.Trim(),
            Rules = new List<AudioSkinRule>()
        };

        SaveScheme(gameDirectory, id, scheme);
        return scheme;
    }

    public void DeleteScheme(string gameDirectory, string id)
    {
        if (string.Equals(id, "default", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("默认方案不可删除");

        var dir = SchemeDirectory(gameDirectory, id);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }

        if (string.Equals(GetActiveSchemeId(gameDirectory), id, StringComparison.OrdinalIgnoreCase))
        {
            SetActiveScheme(gameDirectory, "default");
        }
    }

    public void DuplicateScheme(string gameDirectory, string sourceId, string newId, string newName)
    {
        var sourceScheme = LoadScheme(gameDirectory, sourceId);
        var sourceDir = SchemeDirectory(gameDirectory, sourceId);
        var targetDir = SchemeDirectory(gameDirectory, newId);

        if (Directory.Exists(targetDir))
            throw new InvalidOperationException($"方案 [{newId}] 已存在");

        Directory.CreateDirectory(targetDir);

        // Copy sound files
        if (Directory.Exists(sourceDir))
        {
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fname = Path.GetFileName(file);
                if (fname.Equals("audio-scheme.json", StringComparison.OrdinalIgnoreCase) ||
                    fname.Equals("scheme.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                File.Copy(file, Path.Combine(targetDir, fname), true);
            }
        }

        sourceScheme.Id = newId;
        sourceScheme.Name = string.IsNullOrWhiteSpace(newName) ? newId : newName;
        SaveScheme(gameDirectory, newId, sourceScheme);
    }

    public void UpdateMetadata(string gameDirectory, string id, string name, string author, string description)
    {
        var scheme = LoadScheme(gameDirectory, id);
        scheme.Name = (name ?? id).Trim();
        scheme.Author = (author ?? "").Trim();
        scheme.Description = (description ?? "").Trim();
        SaveScheme(gameDirectory, id, scheme);
    }

    public List<AudioSkinCatalogItem> GetCatalogItems(string gameDirectory, string schemeId)
    {
        var catalog = LoadCatalog();
        var scheme = LoadScheme(gameDirectory, schemeId);
        var ruleMap = new Dictionary<int, AudioSkinRule>();
        var nameMap = new Dictionary<string, AudioSkinRule>(StringComparer.OrdinalIgnoreCase);

        if (scheme.Rules != null)
        {
            foreach (var r in scheme.Rules)
            {
                if (r.ConfigId > 0) ruleMap[r.ConfigId] = r;
                if (!string.IsNullOrEmpty(r.EventName)) nameMap[r.EventName] = r;
            }
        }

        var cacheDir = AudioCacheDirectory(gameDirectory);
        var items = new List<AudioSkinCatalogItem>(catalog.Count);

        foreach (var c in catalog)
        {
            var item = new AudioSkinCatalogItem
            {
                Id = c.Id,
                Name = c.Name,
                Category = c.Category,
                Label = c.Label,
                WwiseId = c.WwiseId,
                Action = "none",
                Volume = 1.0f,
                RelOgg = c.RelOgg,
                CharacterId = c.CharacterId,
                CharacterName = c.CharacterName,
                SkinId = c.SkinId,
                SkinName = c.SkinName,
                Slot = c.Slot,
                SlotName = c.SlotName
            };

            // Check original audio cache availability or relOgg fallback
            bool hasOriginal = false;
            if (Directory.Exists(cacheDir))
            {
                var oggPath = Path.Combine(cacheDir, $"{c.Id}.ogg");
                var wavPath = Path.Combine(cacheDir, $"{c.Id}.wav");
                if (File.Exists(oggPath) || File.Exists(wavPath))
                {
                    hasOriginal = true;
                }
            }
            if (!hasOriginal && !string.IsNullOrEmpty(c.RelOgg))
            {
                var exportRoot = FindExportRoot(gameDirectory);
                if (exportRoot != null && File.Exists(Path.Combine(exportRoot, c.RelOgg)))
                {
                    hasOriginal = true;
                }
            }

            if (hasOriginal)
            {
                item.HasOriginal = true;
                item.OriginalPreviewUrl = $"https://audio.astral.local/original/{c.Id}";
            }

            // Check active rule
            AudioSkinRule? matched = null;
            if (ruleMap.TryGetValue(c.Id, out var byId)) matched = byId;
            else if (!string.IsNullOrEmpty(c.Name) && nameMap.TryGetValue(c.Name, out var byName)) matched = byName;

            if (matched != null)
            {
                item.Action = matched.Action;
                item.CustomFile = matched.File;
                item.Volume = matched.Volume > 0 ? matched.Volume : 1.0f;
                item.RedirectId = matched.RedirectId;
                if (item.Action == "replace" && !string.IsNullOrEmpty(matched.File))
                {
                    item.PreviewUrl = $"https://audio.astral.local/scheme/{Uri.EscapeDataString(schemeId)}/{Uri.EscapeDataString(matched.File)}";
                }
            }

            items.Add(item);
        }

        return items;
    }

    public void SaveCustomAudio(string gameDirectory, string schemeId, int configId, string eventName, byte[] wavBytes, string fileName, float volume)
    {
        var dir = SchemeDirectory(gameDirectory, schemeId);
        Directory.CreateDirectory(dir);

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeFileName)) safeFileName = $"{configId}.wav";
        var filePath = Path.Combine(dir, safeFileName);
        File.WriteAllBytes(filePath, wavBytes);

        var scheme = LoadScheme(gameDirectory, schemeId);
        scheme.Rules.RemoveAll(r => (configId > 0 && r.ConfigId == configId) ||
                                    (!string.IsNullOrEmpty(eventName) && string.Equals(r.EventName, eventName, StringComparison.OrdinalIgnoreCase)));

        scheme.Rules.Add(new AudioSkinRule
        {
            ConfigId = configId,
            EventName = eventName ?? "",
            Action = "replace",
            File = safeFileName,
            Volume = volume > 0 ? volume : 1.0f
        });

        SaveScheme(gameDirectory, schemeId, scheme);
    }

    public void RevertAudio(string gameDirectory, string schemeId, int configId, string eventName)
    {
        var scheme = LoadScheme(gameDirectory, schemeId);
        var removed = scheme.Rules.Where(r => (configId > 0 && r.ConfigId == configId) ||
                                             (!string.IsNullOrEmpty(eventName) && string.Equals(r.EventName, eventName, StringComparison.OrdinalIgnoreCase))).ToList();

        scheme.Rules.RemoveAll(r => removed.Contains(r));

        // Delete unused file
        var dir = SchemeDirectory(gameDirectory, schemeId);
        foreach (var r in removed)
        {
            if (r.Action == "replace" && !string.IsNullOrEmpty(r.File))
            {
                bool stillUsed = scheme.Rules.Any(x => string.Equals(x.File, r.File, StringComparison.OrdinalIgnoreCase));
                if (!stillUsed)
                {
                    var p = Path.Combine(dir, r.File);
                    if (File.Exists(p)) try { File.Delete(p); } catch { }
                }
            }
        }

        SaveScheme(gameDirectory, schemeId, scheme);
    }

    public void SetMute(string gameDirectory, string schemeId, int configId, string eventName, bool mute)
    {
        var scheme = LoadScheme(gameDirectory, schemeId);
        scheme.Rules.RemoveAll(r => (configId > 0 && r.ConfigId == configId) ||
                                    (!string.IsNullOrEmpty(eventName) && string.Equals(r.EventName, eventName, StringComparison.OrdinalIgnoreCase)));

        if (mute)
        {
            scheme.Rules.Add(new AudioSkinRule
            {
                ConfigId = configId,
                EventName = eventName ?? "",
                Action = "mute"
            });
        }

        SaveScheme(gameDirectory, schemeId, scheme);
    }

    public void SetRedirect(string gameDirectory, string schemeId, int configId, string eventName, int targetConfigId)
    {
        var scheme = LoadScheme(gameDirectory, schemeId);
        scheme.Rules.RemoveAll(r => (configId > 0 && r.ConfigId == configId) ||
                                    (!string.IsNullOrEmpty(eventName) && string.Equals(r.EventName, eventName, StringComparison.OrdinalIgnoreCase)));

        if (targetConfigId > 0)
        {
            scheme.Rules.Add(new AudioSkinRule
            {
                ConfigId = configId,
                EventName = eventName ?? "",
                Action = "redirect",
                RedirectId = targetConfigId
            });
        }

        SaveScheme(gameDirectory, schemeId, scheme);
    }

    public void SetVolume(string gameDirectory, string schemeId, int configId, string eventName, float volume)
    {
        var scheme = LoadScheme(gameDirectory, schemeId);
        var rule = scheme.Rules.FirstOrDefault(r => (configId > 0 && r.ConfigId == configId) ||
                                                    (!string.IsNullOrEmpty(eventName) && string.Equals(r.EventName, eventName, StringComparison.OrdinalIgnoreCase)));
        if (rule != null)
        {
            rule.Volume = volume > 0 ? volume : 1.0f;
            SaveScheme(gameDirectory, schemeId, scheme);
        }
    }
}