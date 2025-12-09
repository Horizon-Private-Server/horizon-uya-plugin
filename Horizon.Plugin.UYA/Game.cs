using Horizon.Plugin.UYA.Messages;
using Newtonsoft.Json;
using RT.Common;
using Server.Common;
using Server.Common.Stream;
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
                await Maps.SendMapOverride(gameClient.Client, metadata.CustomMapConfig);
            }
        }

        public static async Task BroadcastMapOverride(Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);

            // send custom map override
            foreach (var gameClient in game.Clients)
            {
                await Maps.SendMapOverride(gameClient.Client, metadata.CustomMapConfig);
            }
        }

        public static async Task SendMapOverride(ClientObject client)
        {
            var game = client.CurrentGame;
            if (game == null)
                return;

            var metadata = await GetGameMetadata(game);

            // send custom map override
            await Maps.SendMapOverride(client, metadata.CustomMapConfig);
        }

        public static async Task PlayerJoined(ClientObject client, Server.Medius.Models.Game game)
        {
            var metadata = await GetGameMetadata(game);

            // send map override on join
            await Maps.SendMapOverride(client, metadata.CustomMapConfig);

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

        public static async Task<bool> SetGameConfig(Server.Medius.Models.Game game, GameConfig config, GameCustomMapConfig mapConfig)
        {
            var metadata = await GetGameMetadata(game);

            // if no change, return false
            if (metadata.GameConfig.SameAs(config) && metadata.CustomMapConfig.SameAs(mapConfig))
                return false;

            // update
            metadata.GameConfig = config ?? new GameConfig();
            metadata.CustomMapConfig = mapConfig ?? new GameCustomMapConfig();

            // update other metadata
            metadata.CustomMap = String.IsNullOrEmpty(metadata.CustomMapConfig.Name) ? null : metadata.CustomMapConfig.Name;
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
            if (metadata.CustomMapConfig.HasMap() && metadata.CustomMapConfig.ForcedModeId != 0)
            {
                mode = Modes.FindCustomModeById((CustomModeId)metadata.CustomMapConfig.ForcedModeId);
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
                        payloads.Add(new Payload(Regions.MODULE_DEFINITIONS, new PatchModuleEntry()
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
        public GameCustomMapConfig CustomMapConfig { get; set; } = new GameCustomMapConfig();
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


        public void Deserialize(MessageReader reader)
        {
            TeamsEnabled = reader.ReadInt32() != 0;
            RoundNumber = reader.ReadInt32();

            TeamScores = new short[8];
            for (int i = 0; i < 8; ++i)
                TeamScores[i] = reader.ReadInt16();

            ClientIds = new sbyte[8];
            for (int i = 0; i < 8; ++i)
                ClientIds[i] = reader.ReadSByte();

            Teams = new sbyte[8];
            for (int i = 0; i < 8; ++i)
                Teams[i] = reader.ReadSByte();
        }

        public void Serialize(MessageWriter writer)
        {
            writer.Write(TeamsEnabled ? 1 : 0);
            writer.Write(RoundNumber);

            for (int i = 0; i < 8; ++i)
            {
                if (TeamScores == null || i >= TeamScores.Length)
                    writer.Write((short)0);
                else
                    writer.Write(TeamScores[i]);
            }

            for (int i = 0; i < 8; ++i)
            {
                if (ClientIds == null || i >= ClientIds.Length)
                    writer.Write((sbyte)0);
                else
                    writer.Write(ClientIds[i]);
            }

            for (int i = 0; i < 8; ++i)
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

            var clientIdCounter = new int[8];

            for (int i = 0; i < 8; ++i)
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
        void Deserialize(MessageReader reader);
    }

    public class GameCustomMapConfig
    {
        public string Filename { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
        public int BaseMapId { get; set; }
        public int ForcedModeId { get; set; }

        public bool HasMap() => Filename != null && Filename.Any();

        public byte[] Serialize()
        {
            byte[] output = new byte[64 + 32 + 4 + 4 + 4];
            using (var ms = new MemoryStream(output, true))
            {
                using (var writer = new MessageWriter(ms))
                {
                    writer.Write(Version);
                    writer.Write(BaseMapId);
                    writer.Write(ForcedModeId);
                    writer.Write(Name, 32);
                    writer.Write(Filename, 64);
                }
            }

            return output;
        }

        public void Deserialize(MessageReader reader)
        {
            Version = reader.ReadInt32();
            BaseMapId = reader.ReadInt32();
            ForcedModeId = reader.ReadInt32();
            Name = reader.ReadString(32);
            Filename = reader.ReadString(64);
        }

        public bool SameAs(GameCustomMapConfig other)
        {
            return Filename == other.Filename
                && Name == other.Name
                && Version == other.Version
                && BaseMapId == other.BaseMapId
                && ForcedModeId == other.ForcedModeId
                ;
        }
    }

    public class GameConfig
    {
        public byte isCustomMap { get; set; }
        public byte GamemodeOverride { get; set; }
        public byte grRadarBlipsDistance { get; set; }
        public byte grRespawnTimer_Player { get; set; }
        public bool grRespawnInvicibility { get; set; }
        public bool grDisablePenaltyTimers { get; set; }
        public bool grDisableWeaponPacks { get; set; }
        public byte grV2s { get; set; }
        public bool grNoCooldown { get; set; }
        public bool grHealthBars { get; set; }
        public byte grHealthBoxes { get; set; }
        public bool grDisableWeaponCrates { get; set; }
        public bool grDisableAmmoPickups { get; set; }
        public byte grRespawnTimer_HealthBoxes { get; set; }
        public byte grRespawnTimer_WeaponCrates { get; set; }
        public byte grRespawnTimer_AmmoPickups { get; set; }
        public bool grAutoRespawn { get; set; }
        public byte grSetGatlingTurretHealth { get; set; }
        public bool grDisableDrones { get; set; }
        public bool grDisablePlayerTurret { get; set; }
        public bool grNoBaseDefense_Bots { get; set; }
        public bool grNoBaseDefense_SmallTurrets { get; set; }
        public bool grBaseHealthPadActive { get; set; }
        public byte grVampire { get; set; }
        public bool grFluxShotsAlwaysHit { get; set; }
        public bool grFluxNikingDisabled { get; set; }
        public bool grFlagHotspots { get; set; }
        public bool grDestructableBridges { get; set; }
        public byte grSuicidePenaltyTimer { get; set; }
        public byte grAllNodesTimer { get; set; }
        public byte grNodeSelectTimer { get; set; }
        public bool grSiegeNoTies { get; set; }
        public byte grKothScoreLimit { get; set; }
        public byte grKothHillDuration { get; set; }
        public int grSeed { get; set; }
        public bool grNewPlayerSync { get; set; }
        public bool prSurvivor { get; set; }
        public bool prChargebootForever { get; set; }
        public bool prLoadoutWeaponsOnly { get; set; }
        public bool prGravityBombTweakers { get; set; }
        public bool prDisableDlStyleFlips { get; set; }

        public byte[] Serialize()
        {
            byte[] output = new byte[44];
            using (var ms = new MemoryStream(output, true))
            {
                using (var writer = new MessageWriter(ms))
                {
                    writer.Write(isCustomMap);
                    writer.Write(GamemodeOverride);
                    writer.Write(grRadarBlipsDistance);
                    writer.Write(grRespawnTimer_Player);
                    writer.Write(grRespawnInvicibility);
                    writer.Write(grDisablePenaltyTimers);
                    writer.Write(grDisableWeaponPacks);
                    writer.Write(grV2s);
                    writer.Write(grNoCooldown);
                    writer.Write(grHealthBars);
                    writer.Write(grHealthBoxes);
                    writer.Write(grDisableWeaponCrates);
                    writer.Write(grDisableAmmoPickups);
                    writer.Write(grRespawnTimer_HealthBoxes);
                    writer.Write(grRespawnTimer_WeaponCrates);
                    writer.Write(grRespawnTimer_AmmoPickups);
                    writer.Write(grAutoRespawn);
                    writer.Write(grSetGatlingTurretHealth);
                    writer.Write(grDisableDrones);
                    writer.Write(grDisablePlayerTurret);
                    writer.Write(grNoBaseDefense_Bots);
                    writer.Write(grNoBaseDefense_SmallTurrets);
                    writer.Write(grBaseHealthPadActive);
                    writer.Write(grVampire);
                    writer.Write(grFluxShotsAlwaysHit);
                    writer.Write(grFluxNikingDisabled);
                    writer.Write(grFlagHotspots);
                    writer.Write(grDestructableBridges);
                    writer.Write(grSuicidePenaltyTimer);
                    writer.Write(grAllNodesTimer);
                    writer.Write(grNodeSelectTimer);
                    writer.Write(grSiegeNoTies);
                    writer.Write(grKothScoreLimit);
                    writer.Write(grKothHillDuration);
                    writer.Write(grSeed);
                    writer.Write(grNewPlayerSync);
                    writer.Write(prSurvivor);
                    writer.Write(prChargebootForever);
                    writer.Write(prLoadoutWeaponsOnly);
                    writer.Write(prGravityBombTweakers);
                    writer.Write(prDisableDlStyleFlips);
                }
            }

            return output;
        }

        public void Deserialize(MessageReader reader)
        {
            isCustomMap = reader.ReadByte();
            GamemodeOverride = reader.ReadByte();
            grRadarBlipsDistance = reader.ReadByte();
            grRespawnTimer_Player = reader.ReadByte();
            grRespawnInvicibility = reader.ReadBoolean();
            grDisablePenaltyTimers = reader.ReadBoolean();
            grDisableWeaponPacks = reader.ReadBoolean();
            grV2s = reader.ReadByte();
            grNoCooldown = reader.ReadBoolean();
            grHealthBars = reader.ReadBoolean();
            grHealthBoxes = reader.ReadByte();
            grDisableWeaponCrates = reader.ReadBoolean();
            grDisableAmmoPickups = reader.ReadBoolean();
            grRespawnTimer_HealthBoxes = reader.ReadByte();
            grRespawnTimer_WeaponCrates = reader.ReadByte();
            grRespawnTimer_AmmoPickups = reader.ReadByte();
            grAutoRespawn = reader.ReadBoolean();
            grSetGatlingTurretHealth = reader.ReadByte();
            grDisableDrones = reader.ReadBoolean();
            grDisablePlayerTurret = reader.ReadBoolean();
            grNoBaseDefense_Bots = reader.ReadBoolean();
            grNoBaseDefense_SmallTurrets = reader.ReadBoolean();
            grBaseHealthPadActive = reader.ReadBoolean();
            grVampire = reader.ReadByte();
            grFluxShotsAlwaysHit = reader.ReadBoolean();
            grFluxNikingDisabled = reader.ReadBoolean();
            grFlagHotspots = reader.ReadBoolean();
            grDestructableBridges = reader.ReadBoolean();
            grSuicidePenaltyTimer = reader.ReadByte();
            grAllNodesTimer = reader.ReadByte();
            grNodeSelectTimer = reader.ReadByte();
            grSiegeNoTies = reader.ReadBoolean();
            grKothScoreLimit = reader.ReadByte();
            grKothHillDuration = reader.ReadByte();
            grSeed = reader.ReadInt32();
            grNewPlayerSync = reader.ReadBoolean();
            prSurvivor = reader.ReadBoolean();
            prChargebootForever = reader.ReadBoolean();
            prLoadoutWeaponsOnly = reader.ReadBoolean();
            prGravityBombTweakers = reader.ReadBoolean();
            prDisableDlStyleFlips = reader.ReadBoolean();
        }

        public bool SameAs(GameConfig other)
        {
            return isCustomMap == other.isCustomMap
                && GamemodeOverride == other.GamemodeOverride
                && grRadarBlipsDistance == other.grRadarBlipsDistance
                && grRespawnTimer_Player == other.grRespawnTimer_Player
                && grRespawnInvicibility == other.grRespawnInvicibility
                && grDisablePenaltyTimers == other.grDisablePenaltyTimers
                && grDisableWeaponPacks == other.grDisableWeaponPacks
                && grV2s == other.grV2s
                && grNoCooldown == other.grNoCooldown
                && grHealthBars == other.grHealthBars
                && grHealthBoxes == other.grHealthBoxes
                && grDisableWeaponCrates == other.grDisableWeaponCrates
                && grDisableAmmoPickups == other.grDisableAmmoPickups
                && grRespawnTimer_HealthBoxes == other.grRespawnTimer_HealthBoxes
                && grRespawnTimer_WeaponCrates == other.grRespawnTimer_WeaponCrates
                && grRespawnTimer_AmmoPickups == other.grRespawnTimer_AmmoPickups
                && grAutoRespawn == other.grAutoRespawn
                && grSetGatlingTurretHealth == other.grSetGatlingTurretHealth
                && grDisableDrones == other.grDisableDrones
                && grDisablePlayerTurret == other.grDisablePlayerTurret
                && grNoBaseDefense_Bots == other.grNoBaseDefense_Bots
                && grNoBaseDefense_SmallTurrets == other.grNoBaseDefense_SmallTurrets
                && grBaseHealthPadActive == other.grBaseHealthPadActive
                && grVampire == other.grVampire
                && grFluxShotsAlwaysHit == other.grFluxShotsAlwaysHit
                && grFluxNikingDisabled == other.grFluxNikingDisabled
                && grFlagHotspots == other.grFlagHotspots
                && grDestructableBridges == other.grDestructableBridges
                && grSuicidePenaltyTimer == other.grSuicidePenaltyTimer
                && grAllNodesTimer == other.grAllNodesTimer
                && grNodeSelectTimer == other.grNodeSelectTimer
                && grSiegeNoTies == other.grSiegeNoTies
                && grKothScoreLimit == other.grKothScoreLimit
                && grKothHillDuration == other.grKothHillDuration
                && grSeed == other.grSeed
                && grNewPlayerSync == other.grNewPlayerSync
                && prSurvivor == other.prSurvivor
                && prChargebootForever == other.prChargebootForever
                && prLoadoutWeaponsOnly == other.prLoadoutWeaponsOnly
                && prGravityBombTweakers == other.prGravityBombTweakers
                && prDisableDlStyleFlips == other.prDisableDlStyleFlips
                ;
        }
    }
}
