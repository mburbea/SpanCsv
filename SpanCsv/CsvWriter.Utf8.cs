using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SpanCsv
{
    public ref partial struct CsvWriter<T> where T : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf8(long value)
        {
            ref var pos = ref _pos;

            if (value == long.MinValue)
            {
                if (pos > _bytes.Length - 21)
                {
                    Grow(21);
                }

                Constants.LongMinValueUtf8.AsSpan().TryCopyTo(_bytes.Slice(pos));
                pos += Constants.LongMinValueUtf8.Length;
            }
            else if (value < 0)
            {
                if (pos > _bytes.Length - 1)
                {
                    Grow(1);
                }

                _bytes[pos++] = (byte)'-';
                value = unchecked(-value);
            }

            WriteUtf8((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf8(ulong value)
        {
            ref var pos = ref _pos;
            if (value < 10)
            {
                if (pos > _bytes.Length - 1)
                {
                    Grow(1);
                }

                _bytes[pos++] = (byte)('0' + value);
                return;
            }

            var digits = Utils.CountDigits(value);

            if (pos > _bytes.Length - digits)
            {
                Grow(digits);
            }

            for (var i = digits; i > 0; i--)
            {
                var temp = '0' + value;
                value /= 10;
                _bytes[pos + i - 1] = (byte)(temp - value * 10);
            }

            pos += digits;
        }

        private void WriteUtf8(float value)
        {
            Span<byte> span = stackalloc byte[Constants.MaxNumberBufferSize];
            Utf8Formatter.TryFormat(value, span, out var written);

            ref var pos = ref _pos;
            if (pos > _bytes.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_bytes.Slice(pos));
            pos += written;
        }

        private void WriteUtf8(double value)
        {
            Span<byte> span = stackalloc byte[Constants.MaxNumberBufferSize];
            Utf8Formatter.TryFormat(value, span, out var written);

            ref var pos = ref _pos;
            if (pos > _bytes.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_bytes.Slice(pos));
            pos += written;
        }

        private void WriteUtf8(decimal value)
        {
            Span<byte> span = stackalloc byte[Constants.MaxNumberBufferSize];
            Utf8Formatter.TryFormat(value, span, out var written);

            ref var pos = ref _pos;
            if (pos > _bytes.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_bytes.Slice(pos));
            pos += written;
        }

        private void WriteUtf8(DateTime value)
        {
            ref var pos = ref _pos;
            const int dtSize = 35; // Form o + two JsonUtf8Constant.DoubleQuote
            if (pos > _bytes.Length - dtSize)
            {
                Grow(dtSize);
            }

            UnsafeWriteDoubleQuote();
            Utf8Formatter.TryFormat(value, _bytes.Slice(pos), out var bytesWritten, 'O');
            pos += bytesWritten;
            UnsafeWriteDoubleQuote();
        }

        public void WriteUtf8(DateTimeOffset value)
        {
            ref var pos = ref _pos;
            const int dtSize = 35; // Form o + two JsonUtf8Constant.DoubleQuote
            if (pos > _bytes.Length - dtSize)
            {
                Grow(dtSize);
            }

            UnsafeWriteDoubleQuote();
            Utf8Formatter.TryFormat(value, _bytes.Slice(pos), out var bytesWritten, 'O');
            pos += bytesWritten;
            UnsafeWriteDoubleQuote();
        }

        private void WriteUtf8(string value)
        {
            ref var pos = ref _pos;
            var valueLength = value.Length;
            var sLength = valueLength + 4; // 2 double quotes + one special char

            if (pos > _bytes.Length - sLength)
            {
                Grow(sLength);
            }

            UnsafeWriteDoubleQuote();
            var span = value.AsSpan();
            ref var start = ref MemoryMarshal.GetReference(span);
            for (var i = 0; i < valueLength; i++)
            {
                ref var c = ref Unsafe.Add(ref start, i);
                if (c == '"')
                {
                    UnsafeWriteDoubleQuote();
                    UnsafeWriteDoubleQuote();
                    var remaining = 1 + valueLength - i; // we need an extra quote for the double quote.
                    if (pos > _bytes.Length - remaining)
                    {
                        Grow(remaining);
                    }
                }
                else if (c > 0x7F) // UTF8 characters, we need to escape them
                {
                    var temp = MemoryMarshal.CreateReadOnlySpan(ref c, 1);
                    var remaining = 4 + valueLength - i; // make sure that all characters, an extra 5 for a full escape and 4 for the utf8 bytes, still fit
                    if (pos > _bytes.Length - remaining)
                    {
                        Grow(remaining);
                    }

                    pos += Encoding.UTF8.GetBytes(temp, _bytes.Slice(pos));
                }
                else
                {
                    _bytes[pos++] = (byte)c;
                }
            }

            UnsafeWriteDoubleQuote();
        }
        
        private void WriteUtf8RawAscii(char c)
        {
            ref var pos = ref _pos;
            if (pos > _bytes.Length - 1)
            {
                Grow(1);
            }

            _bytes[pos++] = (byte)c;
        }


    }
}
