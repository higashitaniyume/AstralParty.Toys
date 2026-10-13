using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CesiumLoader.CharacterSkins;
namespace AstralParty.Toys.Services;
public sealed record CharacterTemplateResult(int Images,int Actions,int Frames,string[] Missing);
public static class CharacterTemplateExporter
{
    public static Task<CharacterTemplateResult> ExportAsync(string game,string cache,string id,string name,int heroId,string dataDirectory,string protocolDirectory,Action<string> progress, CancellationToken cancellationToken = default, Action<CharacterExportProgress>? detailedProgress = null, int sourceSkinItemId = 0)
    {
        var manager=new CharacterSkinManager();string target=manager.Package(game,id);
        if(Directory.Exists(target))throw new InvalidOperationException("方案已存在");
        void Report(int percent,string message){cancellationToken.ThrowIfCancellationRequested();progress(message);detailedProgress?.Invoke(new(percent,message));}
        return Task.Run(()=>Export(game,cache,id,name,heroId,dataDirectory,protocolDirectory,manager,target,Report,cancellationToken,sourceSkinItemId),cancellationToken);
    }
    internal static object SelectSourceSkin(object hero, object skins, int sourceSkinItemId = 0)
    {
        int heroId=ReflectionValue.Int(hero,"Id");
        var group=ReflectionValue.Items(ReflectionValue.Get(skins,"StandingPaintings"))
            .FirstOrDefault(x=>ReflectionValue.Int(x,"Id")==ReflectionValue.Int(hero,"StandingPainting"));
        var candidates=ReflectionValue.Items(ReflectionValue.Get(group,"SkinStandingPaintingConfigureItems")).ToArray();
        if(sourceSkinItemId!=0)
            return candidates.FirstOrDefault(x=>ReflectionValue.Int(x,"ItemID")==sourceSkinItemId)
                ??throw new InvalidOperationException($"角色 {heroId} 中不存在指定来源皮肤 {sourceSkinItemId}，无法导出；不会回退到默认皮肤");
        return candidates.FirstOrDefault(x=>ReflectionValue.Int(x,"ItemID")==100000000+heroId*1000+1)
            ??candidates.FirstOrDefault()
            ??throw new InvalidOperationException($"角色 {heroId} 没有可导出的皮肤配置");
    }
    static CharacterTemplateResult Export(string game,string cache,string id,string name,int heroId,string dataDirectory,string protocolDirectory,CharacterSkinManager manager,string target,Action<int,string> progress,CancellationToken cancellationToken,int sourceSkinItemId)
    {
        progress(1,"定位安装目录和热更新缓存资源");
        var catalog=new CharacterBundleCatalog(game,cache,cancellationToken);
        using var resources=new CharacterUnityResources();
        var bundles=catalog.Resolve(["GameData_CN"]);resources.Load(bundles,cancellationToken);
        var protocol=new GameProtocolContext(protocolDirectory);
        object Config(string asset,string type)
        {
            var obj=resources.Objects.FirstOrDefault(x=>x.Info.TypeId==49&&x.Value["m_Name"].AsString==asset);
            return protocol.Decode(type,obj!=null?obj.Value["m_Script"].AsByteArray:File.ReadAllBytes(Path.Combine(dataDirectory,asset+".bin")));
        }
        var characters=Config("Character","CharacterConfigure");
        var hero=ReflectionValue.Items(ReflectionValue.Get(characters,"Infos")).FirstOrDefault(x=>ReflectionValue.Int(x,"Id")==heroId)??throw new InvalidOperationException("角色不存在");
        var skins=Config("Skin","SkinConfigure");
        var skin=SelectSourceSkin(hero,skins,sourceSkinItemId);
        int selectedSkinItemId=ReflectionValue.Int(skin,"ItemID");
        var assets=new Dictionary<string,string>();
        foreach(string slot in CharacterSkinRules.ImageSlots)
        {
            string asset;
            if(slot=="CharacterMap")asset=ReflectionValue.Text(hero,slot);
            else if(slot.StartsWith("Bust")||slot.StartsWith("SfwBust"))
            {
                var values=ReflectionValue.Items(ReflectionValue.Get(skin,slot.StartsWith("Sfw")?"SfwBust":"Bust")).Select(x=>x.ToString()??"").ToArray();int index=slot.EndsWith('1')?1:0;asset=index<values.Length?values[index]:"";
            }
            else asset=ReflectionValue.Text(skin,slot);
            if(string.IsNullOrEmpty(asset)&&slot.StartsWith("InGameCharacter"))asset=ReflectionValue.Text(skin,slot.EndsWith("SFW")?"SfwCharacter":"Character");
            if(!string.IsNullOrEmpty(asset))assets[slot]=asset;
        }
        string level=ReflectionValue.Text(skin,"CharacterLevelUp");if(!string.IsNullOrEmpty(level))assets["asset:"+level]=level;
        string label=ReflectionValue.Text(skin,"CharacterLabel");
        bundles=bundles.Concat(catalog.Resolve(assets.Values.Concat([label+"_PC",label+"_Walk_PC"]))).Distinct().ToList();
        progress(10,$"读取 {bundles.Count} 个原资源包");resources.Load(bundles,cancellationToken);
        string staging=Path.Combine(manager.Root(game),".template-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            var m=new CharacterSkinManifest{Id=id,Name=name,HeroId=heroId,SkinItemId=selectedSkinItemId,Description=$"从本地游戏皮肤 {selectedSkinItemId} 导出的制作模板",UseOriginalSpriteLayout=true};
            var missing=new List<string>();var metadata=new List<object>();
            progress(20,$"导出所选皮肤 {selectedSkinItemId} 的图片");
            foreach(var (slot,asset) in assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var obj=resources.Objects.FirstOrDefault(x=>x.Info.TypeId==28&&x.Value["m_Name"].AsString==asset);
                if(obj==null){missing.Add(slot+": "+asset);continue;}
                string relative="images/"+asset+".png";resources.ExportTexture(obj,CharacterSkinRules.SafePath(staging,relative));m.Images[slot]=relative;
            }
            var sprites=resources.Objects.Where(x=>x.Info.TypeId==213).ToArray();
            foreach(var obj in sprites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var s=obj.Value;string sprite=s["m_Name"].AsString;
                if(!Regex.IsMatch(sprite,@"^[A-Za-z0-9_-]+_\d+$"))continue;
                if(m.SpriteMappings.ContainsKey(sprite))throw new InvalidOperationException("原精灵名称重复："+sprite);
                string action=sprite[..sprite.LastIndexOf('_')],relative=$"animations/{action}/{sprite}.png";
                resources.ExportSprite(obj,CharacterSkinRules.SafePath(staging,relative));m.SpriteMappings[sprite]=relative;
                metadata.Add(new{Name=sprite,Size=new[]{s["m_Rect"]["width"].AsFloat,s["m_Rect"]["height"].AsFloat},Pivot=new[]{s["m_Pivot"]["x"].AsFloat,s["m_Pivot"]["y"].AsFloat},PPU=s["m_PixelsToUnits"].AsFloat});
                if(m.SpriteMappings.Count%50==0)progress(25+65*m.SpriteMappings.Count/Math.Max(1,sprites.Length),$"已导出 {m.SpriteMappings.Count} / {sprites.Length} 张动画帧");
            }
            progress(92,"读取动画顺序与播放参数");
            foreach(var obj in resources.Objects.Where(x=>x.Info.TypeId==74))
            {
                var c=obj.Value;var a=new CharacterAnimation{Fps=c["m_SampleRate"].AsFloat,Loop=c["m_MuscleClip"]["m_LoopTime"].AsBool};
                foreach(var pointer in c["m_ClipBindingConstant"]["pptrCurveMapping"]["Array"].Children)
                {
                    var sprite=resources.Resolve(obj,pointer);if(!m.SpriteMappings.TryGetValue(sprite.Value["m_Name"].AsString,out string? relative)){a.Frames.Clear();break;}a.Frames.Add(relative);
                }
                if(a.Frames.Count>0)m.Animations[c["m_Name"].AsString]=a;
            }
            if(m.Images.Count==0||m.Animations.Count==0)throw new InvalidOperationException($"来源皮肤 {selectedSkinItemId} 的图片或动画未完整定位，请先在游戏中查看该皮肤以下载资源");
            progress(97,"校验并保存模板");
            CharacterSkinRules.Validate(m,staging,true);
            File.WriteAllText(Path.Combine(staging,"character-skin.json"),JsonSerializer.Serialize(m,CharacterSkinManager.JsonOptions));
            File.WriteAllText(Path.Combine(staging,"source-metadata.json"),JsonSerializer.Serialize(new{HeroId=heroId,SourceSkinItemId=selectedSkinItemId,Catalog=catalog.CatalogPath,Bundles=bundles,Missing=missing,Sprites=metadata},CharacterSkinManager.JsonOptions));
            File.WriteAllText(Path.Combine(staging,"README.md"),$"# 角色皮肤制作模板\n\n人物 ID：{heroId}\n来源皮肤 ID：{selectedSkinItemId}\n\n由 Toys 的 C# 提取器从本地安装目录和热更新缓存导出。保持动画帧画布、透明边缘和脚底位置。原精灵映射沿用游戏时序与锚点；语音、视频、特效保留原版。\n\n缺失资源：\n"+(missing.Count==0?"无":string.Join('\n',missing)));
            cancellationToken.ThrowIfCancellationRequested();
            if(Directory.Exists(target))throw new InvalidOperationException("方案已存在");Directory.Move(staging,target);
            return new(m.Images.Count,m.Animations.Count,m.SpriteMappings.Count,missing.ToArray());
        }
        finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
}
