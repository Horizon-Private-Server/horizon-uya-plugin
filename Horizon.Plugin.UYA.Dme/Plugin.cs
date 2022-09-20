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
        public static HashSet<WebSocket> websocketConnections = new HashSet<WebSocket>();

        public static WebSocketListener Server = null;

        public Task Start(string workingDirectory, IPluginHost host)
        {
            WorkingDirectory = workingDirectory;
            Host = host;

            Console.WriteLine("Starting Plugin!!!");
            StartServer();
            Console.WriteLine("Finished starting stuff!");

            return Task.CompletedTask;
        }

        public static void StartServer()
        {
            IPAddress ipAddress = IPAddress.Parse("0.0.0.0");

            IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, 9999);

            Server = new WebSocketListener(new IPEndPoint(IPAddress.Any, 8006));
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
                WebSocket client = await Server.AcceptWebSocketAsync(c);
                if (client != null)
                {
                    websocketConnections.Add(client);
                }
            }
        }


        /*
        using Socket listener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(ipEndPoint);

        listener.Listen(100);

        while (true)
        {
            Console.WriteLine("Ready to get handler...");
            Socket handler = await listener.AcceptAsync();
            websocketConnections.Add(handler);
            Console.WriteLine("Found someone!");
        }
        */
        
        public static async void SendData()
        {
            int counter = 1;
            while (true)
            {
                List<WebSocket> ToRemove = new List<WebSocket>();
                foreach (var client in websocketConnections)
                {
                    using (WebSocketMessageWriteStream messageWriterStream = client.CreateMessageWriter(WebSocketMessageType.Text))
                    using (var sw = new StreamWriter(messageWriterStream, Encoding.UTF8))
                    {
                        await sw.WriteAsync("Hello World!");
                    }
                }

                /*
                foreach (var remove in ToRemove)
                    websocketConnections.Remove(remove);
                */

                Console.WriteLine(websocketConnections.Count);
                await Task.Delay(5000);
            }
        }
        
    }
    /*
        var handler = await listener.AcceptAsync();
        int counter = 1;
        while (true)
        {
            Console.WriteLine($"Update {counter}!");
            counter++;
          // Receive message.
          var buffer = new byte[1_024];
          var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
          var response = Encoding.UTF8.GetString(buffer, 0, received);

          var eom = "<|EOM|>";
          if (response.IndexOf(eom) > -1)  // is end of message
          {
              Console.WriteLine(
                  $"Socket server received message: \"{response.Replace(eom, "")}\"");

              var ackMessage = "<|ACK|>";
              var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
              await handler.SendAsync(echoBytes, 0);
              Console.WriteLine(
                  $"Socket server sent acknowledgment: \"{ackMessage}\"");

              break;
          }

          await Task.Delay(100);
        // Sample output:
        //    Socket server received message: "Hi friends 👋!"
        //    Socket server sent acknowledgment: "<|ACK|>"
        }
        return Task.CompletedTask;
    }
    */


    /*
    try
    {
    byte [] tmp = new byte[1];

    client.Blocking = false;
    client.Send(tmp, 0, 0);
    Console.WriteLine("Connected!");
    }
    catch (SocketException e)
    {
    // 10035 == WSAEWOULDBLOCK
    if (e.NativeErrorCode.Equals(10035))
    {
    Console.WriteLine("Still Connected, but the Send would block");
    }
    else
    {
    Console.WriteLine("Disconnected: error code {0}!", e.NativeErrorCode);
    }
    }
    finally
    {
    client.Blocking = blockingState;
    }
    }
    */
}
