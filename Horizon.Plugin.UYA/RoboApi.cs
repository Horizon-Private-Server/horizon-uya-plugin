using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Common.Internal.Logging;
using Newtonsoft.Json;
using Server.Medius;
using Server.Plugins.Interface;
using System.Text.Json;
using Server.Database.Models;
using Server.Medius.Models;

namespace Horizon.Plugin.UYA
{
    public class RoboApi
    {

        public static Plugin Plugin = null;

        public RoboApi(Plugin plugin)
        {
            Plugin = plugin;
            StartAsync();
            // Example: _ = Program.Database.GetUsagePolicy();
        }


        public async Task StartAsync()
        {
            Plugin.Log(InternalLogLevel.INFO, "Starting Robo API ...");

            string[] prefixes = {
                "http://*:8281/games/",
                "http://*:8281/players/",
                "http://*:8281/chat/",
                "http://*:8281/alt/"
            };

            HttpListener listener = new HttpListener();
            foreach (string prefix in prefixes) {
                listener.Prefixes.Add(prefix);
            }
            
            try
            {
                Plugin.Log(InternalLogLevel.INFO, "Starting Robo API Listener start ...");
                listener.Start();
            }
            catch (HttpListenerException hlex)
            {
                Plugin.Log(InternalLogLevel.INFO, "WEIRD ERROR --------------");
                return;
            }
            while (listener.IsListening)
            {
                Plugin.Log(InternalLogLevel.INFO, "Starting Robo API listening ...");

                var context = await listener.GetContextAsync();

                Plugin.Log(InternalLogLevel.INFO, "GOT CONTEXT ASYNC ...");

                try
                {
                    await ProcessRequestAsync(context);
                }
                catch (Exception ex)
                {
                    Plugin.Log(InternalLogLevel.WARN, "# EXCEPTION #   " + ex.StackTrace);
                }
            }

            listener.Close();
        }

        private Task ProcessRequestAsync(HttpListenerContext context)
        {
            Plugin.Log(InternalLogLevel.INFO, "GOT A REQUEST!!!");
            Plugin.Log(InternalLogLevel.INFO, context.Request.RawUrl);

            string RawUrl = context.Request.RawUrl;

            string[] Split = null;
            string Arg = null;

            string Response = "[]";

            if (RawUrl.StartsWith("/alt/"))
            {
                Split = RawUrl.Split("/alt/");
                if (Split.Length != 2)
                {
                    Response = "[\"Error\"]";
                }
                else
                {
                    Arg = Split[1];
                }
                Response = ProcessAltApi(Arg);
            }
            else if (RawUrl.StartsWith("/games/"))
            {
                Response = ProcessGameListApi();
            }
            else if (RawUrl.StartsWith("/players/"))
            {
                Response = ProcessPlayerListApi();
            }

            HttpListenerResponse response = context.Response;
            // Construct a response.
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(Response);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.WriteAsync(buffer, 0, buffer.Length);
            return Task.CompletedTask;
        }

        public string ProcessAltApi(string arg)
        {
            return "[\"" + arg + "\"]";
        }

        public class GamePlayerList
        {
            public List<GamePlayerListPlayer> playerList { get; set; }
        }

        public class GamePlayerListPlayer
        {
            public int AccountId { get; set; }
            public string AccountName { get; set; }
            public int DmeId { get; set; }
            public string StatsWide { get; set; }
            public string MediusStats { get; set; }
        }

        public string ProcessGameListApi()
        {
            string result = Program.Database.GetGameList().Result;
            Plugin.Log(InternalLogLevel.INFO, "Result: " + result);

            dynamic jsonResult = JsonConvert.DeserializeObject(result);

            Plugin.Log(InternalLogLevel.INFO, "Json: " + jsonResult);


            string mainPlayerListString = Program.Database.GetPlayerList().Result;
            dynamic mainPlayerListJson = JsonConvert.DeserializeObject(mainPlayerListString);



            foreach (var game in jsonResult)
            {
                GamePlayerList playerList = new GamePlayerList
                {
                   playerList = new List<GamePlayerListPlayer>()
                };
                int gameid = game.GameId;

                // Get all the players associated with this game
                foreach (var player in mainPlayerListJson)
                {
                    if (player.GameId == gameid)
                    {            

                        Plugin.Log(InternalLogLevel.INFO, "Querying account id: " + player.AccountId);

                        int accId = player.AccountId;
                        AccountDTO accountResult = Program.Database.GetAccountById(accId).Result;

                        ClientObject client = Program.Manager.GetClientByAccountId(accId);

                        int dmeId = -1;
                        if (client.DmeClientId != null)
                        {
                            dmeId = (int)client.DmeClientId;
                        }

                        Plugin.Log(InternalLogLevel.INFO, "Finished!");

                        playerList.playerList.Add(new GamePlayerListPlayer()
                        {
                            AccountId = player.AccountId,
                            AccountName = player.AccountName,
                            DmeId = dmeId,
                            StatsWide = ConvertStatsWideToString(accountResult.AccountWideStats),
                            MediusStats = accountResult.MediusStats
                        });
                    }
                }

                game.playerList = JsonConvert.SerializeObject(playerList);
                //game.playerList = playerList;

            }


            return jsonResult.ToString();

            /*
            foreach (var feature in jsonObj.features)
            {
                feature.geometry.Replace(
                        JObject.FromObject(
                                    new
                                    {
                                        type = "Point",
                                        coordinates = feature.geometry.coordinates[0][0]
                                    }));
            }

            var newJson = contourManifest.ToString();
            string re = Program.MediusManager.
 

            return re;
            */
        }

        public string ProcessPlayerListApi()
        {

            string mainPlayerListString = Program.Database.GetPlayerList().Result;
            dynamic mainPlayerListJson = JsonConvert.DeserializeObject(mainPlayerListString);


            // Get all the players associated with this game
            foreach (var player in mainPlayerListJson)
            {
                Plugin.Log(InternalLogLevel.INFO, "Querying account id: " + player.AccountId);

                int accId = player.AccountId;
                AccountDTO accountResult = Program.Database.GetAccountById(accId).Result;

                ClientObject client = Program.Manager.GetClientByAccountId(accId);

                int dmeId = -1;
                if (client.DmeClientId != null)
                {
                    dmeId = (int)client.DmeClientId;
                }

                player.DmeId = dmeId;
                player.StatsWide = ConvertStatsWideToString(accountResult.AccountWideStats);
                player.MediusStats = accountResult.MediusStats;
            }


            return mainPlayerListJson.ToString();
        }

        private string ConvertStatsWideToString(int[] statsWide)
        {
            string result = "";

            foreach (int stat in statsWide)
            {
                result += stat.ToString("X4");
            }
            return result;
        }
    }
}


/*
 * private volatile bool stop = true;
    private Action<string> methodOne;

    public Server(Action<string> methodOne)
    {
        this.methodOne= methodOne;
    }

    public async Task StartAsync()
    {
        var prefix = "http://localhost:5005/";
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
            stop = false;
        }
        catch (HttpListenerException hlex)
        {
            return;
        }
        while (listener.IsListening)
        {
            var context = await listener.GetContextAsync();
            try
            {
                await ProcessRequestAsync(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine("# EXCEPTION #   " + ex.StackTrace);
            }
            if (stop == true) listener.Stop();
        }
        listener.Close();
    }

    public void Stop() 
    {
        stop = true; 
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        // Get the data from the HTTP stream
        var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();            
        HttpListenerRequest request = context.Request;

        if (request.RawUrl.StartsWith("/methodOne"))
        {
            //Get parameters
            var options = context.Request.QueryString;
            var keys = options.AllKeys;
            //Run function
            methodOne("some method parameter");
            //Respond
            byte[] b = Encoding.UTF8.GetBytes("ack");
            context.Response.StatusCode = 200;
            context.Response.KeepAlive = false;
            context.Response.ContentLength64 = b.Length;
            var output = context.Response.OutputStream;
            await output.WriteAsync(b, 0, b.Length);
            context.Response.Close();
        }
    }
}
*/