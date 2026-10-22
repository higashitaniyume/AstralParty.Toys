using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;

namespace ResourceModLoader.Tool.WWiseTool
{
    /// <summary>
    /// 表示一个声音项，对应一个DIDX描述符
    /// </summary>
    public class SoundItem
    {
        public uint DescriptorId { get; set; }          // 声音ID (source_id)
        public List<string> EventNames { get; set; }    // 引用该声音的事件名称列表
        public bool Modified { get; set; }              // 是否已被修改
        public byte[] Data { get; set; }                // 声音数据
        public long FileOffset { get; set; }            // 在原始文件DATA部分中的偏移
        public int OriginalSize { get; set; }           // 原始数据长度
    }

    /// <summary>
    /// 表示一个事件
    /// </summary>
    public class SoundEvent
    {
        public uint Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// DIDX描述符结构
    /// </summary>
    internal class DIDXDescriptor
    {
        public uint Id { get; set; }
        public uint Offset { get; set; }   // 相对于DATA起始的偏移
        public uint Size { get; set; }
    }

    /// <summary>
    /// STID条目
    /// </summary>
    internal class STIDEntry
    {
        public uint Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// 原始Section信息
    /// </summary>
    internal class RawSection
    {
        public string Magic { get; set; }
        public uint Size { get; set; }
        public byte[] Body { get; set; }
        public long StartPosition { get; set; } // 在文件中的起始位置
    }

    internal class ChainableObjectReference
    {
        public List<uint> Next = new List<uint>();
        public List<uint> SourceId = new List<uint>();
        public long DataOffset = 0;
        public List<long> DurationOffset = new List<long>();
        public bool UnResolved { get; set; }
        public bool End()
        {
            return SourceId.Count > 0;
        }
    }

    /// <summary>
    /// Wwise声音银行解析与修改类（支持可变长度数据）
    /// </summary>
    public class WwiseBank
    {
        private byte[] _originalData;
        private List<RawSection> _sections = new List<RawSection>();
        private List<DIDXDescriptor> _descriptors = new List<DIDXDescriptor>();
        private byte[] _dataSectionData;
        private long _dataSectionStart;           // DATA section在文件中的起始偏移
        private Dictionary<uint, List<uint>> _eventToActions = new Dictionary<uint, List<uint>>();
        private Dictionary<uint, uint> _actionToExternal = new Dictionary<uint, uint>();
        private Dictionary<uint, ChainableObjectReference> _soundChainable = new Dictionary<uint, ChainableObjectReference>();
        private Dictionary<uint, string> _idToName = new Dictionary<uint, string>();
        private List<SoundItem> _soundItems = new List<SoundItem>();
        private string[] _providedEvents;          // 传入的事件名称列表，用于STID缺失时匹配

        // FNV-1 常量
        private const uint FNV_OFFSET_BASIS = 2166136261;
        private const uint FNV_PRIME = 16777619;

        /// <summary>
        /// 构造函数，解析银行文件
        /// </summary>
        /// <param name="data">银行文件字节数组</param>
        /// <param name="events">事件名称列表（用于STID缺失时的哈希匹配）</param>
        public WwiseBank(byte[] data, string[] events)
        {
            _originalData = data;
            _providedEvents = events ?? Array.Empty<string>();
            ParseAllSections();
            ParseDIDX();
            ParseDATA();
            ParseSTID();                     // 尝试解析STID，若不存在则_idToName为空
            ParseHIRC();
            BuildIdToNameFromEventsIfNeeded(); // 若STID不存在则用事件列表构建哈希映射
            BuildSoundItems();
        }

        /// <summary>
        /// 解析所有section，保存原始数据
        /// </summary>
        private void ParseAllSections()
        {
            using (var ms = new MemoryStream(_originalData))
            using (var br = new BinaryReader(ms))
            {
                long position = 0;
                while (position < ms.Length)
                {
                    long startPos = ms.Position;
                    if (ms.Length - ms.Position < 8) break; // 至少需要 magic + size

                    string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                    uint size = br.ReadUInt32(); // 小端

                    // 读取body
                    byte[] body = br.ReadBytes((int)size);

                    _sections.Add(new RawSection
                    {
                        Magic = magic,
                        Size = size,
                        Body = body,
                        StartPosition = startPos
                    });

                    position = ms.Position;
                }
            }
        }

        /// <summary>
        /// 解析DIDX section，获取描述符列表
        /// </summary>
        private void ParseDIDX()
        {
            var didxSection = _sections.FirstOrDefault(s => s.Magic == "DIDX");
            if (didxSection == null) return;

            using (var ms = new MemoryStream(didxSection.Body))
            using (var br = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length)
                {
                    uint id = br.ReadUInt32();
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();
                    _descriptors.Add(new DIDXDescriptor { Id = id, Offset = offset, Size = size });
                }
            }
        }

        /// <summary>
        /// 解析DATA section，保存原始数据
        /// </summary>
        private void ParseDATA()
        {
            var dataSection = _sections.FirstOrDefault(s => s.Magic == "DATA");
            if (dataSection == null) return;

            _dataSectionData = dataSection.Body;
            _dataSectionStart = dataSection.StartPosition + 8; // 跳过 magic(4) + size(4)
        }

        /// <summary>
        /// 解析STID section，获取ID到名称的映射
        /// </summary>
        private void ParseSTID()
        {
            var stidSection = _sections.FirstOrDefault(s => s.Magic == "STID");
            if (stidSection == null) return;

            using (var ms = new MemoryStream(stidSection.Body))
            using (var br = new BinaryReader(ms))
            {
                uint stringEncoding = br.ReadUInt32(); // 忽略
                uint entryCount = br.ReadUInt32();

                for (int i = 0; i < entryCount; i++)
                {
                    uint id = br.ReadUInt32();
                    byte nameLength = br.ReadByte();
                    byte[] nameBytes = br.ReadBytes(nameLength);
                    string name = Encoding.UTF8.GetString(nameBytes);
                    _idToName[id] = name;
                }
            }
        }

        /// <summary>
        /// 解析HIRC section，构建事件、动作、声音之间的映射
        /// </summary>
        private void ParseHIRC()
        {
            var hircSection = _sections.FirstOrDefault(s => s.Magic == "HIRC");
            if (hircSection == null) return;

            using (var ms = new MemoryStream(hircSection.Body))
            using (var br = new BinaryReader(ms))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    byte bodyType = br.ReadByte();
                    uint size = br.ReadUInt32();
                    long nextPos = br.BaseStream.Position + size;
                    uint id = br.ReadUInt32();
                    ChainableObjectReference cor = new ChainableObjectReference();
                    cor.DataOffset = br.BaseStream.Position;
                    switch (bodyType)
                    {
                        case 0x03: // Action
                            ParseAction(br, id);
                            break;
                        case 0x04: // Event
                            ParseEvent(br, id);
                            break;
                        case 0x02: // Sound
                            ParseSound(br, cor);
                            break;
                        case 0x0B: //Track
                            ParseTrack(br, cor);
                            break;
                        case 0x0D: //RSPL
                            ParseRSPL(br, cor);
                            break;
                        case 0x0A: //Seg
                            ParseSegment(br, cor);
                            break;
                        default:
                            cor.UnResolved = true;
                            break;
                    }
                    _soundChainable.Add(id, cor);
                    br.BaseStream.Position = nextPos;
                }
            }
        }

        private void ParseEvent(BinaryReader br, uint eventId)
        {
            byte actionCount = br.ReadByte();
            var actions = new List<uint>();
            for (int i = 0; i < actionCount; i++)
            {
                actions.Add(br.ReadUInt32());
            }
            _eventToActions[eventId] = actions;
        }

        private void ParseAction(BinaryReader br, uint actionId)
        {
            ushort actionType = br.ReadUInt16();
            uint externalId = br.ReadUInt32();
            byte isBus = br.ReadByte();
            _actionToExternal[actionId] = externalId;
        }

        private void ParseSound(BinaryReader br, ChainableObjectReference cor)
        {
            ReadAndSkipFullSound(br, cor);
        }
        private void ParseTrack(BinaryReader br, ChainableObjectReference cor)
        {
            br.ReadByte();
            uint cnt = br.ReadUInt32();
            for (int i = 0; i < cnt; i++)
                ReadAndSkipFullSound(br, cor);
            uint listCnt = br.ReadUInt32();
            for(int i = 0;i < listCnt; i++)
            {
                //u32 u32 u32 d64 d64 d64
                br.ReadUInt32();//trackId
                br.ReadUInt32();//sourceId
                br.ReadUInt32();//eventId
                br.ReadDouble();
                br.ReadDouble();
                br.ReadDouble();
                cor.DurationOffset.Add(br.BaseStream.Position);
                br.ReadDouble();
            }
        }

        private void ReadAndSkipFullSound(BinaryReader br, ChainableObjectReference cor)
        {
            uint plugin = br.ReadUInt32();
            byte sourceType = br.ReadByte();
            uint sourceId = br.ReadUInt32();
            uint inMemoryMediaSize = br.ReadUInt32();
            byte sourceFlags = br.ReadByte();
            //if ((plugin & 0x0F) != 0x2)
            //{
            //    uint sz = br.ReadUInt32();
            //    br.BaseStream.Position += sz;
            //}
            cor.SourceId.Add(sourceId);
        }
        private void ParseRSPL(BinaryReader br, ChainableObjectReference cor)
        {
            BnkReaderUtil.SkipMusicTransNodeParams(br);
            uint len = br.ReadUInt32();
            for (int i = 0; i < len; i++)
            {
                uint sid = br.ReadUInt32(); // segment_id
                br.ReadInt32();  // playlist_item_id
                br.ReadUInt32(); // child_count
                br.ReadUInt32(); // ers_type
                br.ReadInt16();  // loop_base
                br.ReadInt16();  // loop_min
                br.ReadInt16();  // loop_max
                br.ReadUInt32(); // weight
                br.ReadUInt16(); // avoid_repeat_count
                br.ReadByte();   // use_weight
                br.ReadByte();   // shuffle
                cor.Next.Add(sid);
            }
        }
        private void ParseSegment(BinaryReader br, ChainableObjectReference cor)
        {
            br.ReadByte();
            BnkReaderUtil.SkipNodeBaseParams(br);
            uint len = br.ReadUInt32();
            for (int i = 0; i < len; i++)
                cor.Next.Add(br.ReadUInt32());
            BnkReaderUtil.SkipStingerAndMeters(br);
            cor.DurationOffset.Add(br.BaseStream.Position);
            double oLen = br.ReadDouble();
            uint markerCnt = br.ReadUInt32();
            for(int i = 0; i <= markerCnt; i++)
            {
                br.ReadUInt32();
                double oMP = br.ReadDouble();
                if(oLen - oMP < 1000)
                {
                    cor.DurationOffset.Add(br.BaseStream.Position - 8);
                }
                while (br.ReadChar() > 0) ;
            }
        }

        /// <summary>
        /// 如果STID不存在，则使用传入的事件名称列表构建ID到名称的映射（通过FNV-1哈希）
        /// </summary>
        private void BuildIdToNameFromEventsIfNeeded()
        {
            if (_idToName.Count > 0) return; // 已有STID映射，不需要构建

            foreach (string eventName in _providedEvents)
            {
                uint hash = ComputeFNV1Hash(eventName);
                // 注意：一个哈希可能对应多个名称，但通常Wwise中事件名称哈希唯一
                if (!_idToName.ContainsKey(hash))
                {
                    _idToName[hash] = eventName;
                }
                else
                {
                    // 如果哈希冲突（罕见），可以选择保留第一个或覆盖，这里保留原映射
                    // 实际上Wwise保证唯一性，但为安全起见不做特殊处理
                }
            }
        }

        /// <summary>
        /// 计算字符串的FNV-1 32位哈希（非FNV-1a）
        /// </summary>
        private uint ComputeFNV1Hash(string input)
        {
            string lower = input.ToLowerInvariant();
            byte[] bytes = Encoding.ASCII.GetBytes(lower); // Wwise事件名通常为ASCII

            uint hash = FNV_OFFSET_BASIS;
            foreach (byte b in bytes)
            {
                hash *= FNV_PRIME;
                hash ^= b;
            }
            return hash;
        }

        /// <summary>
        /// 构建SoundItem列表
        /// </summary>
        private void BuildSoundItems()
        {
            // 为每个DIDX描述符创建一个SoundItem
            foreach (var desc in _descriptors)
            {
                var item = new SoundItem
                {
                    DescriptorId = desc.Id,
                    EventNames = new List<string>(),
                    Modified = false,
                    OriginalSize = (int)desc.Size,
                    FileOffset = _dataSectionStart + desc.Offset,
                    Data = new byte[desc.Size]
                };
                Array.Copy(_dataSectionData, desc.Offset, item.Data, 0, desc.Size);
                _soundItems.Add(item);
            }

            // 建立事件名称 -> 声音项映射
            // 遍历所有事件
            foreach (var kvp in _eventToActions)
            {
                uint eventId = kvp.Key;
                if (!_idToName.TryGetValue(eventId, out string eventName))
                    continue; // 没有名称的事件忽略

                var actionIds = kvp.Value;
                foreach (uint actionId in actionIds)
                {
                    if (!_actionToExternal.TryGetValue(actionId, out uint externalId))
                        continue;
                    if (!_soundChainable.TryGetValue(externalId, out ChainableObjectReference chainable))
                        continue;
                    var res = GetAllSourceFromChainable(chainable);

                    foreach (var c in res)
                    {
                        var item = _soundItems.FirstOrDefault(i => i.DescriptorId == c);
                        if (item != null && !item.EventNames.Contains(eventName))
                        {
                            item.EventNames.Add(eventName);
                        }
                    }
                }
            }
        }

        private List<uint> GetAllSourceFromChainable(ChainableObjectReference chainableObjectReference)
        {
            if (chainableObjectReference.End())
                return chainableObjectReference.SourceId;
            var result = new List<uint>();
            foreach (var i in chainableObjectReference.Next)
            {
                if (_soundChainable.ContainsKey(i))
                    result.AddRange(GetAllSourceFromChainable(_soundChainable[i]));
            }
            return result;
        }

        /// <summary>
        /// 获取所有声音项
        /// </summary>
        public IReadOnlyList<SoundItem> GetAllItems() => _soundItems.AsReadOnly();

        /// <summary>
        /// 获取所有事件
        /// </summary>
        public IReadOnlyList<SoundEvent> GetAllSoundEvents()
        {
            return _idToName.Select(kvp => new SoundEvent { Id = kvp.Key, Name = kvp.Value }).ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取指定事件对应的所有声音项
        /// </summary>
        public IReadOnlyList<SoundItem> GetItemsForEvent(string eventName)
        {
            return _soundItems.Where(item => item.EventNames.Contains(eventName)).ToList().AsReadOnly();
        }

        /// <summary>
        /// 替换指定ID的声音项的数据（支持任意长度）
        /// </summary>
        public void ReplaceItemData(uint descriptorId, byte[] newData)
        {
            var item = _soundItems.FirstOrDefault(i => i.DescriptorId == descriptorId);
            if (item == null)
                throw new ArgumentException($"未找到ID为{descriptorId}的声音项");

            item.Data = newData;
            item.Modified = true;
        }

        /// <summary>
        /// 重新构建银行文件字节数组（支持可变长度数据）
        /// </summary>
        public byte[] Build()
        {
            // 如果没有修改，直接返回原始数据
            if (!_soundItems.Any(i => i.Modified))
                return (byte[])_originalData.Clone();

            // 有修改，执行完整重建
            return RebuildBank();
        }

        /// <summary>
        /// 完全重建银行文件，更新所有相关结构
        /// </summary>
        private byte[] RebuildBank()
        {
            // 1. 构建新的 DATA section body，同时记录每个声音项的相对偏移
            var orderedItems = _soundItems; // 保持原始顺序
            var relativeOffsets = new List<uint>(); // 相对于 DATA 起始的偏移
            byte[] newDataBody;
            using (var dataMs = new MemoryStream())
            {
                foreach (var item in orderedItems)
                {
                    relativeOffsets.Add((uint)dataMs.Position);
                    dataMs.Write(item.Data, 0, item.Data.Length);
                }
                newDataBody = dataMs.ToArray();
            }
            uint newDataSize = (uint)newDataBody.Length;

            // 2. 构建新的 DIDX section body
            byte[] newDidxBody;
            using (var didxMs = new MemoryStream())
            using (var writer = new BinaryWriter(didxMs))
            {
                for (int i = 0; i < orderedItems.Count; i++)
                {
                    var item = orderedItems[i];
                    writer.Write(item.DescriptorId);
                    writer.Write(relativeOffsets[i]);
                    writer.Write((uint)item.Data.Length);
                }
                newDidxBody = didxMs.ToArray();
            }
            uint newDidxSize = (uint)newDidxBody.Length;

            using (var outMs = new MemoryStream())
            using (var writer = new BinaryWriter(outMs))
            {
                // 记录 DATA section 的起始位置（用于后续更新 FileOffset）
                long dataSectionStart = -1;
                // 记录新的 sections 列表（用于更新内部状态）
                var newSections = new List<RawSection>();

                foreach (var section in _sections)
                {
                    long startPos = outMs.Position; // 记录当前 section 起始位置

                    if (section.Magic == "DIDX")
                    {
                        writer.Write(Encoding.ASCII.GetBytes("DIDX"));
                        writer.Write(newDidxSize);
                        writer.Write(newDidxBody);
                        newSections.Add(new RawSection
                        {
                            Magic = "DIDX",
                            Size = newDidxSize,
                            Body = newDidxBody,
                            StartPosition = startPos
                        });
                    }
                    else if (section.Magic == "DATA")
                    {
                        writer.Write(Encoding.ASCII.GetBytes("DATA"));
                        writer.Write(newDataSize);
                        // 记录 DATA body 的起始位置
                        dataSectionStart = outMs.Position;
                        writer.Write(newDataBody);
                        newSections.Add(new RawSection
                        {
                            Magic = "DATA",
                            Size = newDataSize,
                            Body = newDataBody,
                            StartPosition = startPos
                        });
                    }
                    else if (section.Magic == "HIRC")
                    {
                        byte[] newHircBody = GetHIRCPatches(section.Body);
                        writer.Write(Encoding.ASCII.GetBytes("HIRC"));
                        writer.Write((uint)newHircBody.Length);
                        writer.Write(newHircBody);
                        newSections.Add(new RawSection
                        {
                            Magic = "HIRC",
                            Size = (uint)newHircBody.Length,
                            Body = newHircBody,
                            StartPosition = startPos
                        });
                    }
                    else
                    {
                        // 其他 section 原样保留
                        writer.Write(Encoding.ASCII.GetBytes(section.Magic));
                        writer.Write(section.Size);
                        writer.Write(section.Body);
                        newSections.Add(new RawSection
                        {
                            Magic = section.Magic,
                            Size = section.Size,
                            Body = section.Body,
                            StartPosition = startPos
                        });
                    }
                }

                byte[] newFile = outMs.ToArray();
                _dataSectionData = newDataBody;
                _dataSectionStart = dataSectionStart;

                // 更新 _descriptors
                _descriptors.Clear();
                for (int i = 0; i < orderedItems.Count; i++)
                {
                    _descriptors.Add(new DIDXDescriptor
                    {
                        Id = orderedItems[i].DescriptorId,
                        Offset = relativeOffsets[i],
                        Size = (uint)orderedItems[i].Data.Length
                    });
                }

                // 更新 _soundItems 中的 FileOffset 和 OriginalSize，并重置 Modified
                for (int i = 0; i < orderedItems.Count; i++)
                {
                    var item = orderedItems[i];
                    item.FileOffset = dataSectionStart + relativeOffsets[i];
                    item.OriginalSize = item.Data.Length;
                    item.Modified = false;
                }

                // 更新 _sections 为新的 sections 列表
                _sections = newSections;

                // 更新 _originalData 为新文件
                _originalData = newFile;

                return newFile;
            }
        }

        /// <summary>
        /// 重新计算每个被Replaced的Duration参数
        /// </summary>
        /// <returns></returns>
        byte[] GetHIRCPatches(byte[] o)
        {
            using var ms = new MemoryStream(o);
            using var bw = new BinaryWriter(ms);
            foreach(var c in _soundChainable)
            {
                if (!c.Value.DurationOffset.Any()) continue;

                double sz = 0;
                var chain = GetAllSourceFromChainable(c.Value);
                bool changed = false;
                bool err = false;
                foreach (var c2 in chain)
                {
                    var si = _soundItems.Find(t => t.DescriptorId == c2);
                    if (si.Modified)
                        changed = true;
                    using var msi = new MemoryStream(si.Data);
                    using var br = new BinaryReader(msi);
                    var s1 = WEMUtil.GetDurationMs(br);
                    if (s1 != null)
                        sz += (double)s1;
                    else
                        err = true;
                }
                foreach (var durOffset in c.Value.DurationOffset)
                {
                    bw.BaseStream.Position = durOffset;
                    if (!err && changed)
                        bw.Write(sz);
                }
            }
            return ms.ToArray();
        }
    }
}