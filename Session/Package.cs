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

namespace CossacksLobby
{
    enum PackageNumber : ushort
    {
        Invalid = 0,
        ChatMessageRequest = 0x0196,
        ChatMessageResponse = 0x0197,
        CreateAccountRequest = 0x0198,
        LoginRequest = 0x019A,
        ErrorResponse = 0x019B,
        EnterLobby = 0x019B,
        RoomCreateRequest = 0x019C,
        RoomCreateResponse = 0x019D,
        RoomJoinRequest = 0x019E,
        RoomJoinResponse = 0x019F,
        RoomJoinRequest1 = 0x0065,
        RoomInfoRequest1 = 0x00C8,
        RoomInfoRequest2 = 0x01AA,
        RoomInfoResponse = 0x01A5,
        RoomAdditionalInfo = 0x01AE,
        RoomFullInfo = 0x00C9,
        RoomLeaveRequest = 0x01A0,
        RoomLeaveResponse = 0x01A1,
        RoomVersion = 0x01AD,
        RoomPlayerOption = 0x01BB,
        SelfRequest = 0x01B3,
        SelfResponse = 0x01B4,
        RoomStartGameRequest = 0x01A2, // if not answered connection get closed via client but game runs local
        RoomStartGameResponse = 0x01A3,
        RoomGame1 = 0x456,
        RoomGame2 = 0x457,
        //StartGame: 0x01A2, 0x456, 0x457, 0x460, 0x461
        //0x019F 
        //0x01A1 
        //0x01A3 
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
            if (Number == PackageNumber.Invalid)
                throw new ArgumentException("Invalid is not a valid PackageNumber", nameof(number));
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class PackageHandlerAttribute : Attribute
    {
        public PackageNumber Number { get; }

        public PackageHandlerAttribute()
            : this(PackageNumber.Invalid)
        {
        }

        public PackageHandlerAttribute(PackageNumber number)
        {
            Number = number;
        }
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

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class UnknownAttribute : Attribute
    {
        public byte[] Pattern { get; }
        public bool Validate { get; set; }
        public bool Copy { get; set; }

        public UnknownAttribute(string pattern)
        {
            pattern = pattern.Replace(" ", string.Empty);
            if ((pattern.Length % 2) != 0) throw new ArgumentException();
            Pattern = Enumerable.Range(0, pattern.Length / 2)
                .Select(i => pattern.Substring(i * 2, 2))
                .Select(b => Convert.ToByte(b, 16))
                .ToArray();
            Validate = true;
            Copy = false;
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
            Write(stream, PackageNumber.Invalid, unknown1, unknown2, t);
        }

        public static void Write<T>(Stream stream, PackageNumber number, int unknown1, int unknown2, T t)
        {
            byte[] buffer;
            int offset;
            Write(number, unknown1, unknown2, t, out buffer, out offset);
            stream.Write(buffer, 0, offset);
        }
        public static void Write<T>(IEnumerable<Stream> streams, int unknown1, int unknown2, T t)
        {
            Write(streams, PackageNumber.Invalid, unknown1, unknown2, t);
        }

        public static void Write<T>(IEnumerable<Stream> streams, PackageNumber number, int unknown1, int unknown2, T t)
        {
            byte[] buffer;
            int offset;
            Write(number, unknown1, unknown2, t, out buffer, out offset);
            foreach (Stream stream in streams)
                stream.Write(buffer, 0, offset);
        }

        private static void Write<T>(PackageNumber number, int unknown1, int unknown2, T t, out byte[] buffer, out int offset)
        {
            Tuple<PackageNumber, Delegate> writer = Writers.GetOrAdd(typeof(T), BuildWriter);
            if (number == PackageNumber.Invalid)
            {
                number = writer.Item1;
                if (number == PackageNumber.Invalid)
                    throw new ArgumentException("PackageNumber could not be inferred by type", nameof(number));
            }
            buffer = new byte[Package.MaxSize];
            offset = HeaderSize;
            ((PackageWriter<T>)writer.Item2)(t, buffer, ref offset);
            Buffer.BlockCopy(BitConverter.GetBytes(offset - HeaderSize), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((short)number), 0, buffer, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(unknown1), 0, buffer, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(unknown2), 0, buffer, 10, 4);
        }

        private static Tuple<PackageNumber, Delegate> BuildWriter(Type type)
        {
            PackageAttribute packageAttribute = type.GetCustomAttribute<PackageAttribute>();
            return Tuple.Create(packageAttribute?.Number ?? PackageNumber.Invalid, PackageWriterBuilder.Build(type));
        }

        public static Dispatcher<T> BuildDispatcher<T>()
        {
            Type type = typeof(T);
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var nodes = methods
                .Select(m => new { Method = m, PackageHandlerAttribute = m.GetCustomAttribute<PackageHandlerAttribute>()})
                .Where(m => m.PackageHandlerAttribute != null)
                .Select(m =>
                {
                    ParameterInfo[] parameters = m.Method.GetParameters();
                    if (parameters.Length != 2 && parameters.Length != 3)
                        throw new InvalidOperationException(m.Method.Name + " must have 3 parameters");
                    if (parameters[0].ParameterType != typeof(int))
                        throw new InvalidOperationException(m.Method.Name + " first paramter must be a int");
                    if (parameters[1].ParameterType != typeof(int))
                        throw new InvalidOperationException(m.Method.Name + " second paramter must be a int");
                    PackageNumber number = m.PackageHandlerAttribute.Number;
                    Type parameterType;
                    if (parameters.Length > 2)
                    {
                        parameterType = parameters[2].ParameterType;
                        if (number == PackageNumber.Invalid)
                        {
                            PackageAttribute packageAttribute = parameterType.GetCustomAttribute<PackageAttribute>();
                            if (packageAttribute == null)
                                throw new InvalidOperationException(m.Method.Name + " third parameter is not a Package");
                            number = packageAttribute.Number;
                        }
                    }
                    else
                    {
                        parameterType = null;
                    }
                    return new
                    {
                        Method = m.Method,
                        Type = parameterType,
                        Number = number,
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
                    Expression offsetCheck =
                        Expression.IfThen(
                            Expression.NotEqual(offset, finalOffset),
                            Expression.Throw(Expression.New(typeof(InvalidDataException).GetConstructor(new Type[] { typeof(string) }), Expression.Constant("offsets for '" + c.Method.Name + "'do not match!"))));
                    if (c.Type != null)
                    {
                        Delegate @delegate = PackageReaderBuilder.Build(c.Type);
                        Type delegateType = @delegate.GetType();
                        MethodInfo invoke = delegateType.GetMethod("Invoke");
                        ParameterExpression package = Expression.Variable(c.Type);
                        Expression block = Expression.Block(new ParameterExpression[] { package },
                            Expression.Assign(package, Expression.Call(Expression.Constant(@delegate, delegateType), invoke, buffer, offset)),
                            offsetCheck,
                            Expression.Break(exit, wrapHandlerCall(Expression.Call(t, c.Method, unknown1, unknown2, package), c.Method.ReturnType)));
                        return Expression.SwitchCase(block, Expression.Constant(c.Number));
                    }
                    else
                    {
                        Expression block = Expression.Block(
                            offsetCheck,
                            Expression.Break(exit, wrapHandlerCall(Expression.Call(t, c.Method, unknown1, unknown2), c.Method.ReturnType)));
                        return Expression.SwitchCase(block, Expression.Constant(c.Number));
                    }
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
        [Package((PackageNumber)1)]
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
            Assert.AreEqual((PackageNumber)2, number);
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
            await dispatcher(new PackageTest(), (PackageNumber)1, 1, 2, data, 0, data.Length);
            await dispatcher(new PackageTest(), (PackageNumber)2, 1, 2, data, 0, data.Length);
            Assert.Catch<InvalidDataException>(() => dispatcher(new PackageTest(), (PackageNumber)1, 1, 2, new byte[5], 0, 5));
        }
    }
}
