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
        public byte unknown3 { get; set; }
        public List<Session> Joined { get; }
        public int Unknown6 { get; set; }
        public string RoomVersion { get; set; }
        public string PCName { get; set; }

        public Room(Session host)
        {
            Host = host;
            Joined = new List<Session>();
        }
    }

    [Package(PackageNumber.RoomCreateRequest)]
    class CreateRoomRequest
    {
        public byte Size1 { get; set; } // 07
        public int Size2 { get; set; } // 00
        public string NamePassword { get; set; }
        public int unknown6 { get; set; }
        public int unknown7 { get; set; }
    }

    [Package(PackageNumber.RoomCreateResponse)]
    class CreateRoomResponse
    {
        public byte Size1 { get; set; } // 07
        public int Size2 { get; set; } // 07
        public string NamePassword { get; set; }
        public int unknown6 { get; set; }
        public int unknown7 { get; set; }
    }

    [Package(PackageNumber.RoomJoinRequest)]
    class RoomJoinRequest
    {
        public int HostID { get; set; }
    }

    [Package(PackageNumber.RoomJoinResponse)]
    class RoomJoinResponse
    {
        public int HostID { get; set; }
        public byte StateFlag { get; set; }
    }

    [Package(PackageNumber.RoomJoinRequest1)]
    class RoomJoinRequest1
    {
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        [Length(typeof(Int16))]
        public string Nickname { get; set; }
        public byte Unknown3 { get; set; }
        public int ID { get; set; }
        [Unknown("00000000000000")]
        public byte[] _Unknown { get; set; }
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

    [Package(PackageNumber.RoomPlayerOption)]
    class RoomPlayerOption
    {
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }
        public int Unknown5 { get; set; }
        public int Unknown6 { get; set; }
        public byte Unknown7 { get; set; }
        [Length(typeof(Int32))]
        public string Options { get; set; }
        public int UnknownEnd { get; set; }
    }

    [Package(PackageNumber.RoomVersion)]
    class RoomVersion
    {
        public string Version { get; set; }
    }

    [Package(PackageNumber.RoomLeaveResponse)]
    class RoomLeaveResponse
    {
        public byte unknown1 { get; set; } // 01
        public int unknown2 { get; set; } // 01
        public int PlayerID { get; set; }
        public byte unknown3 { get; set; } // 01
    }

    [Package(PackageNumber.RoomStartGameRequest)]
    class RoomStartGameRequest
    {
        public List<int> HumanPlayerIDs { get; set; }
        public byte unknown { get; set; } // 00
    }

    [Package(PackageNumber.RoomStartGameResponse)]
    class RoomStartGameResponse
    {
        public List<int> HumanPlayerIDs { get; set; }
        public byte unknown { get; set; } // 0F
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
        private void RoomJoin(int clientID, int unknown, RoomJoinRequest request)
        {
            byte state = 0;
            Session host = Server.Temporary.Lobby.GetPlayer(p => p.ID == request.HostID);
            Room room = Server.Temporary.Lobby.GetRoom(host);
            if (room != null && room.Joined.Count < room.MaxPlayers)
                state = 0x03; // TODO check Room-JoinAble, 03 = OK

            Write(PackageNumber.RoomJoinResponse, clientID, unknown, new RoomJoinResponse()
            {
                HostID = request.HostID,
                StateFlag = state, 
            });
        }

        [PackageHandler]
        private void RoomJoin1(int clientID, int unknown, RoomJoinRequest1 request)
        {
            Session host = Server.Temporary.Lobby.GetPlayer(p => p.ID == clientID);
            Room room = Server.Temporary.Lobby.GetRoom(host);
            Write(PackageNumber.RoomInfoResponse, unknown, clientID, new RoomInfoResponse()
            {
                Size1 = room.MaxPlayers,
                NamePassword = room.Name + '\t' + room.Password,
                Options = room.Options,
                JoinedPlayerIDs = room.Joined.Select(s => s.ID).ToList(),
                Unknown1 = room.unknown1,
                Unknown2 = room.unknown2,
                Size2 = (byte)room.MaxPlayers,
            });

            Write(PackageNumber.RoomAdditionalInfo, 0, clientID, new
            {
                host.Account.ProtocolVersion,
                host.Account.GameVersion,
                end = 0,
            });

            Write(PackageNumber.RoomFullInfo, unknown, clientID, new
            {
                hostID = unknown,
                info = new RoomInfoRequest1()
                {
                    NamePassword = room.Name + '\t' + room.Password,
                    Options = room.Options,
                    Size = room.MaxPlayers,
                    Unknown = room.UnknownID,
                    PCName = room.PCName,
                    Unknown1 = room.unknown1,
                    Unknown2 = room.unknown2,
                    Unknown3 = room.unknown3,
                },
                playersCount = room.Joined.Count,
                playerIDs = room.Joined.Select(p => p.ID).ToList(),
                Unknown1 = (short)0,
                HostNameLength = (short)room.Host.Account.Nickname.Length,
                HostName = room.Host.Account.Nickname,
                Unknown2 = (int)0,
                Unknown3 = (short)1,
                Unknown4 = (byte)0,
            });
        }

        [PackageHandler]
        private void RoomInfo1(int clientID, int unknown, RoomInfoRequest1 request)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            room.Options = request.Options;
            room.MaxPlayers = request.Size;
            room.UnknownID = request.Unknown;
            room.PCName = request.PCName;
        }

        [PackageHandler]
        private void RoomInfo2(int clientID, int unknown, RoomInfoRequest2 request)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            room.Options = request.Options;

            RoomInfoResponse response = new RoomInfoResponse()
            {
                Size1 = room.MaxPlayers,
                NamePassword = request.NamePassword,
                Options = room.Options,
                JoinedPlayerIDs = room.Joined.Select(s => s.ID).ToList(),
                Unknown1 = room.unknown1,
                Unknown2 = room.unknown2,
                Size2 = (byte)room.MaxPlayers,
            };
            Write(clientID, unknown, response);

            //Broadcast
            if (unknown == 0)
            {
                List<Session> players = Server.Temporary.Lobby.GetPlayers(p => p.ID != clientID);
                Server.Write(players, clientID, unknown, response);
            }
        }

        [PackageHandler]
        private void RoomPlayerOption(int clientID, int unknown, RoomPlayerOption request)
        {
            // u1 = 66 Player u1 = 64 Host TODO
            Session player = Server.Temporary.Lobby.GetPlayer(p => p.ID == clientID);
            Room room = Server.Temporary.Lobby.GetRoom(this);
        }

        [PackageHandler]
        private void RoomVersion(int clientID, int unknown, RoomVersion request)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            if (room != null)
                room.RoomVersion = request.Version;

            // TODO JoinGame Version Check with clientid-version from 0x0065 request
        }

        [PackageHandler]
        private void RoomStartGame(int clientID, int unknown, RoomStartGameRequest request)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);

            // TODO Host<->Clients communication (clientID, unknown)
            Write(PackageNumber.RoomStartGameResponse, clientID, unknown, new RoomStartGameResponse()
            {
                HumanPlayerIDs = room.Joined.Select(p => p.ID).ToList(),
                unknown = 0x0F,
            });
        }

        [PackageHandler(PackageNumber.RoomLeaveRequest)]
        private void LeaveRoom(int clientID, int unknown)
        {
            Room room = Server.Temporary.Lobby.GetRoom(this);
            if(clientID == room.Host.ID)
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
