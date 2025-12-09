using Server.Medius.Models;
using Server.Medius.PluginArgs;
using System.IO;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.CustomModes
{
    public class KothCustomMode : BaseCustomMode
    {
        public override CustomModeId Id => CustomModeId.CMODE_ID_KOTH;
        public override string Name => "King of the Hill";

        public override Task OnClientPostWideStats(OnPlayerWideStatsArgs args)
        {
            // accept stats
            return Task.CompletedTask;
        }

        public override Task<Payload> GetPayload(Server.Medius.Models.Game game, ClientObject client, GameMetadata metadata)
        {
            var dataPath = Path.Combine(Plugin.WorkingDirectory, $"bin/patch/koth-{client.ApplicationId}.bin");
            if (File.Exists(dataPath))
                return Task.FromResult(new Payload(0x000fa000, File.ReadAllBytes(dataPath)));

            // binary not found for game mode
            return Task.FromResult<Payload>(null);
        }
    }
}
