using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Server server = new Server(new IPEndPoint(IPAddress.Any, 31523)))
            {
                Console.WriteLine("press [enter] to shut down");
                Console.ReadLine();
            }
        }
    }
}
