using System;
using System.Collections.Generic;
using System.Text;

namespace SpanCsv
{
    static class Constants
    {
        public static readonly byte[] LongMinValueUtf8 = Encoding.UTF8.GetBytes(long.MinValue.ToString());
        public static readonly char[] LongMinValueUtf16 = long.MinValue.ToString().ToCharArray();

        public const int MaxNumberBufferSize = 32;
    }
}
