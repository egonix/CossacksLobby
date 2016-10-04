using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    [Serializable]
    class Account
    {
        public string EMail { get; set; }
        public byte Icon { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public string GameKey { get; set; }
        public Int16 Rank { get; set; }

        const string FileName = "Accounts.xml";
        static DataContractSerializer Serializer = new DataContractSerializer(typeof(Account[]));

        public static void Save(string path, Account[] accounts)
        {
            using (var file = File.Create(Path.Combine(path, FileName)))
                Serializer.WriteObject(file, accounts);
        }

        public static Account[] Load(string path)
        {
            try
            {
                using (var file = File.OpenRead(Path.Combine(path, FileName)))
                    return (Account[])Serializer.ReadObject(file);
            }
            catch (FileNotFoundException)
            {
                return new Account[0];
            }
        }
    }
}
