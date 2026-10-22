using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Utils
{
    using System;
    using System.IO;
    using System.Buffers.Binary;

    public class BigEndianReader : IDisposable
    {
        private Stream _stream;
        private bool _leaveOpen;

        public BigEndianReader(Stream stream, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;
        }

        public Stream BaseStream => _stream;
        public long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public byte ReadByte()
        {
            int b = _stream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return (byte)b;
        }

        public byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = _stream.Read(buffer, offset, count - offset);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }

        public bool ReadBoolean() => ReadByte() != 0;

        public short ReadInt16()
        {
            Span<byte> buffer = stackalloc byte[2];
            FillBuffer(buffer);
            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }

        public ushort ReadUInt16()
        {
            Span<byte> buffer = stackalloc byte[2];
            FillBuffer(buffer);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public int ReadInt32()
        {
            Span<byte> buffer = stackalloc byte[4];
            FillBuffer(buffer);
            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        public uint ReadUInt32()
        {
            Span<byte> buffer = stackalloc byte[4];
            FillBuffer(buffer);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public long ReadInt64()
        {
            Span<byte> buffer = stackalloc byte[8];
            FillBuffer(buffer);
            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }

        public ulong ReadUInt64()
        {
            Span<byte> buffer = stackalloc byte[8];
            FillBuffer(buffer);
            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }

        private void FillBuffer(Span<byte> buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = _stream.Read(buffer.Slice(offset));
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
        }

        public void Dispose()
        {
            if (!_leaveOpen)
                _stream?.Dispose();
        }
    }
}
