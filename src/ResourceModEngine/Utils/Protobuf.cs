using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ResourceModLoader.Utils
{
    public enum WireTypes
    {
        VARINT = 0,
        I64 = 1,
        LEN = 2,
        SGROUP = 3,
        EGROUP = 4,
        I32 = 5
    }

    public static class WireMap
    {
        public static readonly Dictionary<WireTypes, string[]> Map = new Dictionary<WireTypes, string[]>
        {
            [WireTypes.VARINT] = new[] { "uint32", "int32", "int64", "uint64", "sint32", "sint64", "bool", "raw", "bytes" },
            [WireTypes.I64] = new[] { "uint32", "int32", "bytes", "fixed64", "sfixed64", "double" },
            [WireTypes.LEN] = new[] { "raw", "bytes", "string", "sub", "packedIntVar", "packedInt32", "packedInt64" },
            [WireTypes.I32] = new[] { "uint32", "int32", "bytes", "fixed32", "sfixed32", "float", "raw" }
        };
    }

    public abstract class ReaderBase
    {
        public byte[] Buffer { get; protected set; }
        public WireTypes Type { get; }
        public string Path { get; }
        public string RenderType { get; }
        public int Length { get; protected set; }
        protected byte[] CachedBytes { get; set; }
        protected ReaderBase? ReplacedBy;
        protected bool hasModifiedChild = false;
        public ReaderBase? Parent;

        protected ReaderBase(byte[] buffer, WireTypes type, string path, ReaderBase? parent)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Type = type;
            Path = path;
            CachedBytes = null;
            this.Parent = parent;
        }
        public abstract byte[] Bytes { get; }
        public abstract string String { get; }

        public byte[] GetBytes()
        {
            if (CachedBytes == null)
            {
                if (ReplacedBy != null)
                    CachedBytes = ReplacedBy.Serialize();
                else
                    CachedBytes = Serialize();
            }
            return CachedBytes;
        }
        public void Replace(ReaderBase? readerBase)
        {
            this.ReplacedBy = readerBase;
            MarkChildReplaced();
        }
        public void MarkChildReplaced()
        {
            CachedBytes = null;
            hasModifiedChild = true;
            this.Parent?.MarkChildReplaced();
        }
        public abstract byte[] Serialize();
    }

    public abstract class ReaderFixed : ReaderBase
    {
        protected ReaderFixed(byte[] buffer, WireTypes type, string path, ReaderBase parent)
            : base(buffer, type, path,parent) { }

        public override byte[] Bytes => GetBytes();
        public override string String => Int.ToString();

        public abstract long Int { get; }
        public abstract ulong Uint { get; }
        public abstract double Double { get; }
        public abstract float Float { get; }

        public override byte[] Serialize()
        {
            // 由子类实现
            throw new NotImplementedException();
        }
    }

    public class ReaderFixed64 : ReaderFixed
    {
        public ReaderFixed64(byte[] buffer, string path, ReaderBase parent)
            : base(buffer, WireTypes.I64, path, parent) { }

        public override long Int
        {
            get => BitConverter.ToInt64(Buffer, 0);
        }

        public override ulong Uint
        {
            get => BitConverter.ToUInt64(Buffer, 0);
        }

        public override double Double
        {
            get => BitConverter.ToDouble(Buffer, 0);
        }

        public override float Float
        {
            get => (float)Double;
        }

        public long Fixed64
        {
            get => (long)Uint;
        }

        public long Sfixed64
        {
            get => Int;
        }

        public override byte[] Serialize()
        {
            return Buffer;
        }
    }

    public class ReaderFixed32 : ReaderFixed
    {
        public ReaderFixed32(byte[] buffer, string path, ReaderBase parent)
            : base(buffer, WireTypes.I32, path, parent) { }

        public override long Int
        {
            get => BitConverter.ToInt32(Buffer, 0);
        }

        public override ulong Uint
        {
            get => BitConverter.ToUInt32(Buffer, 0);
        }

        public override double Double
        {
            get => BitConverter.ToSingle(Buffer, 0);
        }

        public override float Float
        {
            get => (float)Double;
        }

        public uint Fixed32
        {
            get => (uint)Uint;
        }

        public int Sfixed32
        {
            get => (int)Int;
        }

        public override byte[] Serialize() => Buffer;
    }

    public class ReaderVarInt : ReaderBase
    {
        public ulong Value { get; set; }

        public ReaderVarInt(byte[] buffer, string path, ReaderBase parent, ulong value)
            : base(buffer, WireTypes.VARINT, path, parent)
        {
            Value = value;
            CachedBytes = null;
        }
        public override byte[] Bytes => GetBytes();
        public override string String => Uint.ToString();

        public bool Bool
        {
            get => Value != 0;
        }

        public long Int32
        {
            get => (long)Value;
        }
        public long Int64
        {
            get => (long)Value;
        }
        public long Sint32
        {
            get => (long)Value;
        }
        public long Sint64
        {
            get => (long)Value;
        }
        public uint Uint32
        {
            get => (uint)Value;
        }
        public ulong Uint64
        {
            get => Value;
        }

        public long Int
        {
            get => (long)Value;
        }
        public ulong Uint
        {
            get => Value;
        }

        public override byte[] Serialize()
        {
            // 将Value编码为varint
            var bytes = new List<byte>();
            ulong v = Value;
            do
            {
                byte b = (byte)(v & 0x7F);
                v >>= 7;
                if (v != 0) b |= 0x80;
                bytes.Add(b);
            } while (v != 0);
            return bytes.ToArray();
        }
    }

    public class ReaderMessage : ReaderBase
    {
        private int _offset;
        private byte[] _bytes;
        private bool? _couldHaveSub;
        private bool? _likelyString;
        private Dictionary<int, int> _fields;
        private Dictionary<int, List<ReaderBase>> _sub;
        private string _string;

        public ReaderMessage(byte[] buffer, string path = "0", ReaderBase? parent = null)
            : base(buffer, WireTypes.LEN, path, parent)
        {
            _offset = 0;
            _bytes = buffer;
            _sub = null; // 延迟解析
        }

        public override byte[] Bytes => GetBytes();
        public override string String
        {
            get
            {
                if (_string == null)
                    _string = Encoding.UTF8.GetString(Bytes);
                return _string;
            }
        }

        public bool CouldHaveSub
        {
            get
            {
                if (!_couldHaveSub.HasValue)
                    _couldHaveSub = Sub.Keys.Count > 0;
                return _couldHaveSub.Value;
            }
        }

        public bool LikelyString
        {
            get
            {
                if (!_likelyString.HasValue)
                {
                    _likelyString = _bytes.All(b => b >= 32);
                }
                return _likelyString.Value;
            }
        }

        public Dictionary<int, int> Fields
        {
            get
            {
                if (_fields == null)
                    _ = Sub; // trigger parsing
                return _fields;
            }
        }

        public Dictionary<int, List<ReaderBase>> Sub
        {
            get
            {
                if (_sub != null)
                    return _sub;

                _fields = new Dictionary<int, int>();
                _sub = new Dictionary<int, List<ReaderBase>>();

                int rollbackOffset = _offset;

                try
                {
                    while (_offset < Buffer.Length)
                    {
                        ulong indexType = ReadVarInt();
                        int type = (int)(indexType & 7);
                        int index = (int)(indexType >> 3);

                        if (!_fields.ContainsKey(index))
                            _fields[index] = 0;
                        _fields[index]++;

                        if (!_sub.ContainsKey(index))
                            _sub[index] = new List<ReaderBase>();

                        if (type == (int)WireTypes.VARINT)
                        {
                            int start = _offset;
                            ulong value = ReadVarInt();
                            byte[] slice = new byte[_offset - start];
                            Array.Copy(Buffer, start, slice, 0, slice.Length);
                            var reader = new ReaderVarInt(slice, $"{Path}.{index}.{_sub[index].Count}",this, value);
                            _sub[index].Add(reader);
                            rollbackOffset = _offset;
                        }
                        else if (type == (int)WireTypes.LEN)
                        {
                            int length = (int)ReadVarInt();
                            byte[] slice = new byte[length];
                            Array.Copy(Buffer, _offset, slice, 0, length);
                            var inner = new ReaderMessage(slice, $"{Path}.{index}.{_sub[index].Count}", this);
                            _offset += length;

                            // 尝试展开packed字段
                            if (!inner.CouldHaveSub && !inner.LikelyString)
                            {
                                // 尝试解析为packed列表
                                List<ReaderBase> packedList = TryExpandPacked(inner, index);
                                if (packedList != null)
                                {
                                    _sub[index].AddRange(packedList);
                                    rollbackOffset = _offset;
                                    continue;
                                }
                            }
                            _sub[index].Add(inner);
                            rollbackOffset = _offset;
                        }
                        else if (type == (int)WireTypes.SGROUP)
                        {
                            byte[] slice = ReadBufferUntilGroupEnd(index);
                            var reader = new ReaderMessage(slice, $"{Path}.{index}.{_sub[index].Count}",this);
                            _sub[index].Add(reader);
                            rollbackOffset = _offset;
                        }
                        else if (type == (int)WireTypes.I64)
                        {
                            byte[] slice = new byte[8];
                            Array.Copy(Buffer, _offset, slice, 0, 8);
                            var reader = new ReaderFixed64(slice, $"{Path}.{index}.{_sub[index].Count}", this);
                            _sub[index].Add(reader);
                            _offset += 8;
                            rollbackOffset = _offset;
                        }
                        else if (type == (int)WireTypes.I32)
                        {
                            byte[] slice = new byte[4];
                            Array.Copy(Buffer, _offset, slice, 0, 4);
                            var reader = new ReaderFixed32(slice, $"{Path}.{index}.{_sub[index].Count}", this);
                            _sub[index].Add(reader);
                            _offset += 4;
                            rollbackOffset = _offset;
                        }
                    }
                    return _sub;
                }
                catch (Exception)
                {
                    // 解析失败，保留剩余字节
                    // Remainder 等可保留，但为简化，直接返回已有子字段
                    return _sub;
                }
            }
        }

        /// <summary>
        /// 尝试将inner消息展开为packed标量列表
        /// </summary>
        private List<ReaderBase> TryExpandPacked(ReaderMessage inner, int fieldIndex)
        {
            var result = new List<ReaderBase>();
            byte[] data = inner._bytes;
            int pos = 0;

            // 尝试 varint
            bool varintOk = true;
            var varintList = new List<ulong>();
            int savedPos = pos;
            try
            {
                while (pos < data.Length)
                {
                    ulong v = 0;
                    int shift = 0;
                    byte b;
                    do
                    {
                        if (pos >= data.Length) throw new Exception();
                        b = data[pos++];
                        v |= (ulong)(b & 0x7F) << shift;
                        shift += 7;
                    } while ((b & 0x80) != 0);
                    varintList.Add(v);
                }
                if (pos == data.Length)
                {
                    // 全部解析为varint
                    foreach (ulong v in varintList)
                    {
                        // 创建一个新的ReaderVarInt
                        byte[] slice = new byte[0]; // 实际不需要，但需占位
                        var reader = new ReaderVarInt(slice, $"{Path}.{fieldIndex}.{_sub[fieldIndex].Count + result.Count}", this,v);
                        result.Add(reader);
                    }
                    return result;
                }
            }
            catch { varintOk = false; }

            // 尝试 fixed32
            if (data.Length % 4 == 0)
            {
                result.Clear();
                for (int i = 0; i < data.Length; i += 4)
                {
                    byte[] slice = new byte[4];
                    Array.Copy(data, i, slice, 0, 4);
                    var reader = new ReaderFixed32(slice, $"{Path}.{fieldIndex}.{_sub[fieldIndex].Count + result.Count}", null);
                    result.Add(reader);
                }
                return result;
            }

            // 尝试 fixed64
            if (data.Length % 8 == 0)
            {
                result.Clear();
                for (int i = 0; i < data.Length; i += 8)
                {
                    byte[] slice = new byte[8];
                    Array.Copy(data, i, slice, 0, 8);
                    var reader = new ReaderFixed64(slice, $"{Path}.{fieldIndex}.{_sub[fieldIndex].Count + result.Count}", null);
                    result.Add(reader);
                }
                return result;
            }

            return null; // 无法展开
        }

        public override byte[] Serialize()
        {
            if (!hasModifiedChild)
                return _bytes;
            var stream = new List<byte>();
            var sortedFields = Sub.Keys.OrderBy(k => k).ToList();
            foreach (int fieldNum in sortedFields)
            {
                var values = Sub[fieldNum];
                foreach (var value in values)
                {
                    WireTypes wireType = value.Type;
                    // 写入tag
                    uint tag = (uint)((fieldNum << 3) | (int)wireType);
                    WriteVarInt(stream, tag);

                    // 写入数据
                    if (value is ReaderVarInt varint)
                    {
                        stream.AddRange(varint.GetBytes()); // 固定长度，直接追加
                    }
                    else if (value is ReaderFixed64 fixed64)
                    {
                        stream.AddRange(fixed64.GetBytes()); // 固定长度，直接追加
                    }
                    else if (value is ReaderFixed32 fixed32)
                    {
                        stream.AddRange(fixed32.GetBytes());
                    }
                    else if (value is ReaderMessage msg)
                    {
                        byte[] subBytes = msg.GetBytes();
                        WriteVarInt(stream, (ulong)subBytes.Length);
                        stream.AddRange(subBytes);
                    }
                    else
                    {
                        // 其他类型（如SGROUP等）暂不支持序列化，抛出异常或忽略
                        throw new NotSupportedException($"Serialization of type {value.GetType()} not supported");
                    }
                }
            }
            return stream.ToArray();
        }

        private void WriteVarInt(List<byte> stream, ulong value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                stream.Add(b);
            } while (value != 0);
        }
        private ulong ReadVarInt()
        {
            ulong result = 0;
            int shift = 0;
            byte b;
            do
            {
                if (_offset >= Buffer.Length)
                    throw new InvalidOperationException("Buffer overflow while reading varint");
                b = Buffer[_offset++];
                result |= (ulong)(b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return result;
        }


        private byte[] ReadBufferUntilGroupEnd(int index)
        {
            int start = _offset;
            while (true)
            {
                ulong indexType = ReadVarInt();
                int type = (int)(indexType & 7);
                if (type == (int)WireTypes.EGROUP)
                    break;
            }
            byte[] result = new byte[_offset - start];
            Array.Copy(Buffer, start, result, 0, result.Length);
            return result;
        }

        public object[] Query(params string[] queries) => Query(this, Path, queries);

        public static object[] Query(ReaderMessage tree, string prefix, params string[] queries)
        {
            var results = new List<object>();
            foreach (string q in queries)
            {
                string[] parts = q.Split(':');
                string path = parts[0];
                string type = parts.Length > 1 ? parts[1] : "raw";

                if (!path.StartsWith(prefix))
                    path = $"{prefix}.{path}";

                string relative = path.Substring(prefix.Length + 1);
                string[] _indices = relative.Split('.');
                List<string> indices = TransformPath(_indices);
                path = "0." + string.Join(".", indices);

                List<object> current = new List<object> { tree };
                foreach (string idx in indices)
                {
                    var next = new List<object>();
                    foreach (var item in current)
                    {
                        if (item is ReaderMessage msg && msg.Sub.TryGetValue(int.Parse(idx), out var list))
                        {
                            next.Add(list);
                        }
                        else if (item is ReaderBase rb && rb.Path == path)
                        {
                            next.Add(item);
                        }
                        else if (item is List<ReaderBase> l && l.Count > int.Parse(idx))
                        {
                            next.Add(l[int.Parse(idx)]);
                        }
                    }
                    current = next;
                }

                foreach (var item in current)
                {
                    if (item is ReaderBase rb && rb.Path == path)
                    {
                        results.Add(item);
                    }
                }
            }
            return results.ToArray();
        }

        static List<string> TransformPath(string[] path)
        {
            List<string> results = new List<string>();
            foreach (var _item in path)
            {
                var item = _item.Trim();
                if (item.Contains("[") && item.EndsWith("]"))
                {
                    var s = item.Substring(0, item.Length - 1).Split("[", 2);
                    results.Add(s[0].Trim());
                    results.Add(s[1].Trim());
                }
                else
                {
                    results.Add(item);
                    results.Add("0");
                }
            }
            return results;
        }
    }
}