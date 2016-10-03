using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CossacksLobby
{
    [Serializable]
     class CossacksHandler
    {
        public const string DatabaseFile = "database.xml";
        private static readonly DataContractSerializer _Serializer;
        public HashSet<DBAccount> Account = new HashSet<DBAccount>();

        public static CossacksHandler Current { get; private set; }

        static CossacksHandler()
        {
            _Serializer = new DataContractSerializer(typeof(CossacksHandler));
            RefreshCurrent();
        }

        public static void AutoSave()
        {
            Timer _Timer = new Timer((s) => { Current.Save(); }, null, 0, 15000);
        }

        public static void SaveCurrent(string file)
        {
            using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                _Serializer.WriteObject(fs, Current);
        }

        public static void RefreshCurrent()
        {
            try
            {
                Current = ReadCurrent(DatabaseFile);
            }
            catch (FileNotFoundException)
            {
                Current = new CossacksHandler();
            }
        }

        public static CossacksHandler ReadCurrent(string file)
        {
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return (CossacksHandler)_Serializer.ReadObject(fs);
        }

        public void Save()
        {
            SaveCurrent(DatabaseFile);
        }

        public static class Lobby
        {
            public static List<Player> Players = new List<Player>();
            public static List<Game> Games = new List<Game>();

            public static void AddPlayer(Player player)
            {
                Console.WriteLine("New Player login was successful: {0}", player);
                Players.Add(player);
            }

            public static void AddGame(Game game)
            {
                Console.WriteLine("New Game created: {0}", game);
                Games.Add(game);
            }
        }
    }

    [Serializable]
    public class DBAccount
    {
        public int ID;
        public byte Icon;
        public string Name;
        public string Password;
        public string EMail;
        public string CDKey;
        public Int16 Rank;

        public DBAccount(int id, string name, string password, string email, string cdkey)
        {
            ID = id;
            Icon = 0;
            Name = name;
            Password = password;
            EMail = email;
            CDKey = cdkey;
            Rank = 0;
        }
    }

    class Player
    {
        public int ID;
        public byte Icon;
        public string Name;
        public Int16 Rank;

        public Player()
        { }

        public Player(DBAccount account)
        {
            ID = account.ID;
            Icon = account.Icon;
            Name = account.Name;
            Rank = account.Rank;
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}, {3}", ID, Icon, Name, Rank);
        }
    }

    class Game
    {
        public Player Host; // HostID
        public int MaxPlayers;

        public string Name;
        public string Password;
        public string Options; // Options // TODO
        public int UnknownID;
        public int unknown1;
        public Int16 unknown2;
        public int PlayersInServer { get { return JoinedPlayers.Count; } }
        public List<Player> JoinedPlayers = new List<Player>(); // int[] ID

        public Game()
        { }

        public override string ToString()
        {
            return string.Format("[{0}({1})] {2} {3}/{4}", Host.Name, Host.ID, Name, PlayersInServer, MaxPlayers);
        }
    }
}
