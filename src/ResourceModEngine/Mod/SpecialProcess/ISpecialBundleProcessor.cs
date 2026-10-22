using AssetsTools.NET;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.SpecialProcess
{
    interface ISpecialBundleProcessor
    {
        public bool ShouldHandle(AssetFileInfo info,FieldTree field);
        public bool Handle(FieldTree incomingField, FieldTree originalField);
    }
}
