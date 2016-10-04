using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CossacksLobby
{
    class Temporary
    {
        public Server Server { get; }
        public Lobby Lobby { get; }

        public Temporary(Server server)
        {
            Lobby = new Lobby(server);
        }
    }
}
