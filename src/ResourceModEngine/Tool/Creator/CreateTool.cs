using AssetsTools.NET.Texture.TextureDecoders.CrnUnity;
using ResourceModLoader.Mod.Item;
using ResourceModLoader.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ResourceModLoader.Mod.Item.ModJsonItem;
using System.Xml.Linq;
using System.Diagnostics;
using ResourceModLoader.Mod.Patch;
using ResourceModLoader.Tool.WWiseTool;

namespace ResourceModLoader.Tool.Creator
{
    class CreateTool
    {
        public static void Invoke(GameModder modder)
        {
            var cli = new CLI();

            var name = cli.WaitInputText("输入Mod包的文件夹名字","");
            if (name == "") return;
            var path = Path.Combine(modder.modPath, name);
            if (!Path.Exists(path)) Directory.CreateDirectory(path);
            ModDescription mod = new ModDescription() ;
            if (Path.Exists(Path.Combine(path, "mod.json")))
                mod = JsonSerializer.Deserialize<ModDescription>(File.ReadAllText(Path.Combine(path, "mod.json")));
            else
            {
                if (cli.ShowMessage("是否要将mod资产放入data目录内，而不是平铺在同一目录下？",true)) 
                    mod.BaseDir = "data";
            }
            if(mod.Patch == null)
                mod.Patch = new List<string>();
            if(mod.Add == null)
                mod.Add = new List<Add>();
            if(mod.Redirect == null) 
                mod.Redirect = new List<Redirect>();
            if(mod.Bundle == null)
                mod.Bundle = new List<Bundle>();
            string dataPath = Path.Combine(path, mod.BaseDir == null ? "" : "data");
            if(!Path.Exists(dataPath))
                Directory.CreateDirectory(dataPath);
            while (true)
            {
                UpdateCliInfo(mod, cli, path);
                var select = cli.WaitSelect("选择一项操作", ["添加图片并添加重定向", "添加文本资产并添加重定向", "提取bundle并添加修补", "提取文本并添加修补", "导入新的FGUI包", "替换SoundBank音频", "删除项目", "写出安装程序" ,"重新应用mods", "结束"]);
                if (select == 0)
                    AddWrapableAndRedirect(mod, cli, dataPath, modder.addressableMgr, "image", "VA11HallA_atlas0", "png等图像文件");
                else if (select == 1)
                    AddWrapableAndRedirect(mod, cli, dataPath, modder.addressableMgr, "text", "ProductRecommendation_fui", "各种文本（或类似二进制）资产");
                else if (select == 2)
                    FindBundleAndRedirect(mod, cli, dataPath, modder.addressableMgr, modder.scan);
                else if (select == 3)
                    FindTextAndPatch(cli, mod, modder, dataPath);
                else if (select == 4)
                    ImportFGUIPacket(cli, mod, modder, dataPath);
                else if (select == 5)
                    CreateWWiseBnk.FindSoundBankAndPatch(cli, mod, modder, dataPath);
                else if (select == 6)
                    RemoveItem(cli, mod, dataPath);
                else if (select == 7)
                    WriteInfo(mod, cli, modder.modPath,modder.appName, name);
                else if (select == 8)
                {
                    modder.ProcessMods(true);
                    Log.Info("按任意键返回Create tool");
                    Log.Wait();
                }
                else break;
                File.WriteAllText(Path.Combine(path, "mod.json"), JsonSerializer.Serialize(mod));
            }
        }
        public static void UpdateCliInfo(ModJsonItem.ModDescription mod,CLI cli,string path)
        {
            int totalCount = 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("Mod 工作区 ").Append(path).AppendLine();

            sb.Append(" +修补(").Append(mod.Patch.Count).Append(")").AppendLine();
            totalCount += mod.Patch.Count;
            if (mod.Patch.Any())
            {
                var item = mod.Patch.Last();
                sb.Append("   最后一项 > ").Append(item).AppendLine();
            }

            sb.Append(" +添加Addr(").Append(mod.Add.Count).Append(")").AppendLine();
            totalCount += mod.Add.Count;
            if (mod.Add.Any())
            {
                var item = mod.Add.Last();
                sb.Append("   最后一项 > ").Append(item.Name).Append(" -> ").Append(item.File).AppendLine();
            }

            sb.Append(" +重定向(").Append(mod.Redirect.Count).Append(")").AppendLine();
            totalCount += mod.Redirect.Count;
            if (mod.Redirect.Any())
            {
                var item = mod.Redirect.Last();
                sb.Append("   最后一项 > ").Append(item.Name).Append(" -> ").Append(item.File).AppendLine();
            }

            sb.Append(" +AB合并(").Append(mod.Bundle.Count).Append(")").AppendLine();
            totalCount += mod.Bundle.Count;
            if (mod.Bundle.Any())
            {
                var item = mod.Bundle.Last();
                sb.Append("   最后一项 > ").Append(item.File);
            }
            cli.SetStatus($"工作区 {Path.GetFileName(path)} 共[{totalCount}]个修改");
            cli.SetInfo(sb.ToString());
        }

        public static void WriteInfo(ModDescription mod,CLI cli, string modDir,string appName,string modName)
        {
            string appinfo = Path.Combine(Path.GetDirectoryName(modDir), appName + "_Data", "app.info");
            if(!File.Exists(appinfo))
            {
                cli.ShowMessage("无法定位 app.info");
                return;
            }
            string[] infos = File.ReadAllLines(appinfo);
            if(infos.Length < 2) {
                cli.ShowMessage("app.info 不合法");
                return;
            }
            File.WriteAllText(Path.Combine(modDir, modName, "possible_names.txt"), "{User}\\AppData\\LocalLow\\" + infos[0] + "\\" + infos[1]);
            if (mod.BaseDir != null)
            {
                File.WriteAllText(Path.Combine(modDir, modName, "copies.txt"), mod.BaseDir+":"+modName+"/"+mod.BaseDir + "\nmod.json:" + modName + "/mod.json");
            }
            else
            {
                StringBuilder copies = new StringBuilder();
                foreach (var p in mod.Patch) copies.AppendLine(p+":"+modName+"/"+p);
                foreach (var a in mod.Add) copies.AppendLine(a.File + ":" + modName + "/" +a.File);
                foreach (var a in mod.Redirect) copies.AppendLine(a.File + ":" + modName + "/" + a.File);
                foreach (var a in mod.Bundle)copies.AppendLine(a.File + ":" + modName + "/" + a.File);
                File.WriteAllText(Path.Combine(modDir, modName, "copies.txt"),copies.ToString()+"\nmod.json:" + modName + "/mod.json");
            }
            string self = Process.GetCurrentProcess().MainModule.FileName;
            File.Copy(self,Path.Combine(modDir,modName,Path.GetFileName(self)),true);
            cli.ShowMessage("完成");
        }
        public static void AddWrapableAndRedirect(ModJsonItem.ModDescription mod, CLI cli, string dir, AddressableMgr addressable,string wrapableType,string wrapableReference,string typeTip)
        {
            var iptName = cli.WaitInputText("请输入文件地址(可以支持"+typeTip+"，留空来取消", "");
            if (iptName == "") return;
            if (!Path.Exists(iptName))
            {
                cli.SetStatus("文件未找到");
                return;
            }
            if (Path.GetDirectoryName(iptName) != dir)
            {
                File.Copy(iptName, Path.Combine(dir, Path.GetFileName(iptName)));
            }
            var possibleName = Path.GetFileNameWithoutExtension(iptName);
            if (!addressable.IsAddressableName(possibleName))
            {
                possibleName = "";
            }
            while (true)
            {
                string name = cli.WaitInputText("输入要替换的文件名(默认:"+possibleName+")", possibleName);
                if (!addressable.IsAddressableName(name))
                {
                    int s = cli.WaitSelect("名称不在Addressable系统中，你想要：", ["重新输入", "添加文件", "取消"]);
                    if (s == 0)
                        continue;
                    if (s == 2) return;
                    if (s == 1)
                    {
                        ModJsonItem.Add add = new ModJsonItem.Add();
                        add.Name = name;
                        add.File = Path.GetFileName(iptName);
                        add.WrapType = wrapableType;
                        add.Reference = wrapableReference;
                        mod.Add.Add(add);
                        return;
                    }
                }

                ModJsonItem.Redirect redirect = new ModJsonItem.Redirect();
                redirect.Name = name;
                redirect.File = Path.GetFileName(iptName);
                redirect.WrapType = wrapableType;
                mod.Redirect.Add(redirect);
                return;
            }
        }
        public static void FindBundleAndRedirect(ModJsonItem.ModDescription mod, CLI cli, string dir, AddressableMgr addressable,BundleScan bundle)
        {
            var name = cli.WaitInputText("请输入资产名或者AB包名","");
            if (name == "") return ;

            string targetBundleName = "";
            var ava = addressable.GetFirstAvailableResourceLocationList(name);
            if (ava.Any())
            {
                if(ava.First().DependencyKey != null)
                    targetBundleName = ava.First().DependencyKey.ToString();
                if(ava.First().Dependencies != null && ava.First().Dependencies.Any())
                    targetBundleName = ava.First().Dependencies[0].PrimaryKey;
            }
            if(targetBundleName == "")
            {
                if(!cli.ShowMessage("当前名称不在Addressable中，要从Bundle中直接查找吗？这可能会非常慢", true))return;
                cli.Clear();
                var l = bundle.GetAllBundleContainerName();
                foreach(var d in l)
                {
                    foreach(var (_,n) in d.Value)
                    {
                        if(n == name)
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
                cli.ShowMessage("文件未找到");
                return;
            }
            var bundlePath = bundle.GetBundleLocalPath(targetBundleName);
            if (bundlePath == null || bundlePath == "") return;
            string targetPath = Path.Combine(dir,targetBundleName);
            File.Copy(bundlePath, targetPath, true);
            ModJsonItem.Bundle b = new ModJsonItem.Bundle();
            b.File = targetBundleName;
            b.Target = targetBundleName;
            mod.Bundle.Add(b);
            cli.ShowMessage("文件已复制到"+targetPath+",修改后保存即可");
        }

        public static void FindTextAndPatch(CLI cli,ModDescription mod,GameModder modder,string dir)
        {
            string name = cli.WaitInputText("输入Configure文件名(将从全部bundle中搜索，会耗时较久)");
            if (name == "") return;
            var l = modder.scan.GetAllBundleContainerName();
            foreach (var d in l)
            {
                foreach (var (c, n) in d.Value)
                {
                    if (n == name)
                    {
                        var (m,a) = modder.scan.GetBundle(d.Key);

                        foreach(var file in a.file.AssetInfos)
                        {
                            var field = m.GetBaseField(a, file);
                            if (field == null || field["m_Name"].IsDummy)
                                continue;
                            if (field["m_Name"].AsString != name) continue;

                            var dataField = field["m_Script"];
                            if (dataField == null || dataField.IsDummy)
                            {
                                cli.ShowMessage("不是合法的TextAsset");
                                return;
                            }
                            var o = ProtoExportTool.Export(dataField.AsByteArray, dir, false, d.Key, c, name);
                            mod.Patch.Add(Path.GetFileName(o));
                            cli.ShowMessage("文件已导出到" + o + ",删除不需要更改的行，修改你需要更改的字符串，适当修改识别错误的字段后保存");
                            return;
                        }
                    }
                }
            }
            cli.ShowMessage("文件未找到");
            return;
        }

        public static void ImportFGUIPacket(CLI cli, ModDescription mod,GameModder modder, string dir)
        {
            string path = cli.WaitInputText("请输入文件地址（FGUI导出的二进制文件）");
            if (path == "") return;
            List<string> copies = new List<string>();
            FairyGUIPackage package = null;
            try
            {
                package = new FairyGUIPackage(File.ReadAllBytes(path));
                foreach (var s in package.items)
                {
                    if (s.type == GuiItemType.Atlas)
                    {
                        string pp = Path.Combine(Path.GetDirectoryName(path), $"{package.pkgName}_{s.path}");
                        if (!Path.Exists(pp))
                        {
                            cli.ShowMessage($"atlas {Path.GetFileName(pp)} 未找到。检查导出文件名和包名是否一致");
                            return;
                        }
                        copies.Add(pp);
                    }
                }
            }catch(Exception e)
            {
                cli.ShowMessage($"无法解析FGUI文件{e.Message}");
            }
            if (package == null) return;
            string name = cli.WaitInputText("输入FUI文件名，一般以_fui结尾(将从全部bundle中搜索，会耗时较久)");
            if (name == "") return;
            var l = modder.scan.GetAllBundleContainerName();
            foreach (var d in l)
            {
                foreach (var (c, n) in d.Value)
                {
                    if (n == name)
                    {
                        var (m, a) = modder.scan.GetBundle(d.Key);

                        foreach (var file in a.file.AssetInfos)
                        {
                            var field = m.GetBaseField(a, file);
                            if (field == null || field["m_Name"].IsDummy)
                                continue;
                            if (field["m_Name"].AsString != name) continue;

                            var dataField = field["m_Script"];
                            if (dataField == null || dataField.IsDummy)
                            {
                                cli.ShowMessage("不是合法的TextAsset");
                                return;
                            }
                            string targetFileName = $"{name}@{d.Key}@{c}.patch.fgui";
                            File.Copy(path, Path.Combine(dir, targetFileName), true);
                            foreach (var cp in copies)
                                File.Copy(cp, Path.Combine(dir, Path.GetFileName(cp)), true);

                            var op = new FairyGUIPackage(field["m_Script"].AsByteArray);
                            StringBuilder sb = new StringBuilder();
                            foreach(var pa in package.items)
                            {
                                if (pa.raw[9] != 0 && pa.name != null)
                                    sb.Append(pa.name).Append(" => ").Append($"ui://{op.pkgId}{pa.id}").AppendLine();
                            }
                            mod.Patch.Add(targetFileName);
                            cli.SetInfo(sb.ToString());
                            cli.ShowMessage($"{package.pkgName} 已被合并到 {op.pkgName}。请注意，你需要根据新的URL来使用资源");
                            return;
                        }
                    }
                }
            }
            cli.ShowMessage("文件未找到");
            return;
        }

        public static void RemoveItem(CLI cli,ModDescription mod,string dir)
        {
            var s = cli.WaitSelect("选择一个项目", ["修补", "添加", "重定向", "AB合并","取消"]);
            List<string> items = new List<string>();
            List<string> files = new List<string>();
            if (s == 4) return;
            if (s == 0)
                foreach (var p in mod.Patch)
                {
                    items.Add(p);
                    files.Add(Path.Combine(dir,p));
                }
            else if(s == 1)
                foreach (var a in mod.Add)
            {
                items.Add(a.Name+" -> "+a.File);
                files.Add(Path.Combine(dir, a.File));
            }
            else if(s == 2)
                foreach (var a in mod.Redirect)
                {
                    items.Add(a.Name + " -> " + a.File);
                    files.Add(Path.Combine(dir, a.File));
                }
            else if(s == 3)
                foreach (var a in mod.Bundle)
                {
                    items.Add(a.Target + " -> " + a.File);
                    files.Add(Path.Combine(dir, a.File));
                }

            items.Add("取消");
            files.Add("");

            var i = cli.WaitSelect("选择项目", items);
            var toDel = files[i];
            if (toDel == "") return;
            if (s == 0)
                mod.Patch.RemoveAt(i);
            if (s == 1)
                mod.Add.RemoveAt(i);
            if (s == 2)
                mod.Redirect.RemoveAt(i);
            if (s == 3)
                mod.Bundle.RemoveAt(i);
            if(File.Exists(toDel))
                File.Delete(toDel);
            cli.ShowMessage("成功");
        }
    }
}
