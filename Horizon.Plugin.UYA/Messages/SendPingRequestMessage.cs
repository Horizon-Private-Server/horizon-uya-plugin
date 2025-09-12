

using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers.Binary;

namespace Horizon.Plugin.UYA.Messages
{
    public class SendPingRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 26;
        public override bool SkipEncryption { get => true; set { } }

        public long NowMs { get; set; }
        public int CurrentPingMs { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Span<byte> buf = stackalloc byte[8];
            for (int i = 0; i < 8; i++)
            {
                byte b = reader.ReadByte();      // unsigned
                buf[i] = b;                      // keep raw value
            }


            NowMs = BinaryPrimitives.ReadInt64BigEndian(buf);

        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Span<byte> buf = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(buf, nowMs); // network order

            for (int i = 0; i < 8; i++)
                writer.Write(unchecked((sbyte)buf[i]));

            // Write CurrentPingMs
            Span<byte> buf32 = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buf32, CurrentPingMs);
            for (int i = 0; i < 4; i++)
                writer.Write(unchecked((sbyte)buf32[i]));

            writer.Write((sbyte)-1);
        }

        public override string ToString()
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(NowMs).UtcDateTime;
            return $"SendPingRequestMessage: NowMs={NowMs} ({dt:O})";
        }
    }
}
