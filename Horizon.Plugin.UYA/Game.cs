using Horizon.Plugin.UYA.Messages;
using Newtonsoft.Json;
using RT.Common;
using Server.Common;
using Server.Medius.Models;
using Server.Medius.PluginArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA
{
    public static class Game
    {
        private static readonly Dictionary<int, GameMetadata> _metadatas = new Dictionary<int, GameMetadata>();

        public static async Task BroadcastGameConfig(Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);

            foreach (var gameClient in game.Clients)
            {
                // send config
                gameClient.Client.Queue(new SetGameConfigResponseMessage()
                {
                    Config = metadata.GameConfig
                });

                // send custom map override
                var map = Maps.FindCustomMapById((CustomMapId)metadata.GameConfig.MapOverride);
                await Maps.SendMapOverride(gameClient.Client, map);
            }
        }

        public static async Task BroadcastMapOverride(Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);

            // send custom map override
            var map = Maps.FindCustomMapById((CustomMapId)metadata.GameConfig.MapOverride);
            foreach (var gameClient in game.Clients)
            {
                await Maps.SendMapOverride(gameClient.Client, map);
            }
        }

        public static async Task SendMapOverride(ClientObject client)
        {
            var game = client.CurrentGame;
            if (game == null)
                return;

            var metadata = await GetGameMetadata(game);

            // send custom map override
            var map = Maps.FindCustomMapById((CustomMapId)metadata.GameConfig.MapOverride);
            await Maps.SendMapOverride(client, map);
        }

        public static async Task PlayerJoined(ClientObject client, Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);

            // send map override on join
            var map = Maps.FindCustomMapById((CustomMapId)metadata.GameConfig.MapOverride);
            await Maps.SendMapOverride(client, map);

            // send game config if not host
            if (game.Host != client)
            {
                client.Queue(new SetGameConfigResponseMessage()
                {
                    Config = metadata.GameConfig
                });
            }
        }

        public static async Task OnPlayerPostWideStats(OnPlayerWideStatsArgs args)
        {
            var game = args.Game;
            var client = args.Player;

            if (game == null)
                return;

            // must have game metadata
            if (!HasGameMetadata(game))
            {
                Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, $"{client.AccountName} sent wide stats without any gamedata");
                return;
            }

            var metadata = await GetGameMetadata(game);

            // pass to gamemode
            var mode = Modes.FindCustomModeById((CustomModeId)metadata.GameConfig.GamemodeOverride);
            if (mode != null)
            {
                await mode.OnClientPostWideStats(args);
            }

            // store new custom stats in PostStats metadata
            if (game.WorldStatus == MediusWorldStatus.WorldActive || game.WorldStatus == MediusWorldStatus.WorldClosed)
            {
                if (!args.Reject)
                {
                    if (args.IsClan)
                    {
                        if (args.Player.ClanId.HasValue)
                            metadata.PostWideStats.Clans[args.Player.ClanId.Value] = args.WideStats.ToArray();
                    }
                    else
                    {
                        metadata.PostWideStats.Players[args.Player.AccountId] = args.WideStats.ToArray();
                    }
                }
            }

            Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, $"{client.AccountName} sent wide stats.. rejected={args.Reject}");
        }

        public static async Task<bool> SetGameState(Server.Medius.Models.Game game, PackedGameState packedGameState)
        {
            var metadata = await GetGameMetadata(game);

            // update game state
            metadata.GameState = new GameState(packedGameState, game);
            metadata.GameInfo = await GetGameInfo(game, metadata);

            // send to database
            return await SetGameMetadata(game, metadata);
        }

        public static async Task<bool> SetGameConfig(Server.Medius.Models.Game game, GameConfig config)
        {
            var metadata = await GetGameMetadata(game);

            // if no change, return false
            if (metadata.GameConfig.SameAs(config))
                return false;

            // update
            metadata.GameConfig = config;

            // update other metadata
            metadata.CustomMap = Maps.FindCustomMapById((CustomMapId)metadata.GameConfig.MapOverride)?.MapName;
            metadata.CustomGameMode = Modes.FindCustomModeById((CustomModeId)metadata.GameConfig.GamemodeOverride)?.Name;
            metadata.GameInfo = await GetGameInfo(game, metadata);

            // send to database
            return await SetGameMetadata(game, metadata);
        }

        public static bool HasGameMetadata(Server.Medius.Models.Game game)
        {
            return _metadatas.ContainsKey(game.Id);
        }

        public static Task<GameMetadata> GetGameMetadata(Server.Medius.Models.Game game)
        {
            // get from cache
            if (_metadatas.TryGetValue(game.Id, out var metadata))
                return Task.FromResult(metadata);

            // try parse from game
            try { metadata = JsonConvert.DeserializeObject<GameMetadata>(game.Metadata); } catch (Exception) { }
            if (metadata == null)
                metadata = new GameMetadata();

            // add to cache
            _metadatas.Add(game.Id, metadata);

            return Task.FromResult(metadata);
        }

        public static async Task OnGameStarted(Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);

            // send map override to all clients
            await BroadcastMapOverride(game);

            // parse gamemode
            var mode = Modes.FindCustomModeById((CustomModeId)metadata.GameConfig.GamemodeOverride);
            var map = Maps.FindCustomMapById((CustomMapId)metadata.GameConfig.MapOverride);
            if (map != null && map.ModeId.HasValue)
            {
                mode = Modes.FindCustomModeById(map.ModeId.Value);
            }


            // send payloads to all clients
            foreach (var gameClient in game.Clients)
            {
                // construct payloads to send to client
                var payloads = new List<Payload>();

                if (mode != null)
                {
                    var modePayload = await mode.GetPayload(game, gameClient.Client, metadata);
                    if (modePayload != null)
                    {
                        // add mode payload
                        payloads.Add(modePayload);

                        // add mode module entry
                        payloads.Add(new Payload(0x000CF000, new PatchModuleEntry()
                        {
                            Type = PatchModuleEntryType.RUN_ONCE_GAME,
                            GameEntrypoint = modePayload.Address,
                            LobbyEntrypoint = modePayload.Address + 8,
                            LoadEntrypoint = modePayload.Address + 16,
                        }.Serialize()));
                    }
                }

                if (payloads.Count > 0)
                    await Downloader.InitiateDataDownload(gameClient.Client, 201, payloads);
            }

            // store player wide stats
            _ = Task.Run(async () =>
            {
                foreach (var gameClient in game.Clients)
                {
                    metadata.PreWideStats.Players.Add(gameClient.Client.AccountId, gameClient.Client.WideStats.ToArray());
                    metadata.PreCustomWideStats.Players.Add(gameClient.Client.AccountId, gameClient.Client.CustomWideStats.ToArray());
                    metadata.PostWideStats.Players.Add(gameClient.Client.AccountId, gameClient.Client.WideStats.ToArray());
                    metadata.PostCustomWideStats.Players.Add(gameClient.Client.AccountId, gameClient.Client.CustomWideStats.ToArray());

                    if (gameClient.Client.ClanId.HasValue)
                    {
                        var clanId = gameClient.Client.ClanId.Value;
                        if (!metadata.PreWideStats.Clans.ContainsKey(clanId))
                        {
                            var clan = await Server.Medius.Program.Database.GetClanById(clanId);
                            if (clan != null)
                            {
                                metadata.PreWideStats.Clans.Add(clanId, clan.ClanWideStats.ToArray());
                                metadata.PreCustomWideStats.Clans.Add(clanId, clan.ClanCustomWideStats.ToArray());
                                metadata.PostWideStats.Clans.Add(clanId, clan.ClanWideStats.ToArray());
                                metadata.PostCustomWideStats.Clans.Add(clanId, clan.ClanCustomWideStats.ToArray());
                            }
                        }
                    }
                }

                await SetGameMetadata(game, metadata);
            });
        }

        public static async Task OnGameEnded(Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);
            Dictionary<int, int[]> playerCustomStats = null;

            // pass to gamemode
            //var mode = Modes.FindCustomModeById((CustomModeId)metadata.GameConfig.GamemodeOverride);
            //if (mode != null)
            //    playerCustomStats = await mode.OnGameEnd(game, metadata);

            // store new custom stats in PostStats metadata
            if (playerCustomStats != null)
            {
                foreach (var kvp in playerCustomStats)
                {
                    metadata.PostCustomWideStats.Players[kvp.Key] = kvp.Value.ToArray();
                }
            }

#warning TODO: Add support for custom clan stats

            Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, "GAME ENDED");

            // send last metadata to server
            await SetGameMetadata(game, metadata);
        }

        public static async Task OnGameDestroyed(Server.Medius.Models.Game game)
        {
            Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, "GAME DESTROYED");

            var metadata = await GetGameMetadata(game);

            // send last metadata to server
            await SetGameMetadata(game, metadata);

            Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, game.Metadata);

            // remove from cache
            _metadatas.Remove(game.Id);
        }

        private static async Task<string> GetGameInfo(Server.Medius.Models.Game game, GameMetadata metadata)
        {
            string gameInfo = null;

            // let custom game mode override the gameinfo string
            var mode = Modes.FindCustomModeById((CustomModeId)metadata.GameConfig.GamemodeOverride);
            if (mode != null)
            {
                gameInfo = await mode.GetGameInfo(game, metadata);
            }

            // default game info
            if (String.IsNullOrEmpty(gameInfo))
            {
                var scoreToWin = game.GenericField3;
                var timelimit = (game.GenericField7 >> 27) & 7;
                var isLockdown = (game.GenericField7 & (1 << 12)) != 0;
                var isHomenodes = (game.GenericField7 & (1 << 6)) != 0;
                string objectiveLabel = null;

                switch (game.RulesSet)
                {
                    case 0: // siege
                        {
                            objectiveLabel = "Bolts";
                            break;
                        }
                    case 1: // ctf
                        {
                            objectiveLabel = "Flags";
                            break;
                        }
                    case 2: // dm
                        {
                            objectiveLabel = "Kills";
                            break;
                        }
                }

                // timelimit
                var time = $"{timelimit * 5} minutes";
                if (timelimit == 0)
                    time = "None";

                gameInfo += $"\nTimelimit: {time}";

                // objective
                if (!String.IsNullOrEmpty(objectiveLabel))
                {
                    var score = $"{scoreToWin}";
                    if (scoreToWin == 0)
                        score = "None";

                    gameInfo += $"\n{objectiveLabel} to win: {score}";
                }
            }

            return gameInfo?.Trim()?.Trim('\n');
        }

        private static async Task<bool> SetGameMetadata(Server.Medius.Models.Game game, GameMetadata metadata)
        {
            // add/update cache
            if (!_metadatas.ContainsKey(game.Id))
                _metadatas.Add(game.Id, metadata);
            else
                _metadatas[game.Id] = metadata;

            // save metadata to game
            game.Metadata = JsonConvert.SerializeObject(metadata);

            // send to db
            var result = await Server.Medius.Program.Database.UpdateGameMetadata(game.Id, game.Metadata);
            if (!result)
                Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.WARN, $"Unable to post game metadata {game.Id}: {game.Metadata}");

            return result;
        }
    }


    public class GameMetadata
    {
        public string CustomGameMode { get; set; }
        public string CustomMap { get; set; }
        public string GameInfo { get; set; }
        public GameConfig GameConfig { get; set; } = new GameConfig();
        public GameState GameState { get; set; } = new GameState();
        public GameStats PreWideStats { get; set; } = new GameStats();
        public GameStats PostWideStats { get; set; } = new GameStats();
        public GameStats PreCustomWideStats { get; set; } = new GameStats();
        public GameStats PostCustomWideStats { get; set; } = new GameStats();
    }

    public class GameStats
    {
        public Dictionary<int, int[]> Players { get; set; } = new Dictionary<int, int[]>();
        public Dictionary<int, int[]> Clans { get; set; } = new Dictionary<int, int[]>();
    }

    public class PackedGameState
    {
        public bool TeamsEnabled { get; set; }
        public int RoundNumber { get; set; }
        public short[] TeamScores { get; set; }
        public sbyte[] ClientIds { get; set; }
        public sbyte[] Teams { get; set; }


        public void Deserialize(BinaryReader reader)
        {
            TeamsEnabled = reader.ReadInt32() != 0;
            RoundNumber = reader.ReadInt32();

            TeamScores = new short[10];
            for (int i = 0; i < 10; ++i)
                TeamScores[i] = reader.ReadInt16();

            ClientIds = new sbyte[10];
            for (int i = 0; i < 10; ++i)
                ClientIds[i] = reader.ReadSByte();

            Teams = new sbyte[10];
            for (int i = 0; i < 10; ++i)
                Teams[i] = reader.ReadSByte();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(TeamsEnabled ? 1 : 0);
            writer.Write(RoundNumber);

            for (int i = 0; i < 10; ++i)
            {
                if (TeamScores == null || i >= TeamScores.Length)
                    writer.Write((short)0);
                else
                    writer.Write(TeamScores[i]);
            }

            for (int i = 0; i < 10; ++i)
            {
                if (ClientIds == null || i >= ClientIds.Length)
                    writer.Write((sbyte)0);
                else
                    writer.Write(ClientIds[i]);
            }

            for (int i = 0; i < 10; ++i)
            {
                if (Teams == null || i >= Teams.Length)
                    writer.Write((sbyte)0);
                else
                    writer.Write(Teams[i]);
            }
        }
    }

    public class GameState
    {
        public bool TeamsEnabled { get; set; }
        public int RoundNumber { get; set; }
        public List<GameStateTeam> Teams { get; set; } = new List<GameStateTeam>();

        public GameState()
        {

        }

        public GameState(PackedGameState packedGameState, Server.Medius.Models.Game game)
        {
            TeamsEnabled = packedGameState.TeamsEnabled;
            RoundNumber = packedGameState.RoundNumber;

            var clientIdCounter = new int[10];

            for (int i = 0; i < 10; ++i)
            {
                var clientId = packedGameState.ClientIds[i];
                var teamId = packedGameState.Teams[i];
                var player = game.Clients.FirstOrDefault(x => x.DmeId == clientId);
                if (player != null)
                {
                    clientIdCounter[clientId]++;
                    var score = packedGameState.TeamScores[teamId];
                    var name = player.Client.AccountName;
                    if (clientIdCounter[clientId] > 1)
                        name += $" ~ {clientIdCounter[clientId]}";

                    var team = this.Teams.FirstOrDefault(x => x.Id == teamId);
                    if (team == null)
                    {
                        team = new GameStateTeam()
                        {
                            Id = teamId,
                            Name = Constants.Teams.ElementAtOrDefault(teamId) ?? $"Team {teamId}",
                            Players = new List<string>(new string[]{ name }),
                            Score = score
                        };
                        Teams.Add(team);
                    }
                    else
                    {
                        team.Players.Add(name);
                    }
                }
            }
        }
    }

    public class GameStateTeam
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
        public List<string> Players { get; set; } = new List<string>();
    }

    public interface ICustomGameData
    {
        void Deserialize(BinaryReader reader);
    }

    public class GameConfig
    {
        public byte MapOverride { get; set; }
        public byte GamemodeOverride { get; set; }
        public bool grDisableWeaponPacks { get; set; }
        public bool grV2s { get; set; }
        public bool grDisableHealthBoxes { get; set; }
        public bool grAutoRespawn { get; set; }
        public bool grSetGattlingTurretHealth { get; set; }
        public byte grVampire { get; set; }
        public bool prSurvivor { get; set; }
        public bool prChargebootForever { get; set; }

        public byte[] Serialize()
        {
            byte[] output = new byte[14];
            using (var ms = new MemoryStream(output, true))
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(MapOverride);
                    writer.Write(GamemodeOverride);
                    writer.Write(grDisableWeaponPacks);
                    writer.Write(grV2s);
                    writer.Write(grDisableHealthBoxes);
                    writer.Write(grAutoRespawn);
                    writer.Write(grSetGattlingTurretHealth);
                    writer.Write(grVampire);
                    writer.Write(prSurvivor);
                    writer.Write(prChargebootForever);
                }
            }

            return output;
        }

        public void Deserialize(BinaryReader reader)
        {
            MapOverride = reader.ReadByte();
            GamemodeOverride = reader.ReadByte();
            grDisableWeaponPacks = reader.ReadBoolean();
            grV2s = reader.ReadBoolean();
            grDisableHealthBoxes = reader.ReadBoolean();
            grAutoRespawn = reader.ReadBoolean();
            grSetGattlingTurretHealth = reader.ReadBoolean();
            grVampire = reader.ReadByte();
            prSurvivor = reader.ReadBoolean();
            prChargebootForever = reader.ReadBoolean();
        }

        public bool SameAs(GameConfig other)
        {
            return MapOverride == other.MapOverride
                && GamemodeOverride == other.GamemodeOverride
                && grDisableWeaponPacks == other.grDisableWeaponPacks
                && grV2s == other.grV2s
                && grDisableHealthBoxes == other.grDisableHealthBoxes
                && grAutoRespawn == other.grAutoRespawn
                && grSetGattlingTurretHealth == other.grSetGattlingTurretHealth
                && grVampire == other.grVampire
                && prSurvivor == other.prSurvivor
                && prChargebootForever == other.prChargebootForever
                ;
        }
    }
}
