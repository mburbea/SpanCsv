using System;
using System.Runtime.CompilerServices;

namespace SpanCsv
{
    public ref partial struct CsvWriter<T> where T : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(sbyte? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(short? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(long? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ulong? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(DateTime? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(DateTimeOffset? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(float? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(double? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(decimal? value)
        {
            if (value.HasValue)
            {
                Write(value.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(bool? value)
        {
            if(value.HasValue)
            {
                Write(value.Value);
            }
        }

        public void Write<TOther>(TOther? value) where TOther : struct
        {
            Write(value.ToString());
        }
    }
}
