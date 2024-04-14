using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using DotNetty.Common.Internal.Logging;

namespace Horizon.Plugin.UYA.Dme
{
    public class WebSocketClient: IEquatable<WebSocketClient>
    {
        private WebSocket socket = null;
        private Plugin plugin;

        private TimeSpan timeout;

        public WebSocketClient(WebSocket socket1, Plugin plugin1 )
        {
            this.socket = socket1;
            this.plugin = plugin1;
            this.timeout = TimeSpan.FromSeconds(1);
        }

        public bool IsConnected()
        {
            bool t = this.socket.IsConnected;            
            return t;
        }

        public async Task WriteWithCancellationAsync(StreamWriter writer, string text, CancellationToken cancellationToken)
        {
            // Convert the text to bytes
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            // Write asynchronously with timeout logic
            Task writeTask = writer.BaseStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            
            // Wait for the write task or cancellation token
            await Task.WhenAny(writeTask, Task.Delay(Timeout.Infinite, cancellationToken));

            // If the write task completed, check for exceptions
            if (writeTask.IsFaulted)
            {
                plugin.Log(InternalLogLevel.INFO, $"PLUGIN:DME:CLIENT FAILED TO WRITE TO CLIENT BUFFER!");
                throw writeTask.Exception ?? new Exception("WriteAsync failed");
            }
        }

        private void Log(string msg) {
            //plugin.Log(InternalLogLevel.INFO, msg);
        }

        public async Task send(string v)
        {
            Log($"PLUGIN:DME:CLIENT Starting send {v}!");

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(this.timeout);

            using (WebSocketMessageWriteStream messageWriterStream = socket.CreateMessageWriter(WebSocketMessageType.Text))
            using (var sw = new StreamWriter(messageWriterStream, Encoding.UTF8))
            {
                Log($"PLUGIN:DME:CLIENT Setting autoflush!!");
                sw.AutoFlush = true;
                Log($"PLUGIN:DME:CLIENT Checking connected!!!");
                if (IsConnected())
                {
                    Log($"PLUGIN:DME:CLIENT sending {v} ...");
                    //await sw.WriteAsync(v);
                    await WriteWithCancellationAsync(sw, v, cts.Token);
                    Log($"PLUGIN:DME:CLIENT sent {v} ...");
                }
            }
            Log($"PLUGIN:DME:CLIENT Done sending!");
            //Console.WriteLine("Done sending!");
        }

        public WebSocket GetSocket()
        {
            return this.socket;
        }

        bool IEquatable<WebSocketClient>.Equals(WebSocketClient other)
        {
            return socket == other.GetSocket();
        }

        public override int GetHashCode()
        {
            return socket.GetHashCode();
        }
    }
}
