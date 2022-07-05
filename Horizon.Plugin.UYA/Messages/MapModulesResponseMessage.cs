using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class MapModulesResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 4;
        public override bool SkipEncryption { get => true; set { } }

        public int CustomMapsVersion { get; set; }
        public int Module1Size { get; set; }
        public int Module2Size { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            CustomMapsVersion = reader.ReadInt32();
            Module1Size = reader.ReadInt32();
            Module2Size = reader.ReadInt32();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(CustomMapsVersion);
            writer.Write(Module1Size);
            writer.Write(Module2Size);
        }
    }
}
