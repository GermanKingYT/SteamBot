using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Net;

using SteamKit2;
using SteamKit2.Internal;
using SteamKit2.GC;
using SteamKit2.GC.Internal;
using SteamKit2.Unified;
using SteamKit2.Unified.Internal;
using SteamKit.CSGO;
using SteamKit2.GC.CSGO;
using SteamKit2.GC.CSGO.Internal;

using SteamBot.Helpers;

using CsgoClient = SteamKit.CSGO.CsgoClient;

namespace Steam_Friend_Bot
{
    
    class Program
    {
        #region Statics & Publics
        public ulong IDSteam { get; set; }

        static CallbackManager manager;

        static SteamClient steamClient;
        static SteamUser steamUser;
        static SteamFriends steamFriends;
        static SteamGameCoordinator steamGameCoordinator;
        static SteamCloud steamCloud;
        static SteamGameServer steamGameServer;
        static SteamMasterServer steamMasterServer;
        static SteamScreenshots steamScreenshots;
        static SteamTrading steamTrading;
        static SteamUnifiedMessages steamUFM;
        static SteamUserStats steamUserStats;
        static SteamWorkshop steamWorkShop;

        static bool isRunning = false;
        static bool loggedBackOn = false;

        static string user;
        static string pass;
        static string authcode;
        static string twofactor;

        static int reconnectTry;

        public PasswordHelper pwHelper;

        /* Things I never use */
        static uint matchID;
        bool gotMatch;
        const int CSGOID = 730;
        public CDataGCCStrike15_v2_MatchInfo match { get; private set; }

        public Program(ulong steamid)
        {
            this.IDSteam = steamid;
        }
        #endregion

        #region StartUp
        static void Main(string[] args)
        {
            StartUp();
        }

        static void StartUp()
        {
            ConnectionHelper.CheckConnection();

            if (!File.Exists("chat.txt"))
            {
                File.Create("chat.txt").Close();
                File.WriteAllText("chat.txt", "Ping | Pong");
            }

            Console.Title = "Steam Bot - Init...";

            Console.Write("Username: ");
            user = Console.ReadLine();

            Console.Write("Password: ");
            pass = PasswordHelper.inputPass();
            Console.Clear();

            SteamLogin();
        }
        #endregion

        static void SteamLogin()
        {

            steamClient = new SteamClient();

            #region callbacks
            manager = new CallbackManager(steamClient);
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(UpdateMachineAuthCallback); // If steam guard auth succeeded
            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnChatMessage);
            manager.Subscribe<SteamFriends.FriendAddedCallback>(OnFriendAdded);
            manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);
            manager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
            #endregion

            #region Handlers
            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();
            steamCloud = steamClient.GetHandler<SteamCloud>();
            steamGameServer = steamClient.GetHandler<SteamGameServer>();
            steamMasterServer = steamClient.GetHandler<SteamMasterServer>();
            steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();
            steamScreenshots = steamClient.GetHandler<SteamScreenshots>();
            steamTrading = steamClient.GetHandler<SteamTrading>();
            steamUFM = steamClient.GetHandler<SteamUnifiedMessages>();
            steamUserStats = steamClient.GetHandler<SteamUserStats>();
            steamWorkShop = steamClient.GetHandler<SteamWorkshop>();
            #endregion

            isRunning = true;

            steamClient.Connect();

            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            Console.ReadKey();
        }

        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine($"(ERROR) Unable to connect to steam => {callback.Result}");
                Console.WriteLine($"(HELP US) Screenshot this error and send it to us!");
                isRunning = false;
                return;
            }
            Console.WriteLine("\n(INFORMATION) Connected to Steam!");
            Console.WriteLine($"(INFORMATION) Logging in with account {user}");

            #region Update Sentry (Steam Guard)
            byte[] sentryHash = null;

            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");

                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }
            #endregion

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
                AuthCode = authcode,
                TwoFactorCode = twofactor,
                SentryFileHash = sentryHash,
            });
        } // Wird gecalled wenn wir zu Steam connecten
        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.Title = $"SteamBot - Reconnecting (Try: {reconnectTry})";
            if (reconnectTry == 0)
                Console.WriteLine("(INFORMATION) Reconnecting to Steam...");
            if (reconnectTry > 1 && reconnectTry < 3)
            {
                Console.WriteLine("(WARNING) We couldn't establish a connection yet! Steam Server down?");
                Console.WriteLine("(WARNING) Still trying to connect...");
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));
            reconnectTry++;

            steamClient.Connect();
        } // Wird gecalled wenn wir von Steam disconnected werden

        static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
           
            if(loggedBackOn)
            {
                Console.Clear();
                Console.WriteLine("(SUCCESS) Logged back on.");
                loggedBackOn = false;
            }

            #region EResults
            if (callback.Result == EResult.AccountLogonDenied)
            {
                var email = callback.EmailDomain;

                Console.WriteLine($"\n(PROTECTED) The account {user} is Steam Guard protected");
                Console.Write($"(INFORMATION) Please enter your SteamGuard code, Steam sent you to your gmail at: xxx@{email}");

                authcode = Console.ReadLine();
                return;
            }
            if (callback.Result == EResult.AccountLoginDeniedNeedTwoFactor)
            {
                Console.WriteLine($"\n(PROTECTED) The account {user} uses Two-Factor Authentication");
                Console.Write("(INFORMATION) Please enter your 2FA code, you got on your phone: ");

                twofactor = Console.ReadLine();
                return;
            }
            if (callback.Result == EResult.TwoFactorCodeMismatch)
            {
                Console.WriteLine("\n(REJECTED) Wrong TwoFactor Code.");
                isRunning = false;
                return;
            }
            if (callback.Result == EResult.InvalidPassword)
            {
                Console.WriteLine("(REJECTED) Wrong password.");
                isRunning = false;
                return;
            }            
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine($"(ERROR) Unable to connect to Steam => {callback.Result}");
                Console.WriteLine($"(HELP US) Send us a screenshot of this error!");
                isRunning = false;
                return;
            }
            #endregion

            Console.Clear();
            Console.WriteLine("(SUCCESS) Logged in!");
        } // Wird gecalled wenn wir uns einloggen
        static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine($"(WARNING) Steam logged us of: {callback.Result}");
            Console.WriteLine("(HELP US) Please send us a screenshot of this error!");
            Console.WriteLine("(INFO) Logging back in.");

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
                AuthCode = authcode,
                TwoFactorCode = twofactor,
                //SentryFileHash = sentryHash,
            });

            loggedBackOn = true;
        } // Wird gecalled wenn wir ausgelogged werden

        static void UpdateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.Clear();
            Console.WriteLine("(STEAM GUARD) Updating Sentry File...");

            byte[] sentryHash = CryptoHelper.SHAHash(callback.Data);

            File.WriteAllBytes("sentry.bin", callback.Data);

            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,
                BytesWritten = callback.BytesToWrite,
                FileSize = callback.Data.Length,
                Offset = callback.Offset,
                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash,
                
            });
            Console.WriteLine("(STEAM GUARD) Done!");
            Thread.Sleep(3000);
            Console.Clear();
        } // Wird gecalled wenn wir das erste mal Steam Guard authentifizieren

        static void OnAccountInfo(SteamUser.AccountInfoCallback callback) // Wird abgerufen wenn Steam die Account Information grabbed
        {
            steamFriends.SetPersonaState(EPersonaState.Online); 
        }

        static void OnChatMessage(SteamFriends.FriendMsgCallback callback)
        {
            var sender = steamFriends.GetFriendPersonaName(callback.Sender);

            string[] args;

            if(callback.EntryType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length >= 1) // Wenn wir das hier nicht >= 1 Setzen antwortet der Bot während wir schreiben... 
                {
                    if(callback.Message.Remove(1) == "!") // removed das präfix '!' von der chat nachricht. Beispiel !coder => coder
                    {
                        string command = callback.Message;
                        if(callback.Message.Contains(" "))
                        {
                            command = callback.Message.Remove(callback.Message.IndexOf(' '));
                            
                        }

                        switch(command)
                        {
                            case "!coder":
                                args = Misc.seperate(0, ' ',callback.Message );
                                Console.WriteLine($"(INFORMATION) Your Steamfriend {sender} has used the command !coder");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Coded by Logxn (github.com/Logxn/)");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Coded using C#"); 
                            break;
                            #region Wer was fixen will hier bitte sehr
                            case "!send": // Geht noch nicht ganz, kp warum, wer bock hat gerne
                                /*args = seperate(2, ' ', callback.Message);
                               
                                if(args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Correct Syntax: !send [Friend] [message]");
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Please write the name in lower cases!");
                                    return;
                                }
                                Console.WriteLine($"(INFORMATION) Your Steamfriend {sender} has orderd you to send {args[1]} a message containing: {args[2]}!");
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    if(steamFriends.GetFriendPersonaName(friend).ToLower().Contains(args[1]))
                                    {
                                        steamFriends.SendChatMessage(friend, EChatEntryType.ChatMsg, args[2]);
                                    }
                                }*/
                            break;
                            case "!imp": //Geht auch nicht kp warum?!?!?!
                                /*args = seperate(1, ' ', callback.Message);
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {

                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    if (args[0] == "-1")
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Correct Syntax: !imp [message]");
                                        return;
                                    }
                                    if (steamFriends.GetFriendPersonaName(friend).Contains("Logan"))
                                    {


                                        steamFriends.SendChatMessage(friend, EChatEntryType.ChatMsg, "Important message from: " + steamFriends.GetFriendPersonaName(callback.Sender) + ": " + args[1]);
                                        Console.Beep();


                                    }


                                }
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Message sent succesfully!");
                                Console.WriteLine("\n\n==============================================================");
                                Console.WriteLine("IMPORTANT: You have recieved an IMPORTANT message from {0}", steamFriends.GetFriendPersonaName(callback.Sender));
                                Console.WriteLine("{0}: " + args[1], steamFriends.GetFriendPersonaName(callback.Sender));
                                Console.WriteLine("[i] End of message.");
                                Console.WriteLine("==============================================================\n");
                                break;*/
                            #endregion
                            case "!steamID":
                                Console.WriteLine("(INFORMATION) Your Steamfriend {sender} has used the Command !steamID");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, $"Your SteamID is: {callback.Sender}");

                            break;
                            case "!friends":
                                Console.WriteLine($"(INFORMATION) Your Steamfriend {sender} has used !friends");
                                for(int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Friend: " + steamFriends.GetFriendPersonaName(friend) + " State: " + steamFriends.GetFriendPersonaState(friend));
                                   
                                }
                            break;
                            case "!music":
                                    // ?!?!?!?!?!?!?!!?!?!? :^)
                            break;
                            case "!commands":
                                Console.WriteLine($"(INFORMATION) I listed some commands to {sender}");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Commandlist:");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "!steamID - Get your SteamID");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "!friends - Get my Friendslist");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "!coder - Get the coder of this bot");
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "!commands - Get the full command list");
                            break;
                            case "!rename":
                                args = Misc.seperate(1, ' ', callback.Message);
                                steamFriends.SetPersonaName(args[1]);
                                break;
                            case "!news":
                                News(); //Gettet derzeit CSGO News eventuell werd ich da AppID argument mit einbauen
                                break;
                            case "!dev": // Ignoriert das erstmal xD
                                CsgoClient csgo = new CsgoClient(steamClient, manager, true);
                                csgo.Launch(protobuf =>
                                {
                                    Console.WriteLine("Waiting 15 secs");
                                    Thread.Sleep(15000);
                                    Console.WriteLine("Waiting Completed.");
                                    csgo.MatchmakingStatsRequest(msgProtobuf =>
                                    {
                                        Console.WriteLine($"{msgProtobuf.global_stats.players_online} players searching");
                                    });
                                    Console.WriteLine("Recieved text?", ConsoleColor.Red);
                                    //csgo.RequestCurrentLiveGames(list => { Console.WriteLine(list.matches.Count); });
                                    //csgo.RequestRecentGames(list => { Console.WriteLine(list.accountid); });
                                });
                        break;
                        }
                    }

                    #region FASS DAS NICHT AN AMK
                    string rLine;
                    string trimmed = callback.Message;
                    char[] trim = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '\\', '|', ';', ':', '"', '\'', ',', '<', '.', '>', '/', '?' };
                    for(int i = 0; i < 30; i++)
                    {
                        trimmed = trimmed.Replace(trim[i].ToString(), "");
                    }
                    StreamReader sReader = new StreamReader("chat.txt");
                    while((rLine = sReader.ReadLine()) != null)
                    {
                        string text = rLine.Remove(rLine.IndexOf('|') - 1);
                        string response = rLine.Remove(0, rLine.IndexOf('|') + 2);

                        if(callback.Message.Contains(text))
                        {
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, response);
                            sReader.Close();
                            return;
                        }
                    }
                    #endregion
                }
            }          
        } // Alle commands befinden sich hier. Kann auch noch anderer stuff geadded werden
      
        static void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            var friendCount = steamFriends.GetFriendCount();

            Thread.Sleep(TimeSpan.FromSeconds(5));

            if (steamFriends.GetPersonaState() == EPersonaState.Online)
            {
                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                {
                    SteamID friend = steamFriends.GetFriendByIndex(i);
                    if (steamFriends.GetFriendPersonaState(friend) != EPersonaState.Online)
                        continue;
                    Console.WriteLine("Friend: " + steamFriends.GetFriendPersonaName(friend) + " State: " + steamFriends.GetFriendPersonaState(friend));              
                }
            }

            Console.WriteLine($"You have {friendCount} friends on this account.\n");
        } // Wird gecalled wenn Steam durch die Freundesliste crawled.

        static void OnFriendAdded(SteamFriends.FriendAddedCallback callback)
        {     
            Console.WriteLine($"(INFORMATION) {callback.PersonaName} is now a friend");
        } // Weiß noch nicht ganz für was das ist, Steam ist gerade down (23.12.16 19:18)
                                                                                // Nein ich bin kein retard. weiß nur nicht ob das für, wenn wir adden oder wenn uns einer added?!

        static void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
        {
            Console.WriteLine("EMSG: " + callback.EMsg.ToString());

            switch (callback.EMsg)
            {
                case 4004: // GC Welcome
                    new ClientGCMsgProtobuf<CMsgClientWelcome>(callback.Message);
                    Console.WriteLine("> GC sagt hallo.");
                    JoiningTheLobby();
                    break;
                case 6604: // GC Join Lobby
                    new ClientGCMsgProtobuf<CMsgClientMMSJoinLobby>(callback.Message);
                    Console.WriteLine("> Joined a lobby");
                    break;
                case 9110:
                    Console.WriteLine($"EMSG Response{callback.Message}");
                    break;
            }
        } // Wird gecalled wenn wir mit dem game coordinator kommunizieren

        #region Experiment
        static void JoiningTheLobby()
        {
            ClientMsgProtobuf<CMsgClientMMSJoinLobby> join = new ClientMsgProtobuf<CMsgClientMMSJoinLobby>(EMsg.ClientMMSJoinLobby);

            join.ProtoHeader.routing_appid = 730;
            join.Body.app_id = 730;
            join.Body.persona_name = "AMK";
            join.Body.steam_id_lobby = (ulong)109775243754032135;
            steamClient.Send(join);
            Thread.Sleep(5000);
        }
        #endregion

        #region Misc (Maybe move this to a helper?!)
        static void News()
        {
            using (dynamic steamNews = WebAPI.GetInterface("ISteamNews"))
            {
                KeyValue kvNews = steamNews.GetNewsForApp(appid: 730);

                foreach (KeyValue news in kvNews["newsitems"]["newsitem"].Children)
                {
                    Console.WriteLine("News: {0}", news["title"].AsString());
                    
                }

            }
        }
        #endregion

    }
}