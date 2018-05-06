using SpanCsv;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace SpanCsvTest
{
    class Test
    {
        public Byte Byte { get; set; }
        public SByte SByte { get; set; }
        public Byte? NByte { get; set; }
        public SByte? NSByte { get; set; }

        public short Short { get; set; }
        public int Int { get; set; }
        public long Long { get; set; }


        public ushort UShort { get; set; }
        public uint UInt { get; set; }
        public ulong ULong { get; set; }

        public float Float { get; set; }
        public double Double { get; set; }
        public decimal Decimal { get; set; }
        public string String { get; set; }

        public DateTime DateTime { get; set; }
        public bool Bool { get; set; }


        public short? NShort { get; set; }
        public int? NInt { get; set; }
        public long? NLong { get; set; }


        public ushort? NUShort { get; set; }
        public uint? NUInt { get; set; }
        public ulong? NULong { get; set; }

        public float? NFloat { get; set; }
        public double? NDouble { get; set; }
        public decimal? NDecimal { get; set; }

        public DateTime? NDateTime { get; set; }
        public bool? NBool { get; set; }
        public B Unassigned { get; set; }
        public B Assigned { get; set; }
    }

    class B
    {
        public B(int b) => _b = b;
        int _b;
        public override string ToString() => _b.ToString();
    }

    class Program
    {
        static Test Populate(int value)
        {
            var properties = typeof(Test).GetProperties();
            var t = new Test();
            
            foreach (var (property,i) in properties.Select((x,i)=>(x,i)))
            {
                var f = value +i * 2  + ((value + i) / 100d * (Math.E - 2));
                if (typeof(IConvertible).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(DateTime) && property.PropertyType != typeof(DateTime?))
                {
                    property.SetValue(t, Convert.ChangeType(f, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType));
                }
                else if(property.Name == nameof(Test.DateTime))
                {
                    t.DateTime = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(f);
                }
                else if (property.Name == nameof(Test.NDateTime))
                {
                    t.NDateTime = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(f);
                }
                else if(property.Name == nameof(Test.Assigned))
                {
                    t.Assigned = new B((int)f);
                }

            }

            return t;
        }
        static void Main(string[] args)
        {
            var data = Enumerable.Range(0, 100).Select(i => Populate(i)).ToArray();
            var q = Enumerable.Range(0, 30_000).SelectMany(d => data);
            Console.WriteLine(q.Count());
            var csvSerializer = new CsvSerializer()
            {
                (Test t)=> new Test
                {
                    Byte = t.Byte,
                    Decimal = t.Decimal,
                    Double = t.Double,
                    Float = t.Float,
                    Int = t.Int,
                    Long = t.Long,
                    Short = t.Short,
                    String = t.String,
                    UInt = t.UInt,
                    ULong = t.ULong,
                    UShort = t.UShort,
                    SByte = t.SByte,
                    DateTime = t.DateTime,
                    Bool = t.Bool,

                    NByte = t.NByte,
                    NDecimal = t.NDecimal,
                    NDouble = t.NDouble,
                    NFloat = t.NFloat,
                    NInt = t.NInt,
                    NLong = t.NLong,
                    NShort = t.NShort,
                    NUInt = t.NUInt,
                    NULong = t.NULong,
                    NUShort = t.NUShort,
                    NSByte = t.NSByte,
                    NDateTime = t.NDateTime,
                    NBool = t.NBool,

                    Assigned = t.Assigned
                }
            };

            Stopwatch sw = Stopwatch.StartNew();
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    csvSerializer.Write(stream, q);
                }
                
            }
            Console.WriteLine(sw.Elapsed);
            sw = Stopwatch.StartNew();
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    csvSerializer.Write(writer, q);
                }

            }
            Console.WriteLine(sw.Elapsed);

        }
    }
}
