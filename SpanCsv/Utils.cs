using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SpanCsv
{
    internal static class Utils
    {
        internal static (char[],byte[]) BuildHeaderArrays(string[] headers, char delimiter, bool camelCase)
        {
            var valueBuilder = new ValueStringBuilder();
            for(int i = 0; i < headers.Length;i++)
            {
                if(i > 0)
                {
                    valueBuilder.Append(delimiter);
                }

                valueBuilder.Append(camelCase ? char.ToLowerInvariant(headers[i][0]): headers[i][0]);
                valueBuilder.Append(headers[i].AsSpan(1));
            }
            valueBuilder.Append('\n');
            var chars = new char[valueBuilder.Length];
            valueBuilder.TryCopyTo(chars, out int written);
            valueBuilder.Dispose();
            var bytes = Encoding.UTF8.GetBytes(chars);

            

            return (chars, bytes);
        }
        internal static Dictionary<TKey, TValue> AddRange<TKey, TValue>(this Dictionary<TKey, TValue> collection, IEnumerable<(TKey, TValue)> values)
        {
            foreach(var (key,value) in values)
            {
                collection.Add(key,value);
            }

            return collection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountDigits(ulong value)
        {
            var digits = 1;
            uint part;
            if (value >= 10000000)
            {
                if (value >= 100000000000000)
                {
                    part = (uint) (value / 100000000000000);
                    digits += 14;
                }
                else
                {
                    part = (uint) (value / 10000000);
                    digits += 7;
                }
            }
            else
            {
                part = (uint) value;
            }

            if (part < 10)
            {
                // no-op
            }
            else if (part < 100)
            {
                digits += 1;
            }
            else if (part < 1000)
            {
                digits += 2;
            }
            else if (part < 10000)
            {
                digits += 3;
            }
            else if (part < 100000)
            {
                digits += 4;
            }
            else if (part < 1000000)
            {
                digits += 5;
            }
            else
            {
                Debug.Assert(part < 10000000);
                digits += 6;
            }

            return digits;
        }
    }
}
