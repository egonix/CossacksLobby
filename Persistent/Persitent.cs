using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CossacksLobby
{
    class Persistent
    {
        public string Storage { get; }

        private ConcurrentDictionary<string, Account> Accounts;

        public Persistent(string storage)
        {
            Storage = storage;
            Account[] accounts = Account.Load(Storage);
            Accounts = new ConcurrentDictionary<string, Account>(accounts.Select(i => new KeyValuePair<string, Account>(i.EMail, i)));
        }
        public void Save()
        {
            Log.Info("saving persistent storage");
            Account.Save(Storage, Accounts.Values.ToArray());
        }

        public Account CreateAccount(string email)
        {
            Account result = new Account()
            {
                EMail = email
            };
            if (Accounts.TryAdd(result.EMail, result))
                return result;
            else
                return null;
        }

        public bool TryGetAccount(string email, out Account result)
        {
            return Accounts.TryGetValue(email, out result);
        }

        public bool IsEMailTaken(string email)
        {
            return Accounts.Values.Any(a => a.EMail == email);
        }
    }
}
