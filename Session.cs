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
    class Session : SessionBase
    {
        static readonly Dispatcher<Session> Dispatcher = Package.BuildDispatcher<Session>();

        public DBAccount Account { get; private set; }
        public Player Player { get; private set; }

        public Session(Server server, TcpClient client) : base(server, client)
        {
        }

        protected override Task HandlePackage(PackageNumber number, int unknown1, int unknown2, byte[] buffer, int offset, int count)
        {
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
            Console.WriteLine($"Unknown Package: {number}");
            return Task.FromResult(0);
        }

        [PackageHandler]
        private void EmailCheck(int unknown1, int unknown2, EmailCheckRequest request)
        {
            Write(unknown1, unknown2, new EmailCheckResponse()
            {
                Email = request.Email,
                IsInUse = CossacksHandler.Current.Account.Any(a => a.EMail == request.Email),
            });
        }

        [PackageHandler]
        private void NewAccount(int unknown1, int unknown2, CreateAccountRequest request)
        {
            Account = new DBAccount((int)DateTime.Now.Ticks, request.Nickname, request.Password, request.Email, request.GameKey);
            Player = new Player(Account);
            CossacksHandler.Current.Account.Add(Account);
            EnterLobby();
            CossacksHandler.Current.Save();
        }

        [PackageHandler]
        private void Login(int unknown1, int unknown2, LoginRequest request)
        {
            Account = CossacksHandler.Current.Account.FirstOrDefault(a => a.EMail == request.EMail && a.Password == request.Password && a.CDKey == request.CDKey);
            if (Account != null)
            {
                Player = new Player(Account);
                EnterLobby();
            }
            else
            {
                Console.WriteLine("User with {0} not found", request.EMail, request.CDKey);
            }
        }

        [PackageHandler]
        private void ChatMessage(int clientID, int unknown, ChatMessageRequest request)
        {
            Write(clientID, unknown, new ChatMessageResponse()
            {
                Message = request.Message,
            });
        }

        private void EnterLobby()
        {
            CossacksHandler.Lobby.AddPlayer(Player);
            Write(Player.ID, Player.ID, new EnterLobby()
            {
                Unknown1 = 0, // 0 = Bugs (Players)
                Nickname = Account.Name,
                Rank = Account.Rank,
                Unknown2 = 0,
                Unknown3 = 0,
                Unknown4 = 0,
                Unknown5 = 0,
                Unknown6 = 0,
                Players = CossacksHandler.Lobby.Players.Select(p => new Network.Player()
                {
                    ID = p.ID,
                    Icon = p.Icon,
                    Name = p.Name,
                    Rank = p.Rank,
                }).ToList(),
                Games = CossacksHandler.Lobby.Games.Select(g => new Network.Game()
                {
                    HostID = g.Host.ID,
                    MaxPlayers = g.MaxPlayers,
                    Name = g.Name,
                    Options = g.Options,
                    Unknown1 = g.unknown1,
                    Unknown2 = g.unknown2,
                    JoinedPlayerIDs = g.JoinedPlayers.Select(p => p.ID).ToList(),
                }).ToList(),
            });
            Write(Player.ID, 0, new NewPlayer()
            {
                Nickname = Player.Name,
                Rank = Player.Rank,
                LoginSuccess = true,
            });
        }
    }
}
