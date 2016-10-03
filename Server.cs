using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    class Server: IDisposable
    {
        private TcpListener Listener;
        public LinkedList<Session> Sessions { get; } = new LinkedList<Session>();

        public Server(IPEndPoint localEndpoint)
        {
            Listener = new TcpListener(localEndpoint);
            ListenerLoop();
        }

        private async void ListenerLoop()
        {
            Listener.Start();

            while (true)
            {
                try
                {
                    TcpClient client = await Listener.AcceptTcpClientAsync();
                    Console.WriteLine($"new connection from {client.Client.RemoteEndPoint}");
                    Sessions.AddLast(new Session(this, client));
                }
                catch (InvalidOperationException) { return; }
            }
        }

   
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
       
        ~Server()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposed)
        {
            if (disposed)
            {
                Listener.Stop();
                lock (Sessions)
                {
                    while (Sessions.First != null)
                        Sessions.First.Value.Dispose();
                }
            }
        }
    }
}
