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
    class ModContext
    {
        private AddressableMgr  addressableMgr;
        private BundleScan scan;
        public List<IModItem> modItems = new List<IModItem>();
        public List<string> forcePatchNames = new List<string>();
        public 
        Dictionary<string,string> lastRedirect = new Dictionary<string,string>();
        public ModRecords modRecords;
        public bool isDebugMode;
        public ModContext(AddressableMgr mgr, BundleScan scan,ModRecords modRecords, bool isDebugMode = false)
        {
            this.addressableMgr = mgr;
            this.scan = scan;
            this.modRecords = modRecords;
            this.isDebugMode = isDebugMode;
        }
        public void Redirect(string name,string bundleFile,string container,string originalBundle,bool noReport = false)
        {
            if(bundleFile == "")
            {
                Log.Error($"对{name}的重定向目标不存在");
                return;
            }
            if (lastRedirect.ContainsKey(name))
            {
                Report.Error(bundleFile, $"{name}的修改与{lastRedirect[name]}冲突");
                return;
            }
            if (!noReport)
            {
                Report.AddModFile(bundleFile);
                Report.AddTaintFile(bundleFile, name);
            }
            if(addressableMgr.IsAddressableName(name))
            {
                addressableMgr.ApplyBundleMod(name, bundleFile, container, originalBundle);
            }
            else
            {
                Report.Error(bundleFile, "目标名字不存在");
            }
            lastRedirect[name] = bundleFile;
        }
        public void NewItem(string name, string bundleFile,string container,string referenceName,bool noReport=false) {
            if (bundleFile == "")
            {
                Log.Error($"对{name}的添加目标不存在");
                return;
            }
            if (!noReport)
            {
                Report.AddModFile(bundleFile);
                Report.AddTaintFile(bundleFile, name);
            }
            addressableMgr.NewAddressableName(name, bundleFile, container, referenceName);
        }
        public void Add(IModItem modItem)
        {
            for (int i=0;i<modItems.Count;i++)
            {
                if (modItems[i].MergeToThis(modItem)) return;
            }
            modItems.Add(modItem);
        }
        public List<string> CollectToPatch(string name)
        {
            List<string> result = new List<string>();
            foreach(IModItem modItem in modItems)
            {
                var collected = modItem.GetToPatchBundles(name);
                result.AddRange(collected);
            }
            foreach(string r in result)
            {
                Report.AddModFile(r);
                Report.AddTaintFile(r, name);
            }
            return result;
        }
        public void InitMod()
        {
            for (int i = 0; i < modItems.Count; i++)
            {
                modItems[i].Init(this, addressableMgr, scan);
            }
        }
        public List<string> CollectHashList(string name)
        {
            List<string> results = new List<string>();
            foreach (IModItem modItem in modItems)
            {
                var res = modItem.getHashes(name);
                if (res == null) return null;
                results.AddRange(res);
            }
            return results;
        }
        public bool IsRequiredPatch(string name,string addressableName)
        {
            foreach (IModItem modItem in modItems)
            {
                if (modItem.RequirePatch(name, addressableName)) return true;
            }
            return false;
        }
        public void PostPatch(string bundleName, string addressableName, AssetsManager m,BundleFileInstance b,AssetsFileInstance[] a, Dictionary<long, string>[] patched, List<List<PatchEntry>> patches)
        {
            Log.StepProgress("其他修补...", 0);
            foreach(var modItem in modItems)
            {
                modItem.PostPatch(bundleName, addressableName, m, b, a, patched, patches); ;
            }
        }
        public void ApplyAll()
        {
            for (int i = 0; i < modItems.Count; i++)
                modItems[i].Apply(this);
        }
        public void Sort()
        {
            modItems.Sort((a, b) => a.priority - b.priority);
        }
    }
}
