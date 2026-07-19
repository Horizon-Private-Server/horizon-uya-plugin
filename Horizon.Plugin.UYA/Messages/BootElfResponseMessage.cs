using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class BootElfResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 31;
        public override bool SkipEncryption { get => true; set { } }

        public int BootElfId { get; set; }
        public uint Address { get; set; }
        public uint Size { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            BootElfId = reader.ReadInt32();
            Address = reader.ReadUInt32();
            Size = reader.ReadUInt32();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(BootElfId);
            writer.Write(Address);
            writer.Write(Size);
        }
    }
}
