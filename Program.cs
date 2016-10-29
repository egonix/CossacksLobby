using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CossacksLobby
{
    class Program
    {
        static void Main(string[] args)
        {
            #if DEBUG
                Log.Level = LogLevel.Debug;
            #else
                Log.Level = LogLevel.Info;
            #endif

            CancellationTokenSource cancellationSource = new CancellationTokenSource();
            Server server = new Server(new IPEndPoint(IPAddress.Any, 31523), cancellationSource.Token);
            Console.WriteLine("press [enter] to shut down");
            Console.ReadLine();
            cancellationSource.Cancel();
        }
    }
}
