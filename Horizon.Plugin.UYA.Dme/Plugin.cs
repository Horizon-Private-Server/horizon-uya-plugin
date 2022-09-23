using DotNetty.Common.Internal.Logging;
using RT.Common;
using RT.Models;
using Server.Dme.PluginArgs;
using Server.Plugins.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// http://vtortola.github.io/WebSocketListener/
// https://github.com/vtortola/WebSocketListener
using vtortola.WebSockets;

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

        public static DmeRelayWebsocketServer SocketRelay = null;

        public Task Start(string workingDirectory, IPluginHost host)
        {
            WorkingDirectory = workingDirectory;
            Host = host;

            host.Log(InternalLogLevel.INFO, "TEST-------------------");

            Console.WriteLine("Starting Plugin!!!");
            SocketRelay = new DmeRelayWebsocketServer();
            Console.WriteLine("Finished starting stuff!");

            return Task.CompletedTask;
        }
        
    }
}
