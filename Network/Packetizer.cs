using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
    class Packetizer
    {
        enum PacketizerState
        {
            Header,
            Payload,
        }

        PacketizerState State = PacketizerState.Header;

        public PackageNumber Type { get; private set; }
        public int PayloadSize { get; private set; }
        public int Unknown1 { get; private set; }
        public int Unknown2 { get; private set; }
        
        public bool GetNextPackage(byte[] buffer, ref int offset, ref int count)
        {
            switch (State)
            {
                case PacketizerState.Header:
                    if (count < Package.HeaderSize) return false;
                    PayloadSize = BitConverter.ToInt32(buffer, offset);
                    Type = (PackageNumber)BitConverter.ToInt16(buffer, offset + 4);
                    Unknown1 = BitConverter.ToInt32(buffer, offset + 6);
                    Unknown2 = BitConverter.ToInt32(buffer, offset + 10);
                    State = PacketizerState.Payload;
                    offset += Package.HeaderSize;
                    count -= Package.HeaderSize;
                    goto case PacketizerState.Payload;
                case PacketizerState.Payload:
                    if (count < PayloadSize) return false;
                    offset += PayloadSize;
                    count -= PayloadSize;
                    State = PacketizerState.Header;
                    return true;
                default: throw new NotImplementedException();
            }
        }

    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 14)]
    struct Header
    {
        public static int Size = Marshal.SizeOf(typeof(Header));
        public readonly int PayloadLength;
        public readonly PackageNumber Type;
        public readonly int Unknown1;
        public readonly int Unknown2;
    }
}
