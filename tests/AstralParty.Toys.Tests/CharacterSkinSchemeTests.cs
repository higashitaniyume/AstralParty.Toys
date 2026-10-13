using System.IO.Compression;
using System.Text.Json;
using AstralParty.Toys.Services;
using CesiumLoader.CharacterSkins;
using Xunit;

namespace AstralParty.Toys.Tests;
public sealed class CharacterSkinSchemeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "character-scheme-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CharacterSkinManager packages = new();
    private readonly CharacterSkinSchemeManager manager = new(new[] {
        new CharacterSkinTarget(114, 100114001, "A", "A", []), new CharacterSkinTarget(114, 100114002, "B", "B", []),
        new CharacterSkinTarget(102, 100102001, "C", "C", []) });
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private CharacterSkinScheme Scheme(params CharacterSkinBinding[] rules) => new() { Id = "demo", Name = "Demo", Rules = rules.ToList() };
    private static CharacterSkinBinding Rule(string id, int hero, int target = 0, int official = 0, string custom = "") => new() { Id = id, HeroId = hero, TargetSkinItemId = target, OfficialSourceSkinItemId = official, CustomPackageId = custom };
    [Fact] public void MultipleHeroesAndExactBeforeWildcardResolve()
    {
        var scheme = Scheme(Rule("all", 114, official: 100114001), Rule("exact",114,100114002,100114002), Rule("other",102,official:100102001));
        manager.Save(root,"demo",scheme); manager.SetActive(root,"demo");
        var saved = manager.Load(root,"demo");
        Assert.Equal("exact",CharacterSkinSchemeRules.Resolve(saved,114,100114002)!.Id);
        Assert.Equal("all",CharacterSkinSchemeRules.Resolve(saved,114,100114001)!.Id);
        Assert.Equal("other",CharacterSkinSchemeRules.Resolve(saved,102,100102001)!.Id);
        Assert.Null(CharacterSkinSchemeRules.Resolve(saved,999,0));
        Assert.True(Assert.Single(manager.Profiles(root)).Active);
    }
    [Fact] public void CustomPackageCanBeReusedAcrossTargetsRegardlessOfOldTarget()
    {
        packages.Create(root,"custom",114,100114001,"Custom");
        Assert.Equal(100114001,manager.CreateBindingFromPackage(root,"custom","default").TargetSkinItemId);
        manager.Save(root,"demo",Scheme(Rule("a",114,100114001,custom:"custom"),Rule("b",114,100114002,custom:"custom")));
        Assert.Equal(2,manager.Load(root,"demo").Rules.Count);
        Assert.Throws<InvalidOperationException>(() => packages.Delete(root,"custom"));
        manager.Delete(root,"demo"); packages.Delete(root,"custom");
    }
    [Theory] [InlineData("scheme")] [InlineData("rule")] [InlineData("hero")] [InlineData("target")] [InlineData("source")] [InlineData("empty")] [InlineData("duplicate-id")] [InlineData("duplicate-target")]
    public void InvalidBasicRulesAreRejected(string invalid)
    {
        var s = Scheme(Rule("a",114,official:100114001));
        switch(invalid) {
            case "scheme": s.Id="../x"; break; case "rule": s.Rules[0].Id="../x"; break;
            case "hero": s.Rules[0].HeroId=0; break; case "target": s.Rules[0].TargetSkinItemId=-1; break;
            case "source": s.Rules[0].OfficialSourceSkinItemId=-1; break; case "empty": s.Rules[0].OfficialSourceSkinItemId=0; break;
            case "duplicate-id": s.Rules.Add(Rule("A",102,official:100102001)); break;
            case "duplicate-target": s.Rules.Add(Rule("b",114,official:100114002)); break;
        }
        Assert.Throws<InvalidOperationException>(() => CharacterSkinSchemeRules.Validate(s));
    }
    [Fact] public void SharedHeroTextConflictIsRejectedBeforeSavingScheme()
    {
        packages.Create(root,"a",114,100114001,"A");packages.Create(root,"b",114,100114002,"B");
        var a=packages.Load(root,"a");a.Texts["Name"]="A";packages.Save(root,"a",a);
        var b=packages.Load(root,"b");b.Texts["Name"]="B";packages.Save(root,"b",b);
        Assert.Throws<InvalidOperationException>(()=>manager.Save(root,"demo",Scheme(Rule("a",114,100114001,custom:"a"),Rule("b",114,100114002,custom:"b"))));
        Assert.False(Directory.Exists(manager.SchemeDirectory(root,"demo")));
    }
    [Fact] public void OwnershipMissingPackageAndWrongHeroFailWithoutReplacingSavedScheme()
    {
        manager.Save(root,"demo",Scheme(Rule("ok",114,official:100114001)));
        foreach (var bad in new[] { Rule("wrong-official",114,official:100102001), Rule("wrong-target",114,100102001,100114001), Rule("missing",114,custom:"missing") })
            Assert.ThrowsAny<Exception>(() => manager.Save(root,"demo",Scheme(bad)));
        packages.Create(root,"other",102,0,"Other");
        Assert.Throws<InvalidOperationException>(() => manager.Save(root,"demo",Scheme(Rule("wrong-package",114,custom:"other"))));
        Assert.Equal("ok",Assert.Single(manager.Load(root,"demo").Rules)!.Id);
    }
    [Fact] public void MigrationPreservesMultipleHeroesIsIdempotentAndBacksUpSettings()
    {
        packages.Create(root,"a",114,0,"A"); packages.Create(root,"b",102,100102001,"B");
        packages.SetActive(root,"a",true); packages.SetActive(root,"b",true);
        var profiles = manager.Profiles(root);
        var legacy = Assert.Single(profiles); Assert.Equal("legacy",legacy.Id); Assert.True(legacy.Active); Assert.Equal(2,legacy.Scheme.Rules.Count);
        Assert.Empty(packages.Settings(root).ActivePackages);
        Assert.Equal(new[]{"a","b"},JsonSerializer.Deserialize<CharacterSkinSettings>(File.ReadAllText(packages.Config(root)+".legacy-backup"))!.ActivePackages);
        manager.EnsureMigration(root); Assert.Single(manager.Profiles(root));
        manager.SetActive(root,""); Assert.Equal("",packages.Settings(root).ActiveSchemeId); Assert.DoesNotContain(manager.Profiles(root),x=>x.Active);
    }
    [Fact] public void MigrationPreservesDisabledSettingButExplicitActivationEnablesScheme()
    {
        packages.Create(root,"a",114,0,"A");
        Directory.CreateDirectory(Path.GetDirectoryName(packages.Config(root))!);
        File.WriteAllText(packages.Config(root),JsonSerializer.Serialize(new CharacterSkinSettings { Enabled=false, ActivePackages=["a"] }));
        manager.EnsureMigration(root);
        Assert.False(packages.Settings(root).Enabled);
        manager.SetActive(root,"legacy");
        Assert.True(packages.Settings(root).Enabled);
    }
    [Fact] public void MigrationDoesNotOverwriteExistingLegacyScheme()
    {
        manager.Create(root,"legacy","User scheme"); packages.Create(root,"a",114,0,"A"); packages.SetActive(root,"a",true);
        manager.EnsureMigration(root);
        Assert.Equal("legacy-1",packages.Settings(root).ActiveSchemeId); Assert.Equal("User scheme",manager.Load(root,"legacy").Name);
    }
    [Fact] public void MissingDependencyPreventsActivationAndMigrationLeavesOriginalSettings()
    {
        packages.Create(root,"a",114,0,"A"); manager.Save(root,"demo",Scheme(Rule("a",114,custom:"a")));
        Directory.Delete(packages.Package(root,"a"),true);
        Assert.ThrowsAny<Exception>(()=>manager.SetActive(root,"demo")); Assert.Equal("",packages.Settings(root).ActiveSchemeId);
        Directory.CreateDirectory(Path.GetDirectoryName(packages.Config(root))!);
        File.WriteAllText(packages.Config(root),JsonSerializer.Serialize(new CharacterSkinSettings { ActivePackages = ["a"] }));
        Assert.ThrowsAny<Exception>(()=>manager.EnsureMigration(root)); Assert.Equal(new[]{"a"},packages.Settings(root).ActivePackages);
    }
    [Fact] public void FailedMigrationStillAllowsListingAndReplacingBrokenLegacySelection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(packages.Config(root))!);
        File.WriteAllText(packages.Config(root),JsonSerializer.Serialize(new CharacterSkinSettings { ActivePackages=["missing"] }));
        Assert.Empty(manager.Profiles(root));
        Assert.NotNull(manager.MigrationError);
        Assert.Equal(new[]{"missing"},packages.Settings(root).ActivePackages);
        manager.Save(root,"demo",Scheme(Rule("official",114,official:100114002)));
        manager.SetActive(root,"demo");
        Assert.Equal("demo",Assert.Single(manager.Profiles(root)).Id);
        Assert.Null(manager.MigrationError);
        Assert.Empty(packages.Settings(root).ActivePackages);
    }
    [Fact] public void EditingPackageDoesNotChangeActiveSchemeAndDeleteRetainsRecoverableScheme()
    {
        packages.Create(root,"a",114,0,"A"); manager.Save(root,"demo",Scheme(Rule("a",114,custom:"a"))); manager.SetActive(root,"demo");
        var package = packages.Load(root,"a"); package.Texts["Name"]="Edited"; packages.Save(root,"a",package);
        Assert.Equal("demo",packages.Settings(root).ActiveSchemeId);
        manager.Delete(root,"demo"); Assert.Equal("",packages.Settings(root).ActiveSchemeId); Assert.Empty(manager.Profiles(root));
        Assert.Single(Directory.GetDirectories(Path.Combine(manager.Root(root),".deleted")));
    }
    [Fact] public void ExportImportRoundTripIncludesOnlyReferencedCustomPackagesAndNoOfficialResources()
    {
        packages.Create(root,"a",114,100114001,"A"); packages.Create(root,"unused",102,0,"Unused");
        string image=Path.Combine(root,"image.png"); File.WriteAllBytes(image,[1,2,3]); packages.ReplaceImage(root,"a","CharacterMap",image);
        manager.Save(root,"demo",Scheme(Rule("a",114,100114002,100114001,"a"),Rule("b",102,official:100102001)));
        string zip=Path.Combine(root,"export.zip"); manager.Export(root,"demo",zip);
        using (var archive=ZipFile.OpenRead(zip)) { Assert.Contains(archive.Entries,x=>x.FullName=="character-scheme.json"); Assert.DoesNotContain(archive.Entries,x=>x.FullName.Contains("unused") || x.FullName.Contains("official")); }
        string other=Path.Combine(root,"other"); Assert.Equal("demo",manager.Import(other,zip)); Assert.Equal(2,manager.Load(other,"demo").Rules.Count);
        var m=packages.Load(other,"a"); Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(CharacterSkinRules.SafePath(packages.Package(other,"a"),m.Images["CharacterMap"])));
        Assert.Equal("",packages.Settings(other).ActiveSchemeId);
        Assert.Throws<InvalidOperationException>(()=>manager.Import(other,zip));
        string conflicting=Path.Combine(root,"conflict"); packages.Create(conflicting,"a",114,0,"Original");
        Assert.Throws<InvalidOperationException>(()=>manager.Import(conflicting,zip)); Assert.Equal("Original",packages.Load(conflicting,"a").Name); Assert.Empty(manager.Profiles(conflicting));
    }
    [Theory] [InlineData("../escape.png")] [InlineData("packages/a/../../escape.png")] [InlineData("C:/escape.png")] [InlineData("extra.png")] [InlineData("CHARACTER-SCHEME.JSON")]
    public void UnsafeOrUnreferencedArchiveFilesAreRejectedWithNoPartialInstall(string entry)
    {
        Directory.CreateDirectory(root); string zip=Path.Combine(root,"bad.zip");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)) {
            using(var writer=new StreamWriter(archive.CreateEntry("character-scheme.json").Open())) writer.Write(JsonSerializer.Serialize(Scheme(Rule("a",114,official:100114001))));
            using(var writer=new StreamWriter(archive.CreateEntry(entry).Open())) writer.Write("bad");
        }
        Assert.ThrowsAny<Exception>(()=>manager.Import(root,zip)); Assert.Empty(manager.Profiles(root)); Assert.Empty(Directory.GetDirectories(manager.Root(root)));
    }
    private string PackageZip(string id, int hero, int target = 0)
    {
        string source = Path.Combine(root, "source-" + Guid.NewGuid().ToString("N"));
        packages.Create(source, id, hero, target, "Imported " + id);
        string zip = Path.Combine(root, id + "-" + Guid.NewGuid().ToString("N") + ".zip");
        packages.Export(source, id, zip);
        return zip;
    }
    [Fact] public void AttachPackageAppendsAndReplacesRuleWithoutLosingOtherHeroesOrOfficialBase()
    {
        manager.Save(root,"demo",Scheme(Rule("blue",114,100114001,100114002),Rule("other",102,official:100102001)));
        manager.SetActive(root,"demo");
        packages.Create(root,"custom",114,0,"Custom"); packages.Create(root,"replacement",114,100114001,"Replacement");
        manager.AttachPackage(root,"demo",Rule("extra",114,100114002,custom:"custom"));
        manager.AttachPackage(root,"demo",Rule("BLUE",114,100114001,100114002,"replacement"));
        var saved=manager.Load(root,"demo"); Assert.Equal(3,saved.Rules.Count);
        var blue=Assert.Single(saved.Rules,x=>x.Id=="BLUE"); Assert.Equal("replacement",blue.CustomPackageId); Assert.Equal(100114002,blue.OfficialSourceSkinItemId);
        var other=Assert.Single(saved.Rules,x=>x.Id=="other"); Assert.Equal(102,other.HeroId); Assert.Equal(100102001,other.OfficialSourceSkinItemId); Assert.Equal("",other.CustomPackageId);
        Assert.Equal("demo",packages.Settings(root).ActiveSchemeId);
    }
    [Theory] [InlineData("")] [InlineData("../bad")] [InlineData("missing")] [InlineData("wrong-hero")]
    public void AttachPackageInvalidDependencyLeavesSchemeBytesUnchanged(string packageId)
    {
        manager.Save(root,"demo",Scheme(Rule("blue",114,100114001,100114002),Rule("other",102,official:100102001)));
        packages.Create(root,"wrong-hero",102,0,"Other");
        string manifest=Path.Combine(manager.SchemeDirectory(root,"demo"),"character-scheme.json"); string before=File.ReadAllText(manifest);
        Assert.ThrowsAny<Exception>(()=>manager.AttachPackage(root,"demo",Rule("blue",114,100114001,100114002,packageId)));
        Assert.Equal(before,File.ReadAllText(manifest));
    }
    [Fact] public void AttachPackageDuplicateTargetDoesNotReplaceExistingRule()
    {
        packages.Create(root,"custom",114,0,"Custom");
        manager.Save(root,"demo",Scheme(Rule("blue",114,100114001,100114002),Rule("other",102,official:100102001)));
        string manifest=Path.Combine(manager.SchemeDirectory(root,"demo"),"character-scheme.json"); string before=File.ReadAllText(manifest);
        Assert.Throws<InvalidOperationException>(()=>manager.AttachPackage(root,"demo",Rule("new-id",114,100114001,custom:"custom")));
        Assert.Equal(before,File.ReadAllText(manifest)); Assert.Equal("",Assert.Single(manager.Load(root,"demo").Rules,x=>x.Id=="blue").CustomPackageId);
    }
    [Fact] public void ImportPackageIntoSchemeUsesDefaultRuleAndKeepsOtherHeroesWithoutActivation()
    {
        manager.Save(root,"demo",Scheme(Rule("other",102,official:100102001)));
        string zip=PackageZip("blue",114,100114001);
        Assert.Equal("blue",manager.ImportPackageIntoScheme(root,"demo",zip));
        var saved=manager.Load(root,"demo"); Assert.Equal(2,saved.Rules.Count);
        var added=Assert.Single(saved.Rules,x=>x.CustomPackageId=="blue"); Assert.StartsWith("rule-",added.Id); Assert.Equal(114,added.HeroId); Assert.Equal(100114001,added.TargetSkinItemId);
        Assert.Equal("other",saved.Rules[0].Id); Assert.Equal("",packages.Settings(root).ActiveSchemeId);
    }
    [Fact] public void ImportPackageIntoExistingOfficialRulePreservesOfficialBaseAndDoesNotMutateDraft()
    {
        manager.Save(root,"demo",Scheme(Rule("blue",114,100114002,100114001),Rule("other",102,official:100102001)));
        var draft=Rule("blue",114,100114002,100114001,"placeholder");
        Assert.Equal("custom",manager.ImportPackageIntoScheme(root,"demo",PackageZip("custom",114,100114001),draft));
        var saved=manager.Load(root,"demo"); Assert.Equal(2,saved.Rules.Count);
        var blue=Assert.Single(saved.Rules,x=>x.Id=="blue"); Assert.Equal("custom",blue.CustomPackageId); Assert.Equal(100114001,blue.OfficialSourceSkinItemId); Assert.Equal(100114002,blue.TargetSkinItemId);
        Assert.Equal("placeholder",draft.CustomPackageId); Assert.Equal("other",saved.Rules[1].Id);
    }
    [Theory] [InlineData("wrong-hero")] [InlineData("duplicate-target")]
    public void ImportPackageFailedBindingRollsBackNewPackageAndPreservesScheme(string failure)
    {
        manager.Save(root,"demo",Scheme(Rule("blue",114,100114001,100114002),Rule("other",102,official:100102001)));
        string manifest=Path.Combine(manager.SchemeDirectory(root,"demo"),"character-scheme.json"); string before=File.ReadAllText(manifest);
        int packageHero=failure=="wrong-hero" ? 102 : 114;
        var draft=Rule(failure=="wrong-hero" ? "blue" : "extra",114,100114001,100114002);
        Assert.Throws<InvalidOperationException>(()=>manager.ImportPackageIntoScheme(root,"demo",PackageZip("new-package",packageHero),draft));
        Assert.Equal(before,File.ReadAllText(manifest)); Assert.False(Directory.Exists(packages.Package(root,"new-package")));
        Assert.Single(Directory.GetDirectories(Path.Combine(packages.Root(root),".deleted"))); Assert.Equal("",draft.CustomPackageId);
    }
    [Fact] public void ImportPackageChecksSchemeBeforeInstallingPackage()
    {
        string zip=PackageZip("new-package",114);
        Assert.ThrowsAny<Exception>(()=>manager.ImportPackageIntoScheme(root,"missing",zip));
        Assert.False(Directory.Exists(packages.Package(root,"new-package")));
    }
    [Fact] public void ImportPackageNeverOverwritesOrDeletesAnExistingPackageOnConflict()
    {
        manager.Save(root,"demo",Scheme(Rule("other",102,official:100102001)));
        packages.Create(root,"existing",114,0,"Original");
        Assert.Throws<InvalidOperationException>(()=>manager.ImportPackageIntoScheme(root,"demo",PackageZip("existing",114)));
        Assert.Equal("Original",packages.Load(root,"existing").Name); Assert.Single(manager.Load(root,"demo").Rules);
        Assert.False(Directory.Exists(Path.Combine(packages.Root(root),".deleted")));
    }
    [Fact] public void ImportPackageRollbackPreservesPackageReferencedByAnotherScheme()
    {
        manager.Save(root,"demo",Scheme(Rule("other",102,official:100102001)));
        // An already-invalid external scheme can refer to a not-yet-installed package.
        string external=manager.SchemeDirectory(root,"external"); Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external,"character-scheme.json"),JsonSerializer.Serialize(new CharacterSkinScheme { Id="external",Rules=[Rule("blue",114,custom:"retained")] }));
        var error=Assert.Throws<AggregateException>(()=>manager.ImportPackageIntoScheme(root,"demo",PackageZip("retained",114),Rule("invalid-hero",999,custom:"retained")));
        Assert.Equal(2,error.InnerExceptions.Count); Assert.True(Directory.Exists(packages.Package(root,"retained")));
        Assert.Single(manager.Load(root,"demo").Rules); Assert.Equal("retained",Assert.Single(manager.Load(root,"external").Rules).CustomPackageId);
    }
    [Fact] public void ImportWrongHeroFailsBeforeAnyPackageIsInstalled()
    {
        Directory.CreateDirectory(root); string zip=Path.Combine(root,"wrong.zip");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)) {
            using(var writer=new StreamWriter(archive.CreateEntry("character-scheme.json").Open())) writer.Write(JsonSerializer.Serialize(Scheme(Rule("a",114,custom:"a"))));
            using(var writer=new StreamWriter(archive.CreateEntry("packages/a/character-skin.json").Open())) writer.Write(JsonSerializer.Serialize(new CharacterSkinManifest { Id="a",HeroId=102 }));
        }
        Assert.Throws<InvalidOperationException>(()=>manager.Import(root,zip)); Assert.False(Directory.Exists(packages.Package(root,"a"))); Assert.Empty(manager.Profiles(root));
    }
}
