using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ResourceModLoader.Mod.Item
{
    class WrappableFileItem : IModItem
    {
        string name;
        string path;
        string refName="";
        string container = "";
        string source;
        public WrappableFileItem(int priority,string path,string targetName = "",string refName="") : base(priority)
        {
            this.source = path;
            this.name = Path.GetFileNameWithoutExtension(path) ;
            if (this.name.Contains("@"))
            {
                string[] parts = this.name.Split("@",2);
                this.name= parts[0];
                this.refName = parts[1];
            }
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif" || ext == ".bmp")
            {
                this.path = AB.CreateImageAbSingle(path, this.name);
                this.container = "d";
            }
            else if (ext == ".txt" || ext == ".text")
            {
                this.path = AB.CreateTextAbSingle(path, this.name);
                this.container = "2";
            }
            else throw new ArgumentException();
            if (targetName != "")
                this.name = targetName;
            if(refName != "")
                this.refName = refName;
        }

        public static bool IsValid(string path,AddressableMgr addressableMgr)
        {
            string fileName= Path.GetFileNameWithoutExtension(path);
            if (!fileName.Contains('@') && !addressableMgr.IsAddressableName(fileName))
            {
                return false;
            }
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif" || ext == ".bmp")
                return true;
            if (ext == ".txt" || ext == ".text")
                return true;
            return false;
        }


        override public void Apply(ModContext context) {
            if(path == "" || path == null)
            {
                Report.Error(source, "无法自动包装资产AB");
                return;
            }
            Report.AddModFile(source);
            Report.AddTaintFile(source, name);
            if (context.IsRequiredPatch("-", name))
            {
                string save = Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+ ".patch.bundle");
                AB.MergeBundles(path, [],save, (a,b,c,d,e)=>context.PostPatch(name, name ,a, b,c,d,e));
                path = save;
            }
            if (refName == "")
                context.Redirect(name, path, this.container, "",true);
            else
                context.NewItem(name, path, this.container, refName, true);
        }

        public override List<string>? getHashes(string name)
        {
            if (File.Exists(source))
                return [Convert.ToHexString(MD5.HashData(File.ReadAllBytes(source)))];
            return [];
        }
    }
}
