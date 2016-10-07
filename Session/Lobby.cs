using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    [Package(PackageNumber.EnterLobby)]
    class EnterLobby
    {
        public class Player
        {
            public int ID { get; set; }
            public byte Icon { get; set; }
            public string Name { get; set; }
            public byte Unknown { get; set; }
            public string DLC { get; set; }
        }

        public class Room
        {
            public int HostID { get; set; }
            public int MaxPlayers { get; set; }
            public string Name { get; set; }
            public string Options { get; set; }
            public int Unknown1 { get; set; }
            public short Unknown2 { get; set; }
            public List<int> JoinedPlayerIDs { get; set; }
        }

        public byte Unknown1 { get; set; }
        public string Nickname { get; set; }
        public short Unknown { get; set; }
        [Unknown("0000000000000000000000000000000000000000")]
        public byte[] _Unknown { get; set; }
        [ZeroTerminated]
        public List<Player> Players { get; set; }
        [ZeroTerminated]
        public List<Room> Rooms { get; set; }
    }

    [Package(PackageNumber.SelfRequest)]
    public class SelfRequest
    {
        public string Password { get; set; }
        public string Nickname { get; set; }
        public byte Unknown { get; set; }
        public string DLC { get; set; }
    }

    [Package(PackageNumber.NewPlayer)]
    public class NewPlayer
    {
        public string Nickname { get; set; }
        public byte Unknown { get; set; }
        public string DLC { get; set; }
        public bool LoginSuccess { get; set; }
    }
}
