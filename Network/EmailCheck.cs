using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
    [Package(PackageNumber.EmailCheckRequest)]
    class EmailCheckRequest
    {
        public string Email { get; set; }
    }

    [Package(PackageNumber.EmailCheckResponse)]
    public class EmailCheckResponse
    {
        public string Email { get; set; }
        public bool IsInUse { get; set; }
    }
}
