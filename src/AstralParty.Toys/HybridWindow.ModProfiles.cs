using System.IO;
using System.Text.Json;
using AstralParty.Toys.Models;

namespace AstralParty.Toys;

/// <summary>
/// 模组方案（全局共享）+ 无损回滚历史。
///
/// 方案文件躲在数据根（<c>state\mod-profiles.json</c>）而不是游戏目录：完整卸载加载器会删掉
/// 整个 <c>AstralParty_ModLoader</c>，方案不该跟着丢。每次写操作都经 <see cref="ModRollbackJournal"/>，
/// 包括套用方案、保存 / 更新 / 改名 / 删除方案、多开档案切换与加载器设置保存。
/// </summary>
public partial class HybridWindow
{
    private int _modProfilesRequest;
    private bool _modProfileRunning;

    private ModProfileService CreateModProfileService()
    {
        string? gameDirectory = null;
        try { gameDirectory = _modManager.GetStoredGameDirectory(); } catch { /* 读不到按未选 */ }
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            try { gameDirectory = _gameLibrary.GetActive()?.Directory; } catch { /* 没有档案 */ }
        }
        return new ModProfileService(_modManager, gameDirectory);
    }

    private ModRollbackJournal CreateRollbackJournal() => new(_modManager);

    /// <summary>安装 / 更新 / 导入 / 恢复加载器期间不要读写模组状态（会读到半套文件）。</summary>
    private bool ModFilesBusy =>
        _modInstallRunning || _modUpdateRunning || _modImportRunning ||
        _modPackageImportRunning || _modRollbackRunning || _loaderRecoveryBusy;

    private bool RejectWhenProfilesBusy()
    {
        if (!ModFilesBusy && !_modProfileRunning) return false;
        Post(new { type = "toast", message = "正在执行安装 / 导入 / 方案切换，请等完成后再操作方案。" });
        return true;
    }

    private async Task PushModProfilesAsync()
    {
        var request = ++_modProfilesRequest;
        try
        {
            var payload = await Task.Run(() =>
            {
                var service = CreateModProfileService();
                var store = service.Load();
                var directory = service.GameDirectory;
                var loaderInstalled = !string.IsNullOrWhiteSpace(directory) &&
                                      File.Exists(_modManager.LoaderConfigPath(directory));
                var loaderMissing = !string.IsNullOrWhiteSpace(directory) &&
                                    !Directory.Exists(Path.Combine(directory, ModManager.LoaderFolderName));
                return new
                {
                    gameDirectory = directory,
                    activeName = store.Active,
                    profiles = service.List(),
                    loaderInstalled,
                    loaderMissing,
                    gameRunning = SpeedhackManager.IsGameRunning(),
                    namePattern = ModProfileService.NamePattern,
                    nameMaxLength = ModProfileService.MaxNameLength,
                    maxNoteLength = ModProfileService.MaxNoteLength,
                    error = string.IsNullOrWhiteSpace(directory)
                        ? "还没有选择游戏目录。"
                        : Directory.Exists(directory) ? "" : "游戏目录已经不存在了。"
                };
            });
            if (request == _modProfilesRequest)
                Post(new { type = "modProfiles", payload });
        }
        catch (Exception ex)
        {
            if (request == _modProfilesRequest)
                Post(new { type = "error", message = $"读取模组方案失败：{ex.Message}" });
        }
    }

    // ============================== 方案定义 ==============================

    private void HandleModProfileCapture(JsonElement root)
    {
        if (RejectWhenProfilesBusy()) return;
        var name = ReadString(root, "name");
        var note = ReadString(root, "note");
        var includeConfigs = ReadBool(root, "includeConfigs", true);
        var includeLoader = ReadBool(root, "includeLoader", true);
        try
        {
            var service = CreateModProfileService();
            var summary = service.Capture(name, note, includeConfigs, includeLoader);
            Post(new
            {
                type = "toast",
                message = $"已保存方案「{summary.Name}」（{summary.ModCount} 个模组，{summary.EnabledCount} 个启用）。"
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"保存方案失败：{ex.Message}" });
        }
        _ = PushModProfilesAsync();
    }

    private void HandleModProfileUpdate(JsonElement root)
    {
        if (RejectWhenProfilesBusy()) return;
        var name = ReadString(root, "name");
        var includeConfigs = ReadBool(root, "includeConfigs", true);
        var includeLoader = ReadBool(root, "includeLoader", true);
        try
        {
            var service = CreateModProfileService();
            var summary = service.Update(name, includeConfigs, includeLoader);
            Post(new { type = "toast", message = $"已用当前状态更新方案「{summary.Name}」。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"更新方案失败：{ex.Message}" });
        }
        _ = PushModProfilesAsync();
    }

    private void HandleModProfileRename(JsonElement root)
    {
        if (RejectWhenProfilesBusy()) return;
        var name = ReadString(root, "name");
        var newName = ReadString(root, "newName");
        try
        {
            CreateModProfileService().Rename(name, newName);
            Post(new { type = "toast", message = $"已改名为「{newName.Trim()}」。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"重命名失败：{ex.Message}" });
        }
        _ = PushModProfilesAsync();
    }

    private void HandleModProfileDelete(JsonElement root)
    {
        if (RejectWhenProfilesBusy()) return;
        var name = ReadString(root, "name");
        try
        {
            CreateModProfileService().Delete(name);
            Post(new { type = "toast", message = $"已删除方案「{name}」，可以在「历史与回滚」里恢复。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"删除方案失败：{ex.Message}" });
        }
        _ = PushModProfilesAsync();
    }

    private void HandleModProfileSetNote(JsonElement root)
    {
        if (RejectWhenProfilesBusy()) return;
        var name = ReadString(root, "name");
        var note = ReadString(root, "note");
        try
        {
            CreateModProfileService().SetNote(name, note);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"保存备注失败：{ex.Message}" });
        }
        _ = PushModProfilesAsync();
    }

    private void HandleModProfileClearActive()
    {
        if (RejectWhenProfilesBusy()) return;
        try
        {
            CreateModProfileService().ClearActive();
            Post(new { type = "toast", message = "已取消当前方案（模组状态不变）。" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"取消当前方案失败：{ex.Message}" });
        }
        _ = PushModProfilesAsync();
    }

    // ============================== 套用 ==============================

    private void HandleModProfilePlan(JsonElement root)
    {
        var name = ReadString(root, "name");
        try
        {
            var plan = CreateModProfileService().Plan(name);
            Post(new { type = "modProfilePlan", payload = plan });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"读取方案差异失败：{ex.Message}" });
        }
    }

    private void HandleModProfileApply(JsonElement root)
    {
        var name = ReadString(root, "name");
        if (_modProfileRunning)
        {
            Post(new { type = "toast", message = "正在切换方案，请稍候。" });
            return;
        }
        _modProfileRunning = true;
        _ = ApplyModProfileAsync(name);
    }

    private async Task ApplyModProfileAsync(string name)
    {
        try
        {
            var outcome = await Task.Run(() =>
            {
                try { return (Result: CreateModProfileService().Apply(name), Error: (Exception?)null); }
                catch (Exception ex) { return (Result: (ModProfileApplyResult?)null, Error: ex); }
            }).ConfigureAwait(true);

            if (outcome.Error is not null)
            {
                ToysLog.Error("mods", $"套用模组方案「{name}」失败", outcome.Error);
                Post(new
                {
                    type = "modProfileApplyResult",
                    payload = new ModProfileApplyResult
                    {
                        Ok = false,
                        Message = $"套用失败：{outcome.Error.Message}",
                        Failed = new List<string> { outcome.Error.Message }
                    }
                });
                Post(new { type = "error", message = $"套用方案失败：{outcome.Error.Message}" });
            }
            else
            {
                var result = outcome.Result!;
                Post(new { type = "modProfileApplyResult", payload = result });
                Post(new { type = "toast", message = result.Message });
                if (result.Ok) ToysLog.Info("mods", $"套用模组方案「{name}」成功");
            }
        }
        finally
        {
            _modProfileRunning = false;
            _ = PushModStatusAsync();      // 会顺带回推 modProfiles
            PushLoaderConfigSync();
        }
    }

    /// <summary>套用方案可能改了加载器设置：把变速栏与首页启动方式同步过来。</summary>
    private void PushLoaderConfigSync()
    {
        try
        {
            var directory = CreateModProfileService().GameDirectory;
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(_modManager.LoaderConfigPath(directory))) return;
            Post(new
            {
                type = "modSpeedhackSync",
                payload = new { config = _modManager.ReadLoaderConfig(directory) }
            });
            _ = PostSteamBypassSyncAsync();
        }
        catch
        {
            // 没装加载器 / 配置读不到：忽略，界面保持原样
        }
    }

    // ============================== 历史与回滚 ==============================

    private void HandleModHistoryList()
    {
        try
        {
            var entries = CreateRollbackJournal().ListEntries();
            Post(new
            {
                type = "modHistoryEntries",
                payload = new
                {
                    entries,
                    gameRunning = SpeedhackManager.IsGameRunning()
                }
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"读取回滚记录失败：{ex.Message}" });
        }
    }

    private void HandleModHistoryRollback(JsonElement root)
    {
        var id = ReadString(root, "id");
        var force = ReadBool(root, "force", false);
        if (_modProfileRunning || ModFilesBusy)
        {
            Post(new { type = "toast", message = "正在执行另一个文件操作，请稍候。" });
            return;
        }

        // 涉及模组文件的回滚要搬目录：游戏运行时先拦下来（界面也会禁用，这里再兜一层）
        try
        {
            var entry = CreateRollbackJournal().ListEntries().FirstOrDefault(e => e.Id == id);
            if (entry is null)
            {
                Post(new { type = "toast", message = "找不到这条回滚记录（可能已被自动清理）。" });
                return;
            }
            if (entry.HasMods && SpeedhackManager.IsGameRunning())
            {
                Post(new { type = "toast", message = "游戏正在运行：先完全退出游戏，再回滚模组文件。" });
                return;
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"读取回滚记录失败：{ex.Message}" });
            return;
        }

        _modProfileRunning = true;
        _ = RollbackModHistoryAsync(id, force);
    }

    private async Task RollbackModHistoryAsync(string id, bool force)
    {
        try
        {
            var outcome = await Task.Run(() =>
            {
                try { return (Result: CreateRollbackJournal().Rollback(id, force), Error: (Exception?)null); }
                catch (Exception ex) { return (Result: (RollbackResult?)null, Error: ex); }
            }).ConfigureAwait(true);

            if (outcome.Error is not null)
            {
                ToysLog.Error("mods", $"回滚 {id} 失败", outcome.Error);
                Post(new { type = "error", message = $"回滚失败：{outcome.Error.Message}" });
                Post(new { type = "toast", message = $"回滚失败：{outcome.Error.Message}" });
            }
            else
            {
                var result = outcome.Result!;
                Post(new
                {
                    type = "modHistoryRollbackResult",
                    payload = new
                    {
                        id,
                        ok = result.Ok,
                        needsForce = result.NeedsForce,
                        message = result.Message,
                        restored = result.Restored,
                        conflicts = result.Conflicts,
                        failed = result.Failed
                    }
                });
                Post(new { type = "toast", message = result.Message });
                if (result.Ok) ToysLog.Info("mods", $"回滚操作 {id} 成功");
            }
        }
        finally
        {
            _modProfileRunning = false;
            _ = PushModStatusAsync();
            PushLoaderConfigSync();
        }
    }

    // ============================== 小工具 ==============================

    private static string ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? ""
            : "";

    private static bool ReadBool(JsonElement root, string name, bool fallback)
        => root.TryGetProperty(name, out var element) &&
           element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : fallback;
}
