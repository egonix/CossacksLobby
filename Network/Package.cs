using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
        //CreateRoomRequest = 0x019C,
        //CreateRoomResponse = 0x019D,
        //RoomHostPcInfoSend = 0x008C,
        //RoomGameOptionsSend = 0x01AA,

    enum PackageNumber : ushort
    {
        Invalid = 0,
        ChatMessageRequest = 0x0196,
        ChatMessageResponse = 0x0197,
        CreateAccountRequest = 0x0198,
        LoginRequest = 0x019A,
        EnterLobby = 0x019B,
        //0x019F 
        //0x01A1 
        //0x01A3 
        NewGame = 0x01A5,
        NewPlayer = 0x01A6,
        //0x01A7 
        EmailCheckRequest = 0x01A8,
        EmailCheckResponse = 0x01A9,
        //0x01B0
        //0x019D 
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class PackageAttribute : Attribute
    {
        public PackageNumber Number { get; }

        public PackageAttribute(PackageNumber number)
        {
            Number = number;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class PackageHandlerAttribute : Attribute
    {
        public PackageHandlerAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class UnknownPackageHandlerAttribute : Attribute
    {
        public UnknownPackageHandlerAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class IgnoreAttribute : Attribute
    {
        public IgnoreAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class LengthAttribute : Attribute
    {
        public Type Type { get; }

        public LengthAttribute(Type type)
        {
            Type = type;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class ZeroTerminatedAttribute : Attribute
    {
        public Type Type { get; }

        public ZeroTerminatedAttribute()
            : this(typeof(Int32))
        {
        }

        public ZeroTerminatedAttribute(Type type)
        {
            Type = type;
        }
    }

    delegate Task Dispatcher<T>(T t, PackageNumber number, int unknown1, int unknown2, byte[] buffer, int offset, int count);

    static class Package
    {
        public const int HeaderSize = 14;
        public const int MaxSize = 65536;

        private static ConcurrentDictionary<Type, Tuple<PackageNumber, Delegate>> Writers = new ConcurrentDictionary<Type, Tuple<PackageNumber, Delegate>>();

        public static void Write<T>(Stream stream, int unknown1, int unknown2, T t)
        {
            Tuple<PackageNumber, Delegate> writer = Writers.GetOrAdd(typeof(T), BuildWriter);
            byte[] buffer = new byte[Package.MaxSize];
            int offset = HeaderSize;
            ((PackageWriter<T>)writer.Item2)(t, buffer, ref offset);
            Buffer.BlockCopy(BitConverter.GetBytes(offset - HeaderSize), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((short)writer.Item1), 0, buffer, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(unknown1), 0, buffer, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(unknown2), 0, buffer, 10, 4);
            stream.Write(buffer, 0, offset);
        }

        private static Tuple<PackageNumber, Delegate> BuildWriter(Type type)
        {
            PackageAttribute packageAttribute = type.GetCustomAttribute<PackageAttribute>();
            if (packageAttribute == null)
                throw new InvalidOperationException(type.Name + " is not a Package");
            return Tuple.Create(packageAttribute.Number, PackageWriterBuilder.Build(type));
        }

        public static Dispatcher<T> BuildDispatcher<T>()
        {
            Type type = typeof(T);
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var nodes = methods
                .Where(m => m.GetCustomAttribute<PackageHandlerAttribute>() != null)
                .Select(m =>
                {
                    ParameterInfo[] parameters = m.GetParameters();
                    if (parameters.Length != 3)
                        throw new InvalidOperationException(m.Name + " must have 3 parameters");
                    if (parameters[0].ParameterType != typeof(int))
                        throw new InvalidOperationException(m.Name + " first paramter must be a int");
                    if (parameters[1].ParameterType != typeof(int))
                        throw new InvalidOperationException(m.Name + " second paramter must be a int");
                    Type parameterType = parameters[2].ParameterType;
                    PackageAttribute packageAttribute = parameterType.GetCustomAttribute<PackageAttribute>();
                    if (packageAttribute == null)
                        throw new InvalidOperationException(m.Name + " third parameter is not a Package");
                    return new
                    {
                        Method = m,
                        Type = parameterType,
                        Number = packageAttribute.Number,
                    };
                });
            var defaultNode = methods.SingleOrDefault(m => m.GetCustomAttribute<UnknownPackageHandlerAttribute>() != null);
            foreach (var multipleDefinitions in nodes.GroupBy(g => g.Number).Where(g => g.Count() >= 2))
            {
                string methodNames = string.Join(", ", multipleDefinitions.Select(d => d.Method.Name));
                throw new InvalidOperationException(multipleDefinitions.Key + " has multiple handlers(" + methodNames + "). ");
            }
            ParameterExpression t = Expression.Parameter(type);
            ParameterExpression packageNumber = Expression.Parameter(typeof(PackageNumber));
            ParameterExpression unknown1 = Expression.Parameter(typeof(int));
            ParameterExpression unknown2 = Expression.Parameter(typeof(int));
            ParameterExpression buffer = Expression.Parameter(typeof(byte[]));
            ParameterExpression offset = Expression.Parameter(typeof(int));
            ParameterExpression count = Expression.Parameter(typeof(int));
            ParameterExpression finalOffset = Expression.Variable(typeof(int));
            LabelTarget exit = Expression.Label(typeof(Task));
            Expression emptyTask = Expression.Constant(Task.FromResult(0), typeof(Task));
            Func<Expression, Type, Expression> wrapHandlerCall = (call, returnType) => returnType == typeof(void) ? Expression.Block(call, emptyTask) : call;
            SwitchCase[] cases = nodes
                .Select(c =>
                {
                    Delegate @delegate = PackageReaderBuilder.Build(c.Type);
                    Type delegateType = @delegate.GetType();
                    MethodInfo invoke = delegateType.GetMethod("Invoke");
                    ParameterExpression package = Expression.Variable(c.Type);
                    Expression block = Expression.Block(new ParameterExpression[] { package },
                        Expression.Assign(package, Expression.Call(Expression.Constant(@delegate, delegateType), invoke, buffer, offset)),
                        Expression.IfThen(
                            Expression.NotEqual(offset, finalOffset),
                            Expression.Throw(Expression.New(typeof(InvalidDataException).GetConstructor(new Type[] { typeof(string) }), Expression.Constant("offsets for '" + c.Method.Name + "'do not match!")))),
                        Expression.Break(exit, wrapHandlerCall(Expression.Call(t, c.Method, unknown1, unknown2, package), c.Method.ReturnType)));
                    return Expression.SwitchCase(block, Expression.Constant(c.Number));
                })
                .ToArray();
            Expression defaultHandler = Expression.Break(exit, defaultNode != null
                ? wrapHandlerCall(Expression.Call(t, defaultNode, packageNumber, unknown1, unknown2, buffer, offset, count), defaultNode.ReturnType)
                : emptyTask);
            Expression body = Expression.Block(new ParameterExpression[] { finalOffset },
                Expression.Assign(finalOffset, Expression.Add(offset, count)),
                Expression.Switch(packageNumber, defaultHandler, cases),
                Expression.Label(exit, Expression.Constant(null, typeof(Task))));
            return Expression.Lambda<Dispatcher<T>>(body, t, packageNumber, unknown1, unknown2, buffer, offset, count).Compile();
        }
    }

    internal class PackageTest
    {
        [Package(PackageNumber.Invalid)]
        class Message
        {
            public int Value { get; set; }
        }

        [PackageHandler]
        void Handler(int unknown1, int unknown2, Message message)
        {
            Assert.AreEqual(1, unknown1);
            Assert.AreEqual(2, unknown2);
            Assert.IsNotNull(message);
            Assert.AreEqual(1, message.Value);
        }

        [UnknownPackageHandler]
        Task UnknownPackage(PackageNumber number, int unknown1, int unknown2, byte[] buffer, int offset, int count)
        {
            Assert.AreEqual((PackageNumber)1, number);
            Assert.AreEqual(1, unknown1);
            Assert.AreEqual(2, unknown2);
            Assert.IsNotNull(buffer);
            Assert.AreEqual(4, buffer.Length);
            Assert.AreEqual(0, offset);
            Assert.AreEqual(4, count);
            return Task.FromResult(0);
        }

        [Test]
        public static async Task Test()
        {
            var dispatcher = Package.BuildDispatcher<PackageTest>();
            byte[] data = new byte[] { 0x01, 0x00, 0x00, 0x00, };
            await dispatcher(new PackageTest(), PackageNumber.Invalid, 1, 2, data, 0, data.Length);
            await dispatcher(new PackageTest(), (PackageNumber)1, 1, 2, data, 0, data.Length);
            Assert.Catch<InvalidDataException>(() => dispatcher(new PackageTest(), PackageNumber.Invalid, 1, 2, new byte[5], 0, 5));
        }
    }
}
