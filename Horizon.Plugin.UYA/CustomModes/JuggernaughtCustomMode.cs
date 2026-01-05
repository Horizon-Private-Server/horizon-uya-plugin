using Server.Medius.Models;
using Server.Medius.PluginArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.CustomModes
{
    public class JuggernaughtCustomMode : BaseCustomMode
    {
        public override CustomModeId Id => CustomModeId.CMODE_ID_JUGGERNAUGHT;
        public override string Name => "Juggernaught";

        public override Task OnClientPostWideStats(OnPlayerWideStatsArgs args)
        {
            // reject all
            args.Reject = true;
            return Task.CompletedTask;
        }

        public override Task<string> GetGameInfo(Server.Medius.Models.Game game, GameMetadata metadata)
        {
            var timelimit = (game.GenericField7 >> 27) & 7;
            var time = $"{timelimit * 5} minutes";
            if (timelimit == 0)
                time = "None";

            var scoreToWin = "None";
            if (game.GenericField3 > 0)
                scoreToWin = game.GenericField3.ToString();

            return Task.FromResult($"Timelimit: {time}\nScore to win: {scoreToWin}");
        }

        public override Task<Payload> GetPayload(Server.Medius.Models.Game game, ClientObject client, GameMetadata metadata)
        {
            var dataPath = Path.Combine(Plugin.WorkingDirectory, $"bin/patch/Juggernaught-{client.ApplicationId}.bin");
            if (File.Exists(dataPath))
                return Task.FromResult(new Payload(Regions.CUSTOM_GAME_MODE, File.ReadAllBytes(dataPath)));

            // binary not found for game mode
            return Task.FromResult<Payload>(null);
        }
    }

}
