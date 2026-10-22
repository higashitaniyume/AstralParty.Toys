using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ProtoUntyped;
using ResourceModLoader.Module;
using ResourceModLoader.Tool.WWiseTool;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.Patch
{
    class WWiseBankPatch : IPatch
    {
        AssetTypeValueField field;
        WwiseBank bnk;
        public void Init(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            field = manager.GetBaseField(assets, file);
            var dataField = field["RawData.Array"];
            var eventNamesField = field["eventNames.Array"];
            List<string> tStr = new List<string>();
            foreach (var c in eventNamesField.Children)
            {
                tStr.Add(c.AsString);
            }
            //File.WriteAllBytes("4.bnk", dataField.AsByteArray);
            bnk = new WwiseBank(dataField.AsByteArray, tStr.ToArray());
        }

        public bool PerformPatch(string source)
        {
            var sn = Path.GetFileNameWithoutExtension(source);
            if (sn.EndsWith(".patch"))
                sn = sn.Substring(0, sn.Length - 6);
            string targetEventName = (sn + "@@@").Split("@")[3].Trim();
            if(targetEventName == "")
            {
                Report.Warning(source, "WWise BankPatch未设置eventName，将覆盖bnk中的所有声音");
            }
            bool ef = false;
            foreach(var s in bnk.GetAllItems())
            {
                if (!s.EventNames.Contains(targetEventName) && targetEventName != "")
                    continue;
                if (s.Modified)
                {
                    Report.Warning(source, $"修改Bank{s.DescriptorId}:{targetEventName}时与前一个修改发生冲突");
                    continue;
                }
                bnk.ReplaceItemData(s.DescriptorId, File.ReadAllBytes(source));
                ef = true;
            }
            return ef;
        }
        public void Finalize(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            field["RawData.Array"].AsByteArray = bnk.Build();
            //File.WriteAllBytes("3.bnk", field["RawData.Array"].AsByteArray);
            file.SetNewData(field);
        }
    }
}
