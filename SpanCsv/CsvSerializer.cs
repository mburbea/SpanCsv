using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly struct NullableGeneric { }
        private readonly struct Generic { }
        
        private static class UTF8
        {
            internal static Dictionary<Type, MethodInfo> WriteMethods = typeof(CsvWriter<byte>).GetMethods()
                .Where(x => x.Name == nameof(CsvWriter<byte>.Write) && x.GetParameters().Length == 1 && x.GetGenericArguments().Length == 0)
                .ToDictionary(x => x.GetParameters()[0].ParameterType, x => x)
                .AddRange(new[]{
                    (typeof(Comma), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.WriteSeperator))),
                    (typeof(NewLine), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.WriteNewLine))),
                    (typeof(Dispose), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.Dispose))),
                    (typeof(Flush), typeof(CsvWriter<byte>).GetMethod(nameof(CsvWriter<byte>.FlushToStream))),
                    (typeof(NullableGeneric), typeof(CsvWriter<byte>).GetMethods().First(x=> x.GetGenericArguments().Length == 1 && x.GetParameters()[0].ParameterType != x.GetGenericArguments()[0])),
                    (typeof(Generic), typeof(CsvWriter<byte>).GetMethods().First(x=> x.GetGenericArguments().Length == 1 && x.GetParameters()[0].ParameterType == x.GetGenericArguments()[0]))
                });
        }

        private static class UTF16
        {
            internal static Dictionary<Type, MethodInfo> WriteMethods = typeof(CsvWriter<char>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == nameof(CsvWriter<char>.Write) && x.GetParameters().Length == 1)
                .ToDictionary(x => x.GetParameters()[0].ParameterType, x => x)
                .AddRange(new[]{
                    (typeof(Comma), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.WriteSeperator))),
                    (typeof(NewLine), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.WriteNewLine))),
                    (typeof(Dispose), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.Dispose))),
                    (typeof(Flush), typeof(CsvWriter<char>).GetMethod(nameof(CsvWriter<char>.FlushToTextWriter))),
                    (typeof(NullableGeneric), typeof(CsvWriter<char>).GetMethods().First(x=> x.GetGenericArguments().Length == 1 && x.GetParameters()[0].ParameterType != x.GetGenericArguments()[0])),
                    (typeof(Generic), typeof(CsvWriter<char>).GetMethods().First(x=> x.GetGenericArguments().Length == 1 && x.GetParameters()[0].ParameterType == x.GetGenericArguments()[0]))

                });
        }

        private static class Tokens
        {
            public static readonly Type Comma = typeof(Comma);
            public static readonly Type NewLine = typeof(NewLine);
            public static readonly Type Dispose = typeof(Dispose);
            public static readonly Type Flush = typeof(Flush);
            public static readonly Type Generic = typeof(Generic);
            public static readonly Type NullableGeneric = typeof(NullableGeneric);
        }

        private ConcurrentDictionary<Type, (string[] keys, Action<Stream, IEnumerable, int> utf8write, Action<TextWriter, IEnumerable, int> utf16writer)> _serializerDictionary =
            new ConcurrentDictionary<Type, (string[] keys, Action<Stream, IEnumerable, int> utf8write, Action<TextWriter, IEnumerable, int> utf16writer)>();

        public void Add<TFrom, TTo>(Expression<Func<TFrom, TTo>> wireConverter)
        {
            _serializerDictionary.TryAdd(typeof(IEnumerable<TFrom>), CreateWriter(wireConverter));
        }

        private void Write<T>(T outputStream, IEnumerable data)
        {
            var t = data.GetType();

            var f = _serializerDictionary.FirstOrDefault(x => x.Key.IsInstanceOfType(data)).Value;

            if (outputStream is Stream stream)
            {
                f.utf8write(stream, data, 1000);
            }
            if (outputStream is TextWriter writer)
            {
                f.utf16writer(writer, data, 1000);
            }
            else
            {
                
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

        private (string[] keys, Action<Stream, IEnumerable, int> utf8write, Action<TextWriter, IEnumerable, int> utf16writer) CreateWriter(LambdaExpression wireConverter)
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
            
            return (keys, CreateWriter<Stream>(thisParam, properties, propAccessors), CreateWriter<TextWriter>(thisParam, properties, propAccessors));
        }

        private static Action<T, IEnumerable, int> CreateWriter<T>(ParameterExpression foreachVar, PropertyInfo[] properties, Expression[] propAccessors)
        {
            var guessParam = Expression.Variable(typeof(int), "guess");
            ParameterExpression outputParam = Expression.Variable(typeof(T), "output");
            ParameterExpression dataParam = Expression.Variable(typeof(IEnumerable), "data");

            ParameterExpression writerVar = default;
            Dictionary<Type, MethodInfo> methods = default;

            if (typeof(T) == typeof(Stream))
            {
                writerVar = Expression.Variable(typeof(CsvWriter<byte>), "writer");
                methods = UTF8.WriteMethods;
            }
            else if(typeof(T) == typeof(TextWriter))
            {
                writerVar = Expression.Variable(typeof(CsvWriter<char>), "writer");
                methods = UTF16.WriteMethods;
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
                    else if (underlyingType != prop.PropertyType)
                    {
                        loopBody.Add(Expression.Call(writerVar, methods[Tokens.NullableGeneric].MakeGenericMethod(underlyingType), propAccessor));
                    }
                    else
                    {
                        loopBody.Add(Expression.Call(writerVar, methods[Tokens.Generic].MakeGenericMethod(underlyingType), propAccessor));
                    }
                }
                loopBody.Add(Expression.Call(writerVar, methods[Tokens.NewLine]));
                loopBody.Add(Expression.Call(writerVar, methods[Tokens.Flush], outputParam));


                return Expression.Block(loopVars, loopBody);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public static Expression ForEach(Expression collection, ParameterExpression loopVar, Expression loopContent)
        {
            var elementType = loopVar.Type;
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
            var getEnumeratorCall = Expression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

            var breakLabel = Expression.Label("LoopBreak");
            Expression loop = Expression.Block(
                enumeratorAssign,
                Expression.Loop(
                    Expression.IfThenElse(
                        moveNextCall,
                        Expression.Block(new[] { loopVar },
                            Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                            loopContent
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel)
            );

            loop = Expression.Block(
                    new[] { enumeratorVar },
                    Expression.TryFinally(
                        loop,
                        Expression.Call(enumeratorVar, typeof(IDisposable).GetMethods()[0])
                    ));

            return loop;
        }
    }
}
