using Horizon.Plugin.UYA.Messages;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Common;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetTeamsResponseMessage : BasePluginMessage
    {
        private static readonly Random _rng = new Random();

        public override Task Run(ClientObject source, string[] args)
        {
            var game = source.CurrentGame;
            var channel = source.CurrentChannel;

            // must be host
            if (game == null || game.Host != source)
                return Task.CompletedTask;

            // construct pool
            var numPlayers = game.Clients.Count;
            var gamemode = game.RulesSet;
            var teamsEnabled = (game.GenericField7 & (1 << 11)) != 0;
            var teamsPool = new List<int>();

            // ensure teams are enabled
            if (!teamsEnabled || gamemode == 4)
            {
                channel.BroadcastSystemMessage(channel.Clients, "ATeams are not enabled.");
                return Task.CompletedTask;
            }

            if (args.Length == 0)
            {
                // auto - assign to red/blue
                while (teamsPool.Count < numPlayers)
                {
                    teamsPool.Add(0);
                    teamsPool.Add(1);
                }
            }
            else if (args[0].ToLower() == "ffa")
            {
                if (gamemode == 0)
                {
                    channel.BroadcastSystemMessage(channel.Clients, $"A'{args[0]}' is not valid for Conquest.");
                    return Task.CompletedTask;
                }
                if (gamemode == 1)
                {
                    channel.BroadcastSystemMessage(channel.Clients, $"A'{args[0]}' is not valid for CTF.");
                    return Task.CompletedTask;
                }

                // ffa
                teamsPool = Enumerable.Range(0, 10).ToList();
            }
            else
            {
                // custom teams
                var customTeams = new List<int>();

                foreach (var arg in args)
                {
                    var teamId = GetTeamIdFromValue(arg, customTeams);
                    if (!teamId.HasValue)
                        channel.BroadcastSystemMessage(channel.Clients, $"A'{arg}' is not a valid team.");
                    else if (gamemode == 0 && teamId >= 2)
                        channel.BroadcastSystemMessage(channel.Clients, $"A'{Constants.Teams[teamId.Value]}' is not a valid team for Conquest.");
                    else if (gamemode == 1 && teamId >= 4)
                        channel.BroadcastSystemMessage(channel.Clients, $"A'{Constants.Teams[teamId.Value]}' is not a valid team for CTF.");
                    else
                        customTeams.Add(teamId.Value);
                }

                // prevent empty list of teams
                if (customTeams.Count == 0)
                    return Task.CompletedTask;

                // add to pool
                while (teamsPool.Count < numPlayers)
                    teamsPool.AddRange(customTeams);
            }

            // send to requestor
            source.Queue(new SetTeamsRequestMessage()
            {
                Seed = _rng.Next(int.MinValue, int.MaxValue),
                TeamIdPool = teamsPool
            });

            return Task.CompletedTask;
        }

        private int? GetTeamIdFromValue(string value, IEnumerable<int> excludeIds = null)
        {
            if (int.TryParse(value, out var intValue) && intValue >= 0 && intValue < 10)
                return intValue;

            for (int i = 0; i < Constants.Teams.Length; ++i)
            {
                if (excludeIds != null && excludeIds.Contains(i))
                    continue;
                if (Constants.Teams[i].StartsWith(value, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return null;
        }
    }
}
