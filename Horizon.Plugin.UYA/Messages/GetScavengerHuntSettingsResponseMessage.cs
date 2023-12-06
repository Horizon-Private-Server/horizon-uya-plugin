using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class GetScavengerHuntSettingsResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 16;
        public override bool SkipEncryption { get => true; set { } }

        public bool Enabled { get; set; }
        public float SpawnFactor { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Enabled = reader.ReadInt32() != 0;
            SpawnFactor = reader.ReadSingle();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Enabled ? 1 : 0);
            writer.Write(SpawnFactor);
        }
    }
}
