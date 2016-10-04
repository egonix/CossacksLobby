using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    [Package(PackageNumber.LoginRequest)]
    class LoginRequest
    {
        public string Version1 { get; set; }
        public string Version2 { get; set; }
        public string EMail { get; set; }
        public string Password { get; set; }
        public string GameKey { get; set; }
    }

    [Package(PackageNumber.CreateAccountRequest)]
    class CreateAccountRequest
    {
        public string ProtocolVersion { get; set; } // Assumed
        public string GameVersion { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string GameKey { get; set; }
        public string Nickname { get; set; }
        public short Unknown { get; set; }
    }

    partial class Session
    {
        [PackageHandler]
        private void NewAccount(int unknown1, int unknown2, CreateAccountRequest request)
        {
            Account account = Persistent.CreateAccount(request.Email);
            if (account == null)
            {
                // Account already exists
                throw new NotImplementedException();
            }
            account.Nickname = request.Nickname;
            account.Password = request.Password;
            account.GameKey = request.GameKey;
            Account = account;
            Server.Temporary.Lobby.Enter(this);
        }

        [PackageHandler]
        private void Login(int unknown1, int unknown2, LoginRequest request)
        {
            Account account;
            if (Persistent.TryGetAccount(request.EMail, out account) && account.Password == request.Password && account.GameKey == request.GameKey)
            {
                Account = account;
                Server.Temporary.Lobby.Enter(this);
            }
            else
            {
                Console.WriteLine("User with {0} not found", request.EMail, request.GameKey);
            }
        }
    }
}
