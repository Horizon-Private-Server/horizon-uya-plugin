using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetMapOverrideResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 6;
        public override bool SkipEncryption { get => true; set { } }

        public string MapFilename { get; set; }
        public int ClientMapVersion { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            MapFilename = reader.ReadString(64);
            ClientMapVersion = reader.ReadInt32();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(MapFilename, 64);
            writer.Write(ClientMapVersion);
        }
    }
}
