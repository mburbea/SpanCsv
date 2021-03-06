﻿using System;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SpanCsv
{
    internal ref partial struct CsvWriter<T> where T : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(sbyte value)=> WriteUtf8((long) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(short value)=> WriteUtf8((long) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(int value)=> WriteUtf8((long) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(byte value)=> WriteUtf8((ulong) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(ushort value)=> WriteUtf8((ulong) value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(uint value)=> WriteUtf8((ulong) value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(long value)
        {
            ref var pos = ref _pos;
            if (pos > _bytes.Length - 2)
            {
                Grow(2);
            }
            if (value < 0)
            {
                _bytes[pos++] = (byte)'-';
                value = unchecked(-value);
            }

            WriteUtf8((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(ulong value)
        {
            ref var pos = ref _pos;
            if (value < 10)
            {
                if (pos > _bytes.Length - 2)
                {
                    Grow(2);
                }

                _bytes[pos++] = (byte)('0' + value);
                WriteUtf8EndingErrata();
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
            WriteUtf8EndingErrata();
        }

        public void WriteUtf8(float value)
        {
            Span<char> span = stackalloc char[Constants.FloatBufferSize];
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
            WriteUtf8EndingErrata();
        }

        public void WriteUtf8(double value)
        {
            Span<char> span = stackalloc char[Constants.DoubleBufferSize];
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
            WriteUtf8EndingErrata();
        }

        public void WriteUtf8(decimal value)
        {
            if (value == 0)
            {
                WriteUtf8(0ul);
                return;
            }

            Span<char> span = stackalloc char[Constants.DecimalBufferSize];
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
            WriteUtf8EndingErrata();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(DateTime value)
        {
            ref var pos = ref _pos;
            const int dtSize = 36; // Form o + two JsonUtf8Constant.DoubleQuote
            if (pos > _bytes.Length - dtSize)
            {
                Grow(dtSize);
            }

            _bytes[_pos++] = (byte)'"';
            DateTimeFormatter.TryFormat(value, _bytes.Slice(pos), out var bytesWritten);
            pos += bytesWritten;
            _bytes[_pos++] = (byte)'"';
            WriteUtf8EndingErrata();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(DateTimeOffset value)
        {
            ref var pos = ref _pos;
            const int dtSize = 36; // Form o + two JsonUtf8Constant.DoubleQuote
            if (pos > _bytes.Length - dtSize)
            {
                Grow(dtSize);
            }

            _bytes[_pos++] = (byte)'"';
            DateTimeFormatter.TryFormat(value, _bytes.Slice(pos), out var bytesWritten);
            pos += bytesWritten;
            _bytes[_pos++] = (byte)'"';
            WriteUtf8EndingErrata();
        }

        public void WriteUtf8(ReadOnlySpan<char> value)
        {
            ref var pos = ref _pos;
            var valueLength = value.Length;
            var sLength = valueLength + 2; // 2 double quotes + errata

            if (pos > _bytes.Length - sLength)
            {
                Grow(sLength);
            }

            _bytes[_pos++] = (byte)'"';

            ref var start = ref MemoryMarshal.GetReference(value);
            for (var i = 0; i < valueLength; i++)
            {
                ref var c = ref Unsafe.Add(ref start, i);
                if (c == '"')
                {
                    _bytes[_pos++] = (byte)'"';
                    _bytes[_pos++] = (byte)'"';
                    var remaining = 2 + valueLength - i; // we need an extra quote for the double quote.
                    if (pos > _bytes.Length - remaining)
                    {
                        Grow(remaining);
                    }
                }
                else if (c > 0x7F) // UTF8 characters, we need to escape them
                {
                    ReadOnlySpan<char> temp = MemoryMarshal.CreateReadOnlySpan(ref c, 1);

                    var remaining = 5 + valueLength - i; // make sure that all characters, an extra 5 for a full escape and 4 for the utf8 bytes, still fit
                    if (pos > _bytes.Length - remaining)
                    {
                        Grow(remaining);
                    }

                    pos += Encoding.UTF8.GetBytes(temp, _bytes.Slice(pos));
                }
                else // ascii fast path.
                {
                    _bytes[pos++] = (byte)c;
                }
            }

            _bytes[_pos++] = (byte)'"';
            WriteUtf8EndingErrata();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(string value)
        {
            if (value != null)
            {
                WriteUtf8(value.AsSpan());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(char value)
        {
            WriteUtf8(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void WriteUtf8Seperator()
        //{
        //    WriteUtf8Verbatim(_utf8Seperator);
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void WriteUtf8NewLine()
        //{
        //    WriteUtf8Verbatim((byte)'\n');
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private void WriteUtf8Verbatim(byte c)
        //{
        //    ref var pos = ref _pos;
        //    if (pos > _bytes.Length - 1)
        //    {
        //        Grow(1);
        //    }

        //    _bytes[pos++] = c;
        //}

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf8EndingErrata()
        {
            _bytes[_pos++] = _elements-- > 0 ? _utf8Seperator : (byte)'\n';
        }

        public void WriteUtf8(bool value)
        {
            ref var pos = ref _pos;
            if (value)
            {
                const int trueLength = 5; // 4 for true + 1 for seperator.
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
                const int falseLength = 6;
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

            WriteUtf8EndingErrata();
        }
    }
}
