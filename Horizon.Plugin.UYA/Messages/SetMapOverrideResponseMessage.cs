using RT.Common;
using RT.Models;
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

        public int ClientMapVersion { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            ClientMapVersion = reader.ReadInt32();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(ClientMapVersion);
        }
    }
}
