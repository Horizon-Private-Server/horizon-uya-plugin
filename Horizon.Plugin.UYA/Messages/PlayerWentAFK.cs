﻿using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class PlayerWentAFK : BasePluginMessage
    {
        public override byte CustomMsgId => 23;
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
