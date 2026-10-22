using ResourceModLoader.Mod;
using ResourceModLoader.Mod.Item;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader
{
    class GameModder
    {
        public readonly string basePath;
        public readonly string executable = "";
        public readonly (string shell, string[] args) shell = ("", []);
        public readonly string appName = "";
        public readonly string localPath = "";
        public readonly string modPath = "";
        public readonly string presistDir = "";
        public BundleScan scan;
        public AddressableMgr addressableMgr;
        public ModContext modContext;
        public ModRecords modRecord;
        public bool isValid;
        public bool mergeMode = false;
        public bool isDebugMode = false;

        public GameModder(string baseDir, string? modsPath = null) {

            for (int i = 0; i < 2; i++)
            {
                string[] allSubDirs = Directory.GetDirectories(baseDir);
                foreach (string subDir in allSubDirs)
                {
                    if (subDir.EndsWith("_Data"))
                    {
                        appName = Path.GetFileName(subDir.Substring(0, subDir.Length - 5));
                    }
                }
                if (appName != "")
                {
                    break;
                }
                baseDir = Directory.GetParent(baseDir).FullName;
            }
            if(appName == "")
            {
                isValid = false;
                return;
            }
            isValid = true;
            this.basePath = baseDir;
            localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");
            executable = Path.Combine(baseDir, appName + ".exe");
            Log.Debug($"使用 {executable} 作为可执行文件");
            shell = GameExecutableSeeker.AutoFindGameStartupShell(executable);
            Log.Debug($"使用 {shell} 来启动游戏");

            string[] appData = File.ReadAllLines(Path.Combine(baseDir, appName + "_Data", "app.info"));
            if (appData.Length < 2)
            {
                Log.Error("Appinfo 不合法");
                return;
            }
            Log.Debug($"{appData[0]} / {appData[1]} ");
            presistDir = Path.Combine(localPath, appData[0], appData[1], "com.unity.addressables");


            string addressableSettings = File.ReadAllText(Path.Combine(baseDir, appName + "_Data", "StreamingAssets", "aa", "settings.json"));
            int offset1 = addressableSettings.IndexOf("/catalog_") + 9;
            int offset2 = addressableSettings.IndexOf(".hash", offset1);
            string version = addressableSettings.Substring(offset1, offset2 - offset1);
            Log.Debug($"Game Version {version}");

            modPath = modsPath ?? Path.Combine(basePath, "mods");

            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
            }
            ReinitAddressableMgr(version);
        }

        /// <summary>
        /// 重新初始化 AddressableMgr：加载 catalog，并注入 GameDataDir / CacheDir
        /// 供跨服 Add 时解析本机 Bundle 路径与 MonoScript CAB。
        /// </summary>
        public void ReinitAddressableMgr(string version)
        {
            addressableMgr = new AddressableMgr();
            // *_Data：解析 RuntimePath；AssetBundles：解析下载缓存里的 __data
            addressableMgr.GameDataDir = Path.Combine(basePath, appName + "_Data");
            addressableMgr.CacheDir = Path.Combine(presistDir, "AssetBundles");
            try
            {
                addressableMgr.Add(Path.Combine(presistDir, "catalog_" + version + ".json"));
            }
            catch (Exception e)
            {
                Log.Error("Addressable 初始化Catalog失败，请检查" + Path.Combine(presistDir, "catalog_" + version + ".json") + " 是否损坏");
                Log.Error(e.ToString());
                if (e.StackTrace != null)
                    Log.Error(e.StackTrace);
                addressableMgr = null;
                return;
            }

            try
            {
                addressableMgr.Add(Path.Combine(basePath, appName + "_Data", "StreamingAssets", "aa", "catalog.bundle"));
            }
            catch (Exception e)
            {
                Log.Error("Addressable 初始化Catalog失败，请检查" + Path.Combine(basePath, appName + "_Data", "StreamingAssets", "aa", "catalog.bundle") + " 是否损坏");
                Log.Error(e.ToString());
                if (e.StackTrace != null)
                    Log.Error(e.StackTrace);
                addressableMgr = null;
                return;
            }

            modRecord = new ModRecords(this);
            scan = new BundleScan(addressableMgr,modRecord, Path.Combine(basePath, appName + "_Data"), Path.Combine(presistDir, "AssetBundles"));
            modContext = new ModContext(addressableMgr, scan, modRecord,this.isDebugMode);
        }

        // 安装

        public void Install(string[] args)
        {
            InstallAndRecordInfo(args);
            InstallDep();
        }
        void InstallDep()
        {
            string self = Process.GetCurrentProcess().MainModule.FileName;
            string dep = Path.Combine(Path.GetDirectoryName(self), "PVRTexLib.dll");
            if(!Directory.Exists(dep))
            {
                File.WriteAllBytes(dep, Resource1.PVRTexLib);
            }
            if (!Path.Exists(dep) || Path.GetDirectoryName(Path.GetDirectoryName(self)) != basePath)
                return;
            File.Copy(dep, Path.Combine(basePath, Path.GetFileName(dep)), true);
            Log.SuccessAll("已将PVRTexLib.dll拷贝到 " + dep);
        }
        void InstallAndRecordInfo(string[] args)
        {
            int result = 1;
            string lastInstall = "";
            string tVer = "";
            bool firstRun = true;
            if (Path.Exists(Path.Combine(basePath, "rml.info")))
            {
                firstRun = false;
                var infos = (File.ReadAllText(Path.Combine(basePath, "rml.info")) + "\n\n").Split("\n");
                tVer = infos[1];
                lastInstall = infos[2];
                result = Util.versionCompare(Program.VERSION, tVer);
            }
            if (lastInstall != "" && !Path.Exists(Path.Combine(basePath, lastInstall)))
            {
                Log.Warn("虽然有安装记录，但是没有找到对应的可执行程序。重新安装当前版本");
                result = 1;
            }
            if (result == 0)
            {
                Log.Debug("当前版本 " + Program.VERSION);
                return;
            }
            string self = Process.GetCurrentProcess().MainModule.FileName;
            if (result < 0)
            {
                if (Path.GetDirectoryName(self) == basePath)
                {
                    Log.Warn("已从更新的版本 "+tVer+" 降级到 "+Program.VERSION);
                    result = 1;
                }
                else
                {
                    Log.Error("已经安装更新版本，使用更新的版本进行安装");
                    Process.Start(Path.Combine(basePath, lastInstall), args).WaitForExit();
                    Environment.Exit(1);
                    return;
                }
            }


            string targetFileName = lastInstall;
            if (targetFileName == "")
                targetFileName = Path.GetFileName(self);
            if (Path.GetDirectoryName(self) != basePath && Path.GetDirectoryName(Path.GetDirectoryName(self)) != basePath)
            {
                if (lastInstall != "" && Path.Exists(Path.Combine(basePath, lastInstall)))
                {
                    try
                    {
                        File.Delete(Path.Combine(basePath, lastInstall));
                    }
                    catch (Exception e)
                    {
                        Log.Warn("更新失败，无法删除目标文件。请确定不在运行 " + lastInstall);
                        Log.Warn("注意：请稍后再次尝试更新本程序，使用低版本modloader加载新版mod描述可能会产生不可控的影响");
                        return;
                    }
                }
                if (!Path.Exists(Path.Combine(basePath, targetFileName)))
                {
                    File.Copy(self, Path.Combine(basePath, targetFileName));
                    Log.SuccessAll("已将本程序拷贝到 " + Path.Combine(basePath, targetFileName));
                    if (firstRun)
                    {
                        Log.Info("将来如果要撤销该程序的影响，请到该目录下删除mods文件夹后再次运行目录下的该程序");
                        Log.Info("即将复制文件并修补游戏文件，如果你已经了解，请按下回车键来继续操作。这条信息之后不会再显示");
                        Log.Wait();
                    }
                }
            }
            File.WriteAllText(Path.Combine(basePath, "rml.info"), "#这是ResourceModLoader的安装信息记录文件，用于管理版本\n" + Program.VERSION + "\n" + targetFileName);
            Log.Info("版本号更新到" + Program.VERSION);
            try
            {
                ShortcutUtils.CreateShortcut(basePath, "RML启动" + appName, Path.Combine(basePath, targetFileName), "u", "使用RML启动并更新" + appName, executable);
                Log.SuccessAll($"你可以通过当前目录下的“RML启动{appName}”来启动并确保mod被应用到对应的软件");
            }
            catch
            {
                Log.Warn("无法创建快速启动快捷方式");
            }
        }


        // 执行资源
        public void ProcessMods(bool resetAfterDone,bool skipSaveAddressable=false)
        {
            try
            {
                EngineControl.Checkpoint();
                Log.Info("扫描Mods");
                Log.SetupProgress(-1);
                ApplyMod(modPath, 100, true);
                Log.FinalizeProgress("搜索结束");
                EngineControl.Checkpoint();
                ApplyAll();
                EngineControl.Checkpoint();
                if (!skipSaveAddressable)
                    addressableMgr.Save();
                Report.Print(modPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                if (e.StackTrace != null)
                    Log.Error(e.StackTrace);
            }
            if (resetAfterDone)
            {
                addressableMgr.Reset();
                Report.Reset();
                modContext = new ModContext(addressableMgr, scan, modRecord,isDebugMode);
            }
        }

        void ApplyMod(string modPath, int priority, bool isTop = false)
        {
            EngineControl.Checkpoint(modPath);
            if (File.Exists(Path.Combine(modPath, "priority.txt")))
                try { priority = int.Parse(File.ReadAllText(Path.Combine(modPath, "priority.txt"))); } catch (Exception _) { }
            if (File.Exists(Path.Combine(modPath, "优先级.txt")))
                try { priority = int.Parse(File.ReadAllText(Path.Combine(modPath, "优先级.txt"))); } catch (Exception _) { }
            if(File.Exists(Path.Combine(modPath, "rmlver.txt")))
            {
                string v = File.ReadAllText(Path.Combine(modPath, "rmlver.txt")).Trim();
                if(Util.versionCompare(Program.VERSION, v) < 0)
                {
                    Log.Warn($"{modPath}要求RML版本不低于{v}，当前版本是{Program.VERSION}");
                    Report.Error(Path.Combine(modPath, "rmlver.txt"), $"{modPath}要求RML版本不低于{v}，当前版本是{Program.VERSION}");
                    return;
                }
            }

            if (File.Exists(Path.Combine(modPath, "replace.txt")))
            {
                Log.StepProgress("Mod扫描 : " + Path.Combine(modPath, "replace.txt"));
                modContext.Add(new ReplaceTxtItem(priority, Path.Combine(modPath, "replace.txt")));
            }
            else if (File.Exists(Path.Combine(modPath, "mod.json")))
            {
                Log.StepProgress("Mod扫描 : " + Path.Combine(modPath, "mod.json"));
                modContext.Add(new ModJsonItem(priority, Path.Combine(modPath, "mod.json")));
            }
            else
            {
                if (isTop)
                    Report.SetCurrentModPath("");
                var filesAll = Directory.GetFiles(modPath);
                Array.Sort(filesAll);
                foreach (var file in filesAll)
                {
                    EngineControl.Checkpoint(file);
                    Log.StepProgress("Mod扫描 : " + file);
                    Report.AddModFile(file);
                    if ((Path.GetExtension(file).ToLower() == ".bundle" || Path.GetFileName(file) == "__data"))
                    {
                        var list = scan.CalculateToReplaceItems(file);
                        if (this.mergeMode)
                            list = new Tuple<string, List<Tuple<string, string>>>(list.Item1, []);
                        modContext.Add(new BundleItem(priority, file, list.Item1, list.Item2));
                    }
                    if (Path.GetExtension(file).ToLower() == ".zip")
                    {
                        string tp = Zip.ExtractAndGetPath(file);
                        if (tp != "")
                        {
                            string tm = Report.GetCurrentModPath();
                            Report.AddModPack(tp, Path.GetFileNameWithoutExtension(file));
                            Report.SetCurrentModPath(tp);
                            ApplyMod(tp, priority);
                            Report.SetCurrentModPath(tm);
                        }
                    }
                    if (WrappableFileItem.IsValid(file, addressableMgr) && !this.mergeMode)
                        modContext.Add(new WrappableFileItem(priority, file));
                    if(ReplaceFileItem.IsValid(file, addressableMgr) && this.mergeMode)
                        modContext.Add(new ReplaceFileItem(priority, file));
                    if (CommonPatchItem.IsValid(file))
                        modContext.Add(new CommonPatchItem(priority, file));
                }
                var dirs = Directory.GetDirectories(modPath);
                Array.Sort(dirs);
                foreach (var dir in dirs)
                {
                    EngineControl.Checkpoint(dir);
                    if (isTop)
                        Report.SetCurrentModPath(dir);
                    var dirName = Path.GetFileName(dir);
                    if (dirName != "_generated")
                    {
                        ApplyMod(Path.Combine(modPath, dir), priority);
                    }
                }
                if (isTop)
                    Report.SetCurrentModPath("");
            }
        }
        void MergeAndPatchBundles()
        {
            if (!Path.Exists(Path.Combine(basePath, "_generated")))
                Directory.CreateDirectory(Path.Combine(basePath, "_generated"));
            foreach (var bundleName in scan.GetAllBundleName())
            {
                var toPatch = modContext.CollectToPatch(bundleName);
                if (toPatch.Any() || modContext.IsRequiredPatch(bundleName, ""))
                {
                    var hashList = modContext.CollectHashList(bundleName);

                    if (hashList != null)
                    {
                        var bundlePath = scan.GetBundleLocalPath(bundleName);
                        if (bundlePath != "" && Path.Exists(bundlePath))
                        {
                            hashList.Add(Convert.ToHexString(MD5.HashData(File.ReadAllBytes(bundlePath))));
                        }
                    }

                    bool result = true;
                    List<Tuple<string, string, string>> conflicts = [];

                    if (hashList==null || modRecord.requireReApply(bundleName, hashList))
                    {
                        (result, conflicts) = AB.MergeBundles(scan.GetBundleLocalPath(bundleName), toPatch, Path.Combine(basePath, "_generated", bundleName), (m, b, a, p, r) => modContext.PostPatch(bundleName, "", m, b, a, p, r),this.isDebugMode);
                    }
                    else {
                        Log.Info("从缓存的修补结果中加载" + bundleName);
                        conflicts = modRecord.getConflicts(bundleName);
                    }

                    if (result)
                    {
                        modContext.Redirect(bundleName, Path.Combine(basePath, "_generated", bundleName), "", "", true);
                        foreach (var (name, i, c) in conflicts)
                        {
                            Report.Warning(i, $"在修补 {name} 时和 {c} 冲突");
                        }
                        if(hashList != null)
                            modRecord.setSourceHashList(bundleName, hashList,conflicts);
                    }
                    else
                    {
                        if (toPatch.Any())
                        {
                            foreach (var tp in toPatch)
                            {
                                foreach (var (name, _, _) in conflicts)
                                {
                                    modContext.Redirect(bundleName, toPatch[0], "", "", true);
                                    Report.Warning(tp, $" {bundleName} 的修补中存在 {name} 和当前的包不能兼容，无法完成修补，使用重定向进行修补");
                                }
                            }
                        }
                    }
                }
            }
        }
        void ApplyAll()
        {
            modContext.InitMod();
            modContext.Sort();
            MergeAndPatchBundles();
            modContext.ApplyAll();
            modRecord.save();
        }
    }
}
