using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace Horizon.Plugin.UYA.Dme
{
    public class DmeRelayWebsocketServer
    {

        public static HashSet<WebSocketClient> websocketConnections = new HashSet<WebSocketClient>();

        public static WebSocketListener Server = null;
        public DmeRelayWebsocketServer()
        {

            int port = 9999;
            WebSocketListenerOptions options = new WebSocketListenerOptions();
            long ticksPerMs = 10000;
            long msSendTimeout = ticksPerMs * 30;
            options.WebSocketSendTimeout = new TimeSpan(msSendTimeout);
            Server = new WebSocketListener(new IPEndPoint(IPAddress.Any, port), options);
            Server.Standards.RegisterStandard(new WebSocketFactoryRfc6455());

            Console.WriteLine("Starting server ...");
            Server.StartAsync();
            Console.WriteLine("Accepting clients ...");
            AcceptWebSocketClients();
            Console.WriteLine("Sending data ...");
            SendData();

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

        public static async void SendData()
        {

            int counter = 1;
            while (true)
            {
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
                        await client.send("Hello World!" + counter);
                    }
                }


                foreach (var remove in ToRemove)
                    websocketConnections.Remove(remove);

                Console.WriteLine(websocketConnections.Count + "| Hello World!" + counter);
                counter++;
                await Task.Delay(100);
            }
        }
    }
}