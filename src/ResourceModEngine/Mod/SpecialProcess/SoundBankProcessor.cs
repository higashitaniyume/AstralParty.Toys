using AssetsTools.NET;
using ResourceModLoader.Mod.Patch;
using ResourceModLoader.Tool.WWiseTool;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.SpecialProcess
{
    class SoundBankProcessor : ISpecialBundleProcessor
    {
        bool ISpecialBundleProcessor.Handle(FieldTree incomingField, FieldTree originalField)
        {
            var incomingData = incomingField["RawData.Array"];
            var incomingEventNamesField = incomingField["eventNames.Array"];
            var toBePatchedData = incomingField["RawData.Array"];
            var toBePatchedEventNamesField = incomingField["eventNames.Array"];
            if (incomingData.IsDummy || incomingEventNamesField.IsDummy || toBePatchedData.IsDummy || toBePatchedEventNamesField.IsDummy) return false;

            List<string> originalEvent = new List<string>();
            foreach (var c in toBePatchedEventNamesField.Children)
            {
                originalEvent.Add(c.AsString);
            }
            List<string> incomingEvent = new List<string>();
            foreach (var c in incomingEventNamesField.Children)
            {
                incomingEvent.Add(c.AsString);
            }

            var soundOriginal = new WwiseBank(toBePatchedData.AsByteArray,originalEvent.ToArray());
            var soundIncoming = new WwiseBank(incomingData.AsByteArray, incomingEvent.ToArray());
            bool any = false;
            foreach(var incomingItem in soundIncoming.GetAllItems()){
                foreach(var originalItem in soundOriginal.GetAllItems())
                {
                    if (originalItem.Modified || incomingItem.EventNames.Count == 0) continue;
                    if (ListEqual(incomingItem.EventNames, originalItem.EventNames))
                    {
                        soundOriginal.ReplaceItemData(originalItem.DescriptorId,incomingItem.Data);
                        Log.StepProgress(incomingItem.EventNames.FirstOrDefault("")+" 声音已替换",0);
                        any = true;
                        break;
                    }
                }
            }

            if (!any) return false;
            var a = originalField["RawData.Array"];
            a.AsByteArray = soundOriginal.Build();
            return true;
        }

        private bool ListEqual(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            return a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));
        }

        bool ISpecialBundleProcessor.ShouldHandle(AssetFileInfo info,FieldTree field)
        {
            var f1 = field["RawData.Array"];
            var f2 = field["eventNames.Array"];
            return (!f1.IsDummy && !f2.IsDummy);
        }
    }
}
