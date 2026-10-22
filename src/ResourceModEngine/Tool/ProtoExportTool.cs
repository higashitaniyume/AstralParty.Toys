using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ResourceModLoader.Mod.Item.ModJsonItem;
using System.Xml.Linq;

namespace ResourceModLoader.Tool
{
    class ProtoExportTool
    {
        public static void Invoke(string[] args,AddressableMgr addressableMgr,BundleScan scan)
        {
            if(args.Length < 2) {
                Log.Warn("用法： tool proto-export [-b:包含非消息] <文件名> <导出路径>");
                return;
            }
            string name = args[0].Trim('"').Trim();
            string path = args[1].Trim('"').Trim();
            bool containsNonStr = false;
            if(args.Length >= 3)
            {
                containsNonStr = (args[0].Trim('"').Trim() == "-b");
                name = args[1].Trim('"').Trim();
                path = args[2].Trim('"').Trim();
            }

            var names = scan.GetAllBundleName();
            Log.SetupProgress(names.Count);
            foreach (var b in names)
            {
                Log.StepProgress($"查找 {b}");
                var (manager, bundle) = scan.GetBundle(b);
                foreach (var file in bundle.file.AssetInfos)
                {
                    var field = manager.GetBaseField(bundle, file);
                    if (field == null) continue;
                    var nameField = field["m_Name"];
                    if (nameField == null || nameField.IsDummy) continue;
                    if (nameField.AsString != name) continue;
                    var dataField = field["m_Script"];
                    if (dataField == null || dataField.IsDummy)
                    {
                        Log.Error("不是合法的TextAsset");
                        return;
                    }

                    var containers = AB.GetContainerDic(manager, bundle);
                    Export(dataField.AsByteArray, path,containsNonStr, b, containers.GetValueOrDefault(file.PathId, ""), name);
                    return;
                }
            }
            Log.Error("未找到文件");
        }
        public static string Export(byte[] messageBytes, string path,bool containsNonStr, string bundleName,string container,string name)
        {
            var message = new ReaderMessage(messageBytes);

            if (Directory.Exists(path))
            {
                string fn = $"{name}@{bundleName}@{container}.patch.proto";
                path = Path.Combine(path, fn);
            }

            using (var f = File.OpenWrite(path))
            {
                var w = new StreamWriter(f);
                PrintLines(message, "", w, containsNonStr);
                w.Flush();
                Log.SuccessAll("成功导出");
            }
            return path;
        }
        private static void PrintLines(ReaderBase reader,string path, StreamWriter of, bool containsNonStr)
        {
            if(reader is ReaderMessage r)
            {

                if (r.LikelyString)
                {
                    of.WriteLine($"{path}={Encoding.UTF8.GetString(r.Buffer)}");
                }
                else if (r.CouldHaveSub)
                {
                    if (path != "")
                        path += ".";
                    foreach (var s in r.Sub)
                    {
                        for (var i = 0; i < s.Value.Count; i++)
                        {
                            var d = s.Value.Count == 1 ? "" : $"[{i}]";
                            PrintLines(s.Value[i], $"{path}{s.Key}{d}", of, containsNonStr);
                        }
                    }
                }
                else if (containsNonStr)
                {
                    of.WriteLine($"{path}=0x{Convert.ToHexString(r.Buffer)}");
                }
                
            }else if (containsNonStr)
            {
                of.WriteLine($"{path}=0x{Convert.ToHexString(reader.Buffer)}");
            }
        }
    }
}
