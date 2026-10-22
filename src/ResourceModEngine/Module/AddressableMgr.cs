// 第三方 RML 代码未按可空引用标注，单独关闭该文件的分析。
#nullable disable
using AddressablesTools;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ResourceModLoader.Module
{
    class AddressableMgr
    {
        /// <summary>匹配 Unity External 中的 MonoScript CAB 名（形如 CAB- + 32 位十六进制）。</summary>
        private static readonly Regex MonoScriptCabNameRegex = new(@"CAB-[0-9a-f]{32}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private Random random = new Random();
        List<ContentCatalogData> contentCatalogDatas = new List<ContentCatalogData>();
        List<string> contentCatalogPath = new List<string>();
        List<bool> createBackup = new List<bool>();
        List<Dictionary<string, ResourceLocation>> generatedAbDictList = new List<Dictionary<string, ResourceLocation>>();
        List<Tuple<string,string, string>> bundleRedirects = new List<Tuple<string,string, string>>();
        List<Tuple<string,string,string>> addressableRedirects = new List<Tuple<string,string, string>>();
        /// <summary>每个 catalog 上缓存「本服共享脚本 Bundle」探测结果。</summary>
        private readonly Dictionary<ContentCatalogData, ResourceLocation?> _localSharedScriptBundleCache = new();

        /// <summary>游戏 *_Data 目录，用于解析官方 AB 的 RuntimePath。</summary>
        public string? GameDataDir { get; set; }
        /// <summary>LocalLow AssetBundles 缓存目录。</summary>
        public string? CacheDir { get; set; }

        public List<Tuple<string, string, string>> GetBundleRedirects()
        {
            return bundleRedirects;
        }
        public int Loaded()
        {
            return contentCatalogDatas.Count;
        }
        public void Add(string path)
        {
            if (!Path.Exists(path))
            {
                return;
            }
            string refer = path + ".modded_ref";
            string bak = path + ".modded_bak";
            string genHash = path + ".modded_hash";
            string toload = path;
            bool needBackup = true;
            if (Path.Exists(refer) && Path.Exists(genHash))
            {
                string hash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path)));
                string hashBefore = File.ReadAllText(genHash);
                if (hash == hashBefore)
                {
                    toload = refer;
                    needBackup = false;
                }
            }
            if (!Path.Exists(bak))
            {
                File.Copy(path, bak);
            }
            if (path.EndsWith(".bundle"))
            {
                contentCatalogDatas.Add(AddressablesCatalogFileParser.FromBundle(toload));
            }
            else
            {
                contentCatalogDatas.Add(AddressablesCatalogFileParser.FromJsonString(File.ReadAllText(toload)));
            }
            contentCatalogPath.Add(path);
            generatedAbDictList.Add(new Dictionary<string, ResourceLocation>());
            createBackup.Add(needBackup);
        }
        public List<Tuple<string,string>> GetAllBundles()
        {
            List<Tuple<string,string>> results = new List<Tuple<string,string>>();
            HashSet<string> seen = new HashSet<string>();
            foreach(var ccd in contentCatalogDatas)
            {
                foreach(var rll in ccd.Resources)
                {
                    foreach(var rl in rll.Value)
                    {
                        if (rl.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider")
                            continue;
                        if (seen.Contains(rl.PrimaryKey))
                            continue;
                        if(rl.Data is WrappedSerializedObject wo && wo.Object is AssetBundleRequestOptions abro)
                        {
                            results.Add(new Tuple<string, string>(rll.Key.ToString(), abro.BundleName));
                            seen.Add(rl.PrimaryKey);
                            break;
                        }
                    }
                }
            }
            return results;
        }
        public List<Tuple<string, string, int>> GetAllBundlesWithCatalog()
        {
            List<Tuple<string, string, int>> results = new List<Tuple<string, string, int>>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < contentCatalogDatas.Count; i++)
            {
                var ccd = contentCatalogDatas[i];
                foreach (var rll in ccd.Resources)
                {
                    foreach (var rl in rll.Value)
                    {
                        if (rl.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider")
                            continue;
                        if (seen.Contains(rl.PrimaryKey))
                            continue;
                        if (rl.Data is WrappedSerializedObject wo && wo.Object is AssetBundleRequestOptions abro)
                        {
                            results.Add(new Tuple<string, string, int>(rll.Key.ToString(), abro.BundleName, i));
                            seen.Add(rl.PrimaryKey);
                            break;
                        }
                    }
                }
            }
            return results;
        }
        public void Reset()
        {
            List<string> toLoad = new List<string>();
            while(contentCatalogPath.Count > 0) { 
                string path = contentCatalogPath[0];
                contentCatalogPath.RemoveAt(0);
                contentCatalogDatas.RemoveAt(0);
                generatedAbDictList.RemoveAt(0);
                createBackup.RemoveAt(0);
                toLoad.Add(path);
            }
            generatedAbDictList.Clear();
            foreach (string path in toLoad) {
                Add(path);
            }
            bundleRedirects.Clear();
            addressableRedirects.Clear();
        }
        public List<Tuple<string,string>> GetAllResources()
        {
            List<Tuple<string, string>> result = new List<Tuple<string, string>>();

            foreach (ContentCatalogData data in contentCatalogDatas)
            {
                foreach(var locations in data.Resources) {
                    foreach(var location in locations.Value) {
                        ResourceLocation? firstDep = null;
                        if (location.Dependencies != null)
                        {
                            firstDep = location.Dependencies.First();
                        }
                        else if (location.DependencyKey != null)
                        {
                            firstDep = data.Resources[location.DependencyKey].First();
                        }

                        if (firstDep == null)
                            firstDep = location;

                        result.Add(new Tuple<string, string>(location.PrimaryKey, firstDep.PrimaryKey));
                    }
                } 
            }
            return result;
        }
        public bool IsAddressableName(string name)
        {
            foreach (ContentCatalogData data in contentCatalogDatas)
            {
                if (data.Resources.ContainsKey(name))
                    return true;
            }
            return false;
        }
        public List<ResourceLocation> GetFirstAvailableResourceLocationList(string name)
        {
            foreach (ContentCatalogData data in contentCatalogDatas)
            {
                if (!data.Resources.ContainsKey(name))
                    continue;

                var rl = data.Resources[name];
                if (rl != null && rl.Count > 0)
                    return rl;
            }
            return new List<ResourceLocation>();
        }

        public void ApplyBundleMod(string name, string bundleFile, string containerRedir = "", string depReq = "")
        {
            if (!Path.Exists(bundleFile))
            {
                Log.Warn($"{bundleFile} 不存在");
                return;
            }

            if (!IsAddressableName(name))
            {
                Log.Warn($"{name} 不在Addressable系统中");
                return;
            }
            bool patched = false;
            for (int i = 0; i < contentCatalogDatas.Count; i++)
            {
                if (ApplyBundleModPreCCD(i, name, bundleFile, containerRedir, depReq))
                    patched = true;
            }
            if (patched) return;
            Log.Warn($"{name} 的来源位置和之前不同. 匹配来源{depReq}");
            foreach (ContentCatalogData ccd in contentCatalogDatas)
            {
                if (!ccd.Resources.ContainsKey(name))
                    continue;
                foreach (var location in ccd.Resources[name])
                {

                    ResourceLocation? firstDep = null;
                    if (location.Dependencies != null)
                    {
                        firstDep = location.Dependencies.First();
                    }
                    else if (location.DependencyKey != null)
                    {
                        firstDep = ccd.Resources[location.DependencyKey].First();
                    }
                    if (firstDep != null)
                        Log.Warn($" - 在 {firstDep.PrimaryKey} 中的 {location.InternalId}");
                }
            }
            if (IsOnlyMatchedResourceLocation(name))
            {
                Log.Warn($"上述 {name} 将被替换，因为他们是唯一满足名称条件的资源");
                for (int i = 0; i < contentCatalogDatas.Count; i++)
                {
                    if (ApplyBundleModPreCCD(i, name, bundleFile, containerRedir))
                        patched = true;
                }
            }
            if (!patched)
            {
                Log.Warn($"{name} 没有被应用到任何地址");
            }
        }
        private bool IsOnlyMatchedResourceLocation(string name)
        {
            foreach (ContentCatalogData ccd in contentCatalogDatas)
            {
                if (!ccd.Resources.ContainsKey(name))
                {
                    continue;
                }
                if (ccd.Resources[name].Count > 1)
                    return false;
            }
            return true;
        }
        private bool ApplyBundleModPreCCD(int idx, string name, string bundleFile, string containerRedir = "", string depReq = "")
        {
            ContentCatalogData ccd = contentCatalogDatas[idx];
            bool patched = false;
            if (!ccd.Resources.ContainsKey(name))
            {
                return false;
            }

            foreach (var location in ccd.Resources[name])
            {
                if (location.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider")
                {
                    // 整包替换（如 Walk 依赖槽）：先把 mod 包 CAB 身份/Externals 对齐到被替换的官方包，再改 InternalId
                    string officialPath = ResolveDependencyBundleFilePath(location);
                    AlignReplacedBundleCabsToOfficial(bundleFile, officialPath, ccd, null);
                    bundleRedirects.Add(new Tuple<string, string, string>(name,location.InternalId, bundleFile));
                    location.InternalId = bundleFile;
                    Log.SuccessPartial($"Bundle {name} --> {location.InternalId}");
                    patched = true;
                    continue;
                }
                else if (location.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider")
                {
                    Log.Warn($"处理中 {name}");
                    Log.Warn($"不支持的提供者类型 {location.ProviderId}");
                    continue;
                }
                ResourceLocation? firstDep = null;
                if (location.Dependencies != null)
                {
                    firstDep = location.Dependencies.First();
                }
                else if (location.DependencyKey != null)
                {
                    firstDep = ccd.Resources[location.DependencyKey].First();
                }
                if (firstDep == null)
                {
                    Log.Warn($"处理中 {name}");
                    Log.Warn($"没找到依赖的文件");
                    continue;
                }
                if (depReq != "" && depReq != firstDep.PrimaryKey && "patched." + depReq != firstDep.PrimaryKey)
                {
                    continue;
                }

                // Redirect 与 Add 一样做跨服 CAB 对齐，并保留 Reference 的 sibling 依赖（Walk/共享脚本等）
                AlignModBundleCabsToLocal(bundleFile, ccd, location);

                var rl = getAbIdFor(idx, bundleFile, firstDep);

                if (rl == null)
                {
                    Log.Warn($"处理中 {name}");
                    Log.Warn($"无法创建虚拟Bundle");
                    continue;
                }

                // 把官方其余依赖挂到 patched 内容包上，再写回 BundledAsset.Dependencies
                CopyExtraReferenceDependenciesOntoContent(ccd, location, rl);

                if (location.Dependencies != null)
                {
                    if (location.Dependencies.Any())
                        addressableRedirects.Add(new Tuple<string, string, string>(name, location.Dependencies.First().InternalId, bundleFile));
                    location.Dependencies.Clear();
                    location.Dependencies.Add(rl);
                    foreach (var sibling in ccd.Resources[rl.PrimaryKey].Skip(1))
                        location.Dependencies.Add(sibling);
                }
                else if (location.DependencyKey != null)
                {
                    addressableRedirects.Add(new Tuple<string, string, string>(name, location.DependencyKey.ToString(), bundleFile));
                    location.DependencyKey = rl.PrimaryKey;
                    location.DependencyHashCode = rl.HashCode;
                }

                if (containerRedir != "")
                {
                    location.InternalId = containerRedir;
                }
                Log.SuccessAll($"Resource {name} --> {rl.PrimaryKey}");
                patched = true;
            }
            return patched;
        }
        private ResourceLocation? getAbIdFor(int idx, string path, ResourceLocation reference)
        {
            Dictionary<string, ResourceLocation> generatedAbDict = generatedAbDictList[idx];
            ContentCatalogData ccd = contentCatalogDatas[idx];

            if (generatedAbDict.ContainsKey(path))
                return generatedAbDict[path];

            var rl = new ResourceLocation();
            rl.ProviderId = reference.ProviderId;
            rl.InternalId = path;
            string hash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path)));
            rl.PrimaryKey = "patched." + Path.GetFileNameWithoutExtension(path)+"."+ hash + ".bundle";
            rl.Type = reference.Type;
            rl.HashCode = random.Next();
            AssetBundleRequestOptions opt = new AssetBundleRequestOptions();
            opt.Hash = "";
            opt.BundleName = rl.PrimaryKey;
            if (reference.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions abro, Type: SerializedType t })
            {
                opt.ComInfo = abro.ComInfo;
                opt.Crc = 0;
                rl.Data = new WrappedSerializedObject(t, opt);
            }
            else
            {
                return null;
            }

            ccd.Resources[rl.PrimaryKey] = new List<ResourceLocation> { rl };
            generatedAbDict[path] = rl;
            return rl;
        }
        public void Save()
        {
            for (int idx = 0; idx < contentCatalogDatas.Count; idx++)
            {
                EngineControl.Checkpoint("保存 catalog");
                ContentCatalogData ccd = contentCatalogDatas[idx];
                string path = contentCatalogPath[idx];
                bool needUpdateRef = createBackup[idx];

                string refer = path + ".modded_ref";
                string genHash = path + ".modded_hash";

                if (needUpdateRef)
                {
                    if (File.Exists(refer))
                        File.Delete(refer);
                    File.Copy(path, refer);
                }

                if (path.EndsWith(".bundle"))
                    AddressablesCatalogFileParser.ToBundle(ccd, refer, path);
                else
                    File.WriteAllText(path, AddressablesCatalogFileParser.ToJsonString(ccd));

                if (File.Exists(genHash))
                    File.Delete(genHash);

                string hash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path)));
                File.WriteAllText(genHash, hash);

                Console.WriteLine($"已保存 {path}@{hash}");
            }
        }

        /// <summary>
        /// 向 catalog 注册新的 Addressable 名（mod.json Add）。
        /// 若同名已存在且 Container 非空，则走 <see cref="ForceOverwriteExistingAddEntry"/> 强制修补；
        /// 新建时会对齐跨服 MonoScript CAB、补全 Reference 的额外 Bundle 依赖，并避免空 InternalId。
        /// </summary>
        /// <param name="name">新 PrimaryKey（Addressable 名）。</param>
        /// <param name="bundleFile">mod 提供的内容 Bundle 路径。</param>
        /// <param name="container">写入的 InternalId；空则回退为 Reference 的 InternalId。</param>
        /// <param name="refName">用作模板的已有 Addressable 名（同类资源）。</param>
        public void NewAddressableName(string name, string bundleFile, string container, string refName)
        {
            // 已存在：仅当显式指定了 Container 时强制覆写，否则保持原条目不动
            if (IsAddressableName(name))
            {
                if (!string.IsNullOrEmpty(container))
                    ForceOverwriteExistingAddEntry(name, bundleFile, container, refName);
                return;
            }
            for(int i=0;i< contentCatalogDatas.Count;i++)
            {
                var ccd = contentCatalogDatas[i];
                string? resolvedRef = ResolveReferenceKeyOrLocalFallback(ccd, refName);
                if (resolvedRef == null)
                    continue;
                if (resolvedRef != refName)
                    Log.Warn($"Reference {refName} 不存在，改用本服 {resolvedRef}");

                var reference = ccd.Resources[resolvedRef]
                    .FirstOrDefault(r => r.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider");
                if (reference == null) continue;

                // 跨服：同名 CAB 保留；缺共享脚本则补本服；旁路 mod CAB 改成官方
                AlignModBundleCabsToLocal(bundleFile, ccd, reference);

                // Container 未写时回退到 Reference 的 InternalId，避免写入空字符串导致白卡
                string internalId = string.IsNullOrEmpty(container) ? reference.InternalId : container;
                var rl = new ResourceLocation();
                rl.ProviderId = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";
                rl.InternalId = internalId;
                rl.PrimaryKey = name;
                rl.Type = reference.Type;
                rl.HashCode = random.Next();
                ResourceLocation? refDep = GetPrimaryBundleDependency(ccd, reference);
                if (refDep != null)
                {
                    var dep = getAbIdFor(i, bundleFile, refDep);
                    if (dep == null) continue;
                    rl.DependencyKey = dep.PrimaryKey;
                    rl.DependencyHashCode = dep.HashCode;
                    CopyExtraReferenceDependenciesOntoContent(ccd, reference, dep);
                    ccd.Resources[rl.PrimaryKey] = new List<ResourceLocation> { rl };
                    Log.SuccessPartial($"New {rl.PrimaryKey} InternalId={rl.InternalId}");
                }
            }
        }

        /// <summary>
        /// 强制覆写 catalog 中已存在的 Add 条目：写入 Container（InternalId）、改指向新 Bundle，
        /// 并同步对齐 MonoScript CAB、复制 Reference 的额外依赖。用于修空白卡或更新已注册 key。
        /// </summary>
        /// <param name="name">已存在的 Addressable 名。</param>
        /// <param name="bundleFile">新的内容 Bundle 路径。</param>
        /// <param name="container">强制写入的 InternalId（须非空才会被调用）。</param>
        /// <param name="refName">依赖结构模板的 Reference 名。</param>
        private void ForceOverwriteExistingAddEntry(string name, string bundleFile, string container, string refName)
        {
            for (int i = 0; i < contentCatalogDatas.Count; i++)
            {
                var ccd = contentCatalogDatas[i];
                if (!ccd.Resources.ContainsKey(name))
                    continue;

                string? resolvedRef = ResolveReferenceKeyOrLocalFallback(ccd, refName);
                if (resolvedRef == null || !ccd.Resources.ContainsKey(resolvedRef))
                    continue;
                if (resolvedRef != refName)
                    Log.Warn($"Reference {refName} 不存在，改用本服 {resolvedRef}");

                var reference = ccd.Resources[resolvedRef]
                    .FirstOrDefault(r => r.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider");
                if (reference == null)
                    continue;

                AlignModBundleCabsToLocal(bundleFile, ccd, reference);

                ResourceLocation? refDep = GetPrimaryBundleDependency(ccd, reference);
                if (refDep == null)
                    continue;

                var dep = getAbIdFor(i, bundleFile, refDep);
                if (dep == null)
                    continue;
                CopyExtraReferenceDependenciesOntoContent(ccd, reference, dep);

                foreach (var location in ccd.Resources[name])
                {
                    if (location.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider")
                        continue;

                    location.InternalId = container;
                    if (location.Dependencies != null)
                    {
                        location.Dependencies.Clear();
                        location.Dependencies.Add(dep);
                        foreach (var sibling in ccd.Resources[dep.PrimaryKey].Skip(1))
                            location.Dependencies.Add(sibling);
                    }
                    else
                    {
                        location.DependencyKey = dep.PrimaryKey;
                        location.DependencyHashCode = dep.HashCode;
                    }
                    Log.SuccessPartial($"Force Container {name} InternalId={container}");
                }
            }
        }

        /// <summary>
        /// 取得 Reference 的主（第一个）Bundle 依赖，用作内容包模板。
        /// 优先读 Dependencies[0]，否则经 DependencyKey 查 catalog。
        /// </summary>
        /// <returns>主依赖；没有则 null。</returns>
        private static ResourceLocation? GetPrimaryBundleDependency(ContentCatalogData ccd, ResourceLocation reference)
        {
            if (reference.Dependencies != null && reference.Dependencies.Any())
                return reference.Dependencies[0];
            if (reference.DependencyKey != null && ccd.Resources.ContainsKey(reference.DependencyKey))
                return ccd.Resources[reference.DependencyKey].First();
            return null;
        }

        /// <summary>
        /// 列出 Reference 的全部 Bundle 依赖（内容包 + 共享脚本包等），供复制与解析 CAB 使用。
        /// </summary>
        /// <returns>依赖列表；无法解析则 null。</returns>
        private static List<ResourceLocation>? ListReferenceBundleDependencies(ContentCatalogData ccd, ResourceLocation reference)
        {
            if (reference.Dependencies != null && reference.Dependencies.Count > 0)
                return reference.Dependencies;
            if (reference.DependencyKey != null && ccd.Resources.ContainsKey(reference.DependencyKey))
                return ccd.Resources[reference.DependencyKey];
            return null;
        }

        /// <summary>
        /// 把 Reference 除主内容包外的其余依赖复制到 contentDep 所在依赖列表上；
        /// 并确保挂上本服 catalog 统计出的共享脚本 Bundle（不依赖固定模板名）。
        /// </summary>
        /// <param name="reference">官方模板条目。</param>
        /// <param name="contentDep">已为 mod 新建的主内容依赖。</param>
        private void CopyExtraReferenceDependenciesOntoContent(ContentCatalogData ccd, ResourceLocation reference, ResourceLocation contentDep)
        {
            var refDeps = ListReferenceBundleDependencies(ccd, reference);
            var contentList = ccd.Resources[contentDep.PrimaryKey];

            if (refDeps != null && refDeps.Count > 1)
            {
                for (int i = 1; i < refDeps.Count; i++)
                {
                    var sibling = refDeps[i];
                    if (sibling == null) continue;
                    if (contentList.Any(d => d.InternalId == sibling.InternalId && Equals(d.PrimaryKey, sibling.PrimaryKey)))
                        continue;
                    contentList.Add(sibling);
                    Log.SuccessPartial($"Keep sibling dep {sibling.PrimaryKey} -> {sibling.InternalId}");
                }
            }

            // 即使 Reference 只有内容包（或跨服 Reference 依赖不全），也挂上本服共享脚本包
            var shared = TryFindLocalSharedScriptBundle(ccd);
            if (shared != null
                && !contentList.Any(d => Equals(d.PrimaryKey, shared.PrimaryKey)))
            {
                contentList.Add(shared);
                Log.SuccessPartial($"Attach local shared script dep {shared.PrimaryKey}");
            }
        }

        /// <summary>
        /// 解析 Add 的 Reference：本服 catalog 有该 key 则原样返回；
        /// 否则在本服 catalog 中找「依赖了共享脚本包且 InternalId 非空」的 BundledAsset，不写死模板名。
        /// </summary>
        /// <param name="refName">mod.json 中的 Reference。</param>
        /// <returns>实际使用的 Reference key；找不到则 null。</returns>
        private string? ResolveReferenceKeyOrLocalFallback(ContentCatalogData ccd, string refName)
        {
            if (ccd.Resources.ContainsKey(refName))
                return refName;

            var shared = TryFindLocalSharedScriptBundle(ccd);
            string? sharedPk = shared?.PrimaryKey?.ToString();

            string? fallbackAny = null;
            foreach (var kv in ccd.Resources)
            {
                string key = kv.Key?.ToString() ?? "";
                if (key.Length == 0 || key.StartsWith("patched.", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var loc in kv.Value)
                {
                    if (loc.ProviderId == null
                        || !loc.ProviderId.Contains("BundledAssetProvider")
                        || string.IsNullOrEmpty(loc.InternalId))
                        continue;

                    fallbackAny ??= key;

                    if (sharedPk == null)
                        continue;

                    var deps = ListReferenceBundleDependencies(ccd, loc);
                    if (deps == null || deps.Count <= 1)
                        continue;
                    if (deps.Skip(1).Any(d => Equals(d.PrimaryKey, sharedPk)))
                        return key;
                }
            }

            return fallbackAny;
        }

        /// <summary>
        /// 跨服 / 同服 CAB 对齐入口（Redirect 与 Add 共用）。
        /// </summary>
        /// <remarks>
        /// 规则概要：
        /// <list type="number">
        /// <item>已与本服相同的 CAB 名不变（如 Hero125 增轨 mod，Externals 已对齐则空操作）。</item>
        /// <item>缺本服共享脚本 CAB 时，把一个 foreign 引用改到本服共享 CAB；catalog 依赖由 <see cref="CopyExtraReferenceDependenciesOntoContent"/> 挂上。</item>
        /// <item>Externals 指向同目录其它 mod 包时：引用与被指包身份一并改成官方对应 CAB。</item>
        /// <item>主包未 External、但同目录有关联旁路包（如 Walk）时：旁路包自身 CAB 对齐到官方未占用的非共享槽。</item>
        /// <item>比官方多出来、找不到映射槽的 CAB 保留原名（包内新增引用，不强行改共享）。</item>
        /// </list>
        /// 写盘前会释放 AssetsTools 句柄，经临时文件覆盖，避免「文件正在使用」。
        /// </remarks>
        /// <param name="bundleFile">mod 内容 Bundle 路径（可能被原地改 CAB 明文）。</param>
        /// <param name="ccd">本服 catalog。</param>
        /// <param name="reference">用作 Externals 模板的 BundledAsset（通常即被 Redirect 的条目）。</param>
        private void AlignModBundleCabsToLocal(string bundleFile, ContentCatalogData ccd, ResourceLocation? reference)
        {
            if (!File.Exists(bundleFile))
                return;

            List<string> modCabs = CollectMonoScriptCabsOrdered(bundleFile);
            if (modCabs.Count == 0)
                return;

            string? sharedCab = TryResolveLocalSharedMonoScriptCab(ccd);
            var map = BuildCrossServerCabRemap(ccd, reference, bundleFile, modCabs, sharedCab);
            ApplyCabRemapToModTree(bundleFile, map);
        }

        /// <summary>
        /// 整包替换官方依赖 AB（AssetBundleProvider Redirect）时的 CAB 对齐：
        /// 先对齐 assets 文件自身 CAB 名（身份），再按 Externals 做与 <see cref="AlignModBundleCabsToLocal"/> 相同的跨服映射。
        /// </summary>
        /// <param name="modBundleFile">替换用的 mod Bundle。</param>
        /// <param name="officialBundleFile">被替换的官方 Bundle 磁盘路径；找不到则仅走共享脚本回退。</param>
        private void AlignReplacedBundleCabsToOfficial(
            string modBundleFile,
            string? officialBundleFile,
            ContentCatalogData ccd,
            ResourceLocation? reference)
        {
            if (!File.Exists(modBundleFile))
                return;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? sharedCab = TryResolveLocalSharedMonoScriptCab(ccd);

            if (!string.IsNullOrEmpty(officialBundleFile) && File.Exists(officialBundleFile))
            {
                // 身份：DirectoryInfo 上的 CAB-…（如 Walk 包 ba1b… → 官方 80f9…）
                var modIdentity = CollectAssetsFileCabNames(modBundleFile);
                var officialIdentity = CollectAssetsFileCabNames(officialBundleFile);
                int nId = Math.Min(modIdentity.Count, officialIdentity.Count);
                for (int i = 0; i < nId; i++)
                {
                    if (!modIdentity[i].Equals(officialIdentity[i], StringComparison.OrdinalIgnoreCase))
                        map[modIdentity[i]] = officialIdentity[i];
                }

                List<string> modExt = CollectMonoScriptCabsOrdered(modBundleFile);
                List<string> localExt = CollectMonoScriptCabsOrdered(officialBundleFile);
                if (localExt.Count == 0 && reference != null)
                {
                    ResourceLocation? refContent = GetPrimaryBundleDependency(ccd, reference);
                    if (refContent != null)
                        localExt = CollectMonoScriptCabsOrdered(ResolveDependencyBundleFilePath(refContent));
                    if (localExt.Count == 0)
                        localExt = CollectMonoScriptCabsFromReferenceDeps(ccd, reference);
                }

                FillCrossServerCabMap(modExt, localExt, sharedCab, IndexSiblingCabFiles(Path.GetDirectoryName(modBundleFile), modBundleFile), map);
            }
            else
            {
                List<string> modExt = CollectMonoScriptCabsOrdered(modBundleFile);
                var built = BuildCrossServerCabRemap(ccd, reference, modBundleFile, modExt, sharedCab);
                foreach (var kv in built)
                    map[kv.Key] = kv.Value;
            }

            ApplyCabRemapToModTree(modBundleFile, map);
        }

        /// <summary>
        /// 把 CAB 映射写回主包，以及同目录内仍含旧 CAB 明文的旁路包（一次 Apply 改齐引用与被指身份）。
        /// </summary>
        private static void ApplyCabRemapToModTree(string primaryBundle, Dictionary<string, string> map)
        {
            if (map == null || map.Count == 0)
                return;

            var targets = new List<string> { primaryBundle };
            string? dir = Path.GetDirectoryName(primaryBundle);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (string f in EnumerateModBundleFiles(dir))
                {
                    if (f.Equals(primaryBundle, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (BundleContainsAnyCab(f, map.Keys))
                        targets.Add(f);
                }
            }

            string detail = string.Join(", ", map.Select(kv => $"{ShortCab(kv.Key)}->{ShortCab(kv.Value)}"));
            foreach (string t in targets)
            {
                int hits = ReplaceMonoScriptCabsByMap(t, map);
                if (hits > 0)
                    Log.SuccessPartial($"Retarget CAB x{hits} ({Path.GetFileName(t)}): {detail}");
            }
        }

        /// <summary>日志用短 CAB（前 12 字符 + …）。</summary>
        private static string ShortCab(string cab)
        {
            if (string.IsNullOrEmpty(cab) || cab.Length < 12) return cab;
            return cab.Substring(0, 12) + "…";
        }

        /// <summary>
        /// 构建 foreign CAB → 本服 CAB 映射。
        /// </summary>
        /// <remarks>
        /// 优先用 Reference 主内容包 Externals 顺序；数量一致则 1:1；
        /// 不一致则 <see cref="FillCrossServerCabMap"/> + <see cref="PairUnreferencedSiblingIdentities"/>；
        /// 仍无模板时只补一个共享脚本 CAB，其余保留。
        /// </remarks>
        private Dictionary<string, string> BuildCrossServerCabRemap(
            ContentCatalogData ccd,
            ResourceLocation? reference,
            string bundleFile,
            List<string> modCabs,
            string? sharedCab)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (modCabs.Count == 0)
                return map;

            List<string>? localOrdered = null;
            if (reference != null)
            {
                ResourceLocation? refContent = GetPrimaryBundleDependency(ccd, reference);
                if (refContent != null)
                {
                    string refPath = ResolveDependencyBundleFilePath(refContent);
                    localOrdered = CollectMonoScriptCabsOrdered(refPath);
                }

                if (localOrdered == null || localOrdered.Count == 0)
                    localOrdered = CollectMonoScriptCabsFromReferenceDeps(ccd, reference);
            }

            var siblingIndex = IndexSiblingCabFiles(Path.GetDirectoryName(bundleFile), bundleFile);

            if (localOrdered != null && localOrdered.Count > 0)
            {
                if (localOrdered.Count == modCabs.Count)
                {
                    // 数量一致：按 Externals 顺序 1:1；指向旁路包的项在 ApplyCabRemapToModTree 时一并改身份
                    for (int i = 0; i < modCabs.Count; i++)
                    {
                        if (!modCabs[i].Equals(localOrdered[i], StringComparison.OrdinalIgnoreCase))
                            map[modCabs[i]] = localOrdered[i];
                    }
                    return map;
                }

                FillCrossServerCabMap(modCabs, localOrdered, sharedCab, siblingIndex, map);
                PairUnreferencedSiblingIdentities(modCabs, localOrdered, sharedCab, siblingIndex, map);
                if (map.Count > 0)
                    return map;
            }

            // 无本服 Externals 模板：仅缺共享时补一个，避免把 Timeline 增轨等多出来的引用全改掉
            if (!string.IsNullOrEmpty(sharedCab))
            {
                bool hasShared = modCabs.Any(m => m.Equals(sharedCab, StringComparison.OrdinalIgnoreCase));
                if (!hasShared)
                {
                    string? victim = modCabs.FirstOrDefault(m => !m.Equals(sharedCab, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(victim))
                        map[victim] = sharedCab;
                }
                return map;
            }

            if (reference != null)
            {
                string? fallback = TryResolveMonoScriptCabFromReferenceDeps(ccd, reference);
                if (!string.IsNullOrEmpty(fallback)
                    && !modCabs.Any(m => m.Equals(fallback, StringComparison.OrdinalIgnoreCase)))
                {
                    string? victim = modCabs.FirstOrDefault();
                    if (!string.IsNullOrEmpty(victim))
                        map[victim] = fallback;
                }
            }

            return map;
        }

        /// <summary>
        /// 数量不一致时的跨服映射：
        /// - 已与本服重合的跳过；
        /// - 缺本服共享脚本时，用 1 个无旁路实体的 foreign 补上共享 CAB；
        /// - 指向同目录旁路包的 foreign → 按序对齐到尚未使用的本服非共享 CAB；
        /// - 比官方多出来、找不到对应槽的 CAB 保留原名（如 Timeline 增轨的包内引用，不强行改共享）。
        /// </summary>
        private static void FillCrossServerCabMap(
            List<string> modCabs,
            List<string> localCabs,
            string? sharedCab,
            Dictionary<string, string> siblingCabToFile,
            Dictionary<string, string> map)
        {
            var localSet = new HashSet<string>(localCabs, StringComparer.OrdinalIgnoreCase);
            var modSet = new HashSet<string>(modCabs, StringComparer.OrdinalIgnoreCase);

            var foreign = new List<string>();
            foreach (string mc in modCabs)
            {
                if (!localSet.Contains(mc))
                    foreign.Add(mc);
            }

            var unusedLocal = new List<string>();
            foreach (string lc in localCabs)
            {
                if (!modSet.Contains(lc))
                    unusedLocal.Add(lc);
            }

            var unusedNonShared = new List<string>();
            bool sharedMissing = false;
            foreach (string lc in unusedLocal)
            {
                if (!string.IsNullOrEmpty(sharedCab) && lc.Equals(sharedCab, StringComparison.OrdinalIgnoreCase))
                {
                    sharedMissing = true;
                    continue;
                }
                unusedNonShared.Add(lc);
            }
            if (!string.IsNullOrEmpty(sharedCab) && !modSet.Contains(sharedCab) && !sharedMissing)
                sharedMissing = true; // 本服有共享名但不在 localOrdered 时仍允许补齐

            var scriptForeign = new List<string>();
            var siblingForeign = new List<string>();
            foreach (string fc in foreign)
            {
                if (siblingCabToFile.ContainsKey(fc))
                    siblingForeign.Add(fc);
                else
                    scriptForeign.Add(fc);
            }

            int scriptIdx = 0;
            // 缺共享脚本：只拿 1 个 foreign 补共享，其余多出来的保留
            if (sharedMissing && !string.IsNullOrEmpty(sharedCab) && scriptForeign.Count > 0)
            {
                map[scriptForeign[0]] = sharedCab;
                scriptIdx = 1;
                Log.SuccessPartial($"共享脚本补齐 {ShortCab(scriptForeign[0])} -> {ShortCab(sharedCab)}");
            }

            // 其余无旁路 foreign：能对上官方未占用非共享槽的才改，对不上的保留
            int nonSharedIdx = 0;
            int kept = 0;
            for (; scriptIdx < scriptForeign.Count; scriptIdx++)
            {
                if (nonSharedIdx < unusedNonShared.Count)
                    map[scriptForeign[scriptIdx]] = unusedNonShared[nonSharedIdx++];
                else
                    kept++;
            }

            // 旁路 mod：按序对齐官方非共享槽；多出来的旁路引用保留（不强行改共享）
            foreach (string sf in siblingForeign)
            {
                if (nonSharedIdx < unusedNonShared.Count)
                    map[sf] = unusedNonShared[nonSharedIdx++];
                else
                    kept++;
            }

            if (siblingForeign.Count > 0)
                Log.SuccessPartial($"旁路 CAB 对齐 siblingForeign={siblingForeign.Count}");
            if (kept > 0)
                Log.SuccessPartial($"比官方多出的 CAB 保留原名 x{kept}");
        }

        /// <summary>
        /// 主包 Externals 未点名、但仍在同目录的旁路包（如拆出的 Walk）：
        /// 将其自身 assets CAB 按序对齐到官方尚未占用的非共享 CAB，便于 catalog 依赖槽加载。
        /// 优先配对「Externals 仍含本次 foreign/共享映射源」的旁路包，避免误改纯贴图包。
        /// </summary>
        private static void PairUnreferencedSiblingIdentities(
            List<string> modCabs,
            List<string> localCabs,
            string? sharedCab,
            Dictionary<string, string> siblingCabToFile,
            Dictionary<string, string> map)
        {
            var modSet = new HashSet<string>(modCabs, StringComparer.OrdinalIgnoreCase);
            var usedTargets = new HashSet<string>(map.Values, StringComparer.OrdinalIgnoreCase);

            var unusedNonShared = new List<string>();
            foreach (string lc in localCabs)
            {
                if (modSet.Contains(lc)) continue;
                if (!string.IsNullOrEmpty(sharedCab) && lc.Equals(sharedCab, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (usedTargets.Contains(lc)) continue;
                unusedNonShared.Add(lc);
            }
            if (unusedNonShared.Count == 0)
                return;

            var relatedKeys = new HashSet<string>(map.Keys, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(sharedCab))
                relatedKeys.Add(sharedCab);

            var leftovers = new List<(string Cab, string File, int Score)>();
            foreach (var kv in siblingCabToFile)
            {
                if (modSet.Contains(kv.Key)) continue;      // 已是主包 External
                if (map.ContainsKey(kv.Key)) continue;       // 已映射
                int score = 0;
                if (BundleContainsAnyCab(kv.Value, relatedKeys))
                    score += 100;
                if (BundleLooksLikeAnimationPack(kv.Value))
                    score += 50;
                if (score <= 0)
                    continue; // 跳过无关联的纯资源包
                leftovers.Add((kv.Key, kv.Value, score));
            }

            leftovers.Sort((a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                return c != 0 ? c : string.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
            });

            int si = 0;
            foreach (var item in leftovers)
            {
                if (si >= unusedNonShared.Count) break;
                map[item.Cab] = unusedNonShared[si++];
                Log.SuccessPartial($"旁路包身份 {ShortCab(item.Cab)} -> {ShortCab(map[item.Cab])} ({Path.GetFileName(item.File)})");
            }
        }

        private static bool BundleLooksLikeAnimationPack(string bundlePath)
        {
            AssetsManager? am = null;
            try
            {
                am = new AssetsManager();
                var bun = am.LoadBundleFile(bundlePath);
                for (int i = 0; i < bun.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                {
                    if (!bun.file.IsAssetsFile(i)) continue;
                    var asset = am.LoadAssetsFileFromBundle(bun, i);
                    if (asset == null) continue;
                    if (asset.file.GetAssetsOfType(AssetClassID.AnimationClip).Any())
                        return true;
                }
            }
            catch { }
            finally
            {
                try { am?.UnloadAll(); } catch { }
            }
            return false;
        }

        /// <summary>
        /// 索引同目录其它 Bundle：assets 文件 CAB 名 → 磁盘路径（被主包 External 指向的旁路包）。
        /// </summary>
        private static Dictionary<string, string> IndexSiblingCabFiles(string? modDir, string excludeBundle)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
                return index;

            foreach (string f in EnumerateModBundleFiles(modDir))
            {
                if (f.Equals(excludeBundle, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (string cab in CollectAssetsFileCabNames(f))
                {
                    if (!index.ContainsKey(cab))
                        index[cab] = f;
                }
            }
            return index;
        }

        private static IEnumerable<string> EnumerateModBundleFiles(string modDir)
        {
            foreach (string f in Directory.GetFiles(modDir))
            {
                string name = Path.GetFileName(f);
                if (name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (name.Equals("__data", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("__data.bundle", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("__data", StringComparison.OrdinalIgnoreCase))
                {
                    // 接受 __data / *.bundle / "__data (2).bundle" 等
                    if (name.Equals("__data", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                        yield return f;
                }
            }
        }

        /// <summary>
        /// 收集 Bundle 内 assets 文件自身的 CAB 名（DirectoryInfo），不含 Externals。
        /// </summary>
        private static List<string> CollectAssetsFileCabNames(string bundlePath)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath))
                return result;
            AssetsManager? am = null;
            try
            {
                am = new AssetsManager();
                var bun = am.LoadBundleFile(bundlePath);
                for (int i = 0; i < bun.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                {
                    if (!bun.file.IsAssetsFile(i)) continue;
                    string name = bun.file.BlockAndDirInfo.DirectoryInfos[i].Name ?? "";
                    var m = MonoScriptCabNameRegex.Match(name);
                    if (m.Success && seen.Add(m.Value))
                        result.Add(m.Value);
                }
            }
            catch { }
            finally
            {
                try { am?.UnloadAll(); } catch { }
            }
            return result;
        }

        private static bool BundleContainsAnyCab(string bundlePath, IEnumerable<string> cabs)
        {
            try
            {
                byte[] data = File.ReadAllBytes(bundlePath);
                string ascii = Encoding.ASCII.GetString(data);
                foreach (string cab in cabs)
                {
                    if (!string.IsNullOrEmpty(cab) && ascii.IndexOf(cab, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 从 Reference 的全部依赖 Bundle 中按依赖顺序收集 MonoScript CAB（去重保序）。
        /// </summary>
        private List<string> CollectMonoScriptCabsFromReferenceDeps(ContentCatalogData ccd, ResourceLocation reference)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deps = ListReferenceBundleDependencies(ccd, reference);
            if (deps == null)
                return result;

            foreach (var dep in deps)
            {
                string path = ResolveDependencyBundleFilePath(dep);
                foreach (string cab in CollectMonoScriptCabsOrdered(path))
                {
                    if (seen.Add(cab))
                        result.Add(cab);
                }
            }
            return result;
        }

        /// <summary>
        /// 从本服 catalog 直接定位共享脚本 Bundle，并读取其中的 MonoScript CAB 名。
        /// </summary>
        private string? TryResolveLocalSharedMonoScriptCab(ContentCatalogData ccd)
        {
            var shared = TryFindLocalSharedScriptBundle(ccd);
            if (shared == null)
                return null;

            string path = ResolveDependencyBundleFilePath(shared);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Log.Warn($"本服共享脚本包无法落到磁盘: {shared.PrimaryKey} -> {path}");
                return null;
            }

            string? cab = TryFindMonoScriptCabInBundle(path);
            if (!string.IsNullOrEmpty(cab))
                Log.SuccessPartial($"Local shared script bundle {shared.PrimaryKey} CAB={cab}");
            return cab;
        }

        /// <summary>
        /// 统计本服 catalog：作为 BundledAsset「非首依赖」出现次数最多的 Bundle，视为共享脚本包。
        /// 不依赖固定 Addressable 模板名；结果按 catalog 缓存。
        /// </summary>
        private ResourceLocation? TryFindLocalSharedScriptBundle(ContentCatalogData ccd)
        {
            if (_localSharedScriptBundleCache.TryGetValue(ccd, out var cached))
                return cached;

            var freq = new Dictionary<string, (int Count, ResourceLocation Loc)>(StringComparer.Ordinal);
            foreach (var kv in ccd.Resources)
            {
                foreach (var loc in kv.Value)
                {
                    if (loc.ProviderId == null || !loc.ProviderId.Contains("BundledAssetProvider"))
                        continue;

                    var deps = ListReferenceBundleDependencies(ccd, loc);
                    if (deps == null || deps.Count <= 1)
                        continue;

                    for (int i = 1; i < deps.Count; i++)
                    {
                        var d = deps[i];
                        string pk = d.PrimaryKey?.ToString() ?? "";
                        if (pk.Length == 0
                            || pk.StartsWith("patched.", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!freq.TryGetValue(pk, out var cur))
                            freq[pk] = (1, d);
                        else
                            freq[pk] = (cur.Count + 1, cur.Loc);
                    }
                }
            }

            ResourceLocation? best = null;
            if (freq.Count > 0)
            {
                best = freq.Values
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => EstimateDependencyBundleSize(x.Loc))
                    .First().Loc;
            }

            _localSharedScriptBundleCache[ccd] = best;
            return best;
        }

        /// <summary>
        /// 回退：从指定 Reference 的各 Bundle 依赖中解析 MonoScript CAB（按体积从小到大尝试）。
        /// </summary>
        private string? TryResolveMonoScriptCabFromReferenceDeps(ContentCatalogData ccd, ResourceLocation reference)
        {
            var deps = ListReferenceBundleDependencies(ccd, reference);
            if (deps == null) return null;

            foreach (var dep in deps.OrderBy(d => EstimateDependencyBundleSize(d)))
            {
                string path = ResolveDependencyBundleFilePath(dep);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;
                string? cab = TryFindMonoScriptCabInBundle(path);
                if (!string.IsNullOrEmpty(cab))
                    return cab;
            }
            return null;
        }

        /// <summary>
        /// 估算依赖 Bundle 大小，供排序：越小越优先（共享 MonoScript 包通常更小）。
        /// 读不到 BundleSize 时返回较大默认值，排到后面。
        /// </summary>
        private static long EstimateDependencyBundleSize(ResourceLocation dep)
        {
            if (dep.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions opt } && opt.BundleSize > 0)
                return opt.BundleSize;
            return 1_000_000;
        }

        /// <summary>
        /// 把 catalog 依赖的 InternalId 解析为磁盘上的 Bundle 文件路径。
        /// 支持绝对路径、RuntimePath 占位符、以及 LocalLow AssetBundles 缓存布局。
        /// </summary>
        /// <returns>可读文件路径；无法解析时返回空串或未展开的 InternalId。</returns>
        private string ResolveDependencyBundleFilePath(ResourceLocation dep)
        {
            string id = dep.InternalId ?? "";
            if (string.IsNullOrEmpty(id))
                return "";

            if (Path.IsPathRooted(id) && File.Exists(id))
                return id;

            if (id.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(GameDataDir))
            {
                string path = id.Replace(
                    "{UnityEngine.AddressableAssets.Addressables.RuntimePath}",
                    Path.Combine(GameDataDir, "StreamingAssets", "aa"),
                    StringComparison.Ordinal);
                if (File.Exists(path)) return path;
            }

            if (id.StartsWith("{App.WebServerConfig.Path}", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(CacheDir)
                && dep.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions abro })
            {
                string abn1 = Path.GetFileNameWithoutExtension(dep.PrimaryKey?.ToString() ?? "");
                string abn2 = Path.GetFileNameWithoutExtension(abro.BundleName ?? "");
                string path = Path.Combine(CacheDir, abn2, abn1, "__data");
                if (File.Exists(path)) return path;
            }

            return id;
        }

        /// <summary>
        /// 按 Externals 出现顺序收集 Bundle 内全部 CAB 名（去重保序）。
        /// 方法名保留历史叫法；实际包含脚本/资源等所有 External CAB，不只 MonoScript。
        /// 读取后必须 <c>UnloadAll</c>，否则后续写回同一文件会触发文件占用。
        /// </summary>
        private static List<string> CollectMonoScriptCabsOrdered(string bundlePath)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath))
                return result;

            AssetsManager? am = null;
            try
            {
                am = new AssetsManager();
                var bun = am.LoadBundleFile(bundlePath);
                for (int i = 0; i < bun.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                {
                    if (!bun.file.IsAssetsFile(i)) continue;
                    var asset = am.LoadAssetsFileFromBundle(bun, i);
                    if (asset == null) continue;
                    foreach (var e in asset.file.Metadata.Externals)
                    {
                        var m = MonoScriptCabNameRegex.Match(e.PathName ?? "");
                        if (m.Success && seen.Add(m.Value))
                            result.Add(m.Value);
                    }
                }
            }
            catch
            {
                try
                {
                    foreach (Match m in MonoScriptCabNameRegex.Matches(Encoding.ASCII.GetString(File.ReadAllBytes(bundlePath))))
                    {
                        if (seen.Add(m.Value))
                            result.Add(m.Value);
                    }
                }
                catch { }
            }
            finally
            {
                try { am?.UnloadAll(); } catch { }
            }

            return result;
        }

        /// <summary>
        /// 在单个 Bundle 中查找 MonoScript CAB 名：遍历全部 assets 的 Externals，
        /// 命中第一个即返回（不收集全集）；AssetsTools 失败时再在整文件字节里搜明文。
        /// </summary>
        /// <param name="bundlePath">官方或缓存中的 Bundle 路径。</param>
        /// <returns>形如 CAB-xxxxxxxx… 的字符串；未找到则 null。</returns>
        private static string? TryFindMonoScriptCabInBundle(string bundlePath)
        {
            AssetsManager? am = null;
            try
            {
                am = new AssetsManager();
                var bun = am.LoadBundleFile(bundlePath);
                for (int i = 0; i < bun.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                {
                    if (!bun.file.IsAssetsFile(i)) continue;
                    var asset = am.LoadAssetsFileFromBundle(bun, i);
                    if (asset == null) continue;
                    foreach (var e in asset.file.Metadata.Externals)
                    {
                        var m = MonoScriptCabNameRegex.Match(e.PathName ?? "");
                        if (m.Success) return m.Value;
                    }
                }
            }
            catch
            {
                // AssetsTools 失败时：整文件字节里搜第一个 CAB- 明文
            }
            finally
            {
                try { am?.UnloadAll(); } catch { }
            }

            try
            {
                byte[] data = File.ReadAllBytes(bundlePath);
                var m = MonoScriptCabNameRegex.Match(Encoding.ASCII.GetString(data));
                if (m.Success) return m.Value;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 按映射表等长替换 Bundle 内 CAB 明文。
        /// </summary>
        /// <remarks>
        /// 读：FileShare.ReadWrite；写：先写 <c>.cabremap.tmp</c> 再覆盖，失败短重试并 GC，
        /// 避免对齐阶段 AssetsTools 句柄未释放导致 IOException。
        /// </remarks>
        /// <returns>替换命中次数；无改动则 0（不写盘）。</returns>
        private static int ReplaceMonoScriptCabsByMap(string bundlePath, Dictionary<string, string> map)
        {
            if (map == null || map.Count == 0 || !File.Exists(bundlePath))
                return 0;

            byte[] data;
            using (var fs = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                data = new byte[fs.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int n = fs.Read(data, offset, data.Length - offset);
                    if (n <= 0) break;
                    offset += n;
                }
                if (offset != data.Length)
                    data = data.AsSpan(0, offset).ToArray();
            }

            int hits = 0;
            foreach (var kv in map)
            {
                string from = kv.Key;
                string to = kv.Value;
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                    continue;
                if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
                    continue;

                byte[] src = Encoding.ASCII.GetBytes(from);
                byte[] target = Encoding.ASCII.GetBytes(to);
                if (src.Length != target.Length)
                {
                    Log.Warn($"跳过不等长 CAB 替换: {from} -> {to}");
                    continue;
                }

                for (int i = 0; i <= data.Length - src.Length; i++)
                {
                    bool ok = true;
                    for (int j = 0; j < src.Length; j++)
                    {
                        if (data[i + j] != src[j]) { ok = false; break; }
                    }
                    if (!ok) continue;
                    Buffer.BlockCopy(target, 0, data, i, target.Length);
                    hits++;
                    i += src.Length - 1;
                }
            }

            if (hits <= 0)
                return 0;

            string tmp = bundlePath + ".cabremap.tmp";
            File.WriteAllBytes(tmp, data);

            Exception? last = null;
            for (int attempt = 0; attempt < 15; attempt++)
            {
                try
                {
                    File.Copy(tmp, bundlePath, overwrite: true);
                    try { File.Delete(tmp); } catch { }
                    return hits;
                }
                catch (IOException ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(40 * (attempt + 1));
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            try { File.Delete(tmp); } catch { }
            throw new IOException($"无法写回 CAB 重映射: {bundlePath}", last);
        }
    }
}