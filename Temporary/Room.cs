using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    // TODO make thread safe
    class Room
    {
        public Session Host { get; }
        public int MaxPlayers { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Options { get; set; } // TODO, does the server has to understand them?
        public int UnknownID { get; set; }
        public int unknown1 { get; set; }
        public Int16 unknown2 { get; set; }
        public List<Session> Joined { get; }
        public int Unknown6 { get; set; }

        public Room(Session host)
        {
            Host = host;
            Joined = new List<Session>();
        }
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

    [Package(PackageNumber.RoomLeaveResponse)]
    class RoomLeaveResponse
    {
        public byte unknown1 { get; set; } // 01
        public int unknown2 { get; set; } // 01
        public int PlayerID { get; set; }
        public byte unknown3 { get; set; } // 01
    }

    partial class Session
    {
        [PackageHandler]
        private void NewRoom(int clientID, int unknown, CreateRoomRequest request)
        {
            System.Diagnostics.Debug.Assert(clientID == ID);
            string[] np = request.NamePassword.Split('\t');
            Room room = new Room(this)
            {
                Name = np[0],
                Password = np[1],
                MaxPlayers = request.Size1,
                Unknown6 = request.unknown6
            };
            room.Joined.Add(this);

            Server.Temporary.Lobby.AddRoom(room);

            State = SessionState.HostingRoom;
            Write(ID, 0, new CreateRoomResponse()
            {
                Size1 = (byte)room.MaxPlayers,
                Size2 = room.MaxPlayers, // 00 00 00 00 => 07 00 00 00
                NamePassword = room.Name + '\t' + room.Password,
                unknown6 = room.Unknown6,
            });
        }

        [PackageHandler]
        private void RoomInfo1(int clientID, int unknown, RoomInfoRequest1 request)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            room.Options = request.Options;
            room.MaxPlayers = request.Size;
            room.UnknownID = request.Unknown;
        }

        [PackageHandler]
        private void RoomInfo2(int clientID, int unknown, RoomInfoRequest2 request)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            room.Options = request.Options;
            Write(clientID, unknown, new RoomInfoResponse()
            {
                Size1 = room.MaxPlayers,
                NamePassword = request.NamePassword,
                Options = room.Options,
                JoinedPlayerIDs = room.Joined.Select(s => s.ID).ToList(),
                Unknown1 = room.unknown1,
                Unknown2 = room.unknown2,
                Size2 = (byte)room.MaxPlayers,
            });
        }

        [PackageHandler(PackageNumber.RoomLeaveRequest)]
        private void LeaveRoom(int clientID, int unknown)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            Server.Temporary.Lobby.DeleteRoom(room);
            Write(clientID, unknown, new RoomLeaveResponse()
            {
                unknown1 = 0x01,
                unknown2 = 1,
                PlayerID = clientID,
                unknown3 = 0x01,
            });
        }
    }
}
