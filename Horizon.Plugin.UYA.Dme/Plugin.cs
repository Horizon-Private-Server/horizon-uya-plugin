using RT.Common;
using RT.Models;
using Server.Dme.PluginArgs;
using Server.Plugins.Interface;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.Dme
{
    public class Plugin : IPlugin
    {
        public static string WorkingDirectory = null;
        public static IPluginHost Host = null;
        public static readonly int[] SupportedAppIds = {
            10683, // PAL
            10684, // NTSC
        };

        public Task Start(string workingDirectory, IPluginHost host)
        {
            WorkingDirectory = workingDirectory;
            Host = host;

            host.RegisterMessageAction(RT_MSG_TYPE.RT_MSG_CLIENT_ECHO, OnRecvClientEcho);
            host.RegisterMessageAction(RT_MSG_TYPE.RT_MSG_SERVER_ECHO, OnRecvServerEcho);

            return Task.CompletedTask;
        }

        async Task OnRecvClientEcho(RT_MSG_TYPE msgId, object data)
        {
            var args = data as OnMessageArgs;
            if (args == null)
                return;

            args.Player.OnRecvServerEcho(new RT_MSG_SERVER_ECHO());
        }

        async Task OnRecvServerEcho(RT_MSG_TYPE msgId, object data)
        {
            var args = data as OnMessageArgs;
            if (args == null)
                return;

            args.Ignore = true;
        }


    }
}
