using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CossacksLobby
{
    partial class Session
    {
        [PackageHandler(PackageNumber.EmailCheckRequest)]
        private void EmailCheck(int to, int from, string email)
        {
            Write(PackageNumber.EmailCheckResponse, to, from, new
            {
                Email = email,
                IsTaken = Persistent.IsEMailTaken(email),
            });
        }
    }
}
