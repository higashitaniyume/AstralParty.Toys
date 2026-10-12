using System.IO;
namespace AstralParty.Toys.Services;
public sealed record CharacterSkinTarget(int HeroId, int ItemId, string Name, string Label, string[] Actions);
public static class CharacterSkinCatalog
{
    public static List<CharacterSkinTarget> Load(GameProtocolContext protocol, string directory)
    {
        var result = new List<CharacterSkinTarget>();
        var characters = protocol.Decode("CharacterConfigure",File.ReadAllBytes(Path.Combine(directory,"Character.bin")));
        var skins = protocol.Decode("SkinConfigure",File.ReadAllBytes(Path.Combine(directory,"Skin.bin")));
        var groups = ReflectionValue.Items(ReflectionValue.Get(skins,"StandingPaintings")).ToDictionary(x=>ReflectionValue.Int(x,"Id"));
        foreach(var hero in ReflectionValue.Items(ReflectionValue.Get(characters,"Infos")))
        {
            if(!groups.TryGetValue(ReflectionValue.Int(hero,"StandingPainting"),out var group)) continue;
            foreach(var skin in ReflectionValue.Items(ReflectionValue.Get(group,"SkinStandingPaintingConfigureItems")))
            {
                int id=ReflectionValue.Int(skin,"ItemID");
                string label=ReflectionValue.Text(skin,"CharacterLabel");
                result.Add(new(ReflectionValue.Int(hero,"Id"),id,id.ToString(),label,[]));
            }
        }
        return result;
    }
}
