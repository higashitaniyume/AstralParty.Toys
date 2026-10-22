using ResourceModLoader.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Tool
{
    class MergedExportTool
    {
        public static void ExportRedirectedBundle(GameModder modder)
        {
            modder.mergeMode = true;
            modder.ProcessMods(false);
            AddressableMgr addressableMgr = modder.addressableMgr;
            BundleScan scan = modder.scan;
            foreach(var (name,from,to) in addressableMgr.GetBundleRedirects())
            {
                var (g,path) = scan.GetBundleGroupPath(name,from);

                string target = Path.Combine(modder.basePath, "exported", g, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                File.Copy(to, target, true);
                Log.SuccessAll($"导出{name} -> {target}");
            }
        }
    }
}
