using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby.Network
{
    [Package(PackageNumber.ChatMessageRequest)]
    class ChatMessageRequest
    {
        public string Message { get; set; }
    }

    [Package(PackageNumber.ChatMessageResponse)]
    class ChatMessageResponse
    {
        public string Message { get; set; }
    }
}
