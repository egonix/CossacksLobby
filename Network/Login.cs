using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
    [Package(PackageNumber.LoginRequest)]
    class LoginRequest
    {
        public string Version1 { get; set; }
        public string Version2 { get; set; }
        public string EMail { get; set; }
        public string Password { get; set; }
        public string CDKey { get; set; }
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
}
