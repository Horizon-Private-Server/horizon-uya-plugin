using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class GetScavengerHuntSettingsRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 17;
        public override bool SkipEncryption { get => true; set { } }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);
        }
    }
}
