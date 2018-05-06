﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SpanCsv
{
    public ref partial struct CsvWriter<T> where T:unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16(long value)
        {
            ref var pos = ref _pos;
            if (value == long.MinValue)
            {
                if (pos > _chars.Length - 21)
                {
                    Grow(21);
                }

                Constants.LongMinValueUtf16.AsSpan().TryCopyTo(_chars.Slice(pos));
                pos += Constants.LongMinValueUtf16.Length;
            }
            else if (value < 0)
            {
                if (pos > _chars.Length - 1)
                {
                    Grow(1);
                }

                _chars[pos++] = '-';
                value = unchecked(-value);
            }

            WriteUtf16((ulong) value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16(ulong value)
        {
            ref var pos = ref _pos;
            if (value < 10)
            {
                if (pos > _chars.Length - 1)
                {
                    Grow(1);
                }

                _chars[pos++] = (char) ('0' + value);
                return;
            }

            var digits = Utils.CountDigits(value);

            if (pos > _chars.Length - digits)
            {
                Grow(digits);
            }

            for (var i = digits; i > 0; i--)
            {
                var temp = '0' + value;
                value /= 10;
                _chars[pos + i - 1] = (char) (temp - value * 10);
            }

            pos += digits;
        }

        private void WriteUtf16(float value)
        {
            Span<char> span = stackalloc char[Constants.MaxNumberBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
        }

        private void WriteUtf16(double value)
        {
            Span<char> span = stackalloc char[Constants.MaxNumberBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
        }

        private void WriteUtf16(decimal value)
        {
            Span<char> span = stackalloc char[Constants.MaxNumberBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
        }


        private void WriteUtf16(string value)
        {
            ref var pos = ref _pos;
            var valueLength = value.Length;
            var sLength = valueLength + 4; // 2 double quotes + one special char

            if (pos > _chars.Length - sLength)
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
                    if (pos > _chars.Length - remaining)
                    {
                        Grow(remaining);
                    }
                }
                else
                {
                    _chars[pos++] = c;
                }
            }

            UnsafeWriteDoubleQuote();
        }

        public void WriteUtf16(DateTime value)
        {
            ref var pos = ref _pos;
            const int dtSize = 35; // Form o + two JsonUtf16Constant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            UnsafeWriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written, "O", CultureInfo.InvariantCulture);
            pos += written;
            UnsafeWriteDoubleQuote();
        }

        public void WriteUtf16(DateTimeOffset value)
        {
            ref var pos = ref _pos;
            const int dtSize = 35; // Form o + two JsonUtf16Constant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            UnsafeWriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written, "O", CultureInfo.InvariantCulture);
            pos += written;
            UnsafeWriteDoubleQuote();
        }


        private void WriteUtf16RawAscii(char c)
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 1)
            {
                Grow(1);
            }

            _chars[pos++] = c;
        }
    }
}
