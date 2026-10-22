using AssetsTools.NET.Extra;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod
{
    internal abstract class IModItem
    {
        public int priority;
        protected IModItem(int priority)
        {
            this.priority = priority;
        }
        virtual public bool MergeToThis(IModItem modItem) { return false; }
        virtual public void Init(ModContext context,AddressableMgr addressableMgr, BundleScan bundleScan) { }
        virtual public void Apply(ModContext context) {  }
        virtual public void PostPatch(string bundleName,string addressableName, AssetsManager manager, BundleFileInstance bundle, AssetsFileInstance[] assets, Dictionary<long, string>[] patched, List<List<PatchEntry>> patches) {  }
        virtual public List<string> GetToPatchBundles(string targetBundleName) { return []; }
        virtual public bool RequirePatch(string name, string addressableName) { return false;}
        virtual public List<string>? getHashes(string name){ return []; }
    }
}
