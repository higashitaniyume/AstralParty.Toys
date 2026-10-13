using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AstralParty.Toys.Services;
using CesiumLoader.CharacterSkins;
using Microsoft.Win32;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private sealed record CharacterPackageAttachment(string SchemeId, CharacterSkinBinding Binding);
    private CharacterSkinSchemeManager CharacterSchemeManager() => new(CharacterSkinCatalog.Load(new GameProtocolContext(Path.Combine(_appDirectory, "Protocol")), Path.Combine(_appDirectory, "GameData")));
    private CharacterPackageAttachment? ReadCharacterAttachment(JsonElement root, string game)
    {
        if (!root.TryGetProperty("schemeId", out var schemeValue) || string.IsNullOrEmpty(schemeValue.GetString())) return null;
        if (root.TryGetProperty("gameDirectory", out var gameValue) && !string.Equals(gameValue.GetString(), game, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前游戏已切换，请重新添加人物皮肤包");
        string schemeId = schemeValue.GetString()!;
        var binding = JsonSerializer.Deserialize<CharacterSkinBinding>(root.GetProperty("binding").GetRawText(), CharacterSkinManager.JsonOptions) ?? throw new InvalidOperationException("人物配置为空");
        CharacterSkinSchemeRules.ValidateId(binding.Id);
        var scheme = CharacterSchemeManager().Load(game, schemeId);
        if (scheme.Rules.Any(r => r.Id != binding.Id && r.HeroId == binding.HeroId && r.TargetSkinItemId == binding.TargetSkinItemId)) throw new InvalidOperationException("此人物的相同原皮肤已在方案中，请编辑已有的人物皮肤");
        return new(schemeId, binding);
    }
    private void AttachCreatedCharacterPackage(string game, string packageId, CharacterPackageAttachment? attachment)
    {
        if (attachment == null) return;
        var rule = new CharacterSkinBinding { Id = attachment.Binding.Id, HeroId = attachment.Binding.HeroId, TargetSkinItemId = attachment.Binding.TargetSkinItemId, OfficialSourceSkinItemId = attachment.Binding.OfficialSourceSkinItemId, CustomPackageId = packageId };
        try { CharacterSchemeManager().AttachPackage(game, attachment.SchemeId, rule); }
        catch
        {
            // The package was created by this operation; preserve it in .deleted on attachment failure.
            _characterSkins.Delete(game, packageId);
            throw;
        }
    }
    private void HandleCharacterSchemes(JsonElement root)
    {
        try
        {
            string game = RequireModGameDirectory();
            var protocol = new GameProtocolContext(Path.Combine(_appDirectory, "Protocol"));
            var manager = new CharacterSkinSchemeManager(CharacterSkinCatalog.Load(protocol, Path.Combine(_appDirectory, "GameData")));
            string type = root.GetProperty("type").GetString()!;
            string id = root.TryGetProperty("id", out var value) ? value.GetString() ?? "" : "";
            switch (type)
            {
                case "characterSchemesCreate":
                    manager.Create(game, id, root.GetProperty("name").GetString() ?? id);
                    break;
                case "characterSchemesSave":
                    var scheme = JsonSerializer.Deserialize<CharacterSkinScheme>(root.GetProperty("scheme").GetRawText(), CharacterSkinManager.JsonOptions) ?? throw new InvalidOperationException("方案为空");
                    manager.Save(game, id, scheme);
                    break;
                case "characterSchemesRemoveRule":
                    var current = manager.Load(game, id);
                    string ruleId = root.GetProperty("ruleId").GetString()!;
                    if (current.Rules.RemoveAll(r => r.Id == ruleId) == 0) throw new InvalidOperationException("人物规则不存在，请刷新方案");
                    manager.Save(game, id, current);
                    break;
                case "characterSchemesActive": manager.SetActive(game, id); break;
                case "characterSchemesDelete": manager.Delete(game, id); break;
                case "characterSchemesOpen":
                    string folder = id.Length == 0 ? manager.Root(game) : manager.SchemeDirectory(game, id);
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
                    break;
                case "characterSchemesImportPackage":
                    var packageDialog = new OpenFileDialog { Title = "向当前方案添加人物皮肤包", Filter = "人物皮肤包 ZIP (*.zip)|*.zip", CheckFileExists = true };
                    if (packageDialog.ShowDialog(this) != true) return;
                    CharacterSkinBinding? draft = root.TryGetProperty("binding", out var bindingJson) ? JsonSerializer.Deserialize<CharacterSkinBinding>(bindingJson.GetRawText(), CharacterSkinManager.JsonOptions) : null;
                    string importedId = manager.ImportPackageIntoScheme(game, id, packageDialog.FileName, draft);
                    Post(new { type = "characterSchemePackageAttached", payload = new { schemeId = id, packageId = importedId, gameDirectory = game } });
                    break;
                case "characterSchemesImport":
                    var import = new OpenFileDialog { Title = "导入多人物皮肤方案", Filter = "方案 ZIP (*.zip)|*.zip", CheckFileExists = true };
                    if (import.ShowDialog(this) != true) return;
                    manager.Import(game, import.FileName);
                    break;
                case "characterSchemesExport":
                    var export = new SaveFileDialog { Title = "导出方案及自定义皮肤包", Filter = "方案 ZIP (*.zip)|*.zip", FileName = id + ".zip" };
                    if (export.ShowDialog(this) != true) return;
                    string temporary = export.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { manager.Export(game, id, temporary); File.Move(temporary, export.FileName, true); }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    break;
                default: throw new InvalidOperationException("未知的方案操作");
            }
            PushCharacterSkins();
            Post(new { type = "characterSchemesSaved", payload = new { success = true } });
            Post(new { type = "toast", message = "多人物方案已保存。完全退出并重启游戏后生效。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "characterSchemesSaved", payload = new { success = false, error = ex.Message } });
            Post(new { type = "error", message = "方案操作失败：" + ex.Message });
        }
    }
}
