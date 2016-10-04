using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    partial class Session
    {
        [PackageHandler(PackageNumber.ChatMessageRequest)]
        private void ChatMessage(int clientID, int unknown, string message)
        {
            Write(PackageNumber.ChatMessageResponse, clientID, unknown, message);
        }
    }
}
