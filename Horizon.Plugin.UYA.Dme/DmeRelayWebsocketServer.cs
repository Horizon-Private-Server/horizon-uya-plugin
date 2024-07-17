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

        private Task dequeueTask = null;

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

            plugin.Log(InternalLogLevel.INFO, "PLUGIN:DME: Starting DmeRelay server ...");
            Server.StartAsync();
            plugin.Log(InternalLogLevel.INFO, "PLUGIN:DME: Accepting clients ...");

            dequeueTask = Task.Run(() => DequeueTask());

            AcceptWebSocketClients();
            plugin.Log(InternalLogLevel.INFO, "PLUGIN:DME: Accepting clients ...");


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


        private void Log(string msg) {
            //plugin.Log(InternalLogLevel.INFO, msg);
        }


        public async Task DequeueTask()
        {
            int ticker = 1;
            try {
                while (true)
                            {
                                Log($"PLUGIN:DME:ITER Starting While loop!");
                                try {
                                    if (ticker == 1000) {
                                        //plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME:ITER Ticking! Total websocket clients: {websocketConnections.Count} | Total Queue: {Outgoing.Count}");
                                        ticker = 1;
                                    }
                                    ticker +=1;

                                    Log($"PLUGIN:DME:ITER Starting If statement to Outgoing.Count!");
                                    if (Outgoing.Count != 0) {

                                            int totalToDequeue = Math.Min(Outgoing.Count, 10);

                                            List<string> stringsList = new List<string>();

                                            for (int i = 0; i < totalToDequeue; i++) {
                                                String try_deq = null;
                                                Outgoing.TryDequeue(out try_deq);
                                                if (try_deq != null) {
                                                    stringsList.Add(try_deq);
                                                }
                                            }

                                            // Combine the strings in the list into the final result string
                                            String formatted = "[";
                                            for (int i = 0; i < stringsList.Count; i++)
                                            {
                                                formatted += stringsList[i];
                                                // Add comma if not the last element
                                                if (i < stringsList.Count - 1)
                                                {
                                                    formatted += ",";
                                                }
                                            }
                                            formatted += "]";


                                            // String formatted = null;
                                            // Outgoing.TryDequeue(out formatted);

                                            Log($"PLUGIN:DME:ITER Outgoing count > 0!");


                                            List<WebSocketClient> ToRemove = new List<WebSocketClient>();

                                            Log($"PLUGIN:DME:ITER Starting for loop of websocket connections!");

                                            foreach (var client in websocketConnections.Keys)
                                            {
                                                Log($"PLUGIN:DME:ITER Iterating websocket connection!");

                                                if (!client.IsConnected())
                                                {
                                                    ToRemove.Add(client);
                                                    Log($"PLUGIN:DME: Disconnected websocket! Total websocket clients: {websocketConnections.Count-ToRemove.Count}");
                                                }
                                                else
                                                {
                                                        Log($"PLUGIN:DME:ITER Pushing data!!");

                                                        try
                                                        {
                                                            Log($"PLUGIN:DME:ITER Pushing data await!!");
                                                            await client.send(formatted);
                                                            Log($"PLUGIN:DME:ITER Pushing data await done!!");
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            ToRemove.Add(client);
                                                            plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME: Error sending data! Removing client! Total websocket clients: {websocketConnections.Count-ToRemove.Count}");
                                                        }
                                                }
                                            }

                                            Log($"PLUGIN:DME:ITER Removing dead connections!");
                                            foreach (var remove in ToRemove)
                                                websocketConnections.TryRemove(remove, out _);

                                            Log($"PLUGIN:DME:ITER Done removing dead connections!");

                                        }
                                }
                                catch (Exception ex)
                                {
                                    plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME: Error sending data! Removing client! Total websocket clients: {websocketConnections.Count} | Error: {ex.ToString()}");
                                }
                                Log($"PLUGIN:DME:ITER Awaiting!");
                                await Task.Delay(1);
                                Log($"PLUGIN:DME:ITER Done Awaiting!");
                            }
            }
            catch (Exception ex)
            {
                plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME: Error AWAITING!: {websocketConnections.Count} | Error: {ex.ToString()}");
            }
            
        }

        public async Task AcceptWebSocketClients()
        {
            CancellationToken c = new CancellationToken();
            while (true)
            {
                try {
                    WebSocket socket = await Server.AcceptWebSocketAsync(c);
                    if (socket != null)
                    {
                        WebSocketClient socketClient = new WebSocketClient(socket, plugin);
                        websocketConnections.TryAdd(socketClient, 0);
                        plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME: New client added to websockets! Total websocket clients: {websocketConnections.Count} | Total Queue: {Outgoing.Count} | Dequeue task status: {dequeueTask.Status}");
                    }      
                }
                catch (Exception ex)
                {
                    plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME: Error adding new client: Total websocket clients: {websocketConnections.Count} | Total Queue: {Outgoing.Count} | Error: {ex.ToString()}");
                }
                await Task.Delay(1);
            }
        }

    }
}
