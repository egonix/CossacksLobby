using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
    internal delegate void PackageWriter<T>(T t, byte[] buffer, ref int offset);

    internal static class PackageWriterBuilder
    {
        public static PackageWriter<T> Build<T>()
        {
            return (PackageWriter<T>)Build(typeof(T));
        }

        public static Delegate Build(Type type)
        {
            ParameterExpression t = Expression.Parameter(type);
            ParameterExpression buffer = Expression.Parameter(typeof(byte[]));
            ParameterExpression offset = Expression.Parameter(typeof(int).MakeByRefType());

            Expression body = BuildWriter(t.Type, NoAttributes, t, buffer, offset);
            return Expression.Lambda(typeof(PackageWriter<>).MakeGenericType(type), body, t, buffer, offset).Compile();
        }

        private static Expression BuildWriter(Type type, IEnumerable<Attribute> attributes, Expression expression, ParameterExpression buffer, ParameterExpression offset)
        {
            if (type == typeof(Int16)) return BuildIntegerWriter(((Func<Int16, byte[]>)BitConverter.GetBytes).Method, 2, expression, buffer, offset);
            else if (type == typeof(Int32)) return BuildIntegerWriter(((Func<Int32, byte[]>)BitConverter.GetBytes).Method, 4, expression, buffer, offset);
            else if (type == typeof(Int64)) return BuildIntegerWriter(((Func<Int64, byte[]>)BitConverter.GetBytes).Method, 8, expression, buffer, offset);
            else if (type == typeof(UInt16)) return BuildIntegerWriter(((Func<UInt16, byte[]>)BitConverter.GetBytes).Method, 2, expression, buffer, offset);
            else if (type == typeof(UInt32)) return BuildIntegerWriter(((Func<UInt32, byte[]>)BitConverter.GetBytes).Method, 4, expression, buffer, offset);
            else if (type == typeof(UInt64)) return BuildIntegerWriter(((Func<UInt64, byte[]>)BitConverter.GetBytes).Method, 8, expression, buffer, offset);
            else if (type == typeof(Byte)) return BuildByteWriter(expression, buffer, offset);
            else if (type == typeof(SByte)) return BuildByteWriter(Expression.Convert(expression, typeof(Byte)), buffer, offset);
            else if (type == typeof(Boolean)) return BuildByteWriter(ConvertBooleanToByte(expression), buffer, offset);
            else if (type.IsEnum) return BuildWriter(type.GetEnumUnderlyingType(), attributes, Expression.Convert(expression, type.GetEnumUnderlyingType()), buffer, offset);
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return BuildListWriter(type, attributes, expression, type.GetGenericArguments().Single(), buffer, offset);
            else if (type == typeof(String)) return BuildStringWriter(expression, attributes, buffer, offset);
            else if (type.IsClass) return BuildClassWriter(type, expression, buffer, offset);
            else throw new NotSupportedException();
        }

        private static Expression BuildIntegerWriter(MethodInfo method, int size, Expression expression, ParameterExpression buffer, ParameterExpression offset)
        {
            MethodInfo blockCopy = ((Action<Array, int, Array, int, int>)Buffer.BlockCopy).Method;
            return Expression.Block(
                Expression.Call(blockCopy, Expression.Call(method, expression), Expression.Constant(0), buffer, offset, Expression.Constant(size)),
                Expression.AddAssign(offset, Expression.Constant(size)));
        }

        private static Expression BuildByteWriter(Expression expression, ParameterExpression buffer, ParameterExpression offset)
        {
            return Expression.Block(
                Expression.Assign(Expression.ArrayAccess(buffer, offset), expression),
                Expression.AddAssign(offset, Expression.Constant(1)));
        }

        private static Expression BuildStringWriter(Expression expression, IEnumerable<Attribute> attributes, ParameterExpression buffer, ParameterExpression offset)
        {
            Type lengthType = attributes.OfType<LengthAttribute>().FirstOrDefault()?.Type ?? typeof(Byte);
            int sizeOfLength = System.Runtime.InteropServices.Marshal.SizeOf(lengthType);
            Encoding encoding = Encoding.UTF8;
            MethodInfo getByteCount = ((Func<string, int>)encoding.GetByteCount).Method;
            MethodInfo getBytes = ((Func<string, int, int, byte[], int, int>)encoding.GetBytes).Method;
            ParameterExpression length = Expression.Variable(typeof(int));
            return Expression.Block(new ParameterExpression[] { length },
                Expression.Assign(length, Expression.Call(Expression.Constant(encoding), getBytes, expression, Expression.Constant(0), Expression.Property(expression, "Length"), buffer, Expression.Add(offset, Expression.Constant(sizeOfLength)))),
                BuildWriter(lengthType, NoAttributes, Expression.ConvertChecked(length, lengthType), buffer, offset),
                Expression.AddAssign(offset, length));
        }

        private static Expression BuildClassWriter(Type type, Expression expression, ParameterExpression buffer, ParameterExpression offset)
        {
            ParameterExpression result = Expression.Variable(type);
            PropertyInfo[] properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null)
                .ToArray();
            return Expression.Block(properties.Select(property => BuildWriter(property.PropertyType, property.GetCustomAttributes(), Expression.Property(expression, property), buffer, offset)));
        }

        private static Expression BuildListWriter(Type type, IEnumerable<Attribute> attributes, Expression expression, Type itemType, ParameterExpression buffer, ParameterExpression offset)
        {
            Type zeroTerminatedType = attributes.OfType<ZeroTerminatedAttribute>().FirstOrDefault()?.Type;
            Type lengthType = attributes.OfType<LengthAttribute>().FirstOrDefault()?.Type ?? typeof(Int32);
            if (zeroTerminatedType == null)
                return BuildPrefixLengthListWriter(type, attributes, expression, lengthType, itemType, buffer, offset);
            else
                return BuildZeroTerminatedListReader(type, attributes, expression, itemType, zeroTerminatedType, buffer, offset);
        }

        private static Expression BuildPrefixLengthListWriter(Type type, IEnumerable<Attribute> attributes, Expression expression, Type lengthType, Type itemType, ParameterExpression buffer, ParameterExpression offset)
        {
            return Expression.Block(
                BuildWriter(lengthType, NoAttributes, Expression.Convert(Expression.Property(expression, "Count"), lengthType), buffer, offset),
                ForEach(expression, itemType, item => BuildWriter(itemType, NoAttributes, item, buffer, offset)));
        }

        private static Expression BuildZeroTerminatedListReader(Type type, IEnumerable<Attribute> attributes, Expression expression, Type itemType, Type terminatorType, ParameterExpression buffer, ParameterExpression offset)
        {
            return Expression.Block(
                ForEach(expression, itemType, item => BuildWriter(itemType, NoAttributes, item, buffer, offset)),
                BuildWriter(terminatorType, NoAttributes, Expression.Constant(System.Convert.ChangeType(0, terminatorType)), buffer, offset));
        }

        private static Expression ForEach(Expression list, Type itemType, Func<Expression, Expression> buildBody)
        {
            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(itemType);
            Type enumeratorType = typeof(IEnumerator<>).MakeGenericType(itemType);
            MethodInfo moveNext = typeof(IEnumerator).GetMethod("MoveNext");
            ParameterExpression enumerator = Expression.Variable(enumeratorType);
            LabelTarget finished = Expression.Label();
            return Expression.Block(new ParameterExpression[] { enumerator },
                Expression.Assign(enumerator, Expression.Convert(Expression.Call(list, "GetEnumerator", null), enumerator.Type)),
                Expression.Loop(Expression.Block(
                    Expression.IfThen(
                        Expression.IsFalse(Expression.Call(enumerator, moveNext, null)),
                        Expression.Break(finished)),
                    buildBody(Expression.Property(enumerator, "Current"))),
                    finished));
        }

        private static readonly Attribute[] NoAttributes = new Attribute[0];

        private static Expression ConvertBooleanToByte(Expression expression)
        {
            LabelTarget exit = Expression.Label(typeof(Byte));
            return Expression.Block(
                Expression.IfThen(expression, Expression.Return(exit, Expression.Constant((byte)1))),
                Expression.Label(exit, Expression.Constant((byte)0)));
        }
    }

    internal class PackageWriterTest
    {
        public enum Enum : ushort
        {
            A, B, C, D, E, F, G, H, I, J,
        }
        public class SubClass
        {
            public sbyte A { get; set; }
            public ushort B { get; set; }
            public uint C { get; set; }
            public ulong D { get; set; }
        }

        public byte A { get; set; }
        public short B { get; set; }
        public int C { get; set; }
        public long D { get; set; }
        public SubClass E { get; set; }
        [ZeroTerminated(typeof(byte))]
        public List<SubClass> F { get; set; }
        public Enum G { get; set; }
        [Length(typeof(ushort))]
        public string H { get; set; }
        public List<string> I { get; set; }
        [Length(typeof(byte))]
        public List<int> J { get; set; }
        public bool L { get; set; }

        [Test]
        public static void Test()
        {
            PackageWriterTest test = new PackageWriterTest()
            {
                A = 1,
                B = 2,
                C = 3,
                D = 4,
                E = new SubClass()
                {
                    A = 5,
                    B = 6,
                    C = 7,
                    D = 8,
                },
                F = new List<SubClass>()
                {
                    new SubClass()
                    {
                        A = 1,
                        B = 2,
                        C = 3,
                        D = 4,
                    },
                    new SubClass()
                    {
                        A = 5,
                        B = 6,
                        C = 7,
                        D = 8,
                    }
                },
                G = Enum.J,
                H = "Test",
                I = new List<string>() { "1", "2", "3", "4", "5" },
                J = new List<int>() { 1, 2, 3, 4, 5 },
                L = true
            };
            byte[] data = Enumerable.Repeat<byte>(0xFF, 112).ToArray();
            byte[] expected = new byte[] {
                0x01, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05,
                0x06, 0x00, 0x07, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02,
                0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x06, 0x00,
                0x07, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x00, 0x04,
                0x00, 0x54, 0x65, 0x73, 0x74, 0x05, 0x00, 0x00, 0x00, 0x01, 0x31, 0x01, 0x32, 0x01, 0x33, 0x01,
                0x34, 0x01, 0x35, 0x05, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            };

            var writer = PackageWriterBuilder.Build<PackageWriterTest>();
            int offset = 0;
            writer(test, data, ref offset);
            Assert.That(offset, Is.EqualTo(105));
            CollectionAssert.AreEqual(expected, data);
        }
    }
}
