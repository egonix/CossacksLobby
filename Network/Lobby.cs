using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
    [Package(PackageNumber.EnterLobby)]
    class EnterLobby
    {
        public byte Unknown1 { get; set; }
        public string Nickname { get; set; }
        public short Rank;
        public int Unknown2 { get; set; }
        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }
        public int Unknown5 { get; set; }
        public int Unknown6 { get; set; }
        [ZeroTerminated]
        public List<Player> Players { get; set; }
        [ZeroTerminated]
        public List<Game> Games { get; set; }
    }

    [Package(PackageNumber.NewPlayer)]
    public class NewPlayer
    {
        public string Nickname { get; set; }
        public short Rank { get; set; }
        public bool LoginSuccess { get; set; }
    }

    class Player
    {
        public int ID { get; set; }
        public byte Icon { get; set; }
        public string Name { get; set; }
        public short Rank { get; set; }
    }

    class Game
    {
        public int HostID { get; set; }
        public int MaxPlayers { get; set; }
        public string Name { get; set; }
        public string Options { get; set; }
        public int Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public List<int> JoinedPlayerIDs { get; set; }
    }

    [Package(PackageNumber.CreateRoomRequest)]
    class CreateRoomRequest
    {
        public byte Size1 { get; set; } // 07
        public int Size2 { get; set; } // 00
        public string NamePassword { get; set; }
        public int unknown6 { get; set; }
        public int unknown7 { get; set; }
    }

    [Package(PackageNumber.CreateRoomResponse)]
    class CreateRoomResponse
    {
        public byte Size1 { get; set; } // 07
        public int Size2 { get; set; } // 07
        public string NamePassword { get; set; }
        public int unknown6 { get; set; }
        public int unknown7 { get; set; }
    }

    [Package(PackageNumber.RoomInfoRequest1)]
    class RoomInfoRequest1
    {
        [Length(typeof(Int16))]
        public string NamePassword { get; set; }
        [Length(typeof(Int16))]
        public string Options { get; set; }
        public int Size { get; set; }
        public int Unknown { get; set; }
        [Length(typeof(Int16))]
        public string PCName { get; set; }

        public int Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public byte Unknown3 { get; set; }
    }

    [Package(PackageNumber.RoomInfoRequest2)]
    class RoomInfoRequest2
    {
        public string NamePassword { get; set; }
        public string Options { get; set; }

        public int Unknown1 { get; set; }
        public short Unknown2 { get; set; }
    }

    [Package(PackageNumber.RoomInfoResponse)]
    class RoomInfoResponse
    {
        public int Size1 { get; set; }
        public string NamePassword { get; set; }
        public string Options { get; set; }
        public int Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public List<int> JoinedPlayerIDs { get; set; }
        public byte Size2 { get; set; }
    }

    [Package(PackageNumber.RoomLeaveRequest)]
    class RoomLeaveRequest
    { }

    [Package(PackageNumber.RoomLeaveResponse)]
    class RoomLeaveResponse
    {
        public byte unknown1 { get; set; } // 01
        public int unknown2 { get; set; } // 01
        public int PlayerID { get; set; }
        public byte unknown3 { get; set; } // 01
    }
}
