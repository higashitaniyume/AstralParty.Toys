using System.IO;
using System.Diagnostics;
using System.Text.Json;
using CesiumLoader.CharacterSkins;
namespace AstralParty.Toys.Services;
public sealed record CharacterTemplateResult(int Images,int Actions,int Frames,string[] Missing);
public static class CharacterTemplateExporter
{
    public static async Task<CharacterTemplateResult> ExportAsync(string game,string cache,string id,string name,int heroId,string dataDirectory,string protocolDirectory,Action<string> progress)
    {
        var manager=new CharacterSkinManager();string target=manager.Package(game,id);
        if(Directory.Exists(target))throw new InvalidOperationException("方案已存在");
        var protocol=new GameProtocolContext(protocolDirectory);
        var characters=protocol.Decode("CharacterConfigure",File.ReadAllBytes(Path.Combine(dataDirectory,"Character.bin")));
        var hero=ReflectionValue.Items(ReflectionValue.Get(characters,"Infos")).FirstOrDefault(x=>ReflectionValue.Int(x,"Id")==heroId)??throw new InvalidOperationException("角色不存在");
        var skins=protocol.Decode("SkinConfigure",File.ReadAllBytes(Path.Combine(dataDirectory,"Skin.bin")));
        var group=ReflectionValue.Items(ReflectionValue.Get(skins,"StandingPaintings")).FirstOrDefault(x=>ReflectionValue.Int(x,"Id")==ReflectionValue.Int(hero,"StandingPainting"));
        var skin=ReflectionValue.Items(ReflectionValue.Get(group,"SkinStandingPaintingConfigureItems")).FirstOrDefault(x=>ReflectionValue.Int(x,"ItemID")==100000000+heroId*1000+1)??ReflectionValue.Items(ReflectionValue.Get(group,"SkinStandingPaintingConfigureItems")).FirstOrDefault()??throw new InvalidOperationException("没有默认皮肤配置");
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
        string staging=Path.Combine(manager.Root(game),".template-"+Guid.NewGuid().ToString("N"));
        string work=Path.Combine(Path.GetTempPath(),"toys-character-export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        try
        {
            string script=Path.Combine(work,"export.py");
            using(var resource=typeof(CharacterTemplateExporter).Assembly.GetManifestResourceStream("AstralParty.Toys.CharacterTemplateExporter.py")??throw new InvalidOperationException("内置导出脚本缺失"))
            using(var destination=File.Create(script))await resource.CopyToAsync(destination);
            string resultPath=Path.Combine(work,"result.json"),statusPath=Path.Combine(work,"status.json"),requestPath=Path.Combine(work,"request.json");
            File.WriteAllText(requestPath,JsonSerializer.Serialize(new{Game=game,Cache=cache,Id=id,Name=name,HeroId=heroId,SkinItemId=ReflectionValue.Int(skin,"ItemID"),Label=ReflectionValue.Text(skin,"CharacterLabel"),Assets=assets,Output=staging,Result=resultPath,Status=statusPath}));
            // Use the installed Python interpreter; no shell expansion or pip installation during export.
            var start=new ProcessStartInfo("python"){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add(script);start.ArgumentList.Add(requestPath);
            Process process;
            try{process=Process.Start(start)??throw new InvalidOperationException("Python 启动失败");}
            catch(Exception ex){throw new InvalidOperationException("导出需要 Python 3 和 UnityPy、Pillow。请安装后重试：python -m pip install UnityPy Pillow",ex);}
            using(process)
            {
                var exit=process.WaitForExitAsync();string last="";
                while(!exit.IsCompleted)
                {
                    if(File.Exists(statusPath))try{using var status=JsonDocument.Parse(await File.ReadAllTextAsync(statusPath));string message=status.RootElement.GetProperty("message").GetString()??"";if(message!=last){last=message;progress(message);}}catch(JsonException){}
                    await Task.WhenAny(exit,Task.Delay(250));
                }
                await exit;
                if(!File.Exists(resultPath))throw new InvalidOperationException("Python 导出未返回结果。请确认 Python 3 可运行，并安装 UnityPy、Pillow。");
                using var result=JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));var r=result.RootElement;
                if(!r.GetProperty("Success").GetBoolean())throw new InvalidOperationException("默认皮肤导出失败："+r.GetProperty("Error").GetString()+"。导出依赖 Python 3、UnityPy 和 Pillow。");
                var manifest=JsonSerializer.Deserialize<CharacterSkinManifest>(File.ReadAllText(Path.Combine(staging,"character-skin.json")),CharacterSkinManager.JsonOptions)!;
                CharacterSkinRules.Validate(manifest,staging,true);
                if(Directory.Exists(target))throw new InvalidOperationException("方案已存在");
                Directory.Move(staging,target);
                return new(r.GetProperty("Images").GetInt32(),r.GetProperty("Actions").GetInt32(),r.GetProperty("Frames").GetInt32(),r.GetProperty("Missing").EnumerateArray().Select(x=>x.GetString()!).ToArray());
            }
        }
        finally
        {
            // Only clean the exact temporary directories created by this invocation.
            if(Directory.Exists(staging))Directory.Delete(staging,true);
            if(Directory.Exists(work))Directory.Delete(work,true);
        }
    }
}
