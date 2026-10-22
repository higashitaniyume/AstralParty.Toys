using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ProtoUntyped;
using ResourceModLoader.Mod.Item;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.Patch
{
    public class FUIPatch : IPatch
    {
        FairyGUIPackage package;
        AssetTypeValueField field;
        List<Tuple<string, string, string>> atlas = new List<Tuple<string, string, string>> ();
        public void Init(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            field = manager.GetBaseField(assets, file);
            var data = field["m_Script"].AsByteArray;
            package = new FairyGUIPackage(data);
        }
        public bool PerformPatch(string source)
        {
            var bPath = Path.GetDirectoryName(source);
            var np = new FairyGUIPackage(File.ReadAllBytes(source));
            package.Merge(np);
            foreach(var i in np.items)
            {
                if (i.type == GuiItemType.Atlas)
                    atlas.Add(new Tuple<string,string, string>(source, Path.Combine(bPath, $"{np.pkgName}_{i.originalPath}"), $"{package.pkgName}_{Path.GetFileNameWithoutExtension(i.path)}"));
            }
            return true;
        }

        public void Finalize(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            //File.WriteAllBytes("1.bytes", package.GetBytes());
            field["m_Script"].AsByteArray = package.GetBytes();
            file.SetNewData(field);
        }
        void IPatch.AfterPatch(CommonPatchItem item,ModContext modContext)
        {
            foreach(var (s,f,n) in atlas)
            {
                if (!Path.Exists(f))
                {
                    Report.Warning(s, $"FGUI的依赖资产{Path.GetFileName(f)}未找到");
                }
                else
                {
                    modContext.Add(new WrappableFileItem(item.priority,f, n,"Common_atlas0"));
                    Report.AddToSameModPack(f, s);
                }
            }
        }
    }

    public enum GuiItemType
    {
        Image = 0,
        MovieClip = 1,
        Font = 5,
        Component = 3,
        Atlas = 4,
        Sound = 2,
        Misc = 7
    }
    public class GuiItem
    {
        public GuiItemType type;
        public string? id;
        public string? file;
        public string? path;
        public string? originalPath;
        public string? name;
        public byte[] raw;
        ushort stIdx;
        ushort fileIdx;
        ushort nameIdx;
        ushort pathIdx;
        int len;
        public GuiItem(BigEndianReader reader, int len, List<string> st)
        {
            this.len = len;
            long startPos = reader.BaseStream.Position;
            type = (GuiItemType)reader.ReadByte();
            stIdx = reader.ReadUInt16();
            id = FairyGUIPackage.GetStringTable(st, stIdx);
            nameIdx = reader.ReadUInt16();
            name = FairyGUIPackage.GetStringTable(st, nameIdx);
            fileIdx = reader.ReadUInt16();
            file = FairyGUIPackage.GetStringTable(st,fileIdx);
            
            pathIdx = reader.ReadUInt16();
            path = FairyGUIPackage.GetStringTable(st,pathIdx);
            originalPath = path;

            reader.BaseStream.Position = startPos;
            raw = reader.ReadBytes(len);
        }
        public void TransferStringTable(List<string> stringTables, List<string> old)
        {
            nameIdx = FairyGUIPackage.GetOrAddStringTable(stringTables, name);
            if(type == GuiItemType.MovieClip)
            {
                byte[] tBuf = new byte[4];
                using var stream = new MemoryStream(raw);  
                using BigEndianReader reader = new BigEndianReader(stream);
                FairyGUIPackage.SeekBlock(reader, 23, 1);

                short count = reader.ReadInt16();
                for(int i = 0; i < count; i++)
                {
                    short len = reader.ReadInt16();
                    long sp = reader.BaseStream.Position;
                    //reader.ReadInt32(); * 5
                    reader.BaseStream.Position += 4*5;
                    int wp = (int)reader.BaseStream.Position;
                    ushort id = reader.ReadUInt16();
                    BinaryPrimitives.WriteUInt16BigEndian(tBuf, FairyGUIPackage.TransferStringTableId(old, stringTables, id));
                    raw[wp] = tBuf[0];
                    raw[wp+1] = tBuf[1];

                    reader.BaseStream.Position=sp + len;
                }
            }
        }
        public void PrefixIdAndAddST(int prefixId, List<string> stringTables)
        {
            string prefix = "";
            if (type == GuiItemType.Atlas) prefix = $"p{prefixId}_";

            id = prefix + id;
            stIdx = FairyGUIPackage.GetOrAddStringTable(stringTables, id);
            if (file != null)
            {
                file = prefix + file;
                fileIdx = FairyGUIPackage.GetOrAddStringTable(stringTables, file);
            }
            path = prefix + path;
            pathIdx = FairyGUIPackage.GetOrAddStringTable(stringTables, path);
        }
        public void Write(BigEndianWriter writer)
        {
            writer.Write(len);
            writer.Write((byte)type);
            writer.Write(stIdx);
            writer.Write(nameIdx);
            writer.Write(fileIdx);
            writer.Write(pathIdx);
            writer.Write(raw.AsSpan(9));
        }
    }
    public class GuiSprite
    {
        ushort stIdx;
        ushort fileIdx;
        public string? id = "";
        public string? atlas = "";
        public byte[] raw;
        int len;
        public GuiSprite(BigEndianReader reader, int len, List<string> st)
        {
            this.len = len;
            long startPos = reader.BaseStream.Position;
            stIdx = reader.ReadUInt16();
            id = FairyGUIPackage.GetStringTable(st, stIdx);
            fileIdx = reader.ReadUInt16();
            atlas = FairyGUIPackage.GetStringTable(st, fileIdx);
            reader.BaseStream.Position = startPos;
            raw = reader.ReadBytes(len);
        }
        public void TransferStringTables(List<string> stringTables)
        {
            if(id != null)
                stIdx= FairyGUIPackage.GetOrAddStringTable(stringTables, id);
        }
        public void PrefixIdAndAddST(int prefixId, List<string> stringTables)
        {
            if (atlas != null)
            {
                atlas = $"p{prefixId}_{atlas}";
                fileIdx = FairyGUIPackage.GetOrAddStringTable(stringTables, atlas);
            }
        }
        public void Write(BigEndianWriter writer)
        {
            writer.Write((ushort)len);
            writer.Write((ushort)stIdx);
            writer.Write((ushort)fileIdx);
            writer.Write(raw.AsSpan(4));
        }
    }
    public class FairyGUIPackage
    {
        int version;
        public string pkgId;
        public string pkgName;
        List<string> stringTables = new List<string>();
        List<Tuple<ushort, ushort>> deps = new List<Tuple<ushort, ushort>>();
        public List<GuiItem> items = new List<GuiItem>();
        public List<GuiSprite> sprites = new List<GuiSprite>();
        List<byte[]> phtd = new List<byte[]>();
        int atlasTransformCounter = 0;
        byte[] res1;
        public FairyGUIPackage(byte[] data)
        {
            using var s = new MemoryStream(data);
            using var reader = new BigEndianReader(s);
            reader.BaseStream.Position = 0;
            reader.ReadUInt32();
            version = reader.ReadInt32();
            reader.ReadBoolean();//Compressed
            pkgId = ReadUShortPrefixedString(reader);
            pkgName = ReadUShortPrefixedString(reader);
            res1 = reader.ReadBytes(20);//Skip

            long indexTablePos = reader.BaseStream.Position;

            //Read String Table
            SeekBlock(reader, indexTablePos, 4);
            int stringTableCount = reader.ReadInt32();
            for (int i = 0; i < stringTableCount; i++)
            {
                stringTables.Add(ReadUShortPrefixedString(reader));
            }

            //Read dep
            SeekBlock(reader, indexTablePos, 0);
            int depCnt = reader.ReadInt16();
            for (int i = 0; i < depCnt; i++)
            {
                deps.Add(new Tuple<ushort, ushort>(reader.ReadUInt16(), reader.ReadUInt16()));
            }

            SeekBlock(reader, indexTablePos, 1);
            int itemCnt = reader.ReadUInt16();
            for (int i = 0; i < itemCnt; i++)
            {
                int len = reader.ReadInt32();
                items.Add(new GuiItem(reader, len, stringTables));
            }
            SeekBlock(reader, indexTablePos, 2);
            int spriteCnt = reader.ReadInt16();
            for (int i = 0; i < spriteCnt; i++)
            {
                int len = reader.ReadUInt16();
                sprites.Add(new GuiSprite(reader, len, stringTables));
            }
            if (SeekBlock(reader, indexTablePos, 3))
            {
                int c = reader.ReadUInt16();
                for (int i = 0; i < c; i++) phtd.Add(ReadI32PrefixedBytes(reader));
            }
        }

        public void Merge(FairyGUIPackage target)
        {
            atlasTransformCounter++;
            foreach (var item in target.items)
            {
                item.PrefixIdAndAddST(atlasTransformCounter, stringTables);
                int found = -1;
                for (int i = 0; found == -1 && i < items.Count; i++)
                    if (items[i].id == item.id) found = i;
                if (found == -1)
                    items.Add(item);
                else
                    items[found] = item;
                item.TransferStringTable(stringTables,target.stringTables);
            }
            foreach (var sprite in target.sprites)
            {
                sprite.PrefixIdAndAddST(atlasTransformCounter, stringTables);
                int found = -1;
                for (int i = 0; found == -1 && i < sprites.Count; i++)
                    if (sprites[i].id == sprite.id) found = i;
                if (found == -1)
                    sprites.Add(sprite);
                else
                    sprites[found] = sprite;
                sprite.TransferStringTables(stringTables);
            }
        }
        public byte[] GetBytes()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BigEndianWriter(ms))
            {
                // 写入文件头
                writer.Write(0x46475549u);      // magic
                writer.Write(version);
                writer.Write(false);
                WriteUShortPrefixedString(writer, pkgId);
                WriteUShortPrefixedString(writer, pkgName);
                writer.Write(res1);

                long indexTablePos = writer.BaseStream.Position;

                // 块索引范围 0-4
                int segCount = 5;
                // 写入索引表占位符（全部使用长偏移）
                writer.Write((byte)segCount);
                for (int i = 0; i < segCount; i++)
                {
                    writer.Write((byte)0);       // useShort = 0 (4字节偏移)
                    writer.Write(0u);            // 偏移占位
                }

                long[] blockOffsets = new long[segCount];

                // 块0：依赖
                blockOffsets[0] = writer.BaseStream.Position - indexTablePos;
                writer.Write((short)deps.Count);
                foreach (var dep in deps)
                {
                    writer.Write(dep.Item1);
                    writer.Write(dep.Item2);
                }
                //分支管理（不进行写入）
                writer.Write((short)0);

                // 块1：项目
                blockOffsets[1] = writer.BaseStream.Position - indexTablePos;
                writer.Write((ushort)items.Count);
                foreach (var item in items)
                {
                    item.Write(writer);
                }

                // 块2：精灵
                blockOffsets[2] = writer.BaseStream.Position - indexTablePos;
                writer.Write((short)sprites.Count);
                foreach (var sprite in sprites)
                {
                    sprite.Write(writer);
                }

                // 块3：phtd
                blockOffsets[3] = writer.BaseStream.Position - indexTablePos;
                writer.Write((ushort)phtd.Count);
                foreach (var data in phtd)
                {
                    WriteI32PrefixedBytes(writer, data);
                }

                // 块4：字符串表
                blockOffsets[4] = writer.BaseStream.Position - indexTablePos;
                writer.Write(stringTables.Count);
                foreach (var str in stringTables)
                {
                    WriteUShortPrefixedString(writer, str);
                }

                // 回填索引表
                writer.BaseStream.Position = indexTablePos;
                writer.Write((byte)segCount);
                writer.Write(false);
                for (int i = 0; i < segCount; i++)
                {
                    writer.Write((uint)blockOffsets[i]);        // 实际偏移
                }

                return ms.ToArray();
            }
        }
        public static bool SeekBlock(BigEndianReader reader, long indexTablePos, int blockIndex)
        {
            long tmp = reader.BaseStream.Position;
            reader.BaseStream.Position = indexTablePos;

            byte segCount = reader.ReadByte();

            if (blockIndex < segCount)
            {
                bool useShort = reader.ReadByte() == 1;      // 是否使用短索引（2字节）
                uint newPos;

                if (useShort)
                {
                    // 跳过 blockIndex 个短索引（每个2字节）
                    reader.BaseStream.Position += 2 * blockIndex;
                    newPos = reader.ReadUInt16();
                }
                else
                {
                    // 跳过 blockIndex 个长索引（每个4字节）
                    reader.BaseStream.Position += 4 * blockIndex;
                    newPos = reader.ReadUInt32();
                }

                if (newPos > 0)
                {
                    // 有效偏移，跳转到目标位置
                    reader.BaseStream.Position = indexTablePos + newPos;
                    return true;
                }
                else
                {
                    // 无效偏移，恢复原位置
                    reader.BaseStream.Position = tmp;
                    return false;
                }
            }
            else
            {
                // blockIndex 超出范围，恢复原位置
                reader.BaseStream.Position = tmp;
                return false;
            }
        }
        static string ReadUShortPrefixedString(BigEndianReader reader)
        {
            return Encoding.UTF8.GetString(ReadUShortPrefixedBytes(reader));
        }
        static byte[] ReadUShortPrefixedBytes(BigEndianReader reader)
        {
            ushort len = reader.ReadUInt16();
            return reader.ReadBytes(len);
        }
        static byte[] ReadI32PrefixedBytes(BigEndianReader reader)
        {
            int len = reader.ReadInt32();
            return reader.ReadBytes(len);
        }

        static void WriteUShortPrefixedString(BigEndianWriter writer, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            if (bytes.Length > ushort.MaxValue)
                throw new ArgumentException("String too long");
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        static void WriteI32PrefixedBytes(BigEndianWriter writer, byte[] data)
        {
            writer.Write(data.Length);
            writer.Write(data);
        }
        public static string? GetStringTable(List<string> stringTable, ushort id)
        {
            if (id == 65534) return null;
            if (id == 65533) return "";
            return stringTable[id];
        }
        public static ushort GetOrAddStringTable(List<string> stringTable,string? str)
        {
            if (str == null) return 65534;
            if (str == "") return 65533;
            int idx = stringTable.IndexOf(str);
            if(idx == -1)
            {
                idx = stringTable.Count;
                stringTable.Add(str);
            }
            return (ushort)idx;
        }
        public static ushort TransferStringTableId(List<string> bef,List<string> after, ushort id)
        {
            return GetOrAddStringTable(after,GetStringTable(bef,id));
        }
    }
}
