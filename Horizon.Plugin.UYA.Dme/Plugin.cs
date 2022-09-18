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

            return Task.CompletedTask;
        }
    }
}
