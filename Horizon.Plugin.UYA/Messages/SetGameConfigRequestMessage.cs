using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetGameConfigRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 9;
        public override bool SkipEncryption { get => true; set { } }

        public GameConfig Config { get; set; }
        public GameCustomMapConfig CustomMapConfig { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Config = new GameConfig();
            Config.Deserialize(reader);
            reader.ReadBytes(2);
            CustomMapConfig = new GameCustomMapConfig();
            CustomMapConfig.Deserialize(reader);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Config.Serialize());
            writer.Write(new byte[2]);
            writer.Write(CustomMapConfig.Serialize());
        }
    }
}
