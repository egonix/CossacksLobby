using CossacksLobby.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CossacksLobby
{
    public enum SessionState
    {
        InLobby,
        HostingRoom,
    }

    partial class Session : SessionBase
    {
        static readonly Dispatcher<Session> Dispatcher = Package.BuildDispatcher<Session>();

        public Account Account { get; private set; }
        public SessionState State { get; set; }

        public Session(Server server, TcpClient client) : base(server, client)
        {
        }

        protected override Task HandlePackage(PackageNumber number, int unknown1, int unknown2, byte[] buffer, int offset, int count)
        {
#if DEBUG
            Log.Debug($"{number} {count}", buffer, count);
#endif
            try
            {
                return Dispatcher(this, number, unknown1, unknown2, buffer, offset, count);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        [UnknownPackageHandler]
        private Task UnknownPackage(PackageNumber number, int unknown1, int unknown2, byte[] buffer, int offset, int count)
        {
#if DEBUG
            Log.Debug($"Unknown Package: {number}", buffer, count);
#else
            Console.WriteLine($"Unknown Package: {number}");
#endif
            return Task.FromResult(0);
        }

        protected override void OnExit()
        {
            base.OnExit();
            switch (State)
            {
                case SessionState.InLobby:
                    Server.Temporary.Lobby.Exit(this);
                    break;
            }
            Server.Remove(this);
        }
    }
}
