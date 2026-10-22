using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ProtoBuf;
using ProtoUntyped;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ResourceModLoader.Mod.Patch
{
    class ProtobufPatch : IPatch
    {
        AssetTypeValueField field = null;
        private ReaderMessage protoObject;
        protected bool isDebug = false;

        public void Init(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            field = manager.GetBaseField(assets, file);
            var data = field["m_Script"].AsByteArray;
            protoObject = new ReaderMessage(data);
        }

        public bool PerformPatch(string source)
        {
            var patches = File.ReadAllText(source).Split("\n");
            var applyList = new List<Tuple<int, int, byte[]>>();
            bool hasPat = false;
            foreach (var pat in patches)
            {
                if (!pat.Contains("="))
                    continue;
                var p = pat.Trim().Split("=", 2);
                var p1 = p[0].Trim();
                var target = p[1].Trim();

                bool isAdd = false;
                int toAddId = -1;
                int lastPath = -1;
                if (p1.EndsWith("*"))
                {
                    isAdd = true;
                    p1 = p1.Substring(0,p1.Length - 1);
                    if (p1.EndsWith("[]"))
                    {
                        p1 = p1.Substring(0, p1.Length - 2);
                        string last= p1.Substring(p1.LastIndexOf(".") + 1);
                        lastPath= int.Parse(last);
                    }
                    else if (p1.EndsWith("]"))
                    {
                        Report.Warning(source, $"文本修补 {p1} 的新键语法不被支持");
                        continue;
                    }
                    else
                    {
                        string toAdd = p1.Substring(p1.LastIndexOf(".") + 1);
                        toAddId = int.Parse(toAdd);
                        p1 = p1.Substring(0, p1.LastIndexOf("."));
                    }
                }
                try
                {
                    var result = protoObject.Query([p1]);
                    if (result.Length != 1 || (result.Length == 0 && lastPath != -1))
                    {
                        Report.Warning(source, $"文本修补 {p1} 对应 {result.Length} 个候选，已跳过");
                        continue;
                    }
                    var r = (ReaderBase)result[0];
                    byte[] bytes;
                    if (target.StartsWith("##"))
                        bytes = Convert.FromBase64String(target.Substring(2));
                    else if (target.StartsWith("0x"))
                        bytes = Convert.FromHexString(target.Substring(2));
                    else
                        bytes = Encoding.UTF8.GetBytes(target);

                    if(!isAdd)
                        r.Replace(new ReaderMessage(bytes, r.Path, r.Parent));
                    else if (r is ReaderMessage rm)
                    {
                        if(lastPath != -1)
                        {
                            var sub = ((ReaderMessage)(rm.Parent)).Sub[lastPath];
                            sub.Add(new ReaderMessage(bytes, $"{rm.Parent.Path}.{lastPath}.{sub.Count}", r.Parent));
                            r.Parent.MarkChildReplaced();
                        }else if(toAddId != -1)
                        {
                            rm.Sub[toAddId]=new List<ReaderBase> { new ReaderMessage(bytes, $"{rm.Parent.Path}.{lastPath}.{0}", r.Parent) };
                            r.MarkChildReplaced();
                        }
                    }
                    else
                    {
                        Report.Warning(source, $"文本修补 {p1} 尝试添加字段到非Message路径上");
                    }

                    hasPat = true;
                }
                catch (Exception e)
                {
                    Report.Warning(source, $"文本修补 {p1} 读取失败 {e}");
                }
            }
            return hasPat;
        }
        public void Finalize(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            field["m_Script"].AsByteArray = protoObject.GetBytes() ;
            if(isDebug)
                File.WriteAllBytes(field["m_Name"].AsString+".buf", protoObject.GetBytes());
            file.SetNewData(field);
        }
        public void UseDebug()
        {
            this.isDebug = true;
        }
    }
}
