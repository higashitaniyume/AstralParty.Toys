using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.Item
{
    class BundleItem : IModItem
    {
        private List<Tuple<string, string>> addressables;
        private string bundleName;
        private string bundlePath;
        public BundleItem(int priority,string bundlePath , string bundleName,List<Tuple<string,string>> addressables) : base(priority)
        {
            this.addressables = addressables;
            this.bundleName = bundleName;
            this.bundlePath = bundlePath;
        }


        override public void Apply(ModContext context) {
            foreach(var (name,c) in addressables)
            {
                context.Redirect(name, bundlePath, c, bundleName);
            }
        }
        override public List<String> GetToPatchBundles(string name)
        {
            if(addressables.Count == 0  && bundleName == name)
                return new List<string> { bundlePath };
            return [];
        }
        public override List<string>? getHashes(string name)
        {
            if(name == bundleName)
            {
                return [Convert.ToHexString(MD5.HashData(File.ReadAllBytes(bundlePath)))];
            }
            return [];
        }
    }
}
