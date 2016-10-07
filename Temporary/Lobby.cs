using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CossacksLobby
{
    class Lobby
    {
        public Server Server { get; }

        private List<Session> Players;
        private List<Room> Rooms;
        private ReaderWriterLockSlim Lock;

        public Lobby(Server server)
        {
            Server = server;
            Players = new List<Session>();
            Rooms = new List<Room>();
            Lock = new ReaderWriterLockSlim();
        }

        public void Enter(Session session)
        {
            Lock.EnterWriteLock();
            session.Write(session.ID, session.ID, new EnterLobby()
            {
                Unknown1 = 0, // 0 = Bugs (Players)
                Nickname = session.Account.Nickname,
                Rank = session.Account.Rank,
                Players = Players.Select(s => new EnterLobby.Player()
                {
                    ID = s.ID,
                    Icon = s.Account.Icon,
                    Name = s.Account.Nickname,
                    Rank = s.Account.Rank,
                }).ToList(),
                Rooms = Rooms.Select(g => new EnterLobby.Room()
                {
                    HostID = g.Host.ID,
                    MaxPlayers = g.MaxPlayers,
                    Name = g.Name,
                    Options = g.Options,
                    Unknown1 = g.unknown1,
                    Unknown2 = g.unknown2,
                    JoinedPlayerIDs = g.Joined.Select(p => p.ID).ToList(),
                }).ToList()
            });
            Players.Add(session);
            Lock.ExitWriteLock();
            Lock.EnterReadLock();
            Server.Write(Players, session.ID, 0, new NewPlayer()
            {
                Nickname = session.Account.Nickname,
                Rank = session.Account.Rank,
                LoginSuccess = true,
            });
            Lock.ExitReadLock();
        }

        public void Exit(Session session)
        {
            throw new NotImplementedException();
        }

        public void AddRoom(Room room)
        {
            Lock.EnterWriteLock();
            Rooms.Add(room);
            Lock.ExitWriteLock();
        }

        public void DeleteRoom(Room room)
        {
            Lock.EnterWriteLock();
            Rooms.Remove(room);
            Lock.ExitWriteLock();

            // TODO Broadcast
        }

        public Room GetRoom(Session host)
        {
            Lock.EnterReadLock();
            Room result = Rooms.FirstOrDefault(r=>r.Host == host);
            Lock.ExitReadLock();
            return result;
        }

        public Session GetPlayer(Func<Session, bool> exp)
        {
            Lock.EnterReadLock();
            Session result = Players.FirstOrDefault(exp);
            Lock.ExitReadLock();
            return result;
        }

        public List<Session> GetPlayers(Func<Session, bool> exp)
        {
            Lock.EnterReadLock();
            List<Session> result = Players.Where(exp)
                .ToList();
            Lock.ExitReadLock();
            return result;
        }
    }
}
