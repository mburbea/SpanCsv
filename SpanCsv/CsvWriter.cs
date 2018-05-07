using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpanCsv
{
    internal ref partial struct CsvWriter<T> where T : struct
    {
        int _pos;
        private Span<byte> _bytes;
        public T[] Data { get; private set; }
        private Span<char> _chars;
        private readonly char _utf16Seperator;
        private readonly byte _utf8Seperator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = Data;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }

        public CsvWriter(int initialSize) : this(initialSize, ',')
        {
        }

        public CsvWriter(int initialSize, char seperator)
        {
            _utf16Seperator = seperator;
            _utf8Seperator = (byte) seperator;
            Data = ArrayPool<T>.Shared.Rent(initialSize);
            if (typeof(T) == typeof(char))
            {
                _chars = MemoryMarshal.Cast<T, char>(Data);
                _bytes = null;
            }
            else if (typeof(T) == typeof(byte))
            {
                _bytes = MemoryMarshal.Cast<T, byte>(Data);
                _chars = null;
            }
            else
            {
                throw new NotSupportedException();
            }

            _pos = 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int requiredAdditionalCapacity)
        {
            Debug.Assert(requiredAdditionalCapacity > 0);

            var toReturn = Data;
            if (typeof(T) == typeof(char))
            {
                var poolArray = ArrayPool<T>.Shared.Rent(Math.Max(_pos + requiredAdditionalCapacity, _chars.Length * 2));
                var converted = MemoryMarshal.Cast<T, char>(poolArray);
                _chars.CopyTo(converted);
                _chars = converted;
                Data = poolArray;
            }
            else if (typeof(T) == typeof(byte))
            {
                var poolArray = ArrayPool<T>.Shared.Rent(Math.Max(_pos + requiredAdditionalCapacity, _bytes.Length * 2));
                var converted = MemoryMarshal.Cast<T, byte>(poolArray);
                _bytes.CopyTo(converted);
                _bytes = converted;
                Data = poolArray;
            }

            if (toReturn != null)
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }

        public override string ToString()
        {
            var s = _chars.Slice(0, _pos).ToString();
            Dispose();
            return s;
        }

        public void FlushToStream(Stream stream)
        {
            stream.Write((byte[]) (object) Data, 0, _pos);
            _pos = 0;
        }

        public void FlushToTextWriter(TextWriter textWriter)
        {

            textWriter.Write((char[]) (object) Data, 0, _pos);
            _pos = 0;
        }
    }
}

