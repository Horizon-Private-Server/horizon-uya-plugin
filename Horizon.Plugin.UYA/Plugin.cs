using DotNetty.Common.Internal.Logging;
using Horizon.Plugin.UYA.Messages;
using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using Server.Database;
using Server.Database.Models;
using Server.Medius;
using Server.Medius.Models;
using Server.Medius.PluginArgs;
using Server.Plugins.Interface;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Horizon.Plugin.UYA
{
    public class Plugin : IPlugin
    {
        public static string WorkingDirectory = null;
        public static IPluginHost Host = null;
        public static readonly int[] SupportedAppIds = {
            10683, // PAL
            10684, // NTSC
        };

        // Disable for now.
        //public static RoboApi Api = null;
        public static RoboDatabase RoboDb = null;

        // db appsettings
        private static bool hasQueriedAppSettings = false;
        private static DateTime timeLastQueriedAppSettings = DateTime.MinValue;
        private static readonly AppSettings defaultAppSettings = new AppSettings(0);
        private static Dictionary<int, AppSettings> appSettingsByAppId = new Dictionary<int, AppSettings>();

        public static AppSettings GetAppSettingsOrDefault(int appId)
        {
            if (appSettingsByAppId.TryGetValue(appId, out var settings))
                return settings;

            return defaultAppSettings;
        }

        public Task Start(string workingDirectory, IPluginHost host)
        {
            WorkingDirectory = workingDirectory;
            Host = host;

            // if (Api == null)
            //     Api = new RoboApi((Plugin)this);

            if (RoboDb == null)
                RoboDb = new RoboDatabase((Plugin)this);

            SyncRoboDbToHorizon();

            host.RegisterAction(PluginEvent.TICK, OnTick);
            host.RegisterAction(PluginEvent.MEDIUS_PLAYER_ON_GET_POLICY, OnPlayerLoggedIn);
            host.RegisterAction(PluginEvent.MEDIUS_PLAYER_ON_LOGGED_OUT, OnPlayerLoggedOut);
            host.RegisterAction(PluginEvent.MEDIUS_GAME_ON_CREATED, OnGameCreated);
            host.RegisterAction(PluginEvent.MEDIUS_GAME_ON_DESTROYED, OnGameDestroyed);
            host.RegisterAction(PluginEvent.MEDIUS_GAME_ON_STARTED, OnGameStarted);
            host.RegisterAction(PluginEvent.MEDIUS_PLAYER_ON_WORLD_REPORT0, OnWorldReport0);
            host.RegisterAction(PluginEvent.MEDIUS_GAME_ON_ENDED, OnGameEnded);
            host.RegisterAction(PluginEvent.MEDIUS_PLAYER_ON_JOINED_GAME, OnPlayerJoinedGame);
            host.RegisterAction(PluginEvent.MEDIUS_GAME_ON_HOST_LEFT, OnHostLeftGame);
            host.RegisterAction(PluginEvent.MEDIUS_PLAYER_POST_WIDE_STATS, OnPlayerPostWideStats);
            host.RegisterAction(PluginEvent.MEDIUS_FIND_PLAYER_ACCOUNT_NAME, OnFindPlayerAccountName);
            host.RegisterAction(PluginEvent.MEDIUS_ACCOUNT_LOGIN_REQUEST, OnAccountLogin);
            host.RegisterAction(PluginEvent.MEDIUS_CLAN_ON_UPDATE_STATS, OnUpdateClanStats);
            host.RegisterAction(PluginEvent.MEDIUS_ON_ACCOUNT_UPDATE_STATS, OnAccountUpdateStats);
            host.RegisterMediusMessageAction(NetMessageTypes.MessageClassLobby, (byte)MediusLobbyMessageIds.PlayerInfo, OnPlayerInfoRequest);
            host.RegisterMediusMessageAction(NetMessageTypes.MessageClassDME, 8, OnRecvCustomMessage);
            host.RegisterMediusMessageAction(NetMessageTypes.MessageClassLobbyExt, (byte)MediusLobbyExtMessageIds.DnasSignaturePost, OnRecvDnasSignature);
            host.RegisterMessageAction(RT_MSG_TYPE.RT_MSG_SERVER_CHEAT_QUERY, OnRecvCheatQuery);

            return Task.CompletedTask;
        }

        public void SyncRoboDbToHorizon() {
            DebugLog("Syncing robo db to horizon ...");

            if (RoboDb == null)
            {
                DebugLog("Robo db is null");
                return;
            }

            Task<Dictionary<string, string>> taskGetServerSettings = Server.Medius.Program.Database.GetServerSettings(10684); 
            taskGetServerSettings.Wait(); 

            if (taskGetServerSettings.Result == null){
                DebugLog("No server settings found!");
                return;
            }

            Dictionary<string, string> serverSettings = taskGetServerSettings.Result;
            if (serverSettings.ContainsKey("RoboAccountsMigrated")) {
                DebugLog("Robo accounts already migrated!");
                return;
            }
            else {
                DebugLog("Robo database not migrated yet!");
            }


            List<RoboAccount> accounts = RoboDb.DumpUsers();

            DebugLog($"Found {accounts.Count} robo accounts!");

            int processed = 0;
            int totalAdded = 0;

            foreach (RoboAccount account in accounts) {
                processed += 1; 
                if (processed % 100 == 0) 
                    DebugLog($"Processed {processed} / {accounts.Count} ...");

                //DebugLog($"Processing: {account.ToString()}");

                // Check if account already exists in database
                Task<AccountDTO> task = Server.Medius.Program.Database.GetAccountByName(account.Username, 10684); // Call the async method
                task.Wait(); // Wait for the async method to complete

                //DebugLog($"Result: {task.ToString()}");

                if (task.Result != null){
                    //DebugLog("Already exists!");
                    continue;
                }

                Task<AccountDTO> taskAccountCreate = Server.Medius.Program.Database.CreateAccount(new CreateAccountDTO()
                {
                    AccountName = account.Username,
                    AccountPassword = Utils.ComputeSHA256(account.Password),
                    MachineId = "1",
                    MediusStats = Convert.ToBase64String(new byte[Constants.ACCOUNTSTATS_MAXLEN]),
                    AppId = account.AppId
                });

                if (taskAccountCreate.Result == null) {
                    DebugLog($"ERROR! Not able to create: {account.ToString()}");
                    continue;
                }
                AccountDTO accountResult = taskAccountCreate.Result;
                int accountId = accountResult.AccountId;

                try
                {
                    //DebugLog($"Updating stats for: {account.Username}");
                    Task<bool> taskUpdateStats = Server.Medius.Program.Database.PostAccountLadderStats(new StatPostDTO() {
                        AccountId = accountId,
                        Stats = account.Stats
                    }); // Call the async method

                    taskUpdateStats.Wait(); // Wait for the async method to complete
                    //DebugLog($"Update?: {taskUpdateStats.Result.ToString()}");
                }
                catch (Exception ex)
                {
                    DebugLog($"Exception trying to update stats on {account.ToString()}");
                    DebugLog(ex.ToString());
                    continue;
                }
                totalAdded += 1;

            }
            DebugLog($"Added {totalAdded} / {accounts.Count} !");

            serverSettings.Add("RoboAccountsMigrated", "True");

            Task taskUpdate = Server.Medius.Program.Database.SetServerSettings(10684, serverSettings); 
            taskUpdate.Wait(); 

            DebugLog("Done syncing Robo db to Horizon!");
        }



        async Task OnTick(PluginEvent eventId, object data)
        {
            // try and get app settings
            if (!hasQueriedAppSettings && await Server.Medius.Program.Database.AmIAuthenticated() && !String.IsNullOrEmpty(Server.Medius.Program.Database.GetUsername()))
            {
                foreach (var supportedAppId in SupportedAppIds)
                {
                    var appSettings = await Server.Medius.Program.Database.GetServerSettings(supportedAppId) ?? new Dictionary<string, string>();
                    if (!appSettingsByAppId.TryGetValue(supportedAppId, out var settings))
                        appSettingsByAppId.Add(supportedAppId, settings = new AppSettings(supportedAppId));

                    settings.SetSettings(appSettings);

                    // send fully parsed settings to server
                    await Server.Medius.Program.Database.SetServerSettings(supportedAppId, settings.GetSettings());
                }

                Host.Log(InternalLogLevel.WARN, $"ADDED APPSETTINGS FOR {Server.Medius.Program.Database.GetUsername()}");
                hasQueriedAppSettings = true;
                timeLastQueriedAppSettings = DateTime.UtcNow;
            }
            else if (hasQueriedAppSettings && (DateTime.UtcNow - timeLastQueriedAppSettings).TotalMinutes > 5)
            {
                timeLastQueriedAppSettings = DateTime.UtcNow;

                foreach (var supportedAppId in SupportedAppIds)
                {
                    var appSettings = await Server.Medius.Program.Database.GetServerSettings(supportedAppId) ?? new Dictionary<string, string>();
                    if (!appSettingsByAppId.TryGetValue(supportedAppId, out var settings))
                        appSettingsByAppId.Add(supportedAppId, settings = new AppSettings(supportedAppId));

                    settings.SetSettings(appSettings);
                }
            }
        }

        
        Task OnAccountLogin(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnAccountLoginRequestArgs)data;
            
            MediusAccountLoginRequest request = (MediusAccountLoginRequest)msg.Request;
            ClientObject Player = (ClientObject)msg.Player;

            if (msg.Player == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            if (request.Username == null)
                return Task.CompletedTask;


            if (RoboDb != null && RoboDb.AccountExists(request.Username)) {
                // If password = Robo hashed password, then change the password to be the robo encrypted PW?
                string roboPassword = RoboDb.GetPassword(request.Username);

                DebugLog($"Got robo pw: {roboPassword}");

                //Check if account already exists in database
                Task<AccountDTO> task = Server.Medius.Program.Database.GetAccountByName(request.Username, 10684); // Call the async method
                task.Wait(); // Wait for the async method to complete

                if (task.Result == null){
                    return Task.CompletedTask;
                }

                DebugLog($"Got request pw: {request.Password}");


                if (RoboDb.EncryptString(request.Password) != roboPassword) {
                    DebugLog("Passwords don't match!");
                    return Task.CompletedTask;
                }

                // passwords match
                request.Password = roboPassword;
            }

            return Task.CompletedTask;
        }

        Task OnUpdateClanStats(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnUpdateClanStatsRequestArgs)data;

            MediusUpdateClanStatsRequest request = (MediusUpdateClanStatsRequest)msg.Request;
            int clanId = request.ClanID;
            byte[] clanStats = request.Stats;

            Task<List<ClanMessageDTO>> task = Server.Medius.Program.Database.GetClanMessages(0, clanId, 0, 1); // Call the async method
            task.Wait(); // Wait for the async method to complete
            // public async Task<> GetClanMessages(int accountId, int clanId, int startIndex, int pageSize)

            if (task.Result == null){
                return Task.CompletedTask;
            }

            List<ClanMessageDTO> clanMsgResult = task.Result;

            if (clanMsgResult.Count == 0) {
                return Task.CompletedTask;
            }

            string clanMessage = clanMsgResult[0].Message;
            request.Stats = ClanStatsCleaner.CleanStats((Plugin)this, clanMessage, clanStats);
            return Task.CompletedTask;
        }


        Task OnAccountUpdateStats(PluginEvent eventId, object data)
        {
            return Task.CompletedTask;
            var msg = (Server.Medius.PluginArgs.OnAccountUpdateStatsArgs)data;
            ClientObject player = (ClientObject)msg.Player;
        
            MediusAccountUpdateStatsRequest request = (MediusAccountUpdateStatsRequest)msg.Request;

            request.Stats = SkillBoltFixer.Fix((Plugin)this, player, request.Stats);

            return Task.CompletedTask;
        }


        Task PreOnAccountCreateOnNotFound(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnAccountLoginRequestArgs)data;
            
            MediusAccountLoginRequest request = (MediusAccountLoginRequest)msg.Request;
            ClientObject Player = (ClientObject)msg.Player;

            if (msg.Player == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            if (request.Username == null)
                return Task.CompletedTask;


            if (RoboDb != null && RoboDb.AccountExists(request.Username)) {
                string roboPassword = RoboDb.GetPassword(request.Username);
                string horizonPassword = RoboDb.EncryptString(request.Password);

                if (roboPassword != horizonPassword) {
                    DebugLog($"Robo Password ({roboPassword}) != Horizon Password ({horizonPassword}) for username: {request.Username}  ");
                    Player.Queue(new MediusAccountLoginResponse()
                    {
                        MessageID = request.MessageID,
                        StatusCode = MediusCallbackStatus.MediusInvalidPassword
                    });

                    request.Username = null;
                    request.Password = null;
                }
                
                DebugLog($"Robo Password and Horizon Password match for username: {request.Username}!");
            }
            
            return Task.CompletedTask;
        }

        Task OnPlayerLoggedIn(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerRequestArgs)data;
            if (msg.Player == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            return Patch.QueryForPatch(msg.Player);
        }


        async Task OnPlayerLoggedOut(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerArgs)data;
            if (msg.Player == null)
                return;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return;

            await Player.OnPlayerLoggedOut(msg.Player);
            await Downloader.OnPlayerLoggedOut(msg.Player);
        }

        Task OnWorldReport0(PluginEvent eventType, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnWorldReport0Args)data;
            MediusWorldReport0 report = (MediusWorldReport0)msg.Request;

            if (report.WorldStatus == MediusWorldStatus.WorldActive && !report.GameName.StartsWith("[IG] "))
            {

                report.GameName = "[IG] " + report.GameName;
            }
            return Task.CompletedTask;
        }

        Task OnGameStarted(PluginEvent eventId, object data)
        {

            var msg = (Server.Medius.PluginArgs.OnGameArgs)data;
            if (msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Game.ApplicationId))
                return Task.CompletedTask;

            return Game.OnGameStarted(msg.Game);
        }

        Task OnGameEnded(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnGameArgs)data;
            if (msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Game.ApplicationId))
                return Task.CompletedTask;

            return Game.OnGameEnded(msg.Game);
        }

        Task OnGameDestroyed(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnGameArgs)data;
            if (msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Game.ApplicationId))
                return Task.CompletedTask;

            return Game.OnGameDestroyed(msg.Game);
        }

        Task OnGameCreated(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerGameArgs)data;
            if (msg.Player == null || msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            var client = msg.Player;
            var game = msg.Game;
            var playerExtraInfo = Player.GetPlayerExtraInfo(client.AccountId);

            // reset map version
            playerExtraInfo.CurrentMapVersion = 0;

            return Task.CompletedTask;
        }

        Task OnHostLeftGame(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerGameArgs)data;
            if (msg.Player == null || msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            // close world if host left staging
            //if (msg.Game.WorldStatus == MediusWorldStatus.WorldStaging)
            //    return msg.Game.SetWorldStatus(MediusWorldStatus.WorldClosed);

            return Task.CompletedTask;
        }
        

        int[] ConvertStringListToIntegers(string s) {
            string[] parts = s.Split(',');

            int[] numbers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                bool success = int.TryParse(parts[i], out int number);
                if (success)
                {
                    numbers[i] = number;
                }
                else
                {
                    Log($"Invalid number format: {parts[i]}");
                }
            }

            return numbers;
        }

        bool IsCpuGameFromPlayerList(int[] accounts) {
            // Regex.IsMatch(client.AccountName, @"^CPU-(?:[0-9]{3})$");
            bool isCpu = false;
            foreach (int accountId in accounts) {
                // Task<AccountDTO> GetAccountById(int id)
                Task<AccountDTO> getAccountTask = Server.Medius.Program.Database.GetAccountById(accountId); 
                getAccountTask.Wait(); 

                if (getAccountTask.Result == null){
                    DebugLog($"No Account found for {accountId}");
                    continue;
                }

                AccountDTO acc = getAccountTask.Result;
                string username = acc.AccountName;

                bool thisIsCpu = Regex.IsMatch(username, @"^CPU-(?:[0-9]{3})$");
                isCpu = isCpu || thisIsCpu;
            }

            return isCpu;
        }

        public bool AccountIsLoggedIn(string username) {
            ClientObject test = Server.Medius.Program.Manager.GetClientByAccountName(username, 10684); 
            if (test == null) {
                return false;
            }
            return true;
        }

        public (List<string>, List<int>) GetAccountAndAccountId(int numAccounts)
        {
            List<string> usernamesSelected = new List<string>();
            List<int> accountIdsSelected = new List<int>();

            for (int i = 0; i < numAccounts; i++) {
                // Select the bot to use
                Random random = new Random();
                int randomNumber = random.Next(1, 999);
                string formattedNumber = randomNumber.ToString("D3");

                string username = "CPU-" + formattedNumber;

                while (AccountIsLoggedIn(username) && !usernamesSelected.Contains(username)) {
                    randomNumber = random.Next(1, 999);
                    formattedNumber = randomNumber.ToString("D3");
                    username = "CPU-" + formattedNumber;
                    Log($"Testing: {username}");
                }

                // Get the account id from the username
                Task<AccountDTO> task = Server.Medius.Program.Database.GetAccountByName(username, 10684); // Call the async method
                task.Wait(); // Wait for the async method to complete

                AccountDTO acc = task.Result;
                int account_id = acc.AccountId;

                usernamesSelected.Add(username);
                accountIdsSelected.Add(account_id);

            }

            return (usernamesSelected, accountIdsSelected);
        }


        Task OnFindPlayerAccountName(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnFindPlayerRequestArgs)data;
            if (msg.Player == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            return Task.CompletedTask;
        }


        Task OnPlayerJoinedGame(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerGameArgs)data;
            if (msg.Player == null || msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            var client = msg.Player;
            var game = msg.Game;
            var playerExtraInfo = Player.GetPlayerExtraInfo(client.AccountId);

            Server.Medius.Models.Game gameObj = msg.Game;
            int[] players = ConvertStringListToIntegers(gameObj.GetActivePlayerList());

            bool cpuGame = IsCpuGameFromPlayerList(players);
            if (cpuGame)
            {
                Log("A CPU Joined the game! Setting players to CPU Game!");
            }
            UpdatePlayersMetadataToAddCPUGame(players, cpuGame);

            // reset map version
            playerExtraInfo.CurrentMapVersion = 0;

            // pass to game
            return Game.PlayerJoined(client, game);
        }


       public void UpdatePlayersMetadataToAddCPUGame(int[] players, bool cpuGame) {
            foreach (int accountId in players)
            {
                
                JObject metadata = GetAccountMetadata(accountId);

                if (metadata == null) {
                    DebugLog($"No Metadata found for {accountId}");
                    continue;
                }

                metadata["CpuGame"] = cpuGame;

                PostAccountMetadata(accountId, metadata);
            }
       }

    public async Task<string> GetAccountMetadataAsync(int accountId) {
        return await Server.Medius.Program.Database.GetAccountMetadata(accountId);
    }

    public JObject GetAccountMetadata(int accountId) {
        Task<string> GetAccountMetadata = Server.Medius.Program.Database.GetAccountMetadata(accountId); 
        GetAccountMetadata.Wait(); 

        if (GetAccountMetadata.Result == null){
            DebugLog($"No Metadata found for {accountId}");
            JObject emptyObject = new JObject();
            return emptyObject;
        }

        string metadata = GetAccountMetadata.Result;
        Log($"Found metadata for account id {accountId}: {metadata}");


        //JObject jsonObject = JsonConvert.DeserializeObject(metadata);
        JObject jsonObject = JObject.Parse(metadata);


        return jsonObject;
    }

    public void PostAccountMetadata(int accountId, JObject metadata) {
        string updatedJsonString = JsonConvert.SerializeObject(metadata);
        Log($"Posting metadata: {updatedJsonString}");

        try
        {
            Task<bool> taskUpdateMetadata = Server.Medius.Program.Database.PostAccountMetadata(
                accountId,
                updatedJsonString
            ); // Call the async method

            taskUpdateMetadata.Wait(); // Wait for the async method to complete
            Log("Metadata updated!");
        }
        catch (Exception ex)
        {
            DebugLog($"Exception trying to metadata on account id {accountId}");
            DebugLog(ex.ToString());
            return;
        }
    }

        Task OnPlayerPostWideStats(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerWideStatsArgs)data;
            if (msg.Player == null || msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;


            // Check if it's a bot game. If it is, then ignore
            // Get the player's metadata. Then 
            JObject metadata = GetAccountMetadata(msg.Player.AccountId);
            if (metadata != null) {
                if ((bool)metadata["CpuGame"]) {
                    Log("Ignoring CPU GAME STATS!");
                    // MediusUpdateLadderStatsWideRequest updateLadderStatsWideRequest = (MediusUpdateLadderStatsWideRequest)msg.Request;
                    // updateLadderStatsWideRequest.LadderType = null;
                    msg.Reject = true;
                    return Task.CompletedTask;
                }
            }




            // pass to game
            return Game.OnPlayerPostWideStats(msg);
        }

        async Task OnRecvCheatQuery(RT_MSG_TYPE msgId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnMessageArgs)data;
            if (msg.Ignore || !msg.IsIncoming || msg.Player == null)
                return;

            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return;

            var cheatQuery = msg.Message as RT_MSG_SERVER_CHEAT_QUERY;
            if (cheatQuery == null)
                return;

            switch (cheatQuery.SequenceId)
            {
                case 101:
                    {
                        msg.Ignore = true;
                        await Patch.QueryForPatchResponse(msg.Player, cheatQuery);
                        break;
                    }
                default:
                    {
                        Host.Log(InternalLogLevel.WARN, $"Unhandled cheat query sequence id {cheatQuery.SequenceId}: {msg}");
                        break;
                    }
            }
        }

        async Task OnPlayerInfoRequest(NetMessageTypes msgClass, byte msgType, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnMediusMessageArgs)data;
            if (msg.Ignore || !msg.IsIncoming || msg.Player == null)
                return;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return;

            // for some reason UYA requests player info for account id -1
            // maybe this was some kind of special account back in the day
            // the game errors if we don't return success
            // so here we intercept and handle the response instead of letting the server handle it
            if (msg.Message is MediusPlayerInfoRequest playerInfoRequest && playerInfoRequest.AccountID == -1)
            {
                msg.Ignore = true;
                msg.Player.Queue(new MediusPlayerInfoResponse()
                {
                    MessageID = playerInfoRequest.MessageID,
                    StatusCode = MediusCallbackStatus.MediusSuccess
                });
            }
        }

        Task OnRecvDnasSignature(NetMessageTypes msgClass, byte msgType, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnMediusMessageArgs)data;
            if (msg.Ignore || !msg.IsIncoming || msg.Player == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

            msg.Ignore = true;
            return Task.CompletedTask;
        }

        async Task OnRecvCustomMessage(NetMessageTypes msgClass, byte msgType, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnMediusMessageArgs)data;
            if (msg.Ignore || !msg.IsIncoming || msg.Player == null)
                return;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return;

            var contents = (msg.Message as RawMediusMessage).Contents;
            var customMsgId = contents[0];
            msg.Ignore = true;

            using (var ms = new MemoryStream(contents, false))
            {
                using (var reader = new MessageReader(ms))
                {

                    switch (customMsgId)
                    {
                        case 2: // download data response
                            {
                                var downloadDataResponse = new DataDownloadResponseMessage();
                                downloadDataResponse.Deserialize(reader);
                                await Downloader.OnDataDownloadResponse(msg.Player, downloadDataResponse);
                                break;
                            }
                        case 3: // request IRX modules
                            {
                                var irxModulesRequest = new MapModulesRequestMessage();
                                irxModulesRequest.Deserialize(reader);
                                await Maps.SendMapModules(msg.Player, irxModulesRequest.Module1Start, irxModulesRequest.Module2Start);
                                break;
                            }
                        case 5: // player responds with map override version
                            {
                                var request = new SetMapOverrideResponseMessage();
                                request.Deserialize(reader);

                                //await Player.SetPlayerMapVersion(msg.Player, request.ClientMapVersion);
                                break;
                            }
                        case 6: // player responds with map override version
                            {
                                var request = new SetMapOverrideResponseMessage();
                                request.Deserialize(reader);

                                await Player.SetPlayerMapVersion(msg.Player, request.MapFilename, request.ClientMapVersion);
                                break;
                            }
                        case 7: // request for current custom map override
                            {
                                var requestMessage = new GetMapOverrideRequestMessage();
                                requestMessage.Deserialize(reader);
                                await Game.SendMapOverride(msg.Player);
                                break;
                            }
                        case 8: // set player patch config
                            {
                                // Get current CpuGame status and save to a variable
                                Server.Medius.Models.Game currentGame = msg.Player.CurrentGame;
                                bool cpuGame = false;
                                if (currentGame != null) {
                                    int[] players = ConvertStringListToIntegers(currentGame.GetActivePlayerList());
                                    cpuGame = IsCpuGameFromPlayerList(players);
                                }

                                var request = new SetPlayerPatchConfigRequestMessage();
                                request.Deserialize(reader);
                                await Player.SetPatchConfig(msg.Player, request.Config);

                                // After setting the patch config, set the CpuGame status to what it should be before
                                string currentMetadata = await GetAccountMetadataAsync(msg.Player.AccountId);

                                JObject metadata = new JObject();
                                if (currentMetadata != null) {
                                    metadata = JObject.Parse(currentMetadata);
                                }


                                metadata["CpuGame"] = cpuGame;
                                string updatedJsonString = JsonConvert.SerializeObject(metadata);

                                await Server.Medius.Program.Database.PostAccountMetadata(msg.Player.AccountId, updatedJsonString);

                                break;
                            }
                        case 9: // set patch game config
                            {
                                if (msg.Player.CurrentGame != null && msg.Player.CurrentGame.Host == msg.Player && msg.Player.CurrentGame.WorldStatus <= MediusWorldStatus.WorldStaging)
                                {
                                    var request = new SetGameConfigRequestMessage();
                                    request.Deserialize(reader);

                                    // try to update game config
                                    if (await Game.SetGameConfig(msg.Player.CurrentGame, request.Config, request.CustomMapConfig))
                                    {
                                        // send new game config to other players in lobby
                                        await Game.BroadcastGameConfig(msg.Player.CurrentGame);
                                    }
                                }
                                break;
                            }
                        case 11: // game started
                            {
                                var game = msg.Player.CurrentGame;
                                if (game != null && game.Host == msg.Player && game.WorldStatus == MediusWorldStatus.WorldStaging)
                                    await game.SetWorldStatus(MediusWorldStatus.WorldActive);
                                break;
                            }
                        case 12: // game ended
                            {
                                var game = msg.Player.CurrentGame;
                                if (game != null && game.WorldStatus == MediusWorldStatus.WorldActive)
                                    await game.SetWorldStatus(MediusWorldStatus.WorldClosed);
                                break;
                            }
                        case 13: // redownload patch
                            {
                                await Patch.SendPatch(msg.Player);
                                break;
                            }
                        case 14: // set client machine id
                            {
                                var request = new SetClientMachineIdRequest();
                                request.Deserialize(reader);

                                string macAddr = BitConverter.ToString(request.MachineId);

                                await Program.Database.PostMachineId(msg.Player.AccountId, macAddr);

                                _ = Program.Database.GetIsMacBanned(macAddr).ContinueWith((t) =>
                                {
                                    if (t.IsCompletedSuccessfully && t.Result != null && t.Result == true)
                                    {
                                        msg.Player.Logout();
                                        msg.Player.ForceDisconnect();
                                    }
                                });

                                break;
                            }
                        case 16: // player picked up horizon bolt
                            {
                                var request = new PlayerPickedUpHorizonBoltMessage();
                                request.Deserialize(reader);

                                // make sure the scavenger hunt is live
                                var appSettings = GetAppSettingsOrDefault(msg.Player.ApplicationId);
                                if (DateTime.UtcNow >= appSettings.ScavengerHuntBeginDate && DateTime.UtcNow < appSettings.ScavengerHuntEndDate)
                                {
                                    await Player.OnPickedUpHorizonBolt(msg.Player);
                                }
                                break;
                            }
                        case 17: // request scavenger hunt settings
                            {
                                var request = new GetScavengerHuntSettingsRequestMessage();
                                request.Deserialize(reader);

                                var appSettings = GetAppSettingsOrDefault(msg.Player.ApplicationId);
                                var response = new GetScavengerHuntSettingsResponseMessage();
                                response.Enabled = DateTimeOffset.UtcNow >= appSettings.ScavengerHuntBeginDate && DateTimeOffset.UtcNow <= appSettings.ScavengerHuntEndDate;
                                response.SpawnFactor = appSettings.ScavengerHuntSpawnRateFactor;

                                msg.Player.Queue(response);
                                break;
                            }
                        case 19: // request month day
                            {
                                var request = new GetMonthDayRequestMessage();
                                request.Deserialize(reader);

                                var now = DateTime.UtcNow;
                                var response = new GetMonthDayResponseMessage();
                                response.Month = (byte)now.Month;
                                response.Day = (byte)now.Day;

                                msg.Player.Queue(response);
                                break;
                            }
                        case 23: // bot invite
                            {
                                BotInviteRequestMessage request = new BotInviteRequestMessage();
                                request.Deserialize(reader);
                                Host.Log(InternalLogLevel.INFO, $"GOT BOT INVITE: {customMsgId}: {request}");

                                Server.Medius.Models.Game game = msg.Player.CurrentGame;

                                int accountsToInvite = 8 - game.PlayerCount;
                                if (request.NumBotsToInvite != 0) {
                                    accountsToInvite = request.NumBotsToInvite;
                                }

                                var result = GetAccountAndAccountId(accountsToInvite);
                                List<string> accountNames = result.Item1;
                                List<int> accountIds = result.Item2;

                                int world_id = game.DMEWorldId+1;
                                            
                                Bot b = new Bot(this);
                                b.Trigger(accountNames, accountIds, request.Profile, request.BotMode, request.Difficulty, world_id);

                                break;
                            }
                        default:
                            {
                                Host.Log(InternalLogLevel.WARN, $"Unhandled custom msg id {customMsgId}: {msg}");
                                break;
                            }
                    }
                }
            }
        }

        public void Log(string text) {
            DebugLog(text);
        }

        public void DebugLog(string text) {
            Host.Log(InternalLogLevel.INFO, "UYA Plugin Logging: " + text);
        }

    }
}
