using System;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SpanCsv
{
    public ref partial struct CsvWriter<T> where T : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8Int64(long value)
        {
            ref var pos = ref _pos;

            if (value == long.MinValue)
            {
                if (pos > _bytes.Length - 21)
                {
                    Grow(21);
                }

                Constants.LongMinValueUtf8.AsSpan().CopyTo(_bytes.Slice(pos));
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

            WriteUtf8UInt64((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8UInt64(ulong value)
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

        private void WriteUtf8Single(float value)
        {
            Span<char> span = stackalloc char[Constants.MaxNumberBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _bytes.Length - written)
            {
                Grow(written);
            }

            for (int i = 0; i < written; i++)
            {
                _bytes[pos++] = (byte)span[i];
            }
        }

        private void WriteUtf8Double(double value)
        {
            Span<char> span = stackalloc char[Constants.MaxNumberBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _bytes.Length - written)
            {
                Grow(written);
            }

            for (int i = 0; i < written; i++)
            {
                _bytes[pos++] = (byte)span[i];
            }
        }

        private void WriteUtf8Decimal(decimal value)
        {
            ref var pos = ref _pos;
            const int dtSize = Constants.MaxNumberBufferSize; // Form o + two JsonUtf8Constant.DoubleQuote
            if (pos > _bytes.Length - dtSize)
            {
                Grow(dtSize);
            }

            Utf8Formatter.TryFormat(value, _bytes.Slice(pos), out var bytesWritten);
            pos += bytesWritten;
        }

        private void WriteUtf8DateTime(DateTime value)
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

        public void WriteUtf8DateTimeOffset(DateTimeOffset value)
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

        public void WriteUtf8String(string value)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8RawAscii(char c)
        {
            ref var pos = ref _pos;
            if (pos > _bytes.Length - 1)
            {
                Grow(1);
            }

            _bytes[pos++] = (byte)c;
        }

        public void WriteUtf8Boolean(bool value)
        {
            ref var pos = ref _pos;
            if (value)
            {
                const int trueLength = 4;
                if (pos > _bytes.Length - trueLength)
                {
                    Grow(trueLength);
                }

                _bytes[pos++] = (byte)'t';
                _bytes[pos++] = (byte)'r';
                _bytes[pos++] = (byte)'u';
                _bytes[pos++] = (byte)'e';
            }
            else
            {
                const int falseLength = 5;
                if (pos > _bytes.Length - falseLength)
                {
                    Grow(falseLength);
                }

                _bytes[pos++] = (byte)'f';
                _bytes[pos++] = (byte)'a';
                _bytes[pos++] = (byte)'l';
                _bytes[pos++] = (byte)'s';
                _bytes[pos++] = (byte)'e';
            }
        }
    }
}
