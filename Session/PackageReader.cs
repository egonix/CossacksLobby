using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CossacksLobby
{
    internal delegate T PackageReader<T>(byte[] buffer, ref int offset);

    internal static class PackageReaderBuilder
    {
        private static Type[] TupleTypes = new Type[]
        {
            typeof(Tuple<>),
            typeof(Tuple<,>),
            typeof(Tuple<,,>),
            typeof(Tuple<,,,>),
            typeof(Tuple<,,,,>),
            typeof(Tuple<,,,,,>),
            typeof(Tuple<,,,,,,>)
        };


        public static PackageReader<T> Build<T>()
        {
            return (PackageReader<T>)Build(typeof(T));
        }

        public static Delegate Build(Type type)
        {
            ParameterExpression buffer = Expression.Parameter(typeof(byte[]));
            ParameterExpression offset = Expression.Parameter(typeof(int).MakeByRefType());

            ParameterExpression result = Expression.Variable(type);
            Expression[] block = new Expression[] { BuildAssigment(result.Type, NoAttributes, result, Identity, buffer, offset), result };
            Expression body = Expression.Block(new ParameterExpression[] { result }, block);
            return Expression.Lambda(typeof(PackageReader<>).MakeGenericType(type), body, buffer, offset).Compile();
        }

        private static Expression BuildAssigment(Type type, IEnumerable<Attribute> attributes, Expression expression, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            UnknownAttribute unknownAttribute = attributes.OfType<UnknownAttribute>().SingleOrDefault();
            Type genericType = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
            Type[] genericArguments = type.IsGenericType ? type.GetGenericArguments() : null;
            if (unknownAttribute != null) return BuildUnknownReader(unknownAttribute, expression, transform, buffer, offset);
            else if (type == typeof(Int16)) return BuildIntegerReader(((Func<byte[], int, Int16>)BitConverter.ToInt16).Method, 2, expression, transform, buffer, offset);
            else if (type == typeof(Int32)) return BuildIntegerReader(((Func<byte[], int, Int32>)BitConverter.ToInt32).Method, 4, expression, transform, buffer, offset);
            else if (type == typeof(Int64)) return BuildIntegerReader(((Func<byte[], int, Int64>)BitConverter.ToInt64).Method, 8, expression, transform, buffer, offset);
            else if (type == typeof(UInt16)) return BuildIntegerReader(((Func<byte[], int, UInt16>)BitConverter.ToUInt16).Method, 2, expression, transform, buffer, offset);
            else if (type == typeof(UInt32)) return BuildIntegerReader(((Func<byte[], int, UInt32>)BitConverter.ToUInt32).Method, 4, expression, transform, buffer, offset);
            else if (type == typeof(UInt64)) return BuildIntegerReader(((Func<byte[], int, UInt64>)BitConverter.ToUInt64).Method, 8, expression, transform, buffer, offset);
            else if (type == typeof(Byte)) return BuildByteReader(expression, transform, buffer, offset);
            else if (type == typeof(SByte)) return BuildByteReader(expression, Convert(transform, type), buffer, offset);
            else if (type == typeof(Boolean)) return BuildByteReader(expression, ConvertByteToBoolean(transform), buffer, offset);
            else if (type.IsEnum) return BuildAssigment(type.GetEnumUnderlyingType(), attributes, expression, Convert(transform, type), buffer, offset);
            else if (genericType == typeof(List<>)) return BuildListReader(type, attributes, expression, genericArguments.Single(), transform, buffer, offset);
            else if (TupleTypes.Contains(genericType)) return BuildTupleReader(type, expression, genericArguments, transform, buffer, offset);
            else if (type == typeof(String)) return BuildStringReader(expression, attributes, transform, buffer, offset);
            else if (type.IsClass) return BuildClassReader(type, expression, transform, buffer, offset);
            else throw new NotSupportedException();
        }

        private static Expression BuildUnknownReader(UnknownAttribute unknownAttribute, Expression expression, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            List<Expression> parts = new List<Expression>();
            if (unknownAttribute.Validate)
            {
                ParameterExpression i = Expression.Parameter(offset.Type);
                LabelTarget exit = Expression.Label();
                parts.Add(Expression.Block(new ParameterExpression[] { i },
                    Expression.Assign(i, Expression.Constant(System.Convert.ChangeType(0, i.Type))),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.Equal(i, Expression.Constant(unknownAttribute.Pattern.Length)),
                            Expression.Break(exit),
                            Expression.IfThen(
                                Expression.NotEqual(Expression.ArrayAccess(buffer, Expression.Add(offset, i)), Expression.ArrayAccess(Expression.Constant(unknownAttribute.Pattern), Expression.PostIncrementAssign(i))),
                                Expression.Throw(Expression.New(typeof(InvalidDataException))))),
                        exit)));
            }
            if (unknownAttribute.Copy)
            {
                ParameterExpression result = Expression.Variable(typeof(byte[]));
                parts.Add(Expression.Block(new ParameterExpression[] { result },
                    Expression.Assign(result, Expression.NewArrayBounds(result.Type.GetElementType(), Expression.Constant(unknownAttribute.Pattern.Length))),
                    Expression.Call(((Action<Array, int, Array, int, int>)Buffer.BlockCopy).Method, buffer, offset, result, Expression.Constant(0), Expression.Constant(unknownAttribute.Pattern.Length)),
                    Expression.Assign(expression, transform(result))));
            }
            else
            {
                parts.Add(Expression.Assign(expression, Expression.Constant(null, typeof(byte[]))));
            }
            parts.Add(Expression.AddAssign(offset, Expression.Constant(unknownAttribute.Pattern.Length)));
            return Expression.Block(parts);
        }

        private static Expression BuildIntegerReader(MethodInfo method, int size, Expression expression, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            return Expression.Block(
                Expression.Assign(expression, transform(Expression.Call(method, buffer, offset))),
                Expression.AddAssign(offset, Expression.Constant(size)));
        }

        private static Expression BuildByteReader(Expression expression, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            return Expression.Block(
                Expression.Assign(expression, transform(Expression.ArrayAccess(buffer, offset))),
                Expression.AddAssign(offset, Expression.Constant(1)));
        }

        private static Expression BuildStringReader(Expression expression, IEnumerable<Attribute> attributes, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            Type lengthType = attributes.OfType<LengthAttribute>().FirstOrDefault()?.Type ?? typeof(Byte);
            Encoding encoding = Encoding.UTF8;
            MethodInfo method = ((Func<byte[], int, int, string>)encoding.GetString).Method;
            ParameterExpression length = Expression.Variable(typeof(int));
            return Expression.Block(new ParameterExpression[] { length },
                BuildAssigment(lengthType, NoAttributes, length, Convert(Identity, typeof(int)), buffer, offset),
                Expression.Assign(expression, Expression.Call(Expression.Constant(encoding), method, buffer, offset, length)),
                Expression.AddAssign(offset, length));
        }

        private static Expression BuildClassReader(Type type, Expression expression, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            ParameterExpression result = Expression.Variable(type);
            PropertyInfo[] properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null)
                .ToArray();
            Expression[] block = new Expression[properties.Length + 2];
            block[0] = Expression.Assign(result, Expression.New(type));
            block[block.Length - 1] = Expression.Assign(expression, result);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                block[i + 1] = BuildAssigment(property.PropertyType, property.GetCustomAttributes(), Expression.Property(result, property), Identity, buffer, offset);
            }
            return Expression.Block(new ParameterExpression[] { result }, block);
        }

        private static Expression BuildListReader(Type type, IEnumerable<Attribute> attributes, Expression expression, Type itemType, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            Type zeroTerminatedType = attributes.OfType<ZeroTerminatedAttribute>().FirstOrDefault()?.Type;
            Type lengthType = attributes.OfType<LengthAttribute>().FirstOrDefault()?.Type ?? typeof(Int32);
            if (zeroTerminatedType == null)
                return BuildPrefixLengthListReader(type, attributes, expression, lengthType, itemType, transform, buffer, offset);
            else
                return BuildZeroTerminatedListReader(type, attributes, expression, itemType, zeroTerminatedType, transform, buffer, offset);
        }

        private static Expression BuildTupleReader(Type type, Expression expression, Type[] genericArguments, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            ParameterExpression[] items = genericArguments.Select(Expression.Variable).ToArray();
            List<Expression> expressions = new List<Expression>();
            expressions.AddRange(items.Select(item => BuildAssigment(item.Type, NoAttributes, item, Identity, buffer, offset)));
            expressions.Add(Expression.Assign(expression, transform(Expression.Call(typeof(Tuple), "Create", genericArguments, items))));
            return Expression.Block(items, expressions);
        }


        private static Expression BuildPrefixLengthListReader(Type type, IEnumerable<Attribute> attributes, Expression expression, Type lengthType, Type itemType, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            ParameterExpression result = Expression.Variable(type);
            ParameterExpression length = Expression.Variable(typeof(Int32));
            ParameterExpression item = Expression.Variable(itemType);
            LabelTarget finished = Expression.Label(result.Type);
            MethodInfo add = expression.Type.GetMethod("Add", new Type[] { itemType });
            ConstructorInfo constructor = result.Type.GetConstructor(new Type[] { typeof(int) });
            return Expression.Block(new ParameterExpression[] { result, length },
                BuildAssigment(lengthType, NoAttributes, length, e => Expression.ConvertChecked(e, length.Type), buffer, offset),
                Expression.Assign(result, Expression.New(constructor, length)),
                Expression.Assign(expression, transform(result)),
                Expression.Loop(Expression.Block(new ParameterExpression[] { item },
                    BuildAssigment(item.Type, NoAttributes, item, Identity, buffer, offset),
                    Expression.Call(result, add, item),
                    Expression.IfThen(
                        Expression.Equal(Expression.PreDecrementAssign(length), Expression.Constant(0)),
                        Expression.Break(finished, result))),
                    finished));
        }

        private static Expression BuildZeroTerminatedListReader(Type type, IEnumerable<Attribute> attributes, Expression expression, Type itemType, Type terminatorType, Func<Expression, Expression> transform, ParameterExpression buffer, ParameterExpression offset)
        {
            ParameterExpression result = Expression.Variable(type);
            ParameterExpression item = Expression.Variable(itemType);
            ParameterExpression terminator = Expression.Variable(terminatorType);
            ParameterExpression terminatorOffset = Expression.Variable(offset.Type);
            LabelTarget finished = Expression.Label(result.Type);
            MethodInfo add = expression.Type.GetMethod("Add", new Type[] { itemType });
            return Expression.Block(new ParameterExpression[] { result },
                Expression.Assign(result, Expression.New(result.Type)),
                Expression.Assign(expression, transform(result)),
                Expression.Loop(Expression.Block(new ParameterExpression[] { item, terminator, terminatorOffset },
                    Expression.Assign(terminatorOffset, offset),
                    BuildAssigment(terminatorType, NoAttributes, terminator, Identity, buffer, terminatorOffset),
                    Expression.IfThen(
                        Expression.Equal(terminator, Expression.Constant(System.Convert.ChangeType(0, terminator.Type), terminator.Type)),
                        Expression.Block(
                            Expression.Assign(offset, terminatorOffset),
                            Expression.Break(finished, result))),
                    BuildAssigment(item.Type, NoAttributes, item, Identity, buffer, offset),
                    Expression.Call(result, add, item)),
                    finished));
        }

        private static readonly Attribute[] NoAttributes = new Attribute[0];

        private static Expression Identity(Expression expression) { return expression; }

        private static Func<Expression, Expression> Convert(Func<Expression, Expression> transform, Type type)
        {
            return e => transform(Expression.Convert(e, type));
        }

        private static Func<Expression, Expression> ConvertByteToBoolean(Func<Expression, Expression> transform)
        {
            LabelTarget exit = Expression.Label(typeof(Boolean));
            return e => Expression.Block(
                Expression.IfThen(
                    Expression.NotEqual(transform(e), Expression.Constant((byte)0)),
                    Expression.Return(exit, Expression.Constant(true))),
                Expression.Label(exit, Expression.Constant(false)));
        }
    }

    internal class PackageReaderTest
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
        [Unknown("01 02 03 04", Copy = true, Validate = true)]
        public byte[] Unknown { get; set; }

        [Test]
        public static void Test()
        {
            int offset = 0;
            byte[] data = new byte[] {
                0x01, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05,
                0x06, 0x00, 0x07, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02,
                0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x06, 0x00,
                0x07, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x00, 0x04,
                0x00, 0x54, 0x65, 0x73, 0x74, 0x05, 0x00, 0x00, 0x00, 0x01, 0x31, 0x01, 0x32, 0x01, 0x33, 0x01,
                0x34, 0x01, 0x35, 0x05, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0x01, 0x02, 0x03, 0x04, 0xFF, 0xFF, 0xFF,
            };
            var reader = PackageReaderBuilder.Build<PackageReaderTest>();
            var test = reader(data, ref offset);
            Assert.That(offset, Is.EqualTo(109));
            Assert.That(test.A, Is.EqualTo(1));
            Assert.That(test.B, Is.EqualTo(2));
            Assert.That(test.C, Is.EqualTo(3));
            Assert.That(test.D, Is.EqualTo(4));
            Assert.That(test.E.A, Is.EqualTo(5));
            Assert.That(test.E.B, Is.EqualTo(6));
            Assert.That(test.E.C, Is.EqualTo(7));
            Assert.That(test.E.D, Is.EqualTo(8));
            Assert.That(test.F, Has.Count.EqualTo(2));
            Assert.That(test.F[0].A, Is.EqualTo(1));
            Assert.That(test.F[0].B, Is.EqualTo(2));
            Assert.That(test.F[0].C, Is.EqualTo(3));
            Assert.That(test.F[0].D, Is.EqualTo(4));
            Assert.That(test.F[1].A, Is.EqualTo(5));
            Assert.That(test.F[1].B, Is.EqualTo(6));
            Assert.That(test.F[1].C, Is.EqualTo(7));
            Assert.That(test.F[1].D, Is.EqualTo(8));
            Assert.That(test.G, Is.EqualTo(Enum.J));
            Assert.That(test.H, Is.EqualTo("Test"));
            Assert.That(test.I, Has.Count.EqualTo(5));
            Assert.That(test.I[0], Is.EqualTo("1"));
            Assert.That(test.I[1], Is.EqualTo("2"));
            Assert.That(test.I[2], Is.EqualTo("3"));
            Assert.That(test.I[3], Is.EqualTo("4"));
            Assert.That(test.I[4], Is.EqualTo("5"));
            Assert.That(test.J, Has.Count.EqualTo(5));
            Assert.That(test.J[0], Is.EqualTo(1));
            Assert.That(test.J[1], Is.EqualTo(2));
            Assert.That(test.J[2], Is.EqualTo(3));
            Assert.That(test.J[3], Is.EqualTo(4));
            Assert.That(test.J[4], Is.EqualTo(5));
            Assert.That(test.L, Is.EqualTo(true));
            Assert.That(test.Unknown, Has.Length.EqualTo(4));
            Assert.That(test.Unknown[0], Is.EqualTo(1));
            Assert.That(test.Unknown[1], Is.EqualTo(2));
            Assert.That(test.Unknown[2], Is.EqualTo(3));
            Assert.That(test.Unknown[3], Is.EqualTo(4));
        }
    }
}
