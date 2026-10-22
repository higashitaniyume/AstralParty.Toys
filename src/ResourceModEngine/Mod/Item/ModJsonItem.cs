using AssetsTools.NET.Extra;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ResourceModLoader.Mod.Item.ModJsonItem;

namespace ResourceModLoader.Mod.Item
{
    class ModJsonItem : IModItem
    {
        public class ModDescription
        {
            public string? Name { get; set; }
            public string? Version { get; set; }
            public string? BaseDir { get; set; }
            public List<string>? Patch { get; set; }
            public List<Bundle>? Bundle { get; set; }
            public List<Add>? Add { get; set; }
            public List<Redirect>? Redirect { get; set; }
        }
        public class Bundle
        {
            public string Target { get; set; }
            public string File { get; set; }
        }
        public class Add
        {
            public string Name { get; set; }
            public string File { get; set; }
            public string? WrapType {  get; set; }
            /// <summary>Addressable InternalId（pathID）。有值时强制写入，即使同名 key 已存在。</summary>
            public string? Container { get; set; }
            public string Reference { get; set; }
        }
        public class Redirect
        {
            public string Name { get; set; }
            public string File { get; set; }
            public string? WrapType { get; set; }
            public string? Container { get; set; }
        }
        ModDescription content;
        string file;
        public ModJsonItem(int priority,string jsonPath) : base(priority)
        {
            file = jsonPath;
            content = JsonSerializer.Deserialize<ModDescription>(File.ReadAllText(jsonPath));
            if(content == null)
            {
                Report.Error(file, "非法的Mod JSON");
                return;
            }
            if(content.Version !=  null && content.Version != "" && Util.versionCompare(Program.VERSION,content.Version) < 0)
            {
                Log.Warn($"{file}要求RML版本不低于{content.Version}，当前版本是{Program.VERSION}");
                Report.Error(file, $"{file}要求RML版本不低于{content.Version}，当前版本是{Program.VERSION}");
                content = null;
                return;
            }
            string name = Path.GetFileName(Path.GetDirectoryName(file));
            if(content.Name != null)
            {
                name = content.Name;
            }
            if (name != null)
                Report.AddModPack(file, name);
        }
        private string GetBaseDir()
        {
            string b = Path.GetDirectoryName(file);
            if (content.BaseDir != null)
                b = Path.Combine(b, content.BaseDir);
            return b;
        }
        public override void Init(ModContext context, AddressableMgr addressableMgr, BundleScan bundleScan)
        {
            if (content != null && content.Patch != null)
            {
                foreach (var item in content.Patch)
                {
                    context.Add(new CommonPatchItem(priority, Path.Combine(GetBaseDir(), item)));
                    Report.SetModPack(Path.Combine(GetBaseDir(), item), this.file);
                }
            }
            if (content != null && content.Bundle != null)
            {
                foreach (var item in content.Bundle)
                {
                    Tuple<string, List<Tuple<string, string>>> tb = bundleScan.CalculateToReplaceItems(Path.Combine(GetBaseDir(), item.File), item.Target);
                    if (tb.Item1 == "UNK")
                        Report.Error(file,$"{item.Target}无法找到对应文件");
                    item.Target = tb.Item1;
                }
            }
        }
        override public void Apply(ModContext context)
        {
            if (content == null) return;
            string b = GetBaseDir();
            if (content.Redirect != null)
                foreach (var redirect in content.Redirect)
                {
                    string file = Path.Combine(b, redirect.File);
                    string? container = redirect.Container;
                    if(redirect.WrapType != null)
                    {
                        (file,container) = AutoWrap(redirect.Name, file,redirect.WrapType);
                    }
                    if (file == "") continue;
                    if (container == null)
                        container = "";
                    context.Redirect(redirect.Name, file, container,"");
                    Report.SetModPack(file, this.file);
                }
            if (content.Add != null)
                foreach (var add in content.Add)
                {
                    string file = Path.Combine(b, add.File);
                    string? container = add.Container;
                    if(add.WrapType != null)
                        (file, container) = AutoWrap(add.Name, file, add.WrapType);
                    if (file == "") continue;
                    // null = mod.json 未写 Container；空串仅当显式写了 ""。有值则 NewAddressableName 会强制覆盖已有条目。
                    context.NewItem(add.Name, file, container ?? "", add.Reference);
                    Report.SetModPack(file, this.file);
                }
        }
        private Tuple<string,string> AutoWrap(string Name,string filePath,string WrapType)
        {
            string container="";
            string file="";
            if (WrapType != null)
            {
                if (WrapType == "text")
                {
                    file = AB.CreateTextAbSingle(filePath, Name);
                    container = "2";
                }
                else if (WrapType == "image")
                {
                    file = AB.CreateImageAbSingle(filePath, Name);
                    container = "d";
                }
                else
                {
                    Report.Warning(filePath, $"{WrapType}不是合法的包装类型(image,text)");
                    return new Tuple<string, string>("","");
                }
                file = Path.Combine(Path.GetDirectoryName(filePath), "_generated", file);
            }
            return new Tuple<string, string>(file, container);
        }
        override public List<string> GetToPatchBundles(string targetBundleName)
        {
            if (content == null || content.Bundle == null) return [];
            List<string> bundles = new List<string>();
            foreach (var patch in content.Bundle)
            {
                if (patch.Target == targetBundleName)
                {
                    bundles.Add(Path.Combine(GetBaseDir(), patch.File));
                    Report.SetModPack(Path.Combine(GetBaseDir(), patch.File), this.file);
                }
            }
            return bundles;
        }
        public override List<string> getHashes(string name)
        {
            if (content == null) return [];
            List<string> hashes = new List<string>();
            if(content.Bundle != null)
                foreach (var patch in content.Bundle)
                {
                    if (patch.Target == name && File.Exists(Path.Combine(GetBaseDir(), patch.File)))
                    {
                        hashes.Add(Convert.ToHexString(MD5.HashData(File.ReadAllBytes(Path.Combine(GetBaseDir(), patch.File)))));
                    }
                }
            return hashes;
        }
    }
}
