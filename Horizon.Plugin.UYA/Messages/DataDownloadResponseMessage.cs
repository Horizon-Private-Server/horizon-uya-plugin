using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class DataDownloadResponseMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 2;

        public int Id { get; set; }
        public int BytesReceived { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Id = reader.ReadInt32();
            BytesReceived = reader.ReadInt32();
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Id);
            writer.Write(BytesReceived);
        }
    }
}
