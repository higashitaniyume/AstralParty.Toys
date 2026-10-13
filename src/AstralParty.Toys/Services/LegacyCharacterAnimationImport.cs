using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Services;

/// <summary>Bounded, per-bundle conversion. Atlas pixels are retained only for one texture at a time.</summary>
internal sealed class LegacyCharacterAnimationImport(string staging, CancellationToken token)
{
    internal readonly Dictionary<string,string> Sprites = new(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string,CharacterAnimation> Animations = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<string> Media = [];
    readonly List<object> layouts = [];
    readonly HashSet<int> timelineHeroes = [];
    long outputPixels, staticPixels;
    const long MaxFramePixels = 2L * 1024 * 1024 * 1024;
    static readonly Regex SpriteName = new(@"^[A-Za-z0-9_-]+_\d+$", RegexOptions.CultureInvariant);

    internal void ExportBundle(string path, Dictionary<string,string> converted, HashSet<string> names, Action<int,int>? progress = null)
    {
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(path, false); long expanded=0;
            foreach(var block in bundle.file.BlockAndDirInfo.BlockInfos)
                if((expanded+=block.DecompressedSize)>LegacyCharacterSkinImporter.MaxArchiveBytes)throw new InvalidDataException("UnityFS 解压后大小超限。");
            manager.UnloadAll();
        }
        finally { manager.UnloadAll(); }
        using var resources=new CharacterUnityResources();resources.Load([path],token,true);
        foreach(var obj in resources.Objects)
        {
            token.ThrowIfCancellationRequested();
            if(obj.Info.TypeId is not (28 or 74 or 114 or 142 or 213))throw new InvalidDataException("不支持的角色资源类型："+obj.Info.TypeId);
        }
        var sprites=resources.Objects.Where(x=>x.Info.TypeId==213).ToArray();
        var atlases=new HashSet<(AssetsFileInstance,long)>();
        var spriteGroups=new Dictionary<(AssetsFileInstance,long),List<CharacterUnityObject>>();
        foreach(var obj in sprites)
        {
            token.ThrowIfCancellationRequested();
            var texture=resources.Resolve(obj,obj.Value["m_RD"]["texture"]);var key=(texture.File,texture.Info.PathId);
            atlases.Add(key);if(!spriteGroups.TryGetValue(key,out var group))spriteGroups[key]=group=[];group.Add(obj);
        }
        foreach(var texture in resources.Objects.Where(x=>x.Info.TypeId==28))
        {
            token.ThrowIfCancellationRequested();
            int width=texture.Value["m_Width"].AsInt,height=texture.Value["m_Height"].AsInt;CheckSize(width,height);
            if(atlases.Contains((texture.File,texture.Info.PathId)))continue;
            string name=texture.Value["m_Name"].AsString;
            if(string.IsNullOrWhiteSpace(name)||!names.Add(name))throw new InvalidDataException("纹理资源名为空或重复："+name);
            if((staticPixels+=(long)width*height*4)>LegacyCharacterSkinImporter.MaxArchiveBytes)throw new InvalidDataException("静态纹理解码总大小超限。");
            string relative="images/"+converted.Count.ToString("D5")+".png";
            resources.ExportTexture(texture,CharacterSkinRules.SafePath(staging,relative));converted.Add(name,relative);resources.ReleaseTextures();
        }
        int processed=0;
        foreach(var group in spriteGroups.Values)
        {
            foreach(var obj in group)
            {
                token.ThrowIfCancellationRequested();var s=obj.Value;string name=s["m_Name"].AsString;
                if(!SpriteName.IsMatch(name)||Sprites.ContainsKey(name))throw new InvalidDataException("精灵名称无效或重复："+name);
                if(Sprites.Count>=10000)throw new InvalidDataException("动画帧数量超限。");
                int width=checked((int)Math.Round(s["m_Rect"]["width"].AsFloat)),height=checked((int)Math.Round(s["m_Rect"]["height"].AsFloat));CheckSize(width,height);
                if((outputPixels+=(long)width*height*4)>MaxFramePixels)throw new InvalidDataException("动画帧解码总大小超过 2 GiB。");
                string action=name[..name.LastIndexOf('_')],relative="animations/"+action+"/"+name+".png";
                resources.ExportSprite(obj,CharacterSkinRules.SafePath(staging,relative));Sprites.Add(name,relative);
                if(++processed%25==0 || processed==sprites.Length)progress?.Invoke(processed,sprites.Length);
                layouts.Add(new{Name=name,Size=new[]{width,height},Pivot=new[]{s["m_Pivot"]["x"].AsFloat,s["m_Pivot"]["y"].AsFloat},PPU=s["m_PixelsToUnits"].AsFloat});
            }
            resources.ReleaseTextures();
        }
        foreach(var obj in resources.Objects.Where(x=>x.Info.TypeId==74))
        {
            token.ThrowIfCancellationRequested();var c=obj.Value;string name=c["m_Name"].AsString;
            if(!Regex.IsMatch(name,@"^[A-Za-z0-9_-]+$")||Animations.ContainsKey(name))throw new InvalidDataException("动作名称无效或重复："+name);
            var animation=new CharacterAnimation{Fps=c["m_SampleRate"].AsFloat,Loop=c["m_MuscleClip"]["m_LoopTime"].AsBool};
            foreach(var pointer in c["m_ClipBindingConstant"]["pptrCurveMapping"]["Array"].Children)
            {
                var sprite=resources.Resolve(obj,pointer);if(sprite.Info.TypeId!=213||!Sprites.TryGetValue(sprite.Value["m_Name"].AsString,out string? relative))throw new InvalidDataException("动作引用未知精灵："+name);
                animation.Frames.Add(relative);
            }
            if(animation.Frames.Count==0)throw new InvalidDataException("动作没有可转换的精灵帧："+name);
            Animations.Add(name,animation);
        }
        foreach(var obj in resources.Objects.Where(x=>x.Info.TypeId==114))ValidateMetadata(obj,sprites.Length>0);
        if(!resources.Objects.Any(x=>x.Info.TypeId is 28 or 74 or 213)&&!resources.Objects.Any(x=>x.Info.TypeId==114 && Media.Contains(x.Value["m_Name"].AsString)))
            throw new InvalidDataException("资源包没有可转换的图片、动作或已知技能视频。");
    }
    static void CheckSize(int width,int height)
    {
        if(width<=0||height<=0||width>16384||height>16384||(long)width*height*4>64L*1024*1024)throw new InvalidDataException("纹理或动画帧尺寸超限。");
    }
    void ValidateMetadata(CharacterUnityObject obj,bool hasSprites)
    {
        var s=obj.Value;string name=s["m_Name"].AsString;
        bool Has(params string[] fields)=>fields.All(f=>!s[f].IsDummy);
        if(Has("implementation","movieInfo","assetInfo","references"))
        {
            if(!Regex.IsMatch(name,@"^VSkill_Hero[1-9]\d*$")||Media.Contains(name))throw new InvalidDataException("不支持或重复的视频资源："+name);
            var refs=s["references"].Value.AsManagedReferencesRegistry.references;
            if(refs.Count!=1)throw new InvalidDataException("视频资源引用结构无效："+name);
            var r=refs[0];var type=r.type;
            if(r.rid!=s["implementation"]["rid"].AsLong||type.ClassName!="CriSerializedBytesAssetImpl"||type.Namespace!="CriWare.Assets"||type.AsmName!="CriMw.CriWare.Assets.Runtime")throw new InvalidDataException("未知视频实现："+name);
            byte[] data=r.data["data"]["Array"].AsByteArray;
            if(data.Length<32||!data.AsSpan(0,4).SequenceEqual("CRID"u8))throw new InvalidDataException("技能视频不是可识别的 CRI USM："+name);
            Media.Add(name);return;
        }
        if(!hasSprites)throw new InvalidDataException("不支持的 MonoBehaviour："+name);
        bool known=(Has("sprites","textures")&&Regex.IsMatch(name,@"^[A-Za-z0-9_-]+$")) ||
            (Has("m_Tracks","m_DurationMode","m_MarkerTrack")&&Regex.IsMatch(name,@"^Hero[1-9]\d*_Fight[A-Za-z]+$")) ||
            (Has("m_Clips","m_Markers","m_Parent","m_Children")&&Regex.IsMatch(name,@"^(Animation Track|Hit Track|Dodge Track|Wwise Audio Track)( \(\d+\))?$")) ||
            (name=="AnimationPlayableAsset"&&Has("m_Clip","m_ApplyFootIK","m_Loop")) ||
            (Regex.IsMatch(name,@"^(HHShake|LHShake)$")&&Has("_noiseSettings","_amplitudeGain","_frequencyGain")) ||
            (name=="HurtNoise"&&Has("PositionNoise","OrientationNoise")) ||
            (Regex.IsMatch(name,@"^(HeavyHurtCurve|LightHurtCurve)$")&&Has("_xCurve","_yCurve","_zCurve")) ||
            (Regex.IsMatch(name,@"^(HitClip|DodgeClip|WwiseAudioClip)(\(Clone\))*$")&&Has("template"));
        if(!known)throw new InvalidDataException("未知动画附属 MonoBehaviour，不能部分导入："+name);
        var hero=Regex.Match(name,@"^Hero(\d+)_");if(hero.Success)timelineHeroes.Add(int.Parse(hero.Groups[1].Value));
    }
    internal void Apply(CharacterSkinManifest manifest,LegacyCharacterSkinImporter.Target target)
    {
        if(timelineHeroes.Any(h=>h!=target.HeroId)||Media.Any(n=>n!="VSkill_Hero"+target.HeroId))throw new InvalidDataException("动画或技能视频属于其他角色，不能混合导入。");
        var referenced=new HashSet<string>(Animations.Values.SelectMany(a=>a.Frames),StringComparer.Ordinal);
        if(Sprites.Values.Any(p=>!referenced.Contains(p)))throw new InvalidDataException("部分精灵没有被动作引用，不能部分导入。");
        foreach(var (name,path) in Sprites)manifest.SpriteMappings.Add(name,path);
        foreach(var (name,animation) in Animations)manifest.Animations.Add(name,animation);
        if(Sprites.Count>0&&Animations.Count==0)throw new InvalidDataException("精灵帧没有完整动作信息。");
        File.WriteAllText(Path.Combine(staging,"legacy-animation-source.json"),JsonSerializer.Serialize(new{Sprites=layouts,PreservedMedia=Media,EnabledMedia=false,DecodedFrameBytes=outputPixels},CharacterSkinManager.JsonOptions));
    }
}
