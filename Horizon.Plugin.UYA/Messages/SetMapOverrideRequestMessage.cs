using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetMapOverrideRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 5;
        public override bool SkipEncryption { get => true; set { } }

        public byte MapId { get; set; }
        public string MapName { get; set; }
        public string MapFilename { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            MapId = reader.ReadByte();
            MapName = reader.ReadString(32);
            MapFilename = reader.ReadString(128);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(MapId);
            writer.Write(MapName, 32);
            writer.Write(MapFilename, 128);
        }
    }
}
