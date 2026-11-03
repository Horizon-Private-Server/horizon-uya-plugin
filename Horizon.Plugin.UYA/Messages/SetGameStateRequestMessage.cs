using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetGameStateRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 25;
        public override bool SkipEncryption { get => true; set { } }

        public PackedGameState State { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            State = new PackedGameState();
            State.Deserialize(reader);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            State.Serialize(writer);
        }
    }
}
