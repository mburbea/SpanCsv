using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace SpanCsv
{
    public class CsvSerializer : IEnumerable
    {
        private static class UTF8
        {
            internal static Dictionary<Type, MethodInfo> WriteMethods = typeof(CsvWriter<byte>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                 .Where(x => x.Name == nameof(CsvWriter<byte>.Write) && x.GetParameters().Length==1)
                .ToDictionary(x => x.GetParameters()[0].ParameterType, x=> x);            
        }

        private static class UTF16
        {
            internal static Dictionary<Type, MethodInfo> WriteMethods = typeof(CsvWriter<char>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                 .Where(x => x.Name == nameof(CsvWriter<char>.Write) && x.GetParameters().Length == 1)
                .ToDictionary(x => x.GetParameters()[0].ParameterType, x => x);
        }


        private ConcurrentDictionary<Type, (string[] keys, Delegate utf8write, Delegate utf16writer)> _serializerDictionary { get; set; }

        public void Add<TFrom, TTo>(Expression<Func<TFrom, TTo>> wireConverter) {
            _serializerDictionary.TryAdd(typeof(TTo), CreateWriter(wireConverter));
        }

        private (string[] keys, Delegate utf8writer, Delegate utf16writer) CreateWriter(LambdaExpression wireConverter)
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


            return (keys, null, null);
        }

        private static WriteDelegate CreateWriter<T>(Type type, ParameterExpression input, PropertyInfo[] properties, Expression[] propAccessors)
            where T:unmanaged
        {
            var guessParameter = Expression.Parameter(typeof(CsvWriter<T>), "guess");
            var writerVariable = Expression.Variable(typeof(CsvWriter<T>), "writer");

            Dictionary<Type, MethodInfo> writeMethods = null;
            if (typeof(T) == typeof(byte))
            {
                writeMethods = UTF8.WriteMethods;
            }
            else if(typeof(T) == typeof(char))
            {
                writeMethods = UTF16.WriteMethods;
            }
            else
            {
                throw new InvalidOperationException(); // unreachable.
            }
            var blockVars = new List<ParameterExpression>() { writerVariable };
            var block = new List<Expression>
            {
                Expression.Assign(writerVariable, Expression.New(writerVariable.Type.GetConstructors()[1], guessParameter)),
            };

            for(int i = 0; i < propAccessors.Length; i++)
            {
                var propAccessor = propAccessors[i];
                var prop = properties[i];
                var nullableType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var buffer = 
                if(i > 0)
                {
                    Expression.Call(writerVariable, nameof(CsvWriter<byte>.WriteSeperator), Type.EmptyTypes);
                }
                if (propAccessor is ConstantExpression constant && constant.Value == null)
                {
                    // Do nothing we don't write nulls!
                }
                else if(writeMethods.TryGetValue(nullableType, out var method))
                {
                    block.Add(Expression.Call(writerVariable, method, propAccessor));
                }
                else
                {
                    var lvalue = propAccessor;
                    if (!prop.PropertyType.IsValueType)
                    {
                        blockVars.Add((ParameterExpression)(lvalue = Expression.Variable(prop.PropertyType)));
                        block.Add(Expression.Assign(lvalue, propAccessor));

                    }

                    Expression expr = Expression.Call(
                        writerVariable,
                        writeMethods[typeof(string)],
                        Expression.Call(lvalue, nameof(object.ToString), Type.EmptyTypes));

                    if(lvalue != propAccessor)
                    {
                        expr = Expression.IfThen(Expression.Equal(lvalue, Expression.Constant(null, prop.PropertyType)), expr);
                    }

                    block.Add(expr);
                }
            }
            block.Add(Expression.Call(writerVariable, nameof(CsvWriter<int>.WriteNewLine), Type.EmptyTypes);
            block.Add(Expression.Call(writerVariable, nameof(CsvWriter<int>)))
            Expression.Lambda(Expression.Block(blockVars, block), input, guessParameter).Compile();


            return null;
        }


        public void Write<T>(IEnumerable<T> data, Stream s)
        {
            Action<T> serialize;
            foreach(var f in data)
            {
                serialize(f);

            }
        }
        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
