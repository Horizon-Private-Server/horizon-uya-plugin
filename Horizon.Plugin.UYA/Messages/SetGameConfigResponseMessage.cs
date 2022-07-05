using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetGameConfigResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 10;
        public override bool SkipEncryption { get => true; set { } }

        public GameConfig Config { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Config = new GameConfig();
            Config.Deserialize(reader);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Config.Serialize());
        }
    }
}
