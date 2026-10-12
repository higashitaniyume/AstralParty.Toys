using System.IO;
using System.Diagnostics;
using System.Text.Json;
using AstralParty.Toys.Services;
using CesiumLoader.CharacterSkins;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace AstralParty.Toys;
public partial class HybridWindow
{
    private readonly CharacterSkinManager _characterSkins = new();
    private sealed record CharacterCrop(string Token,string Game,string Id,string Slot,System.Windows.Media.Imaging.BitmapSource Image,CharacterImageTarget Target,string? Action=null,int FrameIndex=-1,string? ExpectedPath=null);
    private CharacterCrop? _characterCrop;
    private void SaveCharacterCrop(JsonElement root)
    {
        string token=root.GetProperty("token").GetString()??"";
        try
        {
            var pending=_characterCrop;
            if(pending==null||pending.Token!=token)throw new InvalidOperationException("裁剪已过期，请重新选择图片");
            if(pending.Game!=RequireModGameDirectory())throw new InvalidOperationException("当前游戏已切换，请重新选择素材");
            bool contain=root.GetProperty("contain").GetBoolean();
            var rect=contain?new System.Windows.Int32Rect(0,0,pending.Image.PixelWidth,pending.Image.PixelHeight):new System.Windows.Int32Rect(root.GetProperty("x").GetInt32(),root.GetProperty("y").GetInt32(),root.GetProperty("width").GetInt32(),root.GetProperty("height").GetInt32());
            var rendered=SkinCropImage.RenderRegion(pending.Image,rect,pending.Target.Width,pending.Target.Height,contain);
            if (pending.Action != null) _characterSkins.SaveFrame(pending.Game,pending.Id,pending.Action,pending.FrameIndex,rendered,pending.ExpectedPath!);
            else _characterSkins.SaveImage(pending.Game,pending.Id,pending.Slot,rendered);
            _characterCrop=null;Post(new{type="characterCropSaved",payload=new{token,success=true}});PushCharacterSkins();
        }
        catch(Exception ex){Post(new{type="characterCropSaved",payload=new{token,success=false,error=ex.Message}});}
    }
    private void PushCharacterSkins()
    {
        var game = RequireModGameDirectory();
        var protocol = new GameProtocolContext(Path.Combine(_appDirectory, "Protocol"));
        var catalog = new ConfigCatalog(protocol, Path.Combine(_appDirectory, "GameData"));
        var targets = CharacterSkinCatalog.Load(protocol, Path.Combine(_appDirectory, "GameData"));
        var profiles = _characterSkins.Profiles(game);
        Post(new { type = "characterSkins", payload = new {
            profiles, targets,
            templateCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "feimo", "AstralParty_CN", "com.unity.addressables"),
            heroes = catalog.CharacterIds.Select(id => new { id, name = catalog.Character(id) }),
            installed = File.Exists(Path.Combine(game, "AstralParty_ModLoader", "mods", "CharacterSkinMod", "CharacterSkinMod.dll")),
            imageSlots = CharacterSkinRules.ImageSlots, textSlots = CharacterSkinRules.TextSlots
        } });
    }
    private bool _characterTemplateRunning;
    private async void HandleCharacterSkins(JsonElement root)
    {
        try
        {
            string type = root.GetProperty("type").GetString()!;
            string game = RequireModGameDirectory();
            string id = root.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? "" : "";
            switch (type)
            {
                case "characterSkinsGet": break;
                case "characterSkinsCreate":
                    if (_characterTemplateRunning) throw new InvalidOperationException("正在导出模板，请等待完成");
                    if (root.TryGetProperty("fromGame", out var fromGame) && fromGame.GetBoolean())
                    {
                        _characterTemplateRunning = true;
                        Post(new { type = "characterTemplateState", payload = new { running = true, id, message = "开始提取默认皮肤" } });
                        try
                        {
                            string cache = root.GetProperty("cache").GetString() ?? "";
                            var result = await CharacterTemplateExporter.ExportAsync(game, cache, id, root.GetProperty("name").GetString() ?? id, root.GetProperty("heroId").GetInt32(), Path.Combine(_appDirectory,"GameData"), Path.Combine(_appDirectory,"Protocol"), message => Post(new { type = "characterTemplateState", payload = new { running = true, id, message } }));
                            // Complete before refreshing profiles, so the UI can select the newly created package.
                            Post(new { type = "characterTemplateState", payload = new { running = false, success = true, id, message = $"已提取 {result.Images} 个图片入口、{result.Actions} 个动作、{result.Frames} 帧", missing = result.Missing } });
                            if(game == RequireModGameDirectory()) PushCharacterSkins();
                            return;
                        }
                        catch(Exception ex) { Post(new { type = "characterTemplateState", payload = new { running = false, success = false, id, message = ex.Message } }); return; }
                        finally { _characterTemplateRunning = false; }
                    }
                    _characterSkins.Create(game, id, root.GetProperty("heroId").GetInt32(), root.GetProperty("skinItemId").GetInt32(), root.GetProperty("name").GetString() ?? id);
                    Post(new { type = "characterTemplateState", payload = new { running = false, success = true, id, message = "空白方案已创建" } });
                    break;
                case "characterSkinsActive": _characterSkins.SetActive(game, id, root.GetProperty("enabled").GetBoolean()); break;
                case "characterSkinsDelete": _characterSkins.Delete(game, id); break;
                case "characterSkinsSave":
                    var manifest = JsonSerializer.Deserialize<CharacterSkinManifest>(root.GetProperty("manifest").GetRawText(), CharacterSkinManager.JsonOptions)!;
                    _characterSkins.Save(game, id, manifest); break;
                case "characterSkinsOpen":
                    string folder = id.Length == 0 ? _characterSkins.Root(game) : _characterSkins.Package(game, id);
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true }); break;
                case "characterSkinsImport":
                    var import = new OpenFileDialog { Title = "导入角色皮肤包", Filter = "角色皮肤包 (*.zip)|*.zip", CheckFileExists = true };
                    if (import.ShowDialog(this) != true) return;
                    _characterSkins.Import(game, import.FileName); break;
                case "characterSkinsExport":
                    var export = new SaveFileDialog { Title = "导出角色皮肤包", Filter = "ZIP (*.zip)|*.zip", FileName = id + ".zip" };
                    if (export.ShowDialog(this) != true) return;
                    // Export into a temporary archive so an existing selected file is preserved on failure.
                    string tmp = export.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { _characterSkins.Export(game, id, tmp); File.Move(tmp, export.FileName, true); }
                    finally { if (File.Exists(tmp)) File.Delete(tmp); }
                    break;
                case "characterSkinsImage":
                    var image = new OpenFileDialog { Title = "选择角色图片", Filter = "透明 PNG / JPG|*.png;*.jpg;*.jpeg", CheckFileExists = true };
                    if (image.ShowDialog(this) != true) return;
                    string slot=root.GetProperty("slot").GetString()!;
                    var bitmap=SkinCropImage.Load(image.FileName);
                    var protocol=new GameProtocolContext(Path.Combine(_appDirectory,"Protocol"));
                    var targetSize=CharacterImageSize.Resolve(protocol,Path.Combine(_appDirectory,"GameData"),_characterSkins.Load(game,id),slot);
                    string token=Guid.NewGuid().ToString("N");
                    _characterCrop=new(token,game,id,slot,bitmap,targetSize);
                    Post(new{type="characterCropOpen",payload=new{token,slot,fileName=Path.GetFileName(image.FileName),width=bitmap.PixelWidth,height=bitmap.PixelHeight,targetWidth=targetSize.Width,targetHeight=targetSize.Height,verified=targetSize.Verified,asset=targetSize.Asset,previewUrl=SkinCropImage.Preview(bitmap)}});
                    return;
                case "characterSkinsClearAction":
                    _characterSkins.ClearAction(game, id, root.GetProperty("action").GetString()!); break;
                case "characterSkinsFrame":
                    string action = root.GetProperty("action").GetString()!;
                    int index = root.GetProperty("index").GetInt32();
                    var frameManifest = _characterSkins.Load(game, id);
                    if (!frameManifest.Animations.TryGetValue(action, out var animation) || index < 0 || index >= animation.Frames.Count)
                        throw new InvalidOperationException("选中的动画帧不存在");
                    string expected = animation.Frames[index];
                    var original = SkinCropImage.Load(CharacterSkinRules.SafePath(_characterSkins.Package(game, id), expected));
                    var replacement = new OpenFileDialog { Title = $"替换 {action} 第 {index + 1} 帧", Filter = "PNG / JPG|*.png;*.jpg;*.jpeg", CheckFileExists = true };
                    if (replacement.ShowDialog(this) != true) return;
                    var frameBitmap = SkinCropImage.Load(replacement.FileName);
                    string frameToken = Guid.NewGuid().ToString("N");
                    var frameTarget = new CharacterImageTarget(original.PixelWidth, original.PixelHeight, expected, true);
                    _characterCrop = new(frameToken, game, id, "frame", frameBitmap, frameTarget, action, index, expected);
                    Post(new { type = "characterCropOpen", payload = new { token = frameToken, slot = "frame", action, frameIndex = index, fileName = Path.GetFileName(replacement.FileName), width = frameBitmap.PixelWidth, height = frameBitmap.PixelHeight, targetWidth = frameTarget.Width, targetHeight = frameTarget.Height, verified = true, asset = expected, previewUrl = SkinCropImage.Preview(frameBitmap) } });
                    return;
                case "characterSkinsFrames":
                    var frames = new OpenFileDialog { Title = "选择动画帧（按文件名排序）", Filter = "PNG / JPG 帧|*.png;*.jpg;*.jpeg", Multiselect = true, CheckFileExists = true };
                    if (frames.ShowDialog(this) != true) return;
                    _characterSkins.ImportFrames(game, id, root.GetProperty("action").GetString()!, frames.FileNames, root.GetProperty("fps").GetSingle(), root.GetProperty("loop").GetBoolean()); break;
                case "characterSkinsInstall":
                    string target = Path.Combine(game, "AstralParty_ModLoader", "mods", "CharacterSkinMod");
                    if (!File.Exists(Path.Combine(game, "AstralParty_ModLoader", "sdk", "CesiumLoader.SDK.dll"))) throw new InvalidOperationException("请先在模组页安装 CesiumLoader。");
                    Directory.CreateDirectory(target);
                    foreach (string name in new[] { "CharacterSkinMod.dll", "CharacterSkinMod.json" })
                    {
                        using var stream = typeof(HybridWindow).Assembly.GetManifestResourceStream("AstralParty.Toys.ModLoader." + name) ?? throw new InvalidOperationException("内置角色模组缺失");
                        using var file = File.Create(Path.Combine(target, name)); stream.CopyTo(file);
                    }
                    break;
            }
            PushCharacterSkins();
            if (type != "characterSkinsGet") Post(new { type = "toast", message = "角色皮肤已保存。游戏内修改在完全退出并重启后生效。" });
        }
        catch (Exception ex) { Post(new { type = "error", message = "角色皮肤操作失败：" + ex.Message }); }
    }
    private CoreWebView2WebResourceResponse CreateCharacterSkinResponse(Uri uri)
    {
        try
        {
            string game = RequireModGameDirectory();
            var parts = uri.AbsolutePath.TrimStart('/').Split('/', 2);
            if (parts.Length != 2) throw new InvalidOperationException();
            string dir = _characterSkins.Package(game, Uri.UnescapeDataString(parts[0]));
            string path = CharacterSkinRules.SafePath(dir, Uri.UnescapeDataString(parts[1]));
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) throw new InvalidOperationException();
            return WebView.CoreWebView2.Environment.CreateWebResourceResponse(new WebResourceStream(File.OpenRead(path)), 200, "OK", "Content-Type: " + (ext == ".png" ? "image/png" : "image/jpeg") + "\r\nCache-Control: no-store");
        }
        catch { return WebView.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "Content-Type: text/plain"); }
    }
}
