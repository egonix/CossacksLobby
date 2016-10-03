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
    abstract class SessionBase : IDisposable
    {
        private Server Server;
        private TcpClient Client;
        private NetworkStream Stream;
        private CancellationTokenSource Cancellation;
        private Task Task;

        public SessionBase(Server server, TcpClient client)
        {
            Client = client;
            Stream = client.GetStream();
            Server = server;
            Cancellation = new CancellationTokenSource();
            Task = Loop();
        }

        private async Task Loop()
        {
            byte[] buffer = new byte[Package.MaxSize];
            const int cleanThreshold = 2048;
            try
            {
                CancellationToken cancellationToken = Cancellation.Token;
                Packetizer packetizer = new Packetizer();

                int offset = 0;
                int count = 0;
                while (true)
                {
                    int reserved = offset + count;
                    int n = await Stream.ReadAsync(buffer, reserved, buffer.Length - reserved, cancellationToken);
                    if (n == 0) throw new EndOfStreamException();
                    count += n;
                    while (packetizer.GetNextPackage(buffer, ref offset, ref count))
                    {
                        await HandlePackage(packetizer.Type,
                                            packetizer.Unknown1,
                                            packetizer.Unknown2,
                                            buffer,
                                            offset - packetizer.PayloadSize,
                                            packetizer.PayloadSize);
                    }
                    if (count == 0)
                    {
                        offset = 0;
                    }
                    else if (packetizer.PayloadSize + Package.HeaderSize >= buffer.Length)
                    {
                        throw new InvalidOperationException("PayloadSize is too large for the buffer");
                    }
                    else if (buffer.Length - offset - count < cleanThreshold)
                    {
                        Buffer.BlockCopy(buffer, offset, buffer, 0, count);
                    }
                }
            }
            finally
            {
                Client.Client.Shutdown(SocketShutdown.Both);
                Client.Close();
            }
        }

        protected abstract Task HandlePackage(PackageNumber type, int unknown1, int unknown2, byte[] buffer, int offset, int count);

        public void Write<T>(int unknown1, int unknown2, T t)
        {
            Package.Write(Stream, unknown1, unknown2, t);
        }

        private async Task Stop()
        {
            Cancellation.Cancel();
            await Task;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SessionBase()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposed)
        {
            if (disposed)
            {
                Stop().Wait();
            }
        }
    }
}
