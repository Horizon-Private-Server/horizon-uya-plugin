using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetPlayerPatchConfigRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 8;
        public override bool SkipEncryption { get => true; set { } }

        public PlayerConfig Config { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Config = new PlayerConfig();
            Config.Deserialize(reader);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Config.Serialize());
        }
    }
}
