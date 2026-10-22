using AssetsTools.NET;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.SpecialProcess
{
    class SpecialManager
    {
        public static ISpecialBundleProcessor[] specialBundleProcessors = {
            new SoundBankProcessor()
        };

        public static ISpecialBundleProcessor? GetSpecialProcessor(AssetFileInfo info,FieldTree field)
        {
            foreach (var processor in specialBundleProcessors)
            {
                if(processor.ShouldHandle(info,field)) return processor;
            }
            return null;
        }

    }
}
