using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SpanCsv
{
    public class CsvSerializer : IEnumerable
    {
        private readonly struct Comma { }
        private readonly struct NewLine { }
        private readonly struct Dispose { }
        private readonly struct Flush { }
        
        private static class Utf8
        {
            internal static readonly Dictionary<Type, MethodInfo> WriteMethods = typeof(CsvWriter<byte>).GetMethods()
                .Where(x => x.Name == nameof(CsvWriter<byte>.WriteUtf8) && x.GetParameters().Length == 1 && x.GetGenericArguments().Length == 0)
                .ToDictionary(x => x.GetParameters()[0].ParameterType, x => x)
                .AddRange(new[]{
                    (typeof(Comma), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.WriteUtf8Seperator))),
                    (typeof(NewLine), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.WriteUtf8NewLine))),
                    (typeof(Dispose), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.Dispose))),
                    (typeof(Flush), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.FlushToStream)))
                });
        }

        private static class Utf16
        {
            internal static readonly Dictionary<Type, MethodInfo> WriteMethods = typeof(CsvWriter<char>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == nameof(CsvWriter<char>.WriteUtf16) && x.GetParameters().Length == 1)
                .ToDictionary(x => x.GetParameters()[0].ParameterType, x => x)
                .AddRange(new[]{
                    (typeof(Comma), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.WriteUtf16Seperator))),
                    (typeof(NewLine), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.WriteUtf16NewLine))),
                    (typeof(Dispose), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.Dispose))),
                    (typeof(Flush), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.FlushToTextWriter)))
                });
        }

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        private static class Tokens
        {
            public static readonly Type Comma = typeof(Comma);
            public static readonly Type NewLine = typeof(NewLine);
            public static readonly Type Dispose = typeof(Dispose);
            public static readonly Type Flush = typeof(Flush);
        }

        private readonly ConcurrentDictionary<Type, (byte[] utf8keys, char[] utf16keys, Action<Stream, IEnumerable, int> utf8writer, Action<TextWriter, IEnumerable, int> utf16writer)> _serializerDictionary =
            new ConcurrentDictionary<Type, (byte[], char[], Action<Stream, IEnumerable, int>, Action<TextWriter, IEnumerable, int>)>();

        private readonly bool _writeHeaders;
        private readonly bool _camelCaseHeaders;

        public CsvSerializer(bool camelCaseHeaders = true, bool writeHeaders = true)
        {
            _writeHeaders = writeHeaders;
            _camelCaseHeaders = camelCaseHeaders;
        }

        public void Add<TFrom, TTo>(Expression<Func<TFrom, TTo>> wireConverter)
        {
            _serializerDictionary.TryAdd(typeof(IEnumerable<TFrom>), CreateWriter(wireConverter));
        }

        private void Write<T>(T outputStream, IEnumerable data)
        {
            var (utf8Keys, utf16Keys, utf8Writer, utf16Writer) = _serializerDictionary.FirstOrDefault(x => x.Key.IsInstanceOfType(data)).Value;

            if (outputStream is Stream stream)
            {
                if (_writeHeaders)
                {
                    stream.Write(utf8Keys);
                }
                utf8Writer(stream, data, utf8Keys.Length);
            }
            else if (outputStream is TextWriter writer)
            {
                if (_writeHeaders)
                {
                    writer.Write(utf16Keys);
                }
                utf16Writer(writer, data, utf16Keys.Length);
            }
        }

        public void Write(Stream stream, IEnumerable data)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (data is null) throw new ArgumentNullException(nameof(data));
            
            Write<Stream>(stream, data);
        }

        public void Write(TextWriter writer, IEnumerable data)
        {
            if (writer is null) throw new ArgumentNullException(nameof(writer));
            if (data is null) throw new ArgumentNullException(nameof(data));
            Write<TextWriter>(writer, data);
        }

        private (byte[], char[], Action<Stream, IEnumerable, int>, Action<TextWriter, IEnumerable, int>) CreateWriter(LambdaExpression wireConverter)
        {
            if (!(wireConverter.Body is MemberInitExpression node))
            {
                throw new ArgumentException("Must be of form ()=>new T{...}", nameof(wireConverter));
            }
            var thisParam = wireConverter.Parameters[0];

            var targetType = node.Type;
            var properties = Array.FindAll(targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                prop => prop.CanRead && prop.CanWrite && prop.GetIndexParameters().Length == 0);
            var bindings = node.Bindings.OfType<MemberAssignment>();

            var propAccessors = (from property in properties
                                 join b in bindings
                                   on property equals b.Member
                                 into gj
                                 from binding in gj.DefaultIfEmpty(
                                     Expression.Bind(property, !property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) != null
                                     ? Expression.Constant(null, property.PropertyType)
                                     : (Expression)Expression.Default(property.PropertyType)))
                                 select binding.Expression).ToArray();

            var keys = Array.ConvertAll(properties, prop => prop.Name);
            var (utf16, utf8) = Utils.BuildHeaderArrays(keys, ',', _camelCaseHeaders);
            
            return (utf8, utf16, CreateWriter<Stream>(thisParam, properties, propAccessors), CreateWriter<TextWriter>(thisParam, properties, propAccessors));
        }

        private static Action<T, IEnumerable, int> CreateWriter<T>(ParameterExpression foreachVar, PropertyInfo[] properties, Expression[] propAccessors)
        {
            var guessParam = Expression.Variable(typeof(int), "guess");
            ParameterExpression outputParam = Expression.Variable(typeof(T), "output");
            ParameterExpression dataParam = Expression.Variable(typeof(IEnumerable), "data");

            ParameterExpression writerVar;
            Dictionary<Type, MethodInfo> methods;

            if (typeof(T) == typeof(Stream))
            {
                writerVar = Expression.Variable(typeof(CsvWriter<byte>), "writer");
                methods = Utf8.WriteMethods;
            }
            else if(typeof(T) == typeof(TextWriter))
            {
                writerVar = Expression.Variable(typeof(CsvWriter<char>), "writer");
                methods = Utf16.WriteMethods;
            }
            else
            {
                throw new InvalidOperationException();
            }

            var lambda = Expression.Lambda<Action<T, IEnumerable, int>>(
                Expression.Block(new[] { writerVar },
                Expression.Assign(writerVar, Expression.New(writerVar.Type.GetConstructors()[0], guessParam)),
                ForEach(Expression.Convert(dataParam, typeof(IEnumerable<>).MakeGenericType(foreachVar.Type)), foreachVar, BuildLoopBody()),
                Expression.Call(writerVar, methods[Tokens.Dispose])
                ), outputParam, dataParam, guessParam);

            return lambda.Compile();

            Expression BuildLoopBody()
            {
                var loopVars = new List<ParameterExpression>();
                var loopBody = new List<Expression>();
                for (int i = 0; i < propAccessors.Length; i++)
                {
                    if (i > 0)
                    {
                        loopBody.Add(Expression.Call(writerVar, methods[Tokens.Comma]));
                    }

                    var propAccessor = propAccessors[i];
                    var prop = properties[i];
                    var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    if (propAccessor is ConstantExpression constant && constant.Value == null)
                    {
                        // Do nothing we don't write nulls!
                    }
                    else if (methods.TryGetValue(prop.PropertyType, out var method))
                    {
                        loopBody.Add(Expression.Call(writerVar, method, propAccessor));
                    }
                    else if (methods.TryGetValue(underlyingType, out method))
                    {
                        var lvalue = Expression.Parameter(prop.PropertyType);
                        loopVars.Add(lvalue);
                        loopBody.Add(Expression.Assign(lvalue, propAccessor));
                        loopBody.Add(
                            Expression.IfThen(
                                Expression.Property(lvalue, "HasValue"),
                                Expression.Call(writerVar, method, Expression.Call(lvalue, "GetValueOrDefault", Type.EmptyTypes))));
                    }
                    else
                    {
                        var lvalue = propAccessor;
                        Expression condition = null;

                        if (underlyingType != prop.PropertyType)
                        {
                            lvalue = Expression.Parameter(prop.PropertyType);
                            condition = Expression.Property(lvalue, "HasValue");
                        }
                        else if (underlyingType.IsClass)
                        {
                            lvalue = Expression.Parameter(prop.PropertyType);
                            condition = Expression.NotEqual(lvalue, Expression.Constant(null));
                        }

                        Expression expr = Expression.Call(
                            writerVar, methods[typeof(string)], Expression.Call(lvalue, "ToString", Type.EmptyTypes));

                        if (condition != null)
                        {
                            loopVars.Add((ParameterExpression) lvalue);
                            loopBody.Add(Expression.Assign(lvalue, propAccessor));
                            expr = Expression.IfThen(condition, expr);
                        }

                        loopBody.Add(expr);
                    }
                }
                loopBody.Add(Expression.Call(writerVar, methods[Tokens.NewLine]));
                loopBody.Add(Expression.Call(writerVar, methods[Tokens.Flush], outputParam));


                return Expression.Block(loopVars, loopBody);
            }
        }

        public static Expression ForEach(Expression collection, ParameterExpression loopVar, Expression loopContent)
        {
            var type = loopVar.Type;
            var enumeratorVar = Expression.Variable(typeof(IEnumerator<>).MakeGenericType(type), "enumerator");

            var breakLabel = Expression.Label("LoopBreak");
            Expression loop = Expression.Block(
                Expression.Assign(enumeratorVar, Expression.Call(collection, typeof(IEnumerable<>).MakeGenericType(type).GetMethod("GetEnumerator"))),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext")),
                        Expression.Block(new[] { loopVar },
                            Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                            loopContent
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel)
            );

            return Expression.Block(
                    new[] { enumeratorVar  },
                    Expression.TryFinally(
                        loop,
                        Expression.Call(enumeratorVar, typeof(IDisposable).GetMethods()[0])
                    ));
        }

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }
}
