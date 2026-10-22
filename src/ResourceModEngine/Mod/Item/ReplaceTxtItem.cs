using AssetsTools.NET.Extra;
using ResourceModLoader.Module;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static ResourceModLoader.Mod.Item.ModJsonItem;

namespace ResourceModLoader.Mod.Item
{
    class ReplaceTxtItem : IModItem
    {
        List<Tuple<string, string, string, string>> patches = new List<Tuple<string, string, string, string>>();
        List<Tuple<string, string, string, string>> redirections = new List<Tuple<string, string, string, string>>();
        public ReplaceTxtItem(int priority,string txtPath) : base(priority)
        {
            string content = File.ReadAllText(txtPath);
            string basePath = Path.GetDirectoryName(txtPath);
            string[] files = content.Split('\n');
            foreach (string file in files)
            {
                if (file.Trim().StartsWith("#"))
                    continue;
                string[] def = file.Split(':');
                if (def.Length < 2)
                    continue;
                string name = def[0];
                string bundle = def[1];
                string req = def.Length < 3 ? def[1] : def[2];
                string container = def.Length < 4 ? "" : def[3];
                string bundleFile = Path.Combine(basePath, bundle);
                var r = new Tuple<string, string, string, string>(name.Trim(),bundleFile.Trim(), container.Trim(), req.Trim());
                if (name.EndsWith(".bundle"))
                    patches.Add(r);
                else
                    redirections.Add(r);
            }
        }
        override public void Apply(ModContext context)
        {
            foreach(var (name, bundleFile, container, req) in redirections)
            {
                context.Redirect(name, bundleFile,container,req);
            }
        }
        override public List<string> GetToPatchBundles(string targetBundleName)
        {
            List<string> bundles = new List<string>();
            foreach (var (name, bundleFile, _,_) in redirections)
            {
                if(name == targetBundleName)
                    bundles.Add(bundleFile);
            }
            return bundles;
        }
        public override List<string>? getHashes(string name)
        {
            List<string> results = new List<string>();
            foreach (var (n, bundleFile, _, _) in redirections)
            {
                if (name == n && File.Exists(bundleFile))
                    results.Add(Convert.ToHexString(MD5.HashData(File.ReadAllBytes(bundleFile))));
            }
            return results;
        }
    }
}
