using ResourceModLoader.Tool.Creator;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ResourceModLoader.Mod.Item.ModJsonItem;

namespace ResourceModLoader.Tool.WWiseTool
{
    class WWiseExtractTool
    {
        public static void Invoke(string[] args, GameModder modder)
        {
            if (args.Length < 2)
            {
                Log.Warn("用法： tool wwise-export <文件名> <导出路径>");
                return;
            }
            string name = args[0].Trim('"').Trim();
            string path = args[1].Trim('"').Trim();
            if (File.Exists(path))
            {
                Log.Error("请提供一个输出目录，而不是文件名");
                return;
            }

            string targetBundleName = "";
            var ava = modder.addressableMgr.GetFirstAvailableResourceLocationList(name);
            if (ava.Any())
            {
                if (ava.First().DependencyKey != null)
                    targetBundleName = ava.First().DependencyKey.ToString();
                if (ava.First().Dependencies != null && ava.First().Dependencies.Any())
                    targetBundleName = ava.First().Dependencies[0].PrimaryKey;
            }
            if (targetBundleName == "")
            {
                var l = modder.scan.GetAllBundleContainerName();
                foreach (var d in l)
                {
                    foreach (var (_, n) in d.Value)
                    {
                        if (n == name)
                        {
                            targetBundleName = d.Key;
                            break;
                        }
                    }
                    if (targetBundleName != "") break;
                }
            }

            if (targetBundleName == "")
            {
                Log.Error("无法找到文件");
                return;
            }
            string seekName = name;
            if(seekName.Contains("/"))
                seekName = seekName.Substring(seekName.LastIndexOf("/") + 1);

            var bundle = modder.scan.GetBundle(targetBundleName);
            if (bundle == null)
            {
                Log.Error("无法找到文件");
                return;
            }
            var (m, a) = bundle;
            var container = AB.GetContainerDic(m, a);
            foreach(var f in a.file.AssetInfos)
            {
                var field = m.GetBaseField(a, f);

                if (field == null || field["m_Name"].IsDummy)
                    continue;
                if (field["m_Name"].AsString != seekName) continue;

                var dataField = field["RawData.Array"];
                if (dataField == null || dataField.IsDummy)
                {
                    continue;
                }
                string[] eventNames = [];
                var eventNamesField = field["eventNames.Array"];
                if (!eventNamesField.IsDummy)
                {
                    List<string> tStr = new List<string>();
                    foreach(var c in eventNamesField.Children)
                    {
                        tStr.Add(c.AsString);
                    }
                    eventNames = tStr.ToArray();
                }
                Deal(dataField.AsByteArray, path,$"{seekName}@{targetBundleName}@{container.GetValueOrDefault(f.PathId,"")}",eventNames);
            }

        }
        private static void Deal(byte[] data,string path, string bundleBase, string[] eventNames)
        {
            var ww = new WwiseBank(data, eventNames);
            if(!Directory.Exists(path))
                Directory.CreateDirectory(path);

            foreach(var f in ww.GetAllItems())
            {
                foreach(var e in f.EventNames)
                {
                    var fileName = bundleBase+"@" + e + ".patch.bnk";
                    File.WriteAllBytes(Path.Combine(path, fileName), f.Data);
                }
            }
        }
    }
}
