using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Module
{
    class BundleScan
    {
        private AddressableMgr ccd;
        string DEBUG_CT = "";
        string local;
        string cache;
        private ModRecords modRecords;
        bool hasBuiltFileContainer = false;
        List<Tuple<string, string, int>> bundleFiles = new List<Tuple<string, string, int>>();
        List<string> bundleFilesNames = new List<string>();
        Dictionary<string, int> bundleCatalogIndex = new Dictionary<string, int>();
        Dictionary<string, List<Tuple<string, string>>> bundleFileNameContainerList = new Dictionary<string, List<Tuple<string, string>>>();
        Dictionary<string, List<Tuple<long, string>>> bundleFileNamePathList = new Dictionary<string, List<Tuple<long, string>>>();
        public BundleScan(AddressableMgr ccd,ModRecords modRecords, string local, string cache)
        {
            this.ccd = ccd;
            this.local = local;
            this.cache = cache;
            this.modRecords = modRecords;
            foreach(var f in ccd.GetAllBundlesWithCatalog())
            {
                if(!Path.Exists(GetBundleLocalPath(f.Item1)))
                { continue; }
                this.bundleFiles.Add(f);
                this.bundleCatalogIndex[f.Item1] = f.Item3;
            }
            foreach(var (ff,_,_) in bundleFiles)
            {
                bundleFilesNames.Add(ff);
            }
        }
        private void InitFileNameContainerList()
        {
            if (hasBuiltFileContainer) return;
            Log.Warn("正在建立更加完善的索引来辅助匹配未知的文件。这可能会花费更多时间");
            Log.SetupProgress(bundleFiles.Count);
            foreach (var (ff,fn,_) in bundleFiles)
            {
                EngineControl.Checkpoint(ff);
                if (DEBUG_CT != "" && ff != DEBUG_CT) continue;
                Log.StepProgress(ff);
                var (manager,asset)= GetBundle(ff);
                var fileContainers = GetFileContainerListAndPathList(manager, asset);
                if (fileContainers.Item1.Count > 0)
                    bundleFileNameContainerList[ff] = fileContainers.Item1;
                if(fileContainers.Item2.Count > 0)
                    bundleFileNamePathList[ff] = fileContainers.Item2;
            }
            Log.FinalizeProgress("索引完成");
            hasBuiltFileContainer = true;
        }
        public List<string> GetAllBundleName()
        {
            return bundleFilesNames;
        }
        public Tuple<string, List<Tuple<string, string>>> CalculateToReplaceItems(string bundlePath,string ?preferBundleName = null)
        {
            EngineControl.Checkpoint(bundlePath);
            AssetsManager manager = new AssetsManager();
            List<Tuple<string, string>> results = new List<Tuple<string, string>>();

            var incomingBundle = manager.LoadBundleFile(bundlePath);
            var asset = manager.LoadAssetsFileFromBundle(incomingBundle, 0);
            var assetFile = asset.file;
            var hasUnAddressable = false;

            var abdef = assetFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var fab = manager.GetBaseField(asset, abdef);
            string oBundleName = fab["m_Name"].AsString;
            string bundleName = FindBundleFileName(oBundleName);
            if (preferBundleName != null)
            {
                bundleName = preferBundleName;
            }
            Tuple<AssetsManager, AssetsFileInstance>? targetAssetInfo = null;
            if(bundleName != "")
            {
                targetAssetInfo = GetBundle(bundleName);
                if (targetAssetInfo == null)
                    bundleName = "";
            }
            if (bundleName == "")
            {
                bundleName = modRecords.getMappedBundle(oBundleName);
                if(bundleName != "")
                {
                    Log.Info($"Bundle {oBundleName} 被识别为 {bundleName} ");
                    Report.Warning(bundlePath, $"当前文件被识别为 {bundleName}，这不总是正确");
                }

                targetAssetInfo = GetBundle(bundleName);
            }

            if (targetAssetInfo == null)
            {
                var matchedBundle = MatchForKnownBundleByFileContainer(manager, asset);
                if (matchedBundle != "")
                {
                    Log.Info($"Bundle {oBundleName} 被识别为 {matchedBundle} ");
                    Report.Warning(bundlePath, $"当前文件被识别为 {matchedBundle}，这不总是正确");
                    modRecords.setBundleMap(oBundleName, matchedBundle);
                    bundleName = matchedBundle;
                    targetAssetInfo = GetBundle(bundleName);
                }
            }
            if (targetAssetInfo != null)
            {
                hasUnAddressable = ScanForKnownBundle(manager, results, asset, targetAssetInfo.Item1, targetAssetInfo.Item2);
                targetAssetInfo.Item1.UnloadAllBundleFiles();
            }
            else
            {
                Log.Error($"{bundleName} 无法对应到游戏中任何一个资产文件");
                Report.Error(bundlePath ,$"{bundleName} 无法对应到游戏中任何一个资产文件");
            }
            if (hasUnAddressable || results.Count == 0)
            {
                results.Clear();
            }
            manager.UnloadAllBundleFiles();

            return new Tuple<string, List<Tuple<string, string>>>(bundleName, results);
        }
        private string MatchForKnownBundleByFileContainer(AssetsManager manager, AssetsFileInstance asset)
        {
            InitFileNameContainerList();
            var (fileContainers,filePathes) = GetFileContainerListAndPathList(manager, asset);
            foreach (var bfcl in bundleFileNameContainerList)
            {
                int i = 0;
                int j = 0;

                while (i < fileContainers.Count && j < bfcl.Value.Count)
                {
                    if (fileContainers[i].Equals(bfcl.Value[j]))
                    {
                        i++;
                    }
                    j++;
                }
                if (i == fileContainers.Count)
                {
                    return bfcl.Key;
                }
            }

            foreach (var bfcl in bundleFileNamePathList)
            {
                int i = 0;
                int j = 0;
                int mc = 0;
                int pathMatch = 0;

                while (i < filePathes.Count && j < bfcl.Value.Count)
                {
                    if (filePathes[i].Equals(bfcl.Value[j]))
                        mc++;
                    if (filePathes[i].Item1.Equals(bfcl.Value[j].Item1))
                    {
                        i++;
                        j++;
                        pathMatch++;
                    }
                    else if (filePathes[i].Item1 < bfcl.Value[j].Item1)
                        i++;
                    else
                        j++;
                }
                if ((mc > filePathes.Count / 5 || mc > bfcl.Value.Count / 5) && (mc > 2 || mc == filePathes.Count))
                {
                    return bfcl.Key;
                }
                if ((pathMatch > filePathes.Count / 3 || pathMatch > bfcl.Value.Count / 3) && (pathMatch > 3 || pathMatch == filePathes.Count))
                {
                    return bfcl.Key;
                }
            }
            return "";
        }
        private bool ScanForKnownBundle(AssetsManager manager, List<Tuple<string, string>> results, AssetsFileInstance asset, AssetsManager targetManager, AssetsFileInstance targetAsset)
        {
            bool hasUnAddressable = false;
            AssetsFile assetFile = asset.file;
            AssetsFile targetAssetFile = targetAsset.file;
            var container = AB.GetContainerDic(manager, asset);
            var containerTarget = AB.GetContainerDic(targetManager, targetAsset);
            //metadata

            var bundleInfos = assetFile.GetAssetsOfType(AssetClassID.AssetBundle);
            var bundleInfosTarget = targetAssetFile.GetAssetsOfType(AssetClassID.AssetBundle);
            if (!bundleInfos.Any() || !bundleInfosTarget.Any())
                return true;
            var b1 = bundleInfos.First();
            var b2 = bundleInfosTarget.First();
            var field1 = manager.GetBaseField(asset, b1);
            var field2 = targetManager.GetBaseField(targetAsset, b2);
            if (HasBundleBreakingChange(field1, field2))
                return true;

            //一般文件
            foreach (var assetInfo in assetFile.AssetInfos)
            {
                if (assetInfo.GetTypeId(assetFile) == (int)AssetClassID.AssetBundle)
                    continue;
                var bf = manager.GetBaseField(asset, assetInfo);
                var nameObj = bf["m_Name"];
                if (nameObj.IsDummy)
                    continue;
                var addressableKey = nameObj.AsString;
                if (addressableKey == null)
                    continue;

                foreach (var targetAssetInfo in targetAssetFile.AssetInfos)
                {
                    if (containerTarget.GetValueOrDefault(targetAssetInfo.PathId, "") != container.GetValueOrDefault(assetInfo.PathId, ""))
                        continue;
                    var bf1 = manager.GetBaseField(targetAsset, targetAssetInfo);
                    var nameObj1 = bf1["m_Name"];
                    if (nameObj1.IsDummy)
                        continue;
                    var addressableKey1 = nameObj1.AsString;
                    if (addressableKey1 == null || addressableKey != addressableKey1)
                        continue;

                    long v1Start = assetInfo.GetAbsoluteByteOffset(assetFile);
                    long v1Size = assetInfo.ByteSize;
                    assetFile.Reader.Position = (int)v1Start;
                    var buf1 = assetFile.Reader.ReadBytes((int)v1Size);

                    long v2Start = targetAssetInfo.GetAbsoluteByteOffset(targetAssetFile);
                    long v2Size = targetAssetInfo.ByteSize;
                    targetAssetFile.Reader.Position = (int)v2Start;
                    var buf2 = targetAssetFile.Reader.ReadBytes((int)v2Size);


                    if (!buf1.Equals(buf2))
                    {
                        if (!ccd.IsAddressableName(addressableKey))
                        {
                            hasUnAddressable = true;
                            break;
                        }
                        results.Add(new Tuple<string, string>(nameObj.AsString, container.GetValueOrDefault(assetInfo.PathId, "")));
                    }
                    break;
                }
                if (hasUnAddressable) break;
            }

            return hasUnAddressable;
        }
        public bool HasBundleBreakingChange(AssetTypeValueField field1, AssetTypeValueField field2)
        {
            if (!field1["m_Dependencies.Array"].IsDummy || !field2["m_Dependencies.Array"].IsDummy)
            {
                if (field1["m_Dependencies.Array"].IsDummy || field2["m_Dependencies.Array"].IsDummy)
                    return true;

                var a1 = field1["m_Dependencies.Array"].Children;
                var a2 = field2["m_Dependencies.Array"].Children;

                if(a1.Count !=  a2.Count)return true;

                for( var i = 0; i < a1.Count; i++)
                {
                    if (a1[i].IsDummy || a2[i].IsDummy)
                        return true;
                    if(a1[i].AsString != a2[i].AsString)
                        return true;
                }
            }
            return false;
        }
        public List<Tuple<string, string>> GetFileContainerList(AssetsManager manager, AssetsFileInstance asset)
        {
            return GetFileContainerListAndPathList(manager, asset).Item1;
        }
        public (List<Tuple<string, string>>, List<Tuple<long, string>>) GetFileContainerListAndPathList(AssetsManager manager, AssetsFileInstance asset)
        {
            var assetFile = asset.file;
            var abdef = assetFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var fab = manager.GetBaseField(asset, abdef);
            List<Tuple<string, string>> fileContainers = new List<Tuple<string, string>>();
            List<Tuple<long, string>> filePathes = new List<Tuple<long, string>>();
            HashSet<long> pathIds = new HashSet<long>();
            foreach (var containerDesc in fab["m_Container.Array"].Children)
            {
                var ctr = containerDesc["first"];
                var path = containerDesc["second"]["asset"]["m_PathID"].AsLong;
                var file = assetFile.GetAssetInfo(path);
                if (file == null)
                {
                    fileContainers.Clear();
                    break;
                }
                pathIds.Add(containerDesc["second"]["asset"]["m_PathID"].AsLong);
                var nameField = manager.GetBaseField(asset, file)["m_Name"];
                if (nameField.IsDummy) continue;
                fileContainers.Add(new Tuple<string, string>(ctr.AsString, nameField.AsString));
                filePathes.Add(new Tuple<long, string>(path, nameField.AsString));
            }
            foreach (var ai in asset.file.AssetInfos)
            {
                var path = ai.PathId;
                if(pathIds.Contains(path)) continue;
                pathIds.Add(path);
                var nameField = manager.GetBaseField(asset, ai)["m_Name"];
                if (nameField.IsDummy) continue;
                filePathes.Add(new Tuple<long, string>(path, nameField.AsString));
            }
            if (fileContainers.Count > 0)
            {
                fileContainers.Sort();
            }
            if (filePathes.Count > 0)
            {
                filePathes.Sort();
            }
            return (fileContainers,filePathes);
        }
        public string FindBundleFileName(string bundleMetaName)
        {
            string tfn = Path.GetFileNameWithoutExtension(bundleMetaName);
            foreach (var (ff,fn,_) in bundleFiles)
            {
                if (fn == tfn)
                    return ff;
            }
            return "";
        }
        public Tuple<AssetsManager, AssetsFileInstance>? GetBundle(string bundleName)
        {
            if (!ccd.IsAddressableName(bundleName))
                return null;
            var bundlePath = GetBundleLocalPath(bundleName);
            if (bundlePath == "") return null;
            AssetsManager managerOut = new AssetsManager();
            var bundle = managerOut.LoadBundleFile(bundlePath);
            var af = managerOut.LoadAssetsFileFromBundle(bundle, 0);
            if (af == null) return null;
            var result = new Tuple<AssetsManager, AssetsFileInstance>(managerOut, af);
            return result;
        }
        public Dictionary<string, List<Tuple<string, string>>> GetAllBundleContainerName()
        {
            InitFileNameContainerList();
            return bundleFileNameContainerList;
        }
        public List<Tuple<string, List<Tuple<string, string>>, int>> GetAllBundleContainerNameWithCatalog()
        {
            InitFileNameContainerList();
            var results = new List<Tuple<string, List<Tuple<string, string>>, int>>();
            foreach (var kvp in bundleFileNameContainerList)
            {
                int catIdx = bundleCatalogIndex.TryGetValue(kvp.Key, out int idx) ? idx : -1;
                results.Add(new Tuple<string, List<Tuple<string, string>>, int>(kvp.Key, kvp.Value, catIdx));
            }
            return results;
        }
        public int GetBundleCatalogIndex(string filePath)
        {
            return bundleCatalogIndex.TryGetValue(filePath, out int idx) ? idx : -1;
        }
        public string GetBundleLocalPath(string bundleName)
        {
            var rl = ccd.GetFirstAvailableResourceLocationList(bundleName);
            if (rl == null || rl.Count == 0) return "";
            var bundlePath = rl[0].InternalId;
            if (bundlePath.StartsWith("{App.WebServerConfig.Path}"))
            {
                if (rl[0].Data is WrappedSerializedObject wo && wo.Object is AssetBundleRequestOptions abro)
                {
                    string abn1 = Path.GetFileNameWithoutExtension(bundleName);
                    string abn2 = Path.GetFileNameWithoutExtension(abro.BundleName);
                    return Path.Combine(cache, abn2, abn1, "__data");
                }
                return "";
            }
            else if (bundlePath.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"))
            {
                bundlePath = bundlePath.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", Path.Combine(local, "StreamingAssets", "aa"));
            }
            return bundlePath;
        }
        public (string grp,string path) GetBundleGroupPath(string bundleName,string bundlePath)
        {
            var rl = ccd.GetFirstAvailableResourceLocationList(bundleName);
            if (rl == null || rl.Count == 0) return ("unknown", bundleName);
            if (bundlePath.StartsWith("{App.WebServerConfig.Path}"))
            {
                if (rl[0].Data is WrappedSerializedObject wo && wo.Object is AssetBundleRequestOptions abro)
                {
                    string abn1 = Path.GetFileNameWithoutExtension(bundleName);
                    string abn2 = Path.GetFileNameWithoutExtension(abro.BundleName);
                    return ("cache", $"{abn2}/{abn1}/__data");
                }
                return ("unknown", bundleName);
            }
            else if (bundlePath.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"))
            {
                return ("base",Path.GetFileName(bundlePath));
            }
            return ("unknown",bundleName);
        }
    }
}
