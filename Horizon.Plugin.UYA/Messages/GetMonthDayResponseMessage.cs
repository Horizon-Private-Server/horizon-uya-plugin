using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class GetMonthDayResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 20;
        public override bool SkipEncryption { get => true; set { } }

        public byte Month { get; set; }
        public byte Day { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Month = reader.ReadByte();
            Day = reader.ReadByte();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Month);
            writer.Write(Day);
        }
    }
}
