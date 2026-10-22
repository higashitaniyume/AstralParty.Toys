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

    public class BigEndianWriter : IDisposable
    {
        private Stream _stream;
        private bool _leaveOpen;

        public BigEndianWriter(Stream stream, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;
        }

        public Stream BaseStream => _stream;

        public void Write(byte value)
        {
            _stream.WriteByte(value);
        }

        public void Write(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            _stream.Write(buffer, 0, buffer.Length);
        }

        public void Write(byte[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            _stream.Write(buffer, index, count);
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            _stream.Write(buffer);
        }

        public void Write(bool value)
        {
            _stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public void Write(short value)
        {
            Span<byte> buffer = stackalloc byte[2];
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void Write(ushort value)
        {
            Span<byte> buffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void Write(int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void Write(uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void Write(long value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void Write(ulong value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void Dispose()
        {
            if (!_leaveOpen)
                _stream?.Dispose();
        }
    }
}
