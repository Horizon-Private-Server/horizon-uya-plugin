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

        public GameCustomMapConfig CustomMapConfig { get; set; } = new GameCustomMapConfig();

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            CustomMapConfig = new GameCustomMapConfig();
            CustomMapConfig.Deserialize(reader);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(CustomMapConfig.Serialize());
        }
    }
}
