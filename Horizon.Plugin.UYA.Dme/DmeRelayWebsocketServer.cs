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
        public static ConcurrentDictionary<WebSocketClient, byte> websocketConnections = new ConcurrentDictionary<WebSocketClient, byte>();

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



        public async Task DequeueTask()
        {
            int ticker = 1;
            try {
                while (true)
                            {
                                try {
                                    if (ticker == 1000) {
                                        plugin.Log(InternalLogLevel.INFO, $"Ticking! Total websocket clients: {websocketConnections.Count} | Total Queue: {Outgoing.Count}");
                                        ticker = 1;
                                    }
                                    ticker +=1 ;
                                    if (Outgoing.Count != 0) {
                                            String formatted = null;
                                            Outgoing.TryDequeue(out formatted);

                                            if (formatted == null) {
                                                plugin.Log(InternalLogLevel.INFO, $"Trying to send failing dequeue! Total websocket clients: {websocketConnections.Count} | Total Queue: {Outgoing.Count}");
                                            }

                                            List<WebSocketClient> ToRemove = new List<WebSocketClient>();
                                            foreach (var client in websocketConnections.Keys)
                                            {

                                                if (!client.IsConnected())
                                                {
                                                    ToRemove.Add(client);
                                                    plugin.Log(InternalLogLevel.INFO, $"Disconnected websocket! Total websocket clients: {websocketConnections.Count-ToRemove.Count}");
                                                }
                                                else
                                                {
                                                    if (formatted != null) {
                                                        try
                                                        {
                                                            await client.send(formatted);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            ToRemove.Add(client);
                                                            plugin.Log(InternalLogLevel.INFO, $"Error sending data! Removing client! Total websocket clients: {websocketConnections.Count-ToRemove.Count}");
                                                        }
                                                    }

                                                }
                                            }

                                            foreach (var remove in ToRemove)
                                                websocketConnections.TryRemove(remove, out _);
                                        }
                                }
                                catch (Exception ex)
                                {
                                    plugin.Log(InternalLogLevel.INFO, $"Error sending data! Removing client! Total websocket clients: {websocketConnections.Count} | Error: {ex.ToString()}");
                                }
                                await Task.Delay(1);
                            }
            }
            catch (Exception ex)
            {
                plugin.Log(InternalLogLevel.INFO, $"Error AWAITING!: {websocketConnections.Count} | Error: {ex.ToString()}");
            }
            
        }

        public async Task AcceptWebSocketClients()
        {
            CancellationToken c = new CancellationToken();
            while (true)
            {
                WebSocket socket = await Server.AcceptWebSocketAsync(c);
                if (socket != null)
                {
                    WebSocketClient socketClient = new WebSocketClient(socket);
                    websocketConnections.TryAdd(socketClient, 0);
                    plugin.Log(InternalLogLevel.INFO, $"New client added to websockets! Total websocket clients: {websocketConnections.Count} | Total Queue: {Outgoing.Count}");
                }                
                await Task.Delay(1);
            }
        }

    }
}
