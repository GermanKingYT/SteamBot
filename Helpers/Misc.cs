using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamBot.Helpers
{
    class Misc
    {
        public static string[] seperate(int number, char seperator, string thestring)
        {
            string[] returned = new string[5];

            int i = 0;

            int error = 0;

            int lenght = thestring.Length;

            foreach (char c in thestring)
            {
                if (i != number)
                {
                    if (error > lenght || number > 5)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                    else if (c == seperator)
                    {

                        returned[i] = thestring.Remove(thestring.IndexOf(c));
                        thestring = thestring.Remove(0, thestring.IndexOf(c) + 1);
                        i++;
                    }
                    error++;
                    if (error == lenght && i != number)
                    {
                        returned[0] = "-1";
                        return returned;

                    }
                }
                else
                {
                    returned[i] = thestring;
                }


            }
            return returned;
        }
    }
}
