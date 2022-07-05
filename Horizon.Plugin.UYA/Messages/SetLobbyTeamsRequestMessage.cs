using RT.Common;
using RT.Models;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetLobbyTeamsRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 255;
        public override bool SkipEncryption { get => true; set { } }

        public int Seed { get; set; }
        public List<int> TeamIdPool { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Seed = reader.ReadInt32();
            var len = reader.ReadInt32();
            for (int i = 0; i < 10; ++i)
            {
                var teamId = reader.ReadSByte();
                if (teamId >= 0 && teamId < 10)
                    TeamIdPool.Add(teamId);
            }
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Seed);
            writer.Write(TeamIdPool.Count);
            for (int i = 0; i < 10; ++i)
            {
                if (i < TeamIdPool.Count)
                    writer.Write((sbyte)TeamIdPool[i]);
                else
                    writer.Write((sbyte)-1);
            }
        }
    }
}
