using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Common.Internal.Logging;
using RT.Common;
using Server.Dme.Models;
using Server.Pipeline.Udp;
using vtortola.WebSockets;

namespace Horizon.Plugin.UYA.Dme
{
    public class DmeRelayWebsocketServer
    {

        public static ConcurrentQueue<String> Outgoing = new ConcurrentQueue<String>();
        public static HashSet<WebSocketClient> websocketConnections = new HashSet<WebSocketClient>();

        public static WebSocketListener Server = null;
        private Plugin plugin;

        public DmeRelayWebsocketServer(Plugin plugin)
        {
            this.plugin = plugin;

            int port = 8765;
            WebSocketListenerOptions options = new WebSocketListenerOptions();
            long ticksPerMs = 10000;
            long msSendTimeout = ticksPerMs * 30;
            options.WebSocketSendTimeout = new TimeSpan(msSendTimeout);
            Server = new WebSocketListener(new IPEndPoint(IPAddress.Any, port), options);
            Server.Standards.RegisterStandard(new WebSocketFactoryRfc6455());

            plugin.Log(InternalLogLevel.INFO, "Starting DmeRelay server ...");
            Server.StartAsync();
            plugin.Log(InternalLogLevel.INFO, "Accepting clients ...");

            AcceptWebSocketClients();
            plugin.Log(InternalLogLevel.INFO, "Accepting clients ...");


            DequeueTask();

        }

         public void ParsePacket(String MsgType, ClientObject player, RT.Models.BaseScertMessage packet)
        {
            if (packet == null || player == null)
                return;
            var id = packet.Id;
            int DmeWorldId = player.DmeWorld.WorldId;
            int DmeSrc = player.DmeId;
            int DmeDst = -1;
            String data = null;

            if (id == RT_MSG_TYPE.RT_MSG_CLIENT_APP_BROADCAST)
            {
                //plugin.Log(InternalLogLevel.INFO, BaseMsg.ToString());
                data = packet.ToString().Split("Contents:")[1].Replace("-", "");
            }
            else if (id == RT_MSG_TYPE.RT_MSG_CLIENT_APP_SINGLE)
            {
                var msg = (RT.Models.RT_MSG_CLIENT_APP_SINGLE)packet;

                DmeDst = msg.TargetOrSource;

                data = BitConverter.ToString(msg.Payload).Replace("-", "");
            }
            else
            {
                return;
            }

            Relay(MsgType, DmeWorldId, DmeSrc, DmeDst, data);
        }

        private void Relay(string msgType, int dmeWorldId, int dmeSrc, int dmeDst, string data)
        {
            var formatted = "{\"type\": \"" + msgType + "\", \"dme_world_id\": " + dmeWorldId + ", \"src\": " + dmeSrc + ", \"dst\": " + dmeDst + ", \"data\": \"" + data + "\"}";

            Outgoing.Enqueue(formatted);

            return;
        }


        public static async void DequeueTask()
        {

            while (true)
            {
                if (Outgoing.Count != 0) {
                    String formatted = null;
                    Outgoing.TryDequeue(out formatted);


                    List<WebSocketClient> ToRemove = new List<WebSocketClient>();
                    foreach (var client in websocketConnections)
                    {

                        if (!client.IsConnected())
                        {
                            Console.WriteLine("Disconnected websocket!");
                            ToRemove.Add(client);
                        }
                        else
                        {
                            await client.send(formatted);
                        }
                    }

                    foreach (var remove in ToRemove)
                        websocketConnections.Remove(remove);
                }

                await Task.Delay(1);

            }

        }

        public static async void AcceptWebSocketClients()
        {
            CancellationToken c = new CancellationToken();
            while (true)
            {
                WebSocket socket = await Server.AcceptWebSocketAsync(c);
                if (socket != null)
                {
                    WebSocketClient socketClient = new WebSocketClient(socket);
                    websocketConnections.Add(socketClient);
                }
            }
        }

    }
}
