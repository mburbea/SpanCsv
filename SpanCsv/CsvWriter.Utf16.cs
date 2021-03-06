﻿using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpanCsv
{
    internal ref partial struct CsvWriter<T> where T : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(sbyte value)=> WriteUtf16((long) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(short value)=> WriteUtf16((long) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(int value)=> WriteUtf16((long) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(byte value)=> WriteUtf16((ulong) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(ushort value)=> WriteUtf16((ulong) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(uint value)=> WriteUtf16((ulong) value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(long value)
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 2)
            {
                Grow(2);
            }
            if (value < 0)
            {
                _chars[pos++] = '-';
                value = unchecked(-value);
            }

            WriteUtf16((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(ulong value)
        {
            ref var pos = ref _pos;
            if (value < 10)
            {
                if (pos > _chars.Length - 2)
                {
                    Grow(2);
                }

                _chars[pos++] = (char)('0' + value);
                WriteUtf16EndingErrata();
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
                _chars[pos + i - 1] = (char)(temp - value * 10);
            }

            pos += digits;
            WriteUtf16EndingErrata();
        }

        public void WriteUtf16(float value)
        {
            Span<char> span = stackalloc char[Constants.FloatBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
            WriteUtf16EndingErrata();
        }

        public void WriteUtf16(double value)
        {
            Span<char> span = stackalloc char[Constants.DoubleBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
            WriteUtf16EndingErrata();
        }

        public void WriteUtf16(decimal value)
        {
            if (value == 0)
            {
                WriteUtf16(0ul);
                return;
            }
            Span<char> span = stackalloc char[Constants.DecimalBufferSize];
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
            WriteUtf8EndingErrata();
        }


        public void WriteUtf16(ReadOnlySpan<char> value)
        {
            ref var pos = ref _pos;
            var valueLength = value.Length;
            var sLength = valueLength + 3; // 2 double quotes + ending errata

            if (pos > _chars.Length - sLength)
            {
                Grow(sLength);
            }

            _chars[pos++] = '"';

            ref var start = ref MemoryMarshal.GetReference(value);
            for (var i = 0; i < valueLength; i++)
            {
                ref var c = ref Unsafe.Add(ref start, i);
                if (c == '"')
                {
                    _chars[pos++] = '"';
                    _chars[pos++] = '"';
                    var remaining = 2 + valueLength - i; // we need an extra quote for the double quote.
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

            _chars[pos++] = '"';
            WriteUtf16EndingErrata();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(string value)
        {
            if (value != null)
            {
                WriteUtf16(value.AsSpan());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(char value)
        {
            WriteUtf16(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(DateTime value)
        {
            ref var pos = ref _pos;
            const int dtSize = 36; // Form o + two JsonUtf16Constant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            _chars[pos++] = '"';
            DateTimeFormatter.TryFormat(value, _chars.Slice(pos), out var written);
            pos += written;
            _chars[pos++] = '"';
            WriteUtf16EndingErrata();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16(DateTimeOffset value)
        {
            ref var pos = ref _pos;
            const int dtSize = 36; // Form o + two JsonUtf16Constant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            _chars[pos++] = '"';
            DateTimeFormatter.TryFormat(value, _chars.Slice(pos), out var written);
            pos += written;
            _chars[pos++] = '"';
            WriteUtf16EndingErrata();
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void WriteUtf16Seperator()
        //{
        //    WriteUtf16Verbatim(_utf16Seperator);
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void WriteUtf16NewLine()
        //{
        //    WriteUtf16Verbatim('\n');
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private void WriteUtf16Verbatim(char c)
        //{
        //    ref var pos = ref _pos;
        //    if (pos > _chars.Length - 1)
        //    {
        //        Grow(1);
        //    }

        //    _chars[pos++] = c;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16EndingErrata()
        {
            _chars[_pos++] = _elements-- > 0 ? _utf16Seperator : '\n';
        }

        public void WriteUtf16(bool value)
        {
            ref var pos = ref _pos;
            if (value)
            {
                const int trueLength = 5;
                if (pos > _chars.Length - trueLength)
                {
                    Grow(trueLength);
                }

                _chars[pos++] = 't';
                _chars[pos++] = 'r';
                _chars[pos++] = 'u';
                _chars[pos++] = 'e';
            }
            else
            {
                const int falseLength = 6;
                if (pos > _chars.Length - falseLength)
                {
                    Grow(falseLength);
                }

                _chars[pos++] = 'f';
                _chars[pos++] = 'a';
                _chars[pos++] = 'l';
                _chars[pos++] = 's';
                _chars[pos++] = 'e';
            }
            WriteUtf16EndingErrata();
        }
    }
}
