using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SpanCsv
{
    public ref partial struct CsvWriter<T> where T : unmanaged
    {
        int _pos;
        private Span<byte> _bytes;
        public T[] Data { get; private set; }
        private Span<char> _chars;
        private char _seperator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(sbyte value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(short value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(long value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ulong value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(DateTime value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(DateTimeOffset value)
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8(value);
            }
            else if (typeof(T) == typeof(char))
            {
                WriteUtf16(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNewLine()
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8RawAscii('\n');
            }
            if (typeof(T) == typeof(char))
            {
                WriteUtf16RawAscii('\n');
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSeperator()
        {
            if (typeof(T) == typeof(byte))
            {
                WriteUtf8RawAscii(_seperator);
            }
            if (typeof(T) == typeof(char))
            {
                WriteUtf16RawAscii(_seperator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnsafeWriteDoubleQuote()
        {
            // DOES NOT Check capacity.
            if(typeof(T) == typeof(byte))
            {
                _bytes[_pos++] = (byte)'"';
            }
            if(typeof(T) == typeof(char))
            {
                _chars[_pos++] = '"';
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string value)
        {
            if (value != null)
            {
                if (typeof(T) == typeof(byte))
                {
                    WriteUtf8(value);
                }
                else if (typeof(T) == typeof(char))
                {
                    WriteUtf16(value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Dispose()
        {
            var toReturn = Data;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }


        public CsvWriter(int initialSize, char seperator = ',')
        {
            _seperator = seperator;
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

        public byte[] ToByteArray()
        {
            var result = _bytes.Slice(0, _pos).ToArray();
            Dispose();
            return result;
        }

        public void Reset()
        {
            _pos = 0;
        }
    }
}

