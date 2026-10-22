using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ResourceModLoader.Module
{
    class ModRecord
    {
        public string Version { get; set; }
        public Dictionary<string, string> BundleMapper { get; set; }
        public Dictionary<string, Patched> BundlePatchSources { get; set; }
        public ModRecord()
        {
            BundleMapper = new Dictionary<string, string>();
            BundlePatchSources = new Dictionary<string, Patched>();
        }
    }
    class Patched
    {
        public string Hash { get; set; }
        public List<List<string>> Conflicts { get; set; }
    }

    class ModRecords
    {
        private AddressableMgr addr;
        private string baseDir;
        private ModRecord modRecord;
        public ModRecords(GameModder modder) {
            addr = modder.addressableMgr;
            baseDir = modder.basePath;
            modRecord = new ModRecord();

            if (Path.Exists(Path.Combine(baseDir, "rml.data"))){
                try
                {
                    var tModRecord = JsonSerializer.Deserialize<ModRecord>(File.ReadAllText(Path.Combine(baseDir, "rml.data")));
                    if (tModRecord != null)
                    {
                        if (tModRecord.Version == Program.VERSION)
                        {
                            modRecord = tModRecord;
                            validAll();
                        }
                    }
                }catch {
                    Log.Warn("无法加载缓存文件");
                }
            }
        }

        private void validAll()
        {
            var keyList = new List<string>(modRecord.BundleMapper.Keys);
            foreach(var k in keyList)
            {
                var target = modRecord.BundleMapper[k];
                if (!addr.IsAddressableName(target))
                {
                    Log.Debug("删除过期的名称映射 " + k);
                }
            }
            var keyModList = new List<string>(modRecord.BundlePatchSources.Keys);
            
        }

        public string getMappedBundle(string bundleName)
        {
            if(modRecord.BundleMapper.ContainsKey(bundleName))
                return modRecord.BundleMapper[bundleName];
            return "";
        }

        public void setBundleMap(string bundleName,string target)
        {
            modRecord.BundleMapper[bundleName] = target;
        }

        private string getSourceHashListConcat(List<string> list)
        {
            var ss = new List<string>(list);
            ss.Sort();
            var hashList = String.Join("", ss);
            return hashList;
        }
        public bool requireReApply(string bundleName, List<string> sourceHashList)
        {
            if (modRecord.BundlePatchSources.ContainsKey(bundleName))
            {
                if (modRecord.BundlePatchSources[bundleName].Hash != getSourceHashListConcat(sourceHashList))
                    return true;
                if(!Path.Exists(Path.Combine(this.baseDir, "_generated", bundleName)))
                    return true;
                return false;
            }
            return true;
        }
        public List<Tuple<string, string, string>> getConflicts(string bundleName)
        {
            if (modRecord.BundlePatchSources.ContainsKey(bundleName))
            {
                var results = new List<Tuple<string, string, string>>();
                foreach (var c in modRecord.BundlePatchSources[bundleName].Conflicts)
                {
                    if (c.Count>= 3)
                        results.Add(new Tuple<string, string, string>(c[0], c[1], c[2]));
                }
                return results;
            }
            return [];
        }
        public void setSourceHashList(string bundleName,List<string> sourceHashList,List<Tuple<string,string,string>> conflicts)
        {
            string hash= getSourceHashListConcat(sourceHashList);
            List<List<string>> cl = new List<List<string>>();
            foreach (var c in conflicts)
            {
                cl.Add([c.Item1, c.Item2, c.Item3]);
            }
            modRecord.BundlePatchSources[bundleName] = new Patched { Hash = hash,Conflicts=cl };
        }

        public void save()
        {
            modRecord.Version = Program.VERSION;
            File.WriteAllText(Path.Combine(baseDir, "rml.data"),JsonSerializer.Serialize<ModRecord>(modRecord));
        }
    }
}
