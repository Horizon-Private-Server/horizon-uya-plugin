using Server.Medius.Models;
using Server.Medius.PluginArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.CustomModes
{
    public abstract class BaseCustomMode
    {
        public abstract CustomModeId Id { get; }
        public abstract string Name { get; }

        public virtual Task OnClientPostWideStats(OnPlayerWideStatsArgs args)
        {
            args.Reject = true; // reject by default
            return Task.CompletedTask;
        }

        public virtual Task<string> GetGameInfo(Server.Medius.Models.Game game, GameMetadata metadata)
        {
            return Task.FromResult<string>(null);
        }

        public abstract Task<Payload> GetPayload(Server.Medius.Models.Game game, GameMetadata metadata);


    }
}
