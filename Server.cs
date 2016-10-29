using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace CossacksLobby
{
    class Server
    {
        public IPEndPoint LocalEndpoint { get; }
        public Persistent Persistent { get; }
        public Temporary Temporary { get; }
        public CancellationToken CancellationToken { get; }

        private List<Session> Sessions { get; }
        private ReaderWriterLockSlim SessionsLock { get;}

        public Server(IPEndPoint localEndpoint, CancellationToken cancellationToken)
        {
            LocalEndpoint = localEndpoint;
            CancellationToken = cancellationToken;
            Persistent = new Persistent(Path.GetDirectoryName(typeof(Server).Assembly.FullName));
            Temporary = new Temporary(this);
            Sessions = new List<Session>();
            SessionsLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            Task.Run(UpdateLoop, CancellationToken);
            Task.Run(ListenerLoop, CancellationToken);
            CancellationToken.Register(KillAllSessions);
        }

        public void Remove(Session session)
        {
            Log.Info($"connection closed from {session.Account.Nickname} ({session.Account.EMail})");

            SessionsLock.EnterWriteLock();
            session.Stop().Wait();
            Sessions.Remove(session);
            SessionsLock.ExitWriteLock();
        }

        public void Write<T>(IEnumerable<Session> sessions, int unknown1, int unknown2, T t)
        {
            Package.Write(sessions.Select(s => s.Stream), unknown1, unknown2, t);
        }

        public void Write<T>(IEnumerable<Session> sessions, PackageNumber number, int unknown1, int unknown2, T t)
        {
            Package.Write(sessions.Select(s => s.Stream), number, unknown1, unknown2, t);
        }

        private async Task UpdateLoop()
        {
            DateTime nextRun = DateTime.Now;
            while (CancellationToken.IsCancellationRequested == false)
            {
                TimeSpan waitTime = nextRun - DateTime.Now;
                if (waitTime > TimeSpan.Zero)
                    await Task.Delay(waitTime, CancellationToken);
                Persistent.Save();
                nextRun += TimeSpan.FromMinutes(5);
            }
        }

        private async Task ListenerLoop()
        {
            TcpListener listener = new TcpListener(LocalEndpoint);
            listener.Start();
            CancellationToken.Register(listener.Stop);
            while (CancellationToken.IsCancellationRequested == false)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync();
                }
                catch (SocketException)
                {
                    System.Diagnostics.Debug.Assert(CancellationToken.IsCancellationRequested);
                    return;
                }
                Log.Info($"new connection from {client.Client.RemoteEndPoint}");
                SessionsLock.EnterWriteLock();
                Sessions.Add(new Session(this, client));
                SessionsLock.ExitWriteLock();
            }
        }

        private void KillAllSessions()
        {
            SessionsLock.EnterWriteLock();
            Task.WaitAll(Sessions.Select(s => s.Stop()).ToArray());
            SessionsLock.ExitWriteLock();
        }
    }
}
