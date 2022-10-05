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
using Server.Dme.PluginArgs;
using Server.Pipeline.Udp;


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

        public static DmeRelayWebsocketServer DmeRelay = null;

        public Task Start(string workingDirectory, IPluginHost host)
        {
            WorkingDirectory = workingDirectory;
            Host = host;

            host.RegisterAction(PluginEvent.DME_GAME_ON_RECV_UDP, OnWebsocketPacket);
            host.RegisterAction(PluginEvent.DME_GAME_ON_RECV_TCP, OnWebsocketPacket);

            DmeRelay = new DmeRelayWebsocketServer(this);

            return Task.CompletedTask;
        }

        private Task OnWebsocketPacket(PluginEvent eventId, object data)
        {
            if (eventId == PluginEvent.DME_GAME_ON_RECV_UDP)
            {
                var msg = (Server.Dme.PluginArgs.OnUdpMsg)data;
                DmeRelay.ParsePacket("udp", msg.Player, msg.Packet.Message);
            }
            else if (eventId == PluginEvent.DME_GAME_ON_RECV_TCP)
            {
                var msg = (Server.Dme.PluginArgs.OnTcpMsg)data;
                DmeRelay.ParsePacket("tcp", msg.Player, msg.Packet);
            }

            return Task.CompletedTask;

        }


        private Task OnTcpPacket(PluginEvent eventId, object data)
        {



            return Task.CompletedTask;
        }

        public void Log(InternalLogLevel level, string text)
        {
            Host.Log(level, "UYA DME Plugin Logging: " + text);
        }


    }
}
