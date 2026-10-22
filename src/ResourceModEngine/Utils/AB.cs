using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PVRTexLib;
using AssetsTools.NET.Texture;
using System.Drawing;
using System.Drawing.Imaging;
using static ResourceModLoader.Mod.Item.ModJsonItem;
using ResourceModLoader.Module;
using ResourceModLoader.Mod.SpecialProcess;

namespace ResourceModLoader.Utils
{
    public class SimpleLogProgress : IAssetBundleCompressProgress
    {
        public void SetProgress(float progress)
        {
            Log.StepProgress($"正在压缩合并结果 {((int)(progress * 100))}%");
        }
    }
    public class ResSRec
    {
        public List<byte[]> bytes;
        public long len;
        public string name;
        public byte[] ConcatAndGet()
        {
            byte[] result = new byte[len];
            int offset = 0;
            for (int i = 0; i < bytes.Count; i++)
            {
                byte[] current = bytes[i];
                Buffer.BlockCopy(current, 0, result, offset, current.Length);
                offset += current.Length;
            }
            return result;
        }
    }
    public class PatchEntry
    {
        public int FileIndex;
        public long PathId;
        public byte[]? Data;
        public int? TypeId;
        public ushort? ScriptIndex;
    }
    class AB
    {
        private static int PID = 172001;
        public static string CreateTextAbSingle(string path, string? fileName)
        {
            int pid = ++PID;
            string dirName = Path.GetDirectoryName(path);
            if (fileName == null)
                fileName = Path.GetFileNameWithoutExtension(path);
            string bundleName = fileName + ".bundle";
            if (!Path.Exists(Path.Combine(dirName, "_generated")))
                Directory.CreateDirectory(Path.Combine(dirName, "_generated"));
            string pathAb = Path.Combine(dirName, "_generated", bundleName);
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundleInst = manager.LoadBundleFile(new MemoryStream(Resource1.ref2), "ref2.bundle");
            AssetsFileInstance assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, 0);
            AssetsFile assetsFile = assetsInst.file;

            foreach (var type in Enum.GetValues(typeof(AssetClassID)))
                if ((AssetClassID)type != AssetClassID.AssetBundle)
                    assetsFile.GetAssetsOfType((int)type).ForEach(asset => { asset.SetRemoved(); });
            var abFileInfo = assetsFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var abFileField = manager.GetBaseField(assetsInst, abFileInfo);
            abFileField["m_Name"].AsString = bundleName;
            abFileField["m_AssetBundleName"].AsString = bundleName;
            abFileField["m_Container.Array"].Children[0]["second"]["asset"]["m_PathID"].AsLong = pid;
            abFileField["m_PreloadTable.Array"].Children[0]["m_PathID"].AsLong = pid;

            abFileInfo.SetNewData(abFileField);
            var baseField = manager.CreateValueBaseField(assetsInst, (int)AssetClassID.TextAsset);


            baseField["m_Name"].AsString = fileName;
            baseField["m_Script"].AsByteArray= File.ReadAllBytes(path);

            var newInfo = AssetFileInfo.Create(assetsFile, pid, (int)AssetClassID.TextAsset);
            newInfo.SetNewData(baseField);

            assetsFile.Metadata.AddAssetInfo(newInfo);

            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].Name = "CAB-" + bundleName;
            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile);
            while (bundleInst.file.BlockAndDirInfo.DirectoryInfos.Count > 1)
                bundleInst.file.BlockAndDirInfo.DirectoryInfos.RemoveAt(1);

            if (Path.Exists(pathAb))
                File.Delete(pathAb);
            using FileStream fs = File.OpenWrite(pathAb);
            AssetsFileWriter bundleWriter = new AssetsFileWriter(fs);
            bundleInst.file.Write(bundleWriter);
            return pathAb;
        }

        public static string CreateImageAbSingle(string path,string? fileName)
        {
            int pid = ++PID;
            string dirName = Path.GetDirectoryName(path);
            if (fileName == null)
                fileName = Path.GetFileNameWithoutExtension(path);
            string bundleName = fileName + ".bundle";
            if (!Path.Exists(Path.Combine(dirName, "_generated")))
                Directory.CreateDirectory(Path.Combine(dirName, "_generated"));
            string pathAb = Path.Combine(dirName, "_generated", bundleName);
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundleInst = manager.LoadBundleFile(new MemoryStream(Resource1._ref), "ref.bundle");
            AssetsFileInstance assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, 0);
            AssetsFile assetsFile = assetsInst.file;

            foreach (var type in Enum.GetValues(typeof(AssetClassID)))
                if ((AssetClassID)type != AssetClassID.AssetBundle)
                    assetsFile.GetAssetsOfType((int)type).ForEach(asset => { asset.SetRemoved(); });
            var abFileInfo = assetsFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var abFileField = manager.GetBaseField(assetsInst, abFileInfo);
            abFileField["m_Name"].AsString = bundleName;
            abFileField["m_AssetBundleName"].AsString = bundleName;
            abFileField["m_Container.Array"].Children[0]["second"]["asset"]["m_PathID"].AsLong = pid;
            abFileField["m_PreloadTable.Array"].Children[0]["m_PathID"].AsLong = pid;

            abFileInfo.SetNewData(abFileField);
            var baseField = manager.CreateValueBaseField(assetsInst, (int)AssetClassID.Texture2D);
            baseField["m_Name"].AsString = fileName;
            if (!SetAssetFieldForTexture(baseField, path)) return "";

            var newInfo = AssetFileInfo.Create(assetsFile, pid, (int)AssetClassID.Texture2D);
            newInfo.SetNewData(baseField);

            assetsFile.Metadata.AddAssetInfo(newInfo);

            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].Name = "CAB-" + bundleName;
            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile);
            while (bundleInst.file.BlockAndDirInfo.DirectoryInfos.Count > 1)
                bundleInst.file.BlockAndDirInfo.DirectoryInfos.RemoveAt(1);

            if (Path.Exists(pathAb))
                File.Delete(pathAb);
            using FileStream fs = File.OpenWrite(pathAb);
            AssetsFileWriter bundleWriter = new AssetsFileWriter(fs);
            bundleInst.file.Write(bundleWriter);
            return pathAb;
        }
        public static bool SetAssetFieldForTexture(AssetTypeValueField baseField,string path)
        {
            var encoded = Encode(path);
            if (encoded == null) return false;
            int width = encoded.Item1;
            int height = encoded.Item2;

            AssetTypeValueField m_StreamData = baseField["m_StreamData"];
            m_StreamData["offset"].AsInt = 0;
            m_StreamData["size"].AsInt = 0;
            m_StreamData["path"].AsString = "";

            baseField["m_Width"].AsInt = width;
            baseField["m_Height"].AsInt = height;


            baseField["m_TextureFormat"].AsInt = (int)TextureFormat.ARGB32;
            baseField["m_TextureDimension"].AsInt = 2;
            baseField["m_ImageCount"].AsInt = 1;
            baseField["m_MipCount"].AsInt = 1;
            baseField["m_ForcedFallbackFormat"].AsInt = 4;
            baseField["m_ColorSpace"].AsInt = 1;
            baseField["m_CompleteImageSize"].AsInt = encoded.Item3.Length;

            baseField["m_TextureSettings"]["m_FilterMode"].AsInt = 1;
            baseField["m_TextureSettings"]["m_Aniso"].AsInt = 1;
            baseField["m_TextureSettings"]["m_WrapU"].AsInt = 1;
            baseField["m_TextureSettings"]["m_WrapV"].AsInt = 1;
            baseField["m_TextureSettings"]["m_WrapW"].AsInt = 1;

            AssetTypeValueField image_data = baseField["image data"];
            image_data.Value.ValueType = AssetValueType.ByteArray;
            image_data.TemplateField.ValueType = AssetValueType.ByteArray;
            image_data.AsByteArray = encoded.Item3;

            return true;
        }
        public static Tuple<int, int, byte[]>? EncodeFromBitmap(string path)
        {
            try
            {
                using Bitmap original = new Bitmap(path);
                // 确保像素格式为 32 位 ARGB（实际 BGRA 顺序）
                Bitmap bitmap = original.PixelFormat == PixelFormat.Format32bppArgb
                    ? original
                    : new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

                // 锁定像素数据
                BitmapData data = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    int width = bitmap.Width;
                    int height = bitmap.Height;
                    int stride = data.Stride;
                    int bytesPerPixel = 4;

                    // 如果 stride 不等于 width * bytesPerPixel，需要复制到连续缓冲区
                    byte[] pixelData;
                    if (stride == width * bytesPerPixel)
                    {
                        // 直接使用扫描指针
                        pixelData = new byte[width * height * bytesPerPixel];
                        unsafe
                        {
                            fixed (byte* dest = pixelData)
                            {
                                Buffer.MemoryCopy((void*)data.Scan0, dest, pixelData.Length, pixelData.Length);
                            }
                        }
                    }
                    else
                    {
                        // 逐行复制，去除行填充
                        pixelData = new byte[width * height * bytesPerPixel];
                        unsafe
                        {
                            byte* src = (byte*)data.Scan0;
                            fixed (byte* dest = pixelData)
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    int srcOffset = y * stride;
                                    int destOffset = y * width * bytesPerPixel;
                                    Buffer.MemoryCopy(src + srcOffset, dest + destOffset, width * bytesPerPixel, width * bytesPerPixel);
                                }
                            }
                        }
                    }

                    // 定义纹理头：使用 BGRA 顺序（因为 System.Drawing 的 32bppArgb 实际为 BGRA）
                    ulong bgra8888 = PVRDefine.PVRTGENPIXELID4('b', 'g', 'r', 'a', 8, 8, 8, 8);
                    using PVRTextureHeader header = new PVRTextureHeader(
                        bgra8888,
                        (uint)width,
                        (uint)height,
                        1,  // depth
                        1,  // mipmaps
                        1,  // array members
                        1   // faces
                    );

                    // 从像素数据创建纹理
                    unsafe
                    {
                        fixed (byte* ptr = pixelData)
                        {
                            using PVRTexture texture = new PVRTexture(header, ptr);

                            // 可选：翻转 Y 轴（如果需要与原方法保持一致）
                            texture.Flip(PVRTexLibAxis.Y);
                            ulong RGBA8888 = PVRDefine.PVRTGENPIXELID4('a', 'r', 'g', 'b', 8, 8, 8, 8);

                            // 转码为压缩格式
                            if (!texture.Transcode(RGBA8888,
                                                   PVRTexLibVariableType.UnsignedByteNorm,
                                                   PVRTexLibColourSpace.sRGB,
                                                   0, false))
                            {
                                return null;
                            }

                            // 获取压缩后的数据
                            byte* compressedData = (byte*)texture.GetTextureDataPointer(0);
                            int compressedSize = (int)texture.GetTextureDataSize(0);
                            byte[] result = new byte[compressedSize];
                            fixed (byte* dest = result)
                            {
                                Buffer.MemoryCopy(compressedData, dest, compressedSize, compressedSize);
                            }

                            return new Tuple<int, int, byte[]>((int)texture.GetTextureWidth(),
                                                               (int)texture.GetTextureHeight(),
                                                               result);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                    if (bitmap != original) bitmap.Dispose();
                }
            }
            catch
            {
                return null;
            }
        }
        private static Tuple<int, int, byte[]>? Encode(string path)
        {
            try
            {
                using PVRTexture texture = new PVRTexture(path);
                // Check that PVRTexLib loaded the file successfully
                if (texture.GetTextureDataSize() == 0)
                {
                    return null;
                }
                texture.Flip(PVRTexLibAxis.Y);

                // Decompress texture to the standard RGBA8888 format.
                ulong RGBA8888 = PVRDefine.PVRTGENPIXELID4('a', 'r', 'g', 'b', 8, 8, 8, 8);

                if (!texture.Transcode(RGBA8888, PVRTexLibVariableType.UnsignedByteNorm, PVRTexLibColourSpace.BT2020))
                {
                    return null;
                }
                unsafe
                {
                    byte* result = (byte*)texture.GetTextureDataPointer();
                    byte[] resultArr = new byte[texture.GetTextureDataSize()];
                    fixed (byte* ptr = resultArr)
                    {
                        Buffer.MemoryCopy(result, ptr, texture.GetTextureDataSize(), texture.GetTextureDataSize());
                    }
                    return new Tuple<int, int, byte[]>((int)texture.GetTextureWidth(), (int)texture.GetTextureHeight(), resultArr);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    return EncodeFromBitmap(path);
                }catch (Exception ex2) { }
                return null;
            }
        }

        public static Tuple<bool, List<Tuple<string, string, string>>> MergeBundles(string originalPath, List<string> bundles, string save, Action<AssetsManager, BundleFileInstance, AssetsFileInstance[], Dictionary<long, string>[], List<List<PatchEntry>>>? post = null,bool isDebugMode = false)
        {
            Log.Debug("开始修补" + originalPath);
            List<Tuple<string, string, string>> conflictResults = new List<Tuple<string, string, string>>();
            Log.SetupProgress(bundles.Count);
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundle = manager.LoadBundleFile(originalPath, false);
            bool result = true;

            string localTmp = "";
            if (bundle.file.BlockAndDirInfo.DirectoryInfos.Find(t => t.DecompressedSize > 20 * 1024 * 1024) != null)
            {
                localTmp = save + ".temp";
                FileStream bundleStream = File.Open(localTmp, FileMode.Create);
                bundle.file.Unpack(new AssetsFileWriter(bundleStream));
                bundleStream.Close();

                manager = new AssetsManager();
                bundle = manager.LoadBundleFile(localTmp);
            }
            else
            {
                bundle = manager.LoadBundleFile(originalPath);
            }

            AssetsFileInstance[] assets = new AssetsFileInstance[bundle.file.GetAllFileNames().Count];
            Dictionary<long, string>[] patched = new Dictionary<long, string>[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                patched[i] = new Dictionary<long, string>();
                assets[i] = manager.LoadAssetsFileFromBundle(bundle, i);
            }

            var inBundleFileNames = bundle.file.GetAllFileNames();
            if (inBundleFileNames.Count == 0)
            {
                Log.FinalizeProgress();
                return new Tuple<bool, List<Tuple<string, string, string>>>(false, []);
            }
            var name0 = inBundleFileNames[0];
            ResSRec? resSRec = null;
            for (int i = 1; i < inBundleFileNames.Count; i++)
            {
                if (bundle.file.IsAssetsFile(i)) continue;
                bundle.file.GetFileRange(i, out long iStart, out long iLength);
                bundle.file.DataReader.Position = iStart;
                byte[] iBytes = bundle.file.DataReader.ReadBytes((int)iLength);
                resSRec = new ResSRec(){bytes=new List<byte[]> { iBytes }, len = iLength, name = $"archive:/{name0}/{inBundleFileNames[i]}" };
                break;
            }
            List<List<PatchEntry>> patches = new List<List<PatchEntry>>();
            foreach (string file in bundles)
            {
                Log.Debug("开始合并bundle:"+file);
                var r = PatchBundle(manager,bundle, assets, file, patched, resSRec, save + ".temp1", conflictResults,isDebugMode);
                if (r == null)
                {
                    result = false;
                    conflictResults.Clear();
                    conflictResults.Add(new Tuple<string, string, string>(file, "", ""));
                    break;
                }
                patches.Add(r);
                MergeAssetBundleContainers(manager, bundle, assets, file, save + ".temp1");
                Log.Info($"从 {file} 提取和替换了共 {r.Count} 个文件");
            }
            if (!result) {
                return new Tuple<bool, List<Tuple<string, string, string>>>(result, conflictResults);
            }
            if (post != null)
                post(manager,bundle, assets, patched,patches);


            foreach (var pl in patches)
                foreach (var entry in pl)
                {
                    if (entry.Data == null) continue;
                    if (entry.TypeId == null)
                        assets[entry.FileIndex].file.GetAssetInfo(entry.PathId).Replacer = new ContentReplacerFromBuffer(entry.Data);
                    else
                    {
                        var asif = AssetFileInfo.Create(assets[entry.FileIndex].file, entry.PathId, (int)entry.TypeId, entry.ScriptIndex ?? 0);
                        if (asif != null)
                        {
                            asif.Replacer = new ContentReplacerFromBuffer(entry.Data);
                            assets[entry.FileIndex].file.Metadata.AddAssetInfo(asif);
                        }
                    }
                }

            MemoryStream[] streams = new MemoryStream[assets.Count()];
            for (int i = 0; i < assets.Count(); i++)
                if (assets[i] != null)
                {
                    streams[i] = new MemoryStream();
                    AssetsFileWriter w = new AssetsFileWriter(streams[i]);
                    assets[i].file.Write(w);
                }

            for (int i = 0; i < assets.Count(); i++)
                if (streams[i] != null)
                    bundle.file.BlockAndDirInfo.DirectoryInfos[i].Replacer = new ContentReplacerFromStream(streams[i]);

            using (FileStream fsw = File.OpenWrite(save + ".preload"))
            {
                AssetsFileWriter tmpWriter = new AssetsFileWriter(fsw);
                bundle.file.Write(tmpWriter);
            }
            bundle.file.Close();

            if (resSRec != null)
                ABMergeTransformStreamData(save, resSRec, patches,isDebugMode);
            else
                File.Move(save + ".preload", save + ".uncompressed", true);

            using FileStream fsr = File.OpenRead(save + ".uncompressed");
            AssetsFileReader bundleRead = new AssetsFileReader(fsr);
            bundle.file.Read(bundleRead);
            using FileStream fs = File.OpenWrite(save);
            AssetsFileWriter bundleWriter = new AssetsFileWriter(fs);
            Log.SetupProgress(0);
            bundle.file.Pack(bundleWriter, AssetBundleCompressionType.LZ4, false, new SimpleLogProgress());
            Log.FinalizeProgress();
            fsr.Close();
            File.Delete(save + ".uncompressed");            
            if (localTmp != "")
            {
                bundle.file.Close();
                File.Delete(localTmp);
            }
            return new Tuple<bool,List<Tuple<string,string,string>>>(result, conflictResults);
        }
        private static List<PatchEntry>? PatchBundle(AssetsManager manager,BundleFileInstance bundleFileInst, AssetsFileInstance[] assets, string toLoad, Dictionary<long,string>[] patched,ResSRec? resS, string cacheFile,List<Tuple<string, string,string>> conflictResults, bool isDebugMode)
        {
            List<PatchEntry> result = new List<PatchEntry>();
            AssetsManager incomingManager = new AssetsManager();
            BundleFileInstance incomingBundle = incomingManager.LoadBundleFile(toLoad, false);

            //根据预计大小决定从磁盘读取还是直接从从硬盘解压
            string localTmp = "";
            if (incomingBundle.file.BlockAndDirInfo.DirectoryInfos.Find(t => t.DecompressedSize > 20 * 1024 * 1024) != null)
            {
                localTmp = cacheFile;
                FileStream bundleStream = File.Open(localTmp, FileMode.Create);
                incomingBundle.file.Unpack(new AssetsFileWriter(bundleStream));
                bundleStream.Close();

                incomingManager = new AssetsManager();
                incomingBundle = incomingManager.LoadBundleFile(localTmp);
            }
            else
            {
                incomingBundle = incomingManager.LoadBundleFile(toLoad);
            }
            
            //resS检测，追加一段全局ress表
            bool found = false;
            for (int i = 0; i < incomingBundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                if (incomingBundle.file.IsAssetsFile(i)) continue;
                if (resS == null) return null;
                if (found) return null;
                incomingBundle.file.GetFileRange(i, out long iStart, out long iLength);
                incomingBundle.file.DataReader.Position = iStart;
                byte[] iBytes = incomingBundle.file.DataReader.ReadBytes((int)iLength);
                resS.bytes.Add(iBytes);
                resS.len += iLength;
                found = true;
            }
            if (!found && resS != null)
                resS.bytes.Add([]);

            //传入文件遍历
            for (int i = 0; i < incomingBundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                AssetsFileInstance incomingAsset = incomingManager.LoadAssetsFileFromBundle(incomingBundle, i);
                if (incomingAsset == null)
                    continue;
                var incomingContainers = GetContainerDic(incomingManager, incomingAsset);
                AssetsFile incomingAssetsFile = incomingAsset.file;
                for (var ai = 0; ai < assets.Length; ai++)
                {
                    var asset = assets[ai];
                    if (asset == null)
                        continue;
                    var originalContainers = GetContainerDic(manager, asset);

                    Log.SetupProgress(assets.Length);
                    foreach (var incomingFile in incomingAssetsFile.AssetInfos)
                    {
                        if (incomingFile.TypeId == (int)AssetClassID.AssetBundle)
                        {
                            Log.StepProgress("Bundle Manifest");
                            continue;
                        }
                        FieldTree iField = incomingManager.GetBaseField(incomingAsset, incomingFile);
                        var iName = iField["m_Name"];
                        if (iName.IsDummy)
                        {
                            Log.StepProgress("<NoName>");
                            continue;
                        }
                        Log.StepProgress("File "+iName.AsString);

                        bool needCreate = true;
                        //原有文件遍历
                        foreach (var file in asset.file.AssetInfos)
                        {
                            FieldTree oField = manager.GetBaseField(asset, file);
                            var oName = oField["m_Name"];
                            if (oName.IsDummy) continue;
                            //container不同，且path不同的，直接跳过
                            if (
                                (!incomingContainers.ContainsKey(incomingFile.PathId) || !originalContainers.ContainsKey(file.PathId) || incomingContainers[incomingFile.PathId] != originalContainers[file.PathId])
                                &&
                                incomingFile.PathId != file.PathId
                            )
                                continue;

                            //名称匹配
                            if (iName.AsString != oName.AsString)
                                continue;

                            //这里虽然找到了同名文件，但是还得检验一次类型
                            if(incomingFile.TypeId != file.TypeId)
                            {
                                Log.Warn($"Type不匹配 {iName.AsString}@({incomingFile.TypeId}/{file.TypeId})");
                                Report.Warning(toLoad, $"Type不匹配 {iName.AsString}@({incomingFile.TypeId}/{file.TypeId})");
                                continue;
                            }

                            if (!FieldTree.IsSame(iField, oField))
                            {
                                if (patched[ai].ContainsKey(file.PathId))
                                {
                                    conflictResults.Add(new Tuple<string, string, string>(iName.AsString, toLoad, patched[ai][file.PathId]));
                                    continue;
                                }
                                var specialProcessor=SpecialManager.GetSpecialProcessor(incomingFile, iField);
                                if (specialProcessor != null)
                                {
                                    if (specialProcessor.Handle(iField, oField))
                                    {
                                        file.SetNewData(oField.Root);
                                        result.Add(new PatchEntry { FileIndex = ai, Data = null, PathId = file.PathId, ScriptIndex = 0, TypeId = 0 });
                                        needCreate = false;
                                        break;
                                    }
                                    continue;
                                }
                                else if (incomingFile.TypeId == 114)
                                {
                                    if (!IsTypeTreeMatch(incomingFile, incomingAssetsFile, file, asset.file))
                                    {
                                        Log.Warn($"Mono script Type不匹配 {iName.AsString}@({incomingFile.TypeId})");
                                        Report.Warning(toLoad, $"(Mono script) {iName.AsString}@({incomingFile.TypeId})");
                                    }
                                    FieldTree.CopyValues(iField, oField);
                                    file.SetNewData(oField.Root);
                                    result.Add(new PatchEntry { FileIndex = ai, Data = null, PathId = file.PathId, ScriptIndex = 0, TypeId = 0 });
                                }
                                else { 
                                    result.Add(ReadPatchEntry(incomingFile, incomingAssetsFile, ai, file.PathId, null));
                                    patched[ai][file.PathId] = toLoad;
                                    if (!isDebugMode)
                                        Log.StepProgress($"Patched {iName.AsString} -> {toLoad}", 0);
                                    else
                                        Log.Debug($"Patched {iName.AsString} -> {toLoad}");
                                }
                            }
                            needCreate = false;
                        }

                        //传入文件需要进行创建
                        if (needCreate)
                        {
                            //会覆盖之前创建的patch
                            if (patched[ai].ContainsKey(incomingFile.PathId))
                            {
                                string name = incomingFile.PathId.ToString();
                                conflictResults.Add(new Tuple<string, string, string>(iName.AsString, toLoad, patched[ai][incomingFile.PathId]));
                            }
                            else
                            {
                                var existing = asset.file.GetAssetInfo(incomingFile.PathId);
                                //不存在同pathId：完全新的创建
                                if (existing == null)
                                {
                                    var localTypeTree = GetMatchedTypeTree(incomingFile, incomingAssetsFile, asset.file);
                                    // type匹配失败，则不强求将数据复制过去，抛出错误后结束
                                    if (localTypeTree != null)
                                    {
                                        var localScriptIndex = localTypeTree.ScriptTypeIndex;
                                        result.Add(ReadPatchEntry(incomingFile, incomingAssetsFile, ai, incomingFile.PathId, incomingFile.TypeId, localScriptIndex));
                                        patched[ai][incomingFile.PathId] = toLoad;
                                        if (!isDebugMode)
                                            Log.StepProgress($"Add {iName.AsString} -> {toLoad}", 0);
                                        else
                                            Log.Debug($"Add {iName.AsString} -> {toLoad}");
                                    }
                                    else
                                    {
                                        Log.Warn($"Type不存在 {iName.AsString}@({incomingFile.PathId},{incomingFile.TypeId})");
                                        Report.Warning(toLoad, $"Type不存在 {iName.AsString}@({incomingFile.PathId},{incomingFile.TypeId})");
                                    }
                                }
                                else if (existing.TypeId == incomingFile.TypeId)
                                {
                                    result.Add(ReadPatchEntry(incomingFile, incomingAssetsFile, ai, incomingFile.PathId, null));
                                    patched[ai][incomingFile.PathId] = toLoad;
                                }
                                else
                                {
                                    string name = incomingFile.PathId.ToString();
                                    var field = incomingManager.GetBaseField(incomingAsset, incomingFile);
                                    if (field != null && !field["m_Name"].IsDummy)
                                        name = field["m_Name"].AsString;
                                    conflictResults.Add(new Tuple<string, string, string>(iName.AsString, toLoad, name));
                                }
                            }
                        }
                    }
                    Log.FinalizeProgress();
                }
            }
            if (localTmp != "")
            {
                incomingBundle.file.Close();
                File.Delete(localTmp);
            }
            return result;
        }

        private static PatchEntry ReadPatchEntry(AssetFileInfo incomingFile, AssetsFile incomingAssetsFile, int fileIndex, long pathId, int? typeId, ushort? scriptIndex = null)
        {
            long start = incomingFile.GetAbsoluteByteOffset(incomingAssetsFile);
            long size = incomingFile.ByteSize;
            incomingAssetsFile.Reader.Position = (int)start;
            byte[] data = incomingAssetsFile.Reader.ReadBytes((int)size);
            return new PatchEntry { FileIndex = fileIndex, PathId = pathId, Data = data, TypeId = typeId, ScriptIndex = scriptIndex };
        }
        private static bool IsTypeTreeMatch(AssetFileInfo incomingFile, AssetsFile incomingAssetsFile, AssetFileInfo target, AssetsFile targetAsset)
        {
            var scriptIndex = incomingFile.GetScriptIndex(incomingAssetsFile);
            var incomingTypeTree = incomingAssetsFile.Metadata.FindTypeTreeTypeByScriptIndex(scriptIndex);
            var targetScriptIndex = target.GetScriptIndex(targetAsset);
            var targetTypeTree = targetAsset.Metadata.FindTypeTreeTypeByScriptIndex(targetScriptIndex);
            return incomingTypeTree.TypeHash.Equals(targetTypeTree.TypeHash);
        }
        private static TypeTreeType? GetMatchedTypeTree(AssetFileInfo incomingFile, AssetsFile incomingAssetsFile,AssetsFile target)
        {

            var scriptIndex = incomingFile.GetScriptIndex(incomingAssetsFile);
            var incomingTypeTree = incomingAssetsFile.Metadata.FindTypeTreeTypeByScriptIndex(scriptIndex);

            var localTypeTree = target.Metadata.TypeTreeTypes
                .FirstOrDefault(t => t.ScriptIdHash.Equals(incomingTypeTree.ScriptIdHash))
                ?? target.Metadata.TypeTreeTypes
                    .FirstOrDefault(t => t.TypeHash.Equals(incomingTypeTree.TypeHash));
            return localTypeTree;
        }

        private static void MergeAssetBundleContainers(AssetsManager manager, BundleFileInstance bundle, AssetsFileInstance[] assets, string patchPath, string cacheFile)
        {
            AssetsManager patchManager = new AssetsManager();
            BundleFileInstance patchBundle = patchManager.LoadBundleFile(patchPath, false);

            string localTmp = "";
            if (patchBundle.file.BlockAndDirInfo.DirectoryInfos.Find(t => t.DecompressedSize > 20 * 1024 * 1024) != null)
            {
                localTmp = cacheFile;
                FileStream bundleStream = File.Open(localTmp, FileMode.Create);
                patchBundle.file.Unpack(new AssetsFileWriter(bundleStream));
                bundleStream.Close();
                patchManager = new AssetsManager();
                patchBundle = patchManager.LoadBundleFile(localTmp);
            }
            else
            {
                patchBundle = patchManager.LoadBundleFile(patchPath);
            }

            for (int pi = 0; pi < patchBundle.file.BlockAndDirInfo.DirectoryInfos.Count; pi++)
            {
                if (!patchBundle.file.IsAssetsFile(pi)) continue;
                var patchAsset = patchManager.LoadAssetsFileFromBundle(patchBundle, pi);
                if (patchAsset == null) continue;

                var patchAbAssets = patchAsset.file.GetAssetsOfType(AssetClassID.AssetBundle);
                if (patchAbAssets.Count == 0) continue;
                var patchAbInfo = patchAbAssets[0];
                var patchAbField = patchManager.GetBaseField(patchAsset, patchAbInfo);

                if (patchAbField["m_Container.Array"] == null || patchAbField["m_Container.Array"].IsDummy || patchAbField["m_PreloadTable.Array"] == null || patchAbField["m_PreloadTable.Array"].IsDummy)
                    continue;

                var patchContainers = patchAbField["m_Container.Array"].Children;
                var patchPreload = patchAbField["m_PreloadTable.Array"].Children;

                var patchContainerNames = new HashSet<string>();
                foreach (var c in patchContainers)
                    patchContainerNames.Add(c["first"].AsString);

                for (int ai = 0; ai < assets.Length; ai++)
                {
                    if (assets[ai] == null) continue;
                    var origAbAssets = assets[ai].file.GetAssetsOfType(AssetClassID.AssetBundle);
                    if (origAbAssets.Count == 0) continue;
                    var origAbInfo = origAbAssets[0];
                    var origAbField = manager.GetBaseField(assets[ai], origAbInfo);

                    var origContainers = origAbField["m_Container.Array"].Children;

                    bool matched = false;
                    foreach (var c in origContainers)
                    {
                        if (patchContainerNames.Contains(c["first"].AsString))
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched) continue;

                    var origContainerNames = new HashSet<string>();
                    foreach (var c in origContainers)
                        origContainerNames.Add(c["first"].AsString);

                    var origPreload = origAbField["m_PreloadTable.Array"].Children;
                    var origPreloadIds = new HashSet<long>();
                    foreach (var p in origPreload)
                        origPreloadIds.Add(p["m_PathID"].AsLong);

                    bool modified = false;

                    foreach (var pc in patchContainers)
                    {
                        var name = pc["first"].AsString;
                        if (!origContainerNames.Contains(name))
                        { 
                            var newContainer = ValueBuilder.DefaultValueFieldFromTemplate(pc.TemplateField);
                            newContainer["first"].AsString = name;
                            newContainer["second"]["asset"]["m_PathID"].AsLong = pc["second"]["asset"]["m_PathID"].AsLong;
                            origContainers.Add(newContainer);
                            modified = true;
                        }
                    }

                    foreach (var pp in patchPreload)
                    {
                        var pathId = pp["m_PathID"].AsLong;
                        if (!origPreloadIds.Contains(pathId))
                        {
                            var newPreload = ValueBuilder.DefaultValueFieldFromTemplate(pp.TemplateField);
                            newPreload["m_PathID"].AsLong = pathId;
                            origPreload.Add(newPreload);
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        origAbInfo.SetNewData(origAbField);
                        bundle.file.BlockAndDirInfo.DirectoryInfos[ai].SetNewData(assets[ai].file);
                    }
                    break;
                }
            }

            if (localTmp != "")
            {
                patchBundle.file.Close();
                File.Delete(localTmp);
            }
        }

        private static void ABMergeTransformStreamData(string save,ResSRec resSRec, List<List<PatchEntry>> patches,bool isDebug)
        {
            using FileStream fsr = File.OpenRead(save + ".preload");
            AssetsManager manager = new AssetsManager();
            var bundle = manager.LoadBundleFile(fsr);

            ulong offset = (ulong)resSRec.bytes[0].Length;
            for(int i = 0; i < patches.Count; i++)
            {
                foreach (var entry in patches[i])
                {
                    var asf = manager.LoadAssetsFileFromBundle(bundle, entry.FileIndex);
                    var asif = asf.file.GetAssetInfo(entry.PathId);
                    if (asif == null) continue;
                    try
                    {
                        var field = manager.GetBaseField(asf, asif);
                        if (field["m_StreamData"].IsDummy) continue;
                        if (field["m_StreamData"]["size"].AsULong == 0) continue;
                        field["m_StreamData"]["path"].AsString = resSRec.name;
                        field["m_StreamData"]["offset"].AsULong += offset;
                        if (!field["m_Name"].IsDummy)
                            Log.StepProgress("更新Streaming " + field["m_Name"].AsString, 0);
                        asif.SetNewData(field);
                        bundle.file.BlockAndDirInfo.DirectoryInfos[entry.FileIndex].SetNewData(asf.file);
                    }
                    catch(Exception e){
                        if (isDebug)
                        {
                            Log.Warn($"处理{entry.PathId}时发生异常："+e.ToString());
                        }
                    }
                }

                offset += (ulong) resSRec.bytes[i].Length;
            }
            var ian = bundle.file.GetAllFileNames();
            Log.StepProgress("更新并写回ResS", 0);
            for (int i = 0; i < ian.Count; i++)
            {
                if (bundle.file.IsAssetsFile(i)) continue;
                bundle.file.BlockAndDirInfo.DirectoryInfos[i].SetNewData(resSRec.ConcatAndGet());
                break;
            }

            using (FileStream fsw = File.OpenWrite(save + ".uncompressed"))
            {
                AssetsFileWriter tmpWriter = new AssetsFileWriter(fsw);
                bundle.file.Write(tmpWriter);
            }
            manager.UnloadAll();
            File.Delete(save + ".preload");
        }
        public static Dictionary<long, string> GetContainerDic(AssetsManager manager, AssetsFileInstance assets)
        {
            Dictionary<long, string> dic = new Dictionary<long, string>();
            foreach (var asset in assets.file.GetAssetsOfType(AssetClassID.AssetBundle))
            {
                var field = manager.GetBaseField(assets, asset);


                foreach (var containerDesc in field["m_Container.Array"].Children)
                {
                    var ctr = containerDesc["first"].AsString;
                    var pathId = containerDesc["second"]["asset"]["m_PathID"].AsLong;
                    dic[pathId] = ctr;
                }
            }
            return dic;
        }
    }
}

