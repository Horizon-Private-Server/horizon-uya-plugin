using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetClientMachineIdRequest : BasePluginMessage
    {
        public override byte CustomMsgId => 14;
        public override bool SkipEncryption { get => true; set { } }

        public byte[] MachineId { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            MachineId = reader.ReadBytes(6);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(MachineId ?? new byte[6]);
        }
    }
}
