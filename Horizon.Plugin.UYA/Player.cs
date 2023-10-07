using Horizon.Plugin.UYA.Messages;
using Newtonsoft.Json;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA
{
    public static class Player
    {
        private static Dictionary<int, PlayerExtraInfo> _playerExtraInfos = new Dictionary<int, PlayerExtraInfo>();

        public static async Task SetPlayerMapVersion(ClientObject client, int mapVersion)
        {
            var game = client.CurrentGame;
            if (game == null)
                return;

            var gameMetadata = await Game.GetGameMetadata(game);
            var extraInfo = GetPlayerExtraInfo(client.AccountId);
            extraInfo.CurrentMapVersion = mapVersion;


            var map = Maps.FindCustomMapById((CustomMapId)gameMetadata.GameConfig.MapOverride);
            if (map != null)
            {
                if (mapVersion == -1)
                {
                    // maps no enabled
                    //client.CurrentChannel.BroadcastSystemMessage(client.CurrentChannel.Clients, $"A{client.AccountName} does not have custom maps enabled");
                }
                else if (mapVersion == -2 && map != null)
                {
                    // player doesn't have map
                    //client.CurrentChannel.BroadcastSystemMessage(client.CurrentChannel.Clients, $"A{client.AccountName} does not have {map.MapName}");
                }
                else if (mapVersion > 0)
                {
                    // has map
                    var allPlayersResponded = true;
                    var highestVersion = 0;

                    foreach (var gameClient in game.Clients)
                    {
                        var playerMapVersion = GetPlayerExtraInfo(gameClient.Client.AccountId)?.CurrentMapVersion ?? 0;
                        if (playerMapVersion == 0)
                            allPlayersResponded = false;

                        if (playerMapVersion > highestVersion)
                            highestVersion = playerMapVersion;
                    }

                    // if all players responded then process
                    if (allPlayersResponded)
                    {
                        foreach (var gameClient in game.Clients)
                        {
                            var playerMapVersion = GetPlayerExtraInfo(gameClient.Client.AccountId)?.CurrentMapVersion ?? 0;
                            if (playerMapVersion > 0 && playerMapVersion < highestVersion)
                            {
                                //gameClient.Client.CurrentChannel.BroadcastSystemMessage(gameClient.Client.CurrentChannel.Clients, $"A{gameClient.Client.AccountName} has an old version of {map.MapName}");
                            }
                        }
                    }
                }
            }
        }

        public static Task<PlayerConfig> GetPatchConfig(ClientObject client)
        {
            var metadata = GetPlayerMetadata(client);
            if (metadata.Config == null)
                metadata.Config = new PlayerConfig();

            return Task.FromResult(metadata.Config);
        }

        public static async Task SetPatchConfig(ClientObject client, PlayerConfig config)
        {
            var metadata = GetPlayerMetadata(client);
            metadata.Config = config;
            client.Metadata = JsonConvert.SerializeObject(metadata);

            var result = await Server.Medius.Program.Database.PostAccountMetadata(client.AccountId, client.Metadata);
            if (!result)
                Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, $"Unable to post player metadata to {client.AccountId}: {client.Metadata}");
        }

        private static PlayerMetadata GetPlayerMetadata(ClientObject client)
        {
            PlayerMetadata metadata = null;
            try { metadata = JsonConvert.DeserializeObject<PlayerMetadata>(client.Metadata); } catch (Exception) { }
            if (metadata == null)
                metadata = new PlayerMetadata();

            return metadata;
        }

        public static PlayerExtraInfo GetPlayerExtraInfo(int accountId)
        {
            if (!_playerExtraInfos.TryGetValue(accountId, out var extraInfo))
                _playerExtraInfos.Add(accountId, extraInfo = new PlayerExtraInfo());

            return extraInfo;
        }

        public static Task OnPlayerLoggedOut(ClientObject client)
        {
            if (_playerExtraInfos.ContainsKey(client.AccountId))
                _playerExtraInfos.Remove(client.AccountId);

            return Task.CompletedTask;
        }
    }

    public class PlayerMetadata
    {
        public PlayerConfig Config { get; set; } = new PlayerConfig();
    }

    public class PlayerExtraInfo
    {
        public int CurrentMapVersion { get; set; }
        public byte[] PatchHash { get; set; }
    }

    public class PlayerConfig
    {
        public bool enableAutoMaps { get; set; }
        public bool disableCameraShake { get; set; }
        public byte levelOfDetail { get; set; }
        public bool enableFpsCounter { get; set; }
        public byte playerFov { get; set; }
        public bool enableSpectate { get; set; }

        public byte[] Serialize()
        {
            byte[] output = new byte[6];
            using (var ms = new MemoryStream(output, true))
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(enableAutoMaps);
                    writer.Write(disableCameraShake);
                    writer.Write(levelOfDetail);
                    writer.Write(enableFpsCounter);
                    writer.Write(playerFov);
                    writer.Write(enableSpectate);
                }
            }

            return output;
        }

        public void Deserialize(BinaryReader reader)
        {
            enableAutoMaps = reader.ReadBoolean();
            disableCameraShake = reader.ReadBoolean();
            levelOfDetail = reader.ReadByte();
            enableFpsCounter = reader.ReadBoolean();
            playerFov = reader.ReadByte();
            enableSpectate = reader.ReadBoolean();
        }
    }
}
