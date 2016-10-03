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
}
