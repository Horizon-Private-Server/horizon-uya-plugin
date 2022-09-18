using DotNetty.Common.Internal.Logging;
using RT.Common;
using RT.Models;
using Server.Dme.PluginArgs;
using Server.Plugins.Interface;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

            var s = StartServer();

            return Task.CompletedTask;
        }

        public static async Task<Task> StartServer()
        {
            Console.WriteLine("Test Test Test");
            Console.WriteLine("Test Test Test");
            IPAddress ipAddress = IPAddress.Parse("0.0.0.0");

            IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, 9999);

            using Socket listener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine("Socket listener created");

            listener.Bind(ipEndPoint);
            Console.WriteLine("Bound");

            listener.Listen(100);
            Console.WriteLine("Listening...");


            while (true)
            {
                Console.WriteLine("Ready to get handler...");
                Socket handler = await listener.AcceptAsync();
                Console.WriteLine("Found someone!");

                //await Task.Delay(10);
            }
            return Task.CompletedTask;
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
