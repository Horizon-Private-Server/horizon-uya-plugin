﻿using DotNetty.Common.Internal.Logging;
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

        public static RoboApi Api = null;
        public static RoboDatabase RoboDb = null;

        public Task Start(string workingDirectory, IPluginHost host)
        {
            WorkingDirectory = workingDirectory;
            Host = host;

            if (Api == null)
                Api = new RoboApi((Plugin)this);

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
            host.RegisterAction(PluginEvent.MEDIUS_ACCOUNT_LOGIN_REQUEST, OnAccountLogin);
            host.RegisterMediusMessageAction(NetMessageTypes.MessageClassLobby, (byte)MediusLobbyMessageIds.PlayerInfo, OnPlayerInfoRequest);
            host.RegisterMediusMessageAction(NetMessageTypes.MessageClassDME, 8, OnRecvCustomMessage);
            host.RegisterMessageAction(RT_MSG_TYPE.RT_MSG_SERVER_CHEAT_QUERY, OnRecvCheatQuery);

            return Task.CompletedTask;
        }

        public void SyncRoboDbToHorizon() {
            DebugLog("Syncing robo db to horizon ...");
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
                    DebugLog("Already exists!");
                    continue;
                }

                totalAdded += 1;

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

                    task.Wait(); // Wait for the async method to complete
                    //DebugLog($"Update?: {taskUpdateStats.Result.ToString()}");
                }
                catch (Exception ex)
                {
                    DebugLog($"Exception trying to update stats on {account.ToString()}");
                    DebugLog(ex.ToString());
                }
            }
            DebugLog($"Added {totalAdded} / {accounts.Count} !");

            DebugLog("Done syncing Robo db to Horizon!");
        }



        Task OnTick(PluginEvent eventId, object data)
        {
            return Task.CompletedTask;
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


            if (RoboDb.AccountExists(request.Username)) {
                // If password = Robo hashed password, then change the password to be the robo encrypted PW?
                string roboPassword = RoboDb.GetPassword(request.Username);

                //Check if account already exists in database
                Task<AccountDTO> task = Server.Medius.Program.Database.GetAccountByName(request.Username, 10684); // Call the async method
                task.Wait(); // Wait for the async method to complete

                if (task.Result == null){
                    return Task.CompletedTask;
                }

                if (RoboDb.EncryptString(request.Password) != roboPassword) {
                    DebugLog("Passwords don't match!");
                    return Task.CompletedTask;
                }

                // passwords match
                request.Password = roboPassword;
            }

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


            if (RoboDb.AccountExists(request.Username)) {
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

            // reset map version
            playerExtraInfo.CurrentMapVersion = 0;

            // pass to game
            return Game.PlayerJoined(client, game);
        }

        Task OnPlayerPostWideStats(PluginEvent eventId, object data)
        {
            var msg = (Server.Medius.PluginArgs.OnPlayerWideStatsArgs)data;
            if (msg.Player == null || msg.Game == null)
                return Task.CompletedTask;
            if (!SupportedAppIds.Contains(msg.Player.ApplicationId))
                return Task.CompletedTask;

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

                                await Player.SetPlayerMapVersion(msg.Player, request.ClientMapVersion);
                                break;
                            }
                        case 7: // request for current custom map override
                            {
                                var requestMessage = new GetMapOverrideRequestMessage();
                                requestMessage.Deserialize(reader);
                                //await Game.SendMapOverride(msg.Player);
                                break;
                            }
                        case 8: // set player patch config
                            {
                                var request = new SetPlayerPatchConfigRequestMessage();
                                request.Deserialize(reader);
                                await Player.SetPatchConfig(msg.Player, request.Config);
                                break;
                            }
                        case 9: // set patch game config
                            {
                                if (msg.Player.CurrentGame != null && msg.Player.CurrentGame.Host == msg.Player && msg.Player.CurrentGame.WorldStatus <= MediusWorldStatus.WorldStaging)
                                {
                                    var request = new SetGameConfigRequestMessage();
                                    request.Deserialize(reader);

                                    // try to update game config
                                    if (await Game.SetGameConfig(msg.Player.CurrentGame, request.Config))
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
