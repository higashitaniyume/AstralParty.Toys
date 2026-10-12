using System.IO;
using System.Text.Json;
using CesiumLoader.CharacterSkins;
namespace AstralParty.Toys.Services;
public sealed record CharacterImageTarget(int Width,int Height,string Asset,bool Verified);
public static class CharacterImageSize
{
    private static readonly Dictionary<string,int[]> Sizes = ReadSizes();
    private static Dictionary<string,int[]> ReadSizes()
    {
        using var stream=typeof(CharacterImageSize).Assembly.GetManifestResourceStream("AstralParty.Toys.CharacterImageSizes.json")!;
        return JsonSerializer.Deserialize<Dictionary<string,int[]>>(stream)!;
    }
    public static CharacterImageTarget Resolve(GameProtocolContext protocol,string data,CharacterSkinManifest manifest,string slot)
    {
        var characters=protocol.Decode("CharacterConfigure",File.ReadAllBytes(Path.Combine(data,"Character.bin")));
        var hero=ReflectionValue.Items(ReflectionValue.Get(characters,"Infos")).FirstOrDefault(x=>ReflectionValue.Int(x,"Id")==manifest.HeroId) ?? throw new InvalidOperationException("角色不存在");
        string asset="";
        if(slot=="CharacterMap") asset=ReflectionValue.Text(hero,"CharacterMap");
        else
        {
            var config=protocol.Decode("SkinConfigure",File.ReadAllBytes(Path.Combine(data,"Skin.bin")));
            var group=ReflectionValue.Items(ReflectionValue.Get(config,"StandingPaintings")).FirstOrDefault(x=>ReflectionValue.Int(x,"Id")==ReflectionValue.Int(hero,"StandingPainting"));
            var skins=ReflectionValue.Items(ReflectionValue.Get(group,"SkinStandingPaintingConfigureItems")).ToArray();
            var skin=manifest.SkinItemId==0?skins.FirstOrDefault():skins.FirstOrDefault(x=>ReflectionValue.Int(x,"ItemID")==manifest.SkinItemId);
            if(skin==null) throw new InvalidOperationException("原皮肤不存在");
            if(slot.StartsWith("Bust")||slot.StartsWith("SfwBust"))
            {
                var bust=ReflectionValue.Items(ReflectionValue.Get(skin,slot.StartsWith("Sfw")?"SfwBust":"Bust")).Select(x=>x.ToString()??"").ToArray();
                int index=slot.EndsWith('1')?1:0;asset=index<bust.Length?bust[index]:"";
            }
            else asset=ReflectionValue.Text(skin,slot);
            if(string.IsNullOrEmpty(asset)&&slot.StartsWith("InGameCharacter")) asset=ReflectionValue.Text(skin,slot.EndsWith("SFW")?"SfwCharacter":"Character");
        }
        if(Sizes.TryGetValue(asset,out var size))return new(size[0],size[1],asset,true);
        // Unknown resources use explicit bounded presets, never pretend these are the game's original size.
        return slot=="CharacterMap"?new(200,200,asset,false):new(1024,1024,asset,false);
    }
}
