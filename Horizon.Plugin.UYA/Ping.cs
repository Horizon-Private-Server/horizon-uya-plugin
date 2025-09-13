using Horizon.Plugin.UYA.Messages;
using Newtonsoft.Json;
using Server.Medius;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Server.Common;
using Server.Common.Stream;
using Server.Database;
using Server.Database.Models;
using Server.Medius;
using Server.Medius.Models;
using Server.Medius.PluginArgs;
using Server.Plugins.Interface;
using DotNetty.Common.Internal.Logging;
using System;
using System.Threading.Tasks;
using DotNetty.Common.Internal.Logging;
using Horizon.Plugin.UYA.Messages;
using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using Server.Database;
using Server.Database.Models;


namespace Horizon.Plugin.UYA
{
    public static class UYAPing
    {
        public static int PingDelayStartLoginMs = 10000;
        public static int PingDelayMs = 3000;


        public static async Task Start(ClientObject client, IPluginHost host)
        {
            try
            {
                await Task.Delay(PingDelayStartLoginMs);

                var playerInfo = Player.GetPlayerExtraInfo(client.AccountId);
                while (client != null && client.IsConnected && playerInfo != null && playerInfo.PatchHash != null)
                {
                    if (client.CurrentGame != null && client.CurrentGame.WorldStatus == MediusWorldStatus.WorldActive && playerInfo.PlayerInGame == 1) {
                        host.Log(InternalLogLevel.INFO, $"UYA Ping completed for {client?.AccountId} (lastping: {playerInfo.CurrentPingMs}ms");
                        client.Queue(new SendPingRequestMessage(){
                                CurrentPingMs = playerInfo.CurrentPingMs
                        });
                    }
                    else {
                        host.Log(InternalLogLevel.INFO, $"UYA Ping NOT IN GAME YET! {client?.AccountId} (lastping: {playerInfo.CurrentPingMs}ms");
                        playerInfo.CurrentPingMs = 0;
                    }
                    await Task.Delay(PingDelayMs);
                }

                host.Log(InternalLogLevel.INFO, $"UYA Ping finished for {client?.AccountId} (lastping: {playerInfo.CurrentPingMs}ms");
            }
            catch (Exception ex)
            {
                host.Log(InternalLogLevel.ERROR, $"UYA Ping error for {client?.AccountId}: {ex}");
            }

        }
    }
}
