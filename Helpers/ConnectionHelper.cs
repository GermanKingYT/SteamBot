using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SteamBot.Helpers
{
    class ConnectionHelper
    {
        public static void CheckConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return;
                    }
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] - You dont have internet connection! ");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
}
