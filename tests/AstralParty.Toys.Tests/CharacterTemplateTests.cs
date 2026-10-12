using AstralParty.Toys.Services;
using Xunit;
namespace AstralParty.Toys.Tests;
public sealed class CharacterTemplateTests
{
    [Fact] public async Task InvalidIdAndExistingPackageAreRejectedBeforeExtraction()
    {
        var root=Path.Combine(Path.GetTempPath(),"CharacterTemplateTests-"+Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(()=>CharacterTemplateExporter.ExportAsync(root,"missing","../escape","name",114,"missing","missing",_=>{}));
            var manager=new CharacterSkinManager();manager.Create(root,"existing",114,100114001,"keep original");
            var before=File.ReadAllText(Path.Combine(manager.Package(root,"existing"),"character-skin.json"));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>CharacterTemplateExporter.ExportAsync(root,"missing","existing","changed",114,"missing","missing",_=>{}));
            Assert.Equal(before,File.ReadAllText(Path.Combine(manager.Package(root,"existing"),"character-skin.json")));
            Assert.DoesNotContain(Directory.GetDirectories(manager.Root(root)),path=>Path.GetFileName(path).StartsWith(".template-"));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
