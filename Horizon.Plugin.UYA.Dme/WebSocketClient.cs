using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace Horizon.Plugin.UYA.Dme
{
    public class WebSocketClient: IEquatable<WebSocketClient>
    {
        private WebSocket socket = null;

        public WebSocketClient(WebSocket socket1)
        {
            this.socket = socket1;
        }

        public bool IsConnected()
        {
            bool t = this.socket.IsConnected;            
            return t;
        }

        public async Task send(string v)
        {
            Console.WriteLine("Sending: " + v);
            using (WebSocketMessageWriteStream messageWriterStream = socket.CreateMessageWriter(WebSocketMessageType.Text))
            using (var sw = new StreamWriter(messageWriterStream, Encoding.UTF8))
            {
                if (IsConnected())
                {
                    await sw.WriteAsync(v);
                }
            }
            Console.WriteLine("Done sending!");
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
