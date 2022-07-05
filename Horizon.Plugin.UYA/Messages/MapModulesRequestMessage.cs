using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class MapModulesRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 3;
        public override bool SkipEncryption { get => true; set { } }

        public uint Module1Start { get; set; }
        public uint Module2Start { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Module1Start = reader.ReadUInt32();
            Module2Start = reader.ReadUInt32();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Module1Start);
            writer.Write(Module2Start);
        }
    }
}
