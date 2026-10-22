using AddressablesTools.Catalog;
using AssetsTools.NET.Extra;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ResourceModLoader.Mod.Item
{
    class ReplaceFileItem : IModItem
    {
        string name;
        string source;
        string ext;
        string bundleName = "";
        public ReplaceFileItem(int priority,string path,string targetName = "",string refName="") : base(priority)
        {
            this.source = path;
            this.name = Path.GetFileNameWithoutExtension(path);
            if (this.name.Contains("@"))
            {
                Report.Error(path, "文件替换模式不支持添加新的文件");
                this.name = "";
                return;
            }
            this.ext = Path.GetExtension(path).ToLower();
        
            if (targetName != "")
                this.name = targetName;
        }

        public static bool IsValid(string path,AddressableMgr addressableMgr)
        {
            string fileName= Path.GetFileNameWithoutExtension(path);
            if (!fileName.Contains('@') && !addressableMgr.IsAddressableName(fileName))
            {
                return false;
            }
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif" || ext == ".bmp")
                return true;
            if (ext == ".txt" || ext == ".text")
                return true;
            return false;
        }
        public override void Init(ModContext context, AddressableMgr addressableMgr, BundleScan bundleScan)
        {
            if (this.name == "") return;
            ResourceLocation? d = null;
            var rll = addressableMgr.GetFirstAvailableResourceLocationList(name);
            if (rll == null || !rll.Any()) return;

            foreach(var rl in rll)
            {
                if(rl.Dependencies !=  null && rl.Dependencies.Any()) {
                    d = rl.Dependencies.First();
                }else if(rl.DependencyKey != null){
                    var dl = addressableMgr.GetFirstAvailableResourceLocationList(rl.DependencyKey.ToString());
                    if(dl != null && dl.Any())
                        d = dl.First();
                }
                if(d != null) break;
            }

            if(d != null)
            {
                bundleName = d.PrimaryKey;
            }
        }
        public override bool RequirePatch(string name, string addressableName)
        {
            return name == bundleName || addressableName == this.name;
        }
        public override void PostPatch(string bundleName, string addressableName, AssetsManager manager, BundleFileInstance bundle, AssetsFileInstance[] assets, Dictionary<long, string>[] patched, List<List<PatchEntry>> patches)
        {
            if (this.bundleName == "" || ext == "" ) return;
            foreach (var asset in assets)
            {
                var container = Utils.AB.GetContainerDic(manager, asset);
                foreach (var file in asset.file.AssetInfos)
                {
                    if (file.GetTypeId(asset.file) == (int)AssetClassID.AssetBundle) continue;
                    var field = manager.GetBaseField(asset, file);
                    if (field == null) continue;
                    var nameField = field["m_Name"];
                    if (nameField == null || nameField.IsDummy) continue;
                    if (nameField.AsString != this.name) continue;

                    if (ext == ".png" || ext == ".jpg" || ext == ".gif" || ext == ".bmp")
                    {
                        AB.SetAssetFieldForTexture(field, source);
                    }
                    else if (ext == ".txt" || ext == ".text")
                    {
                        field["m_Script"].AsByteArray = File.ReadAllBytes(source);
                    }
                    else
                    {
                        Report.Error(source, "无法确定合并入的数据类型");
                        continue;
                    }
                }
            }
        }
        public override List<string>? getHashes(string name)
        {
            if (name == bundleName)
                return [Convert.ToHexString(MD5.HashData(File.ReadAllBytes(source)))];
            return base.getHashes(name);
        }
    }
}
