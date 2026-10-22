using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ResourceModLoader.Mod.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.Patch
{
    interface IPatch
    {
        public void Init(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file);
        public bool PerformPatch(string source);
        public void Finalize(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file);
        public void AfterPatch(CommonPatchItem item,ModContext modContext) { }
        public void UseDebug() { }
    }
}
