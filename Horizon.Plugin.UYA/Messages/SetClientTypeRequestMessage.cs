using RT.Common;
using RT.Models;
using Server.Common.Stream;

namespace Horizon.Plugin.UYA.Messages
{
    public class SetClientTypeRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 32;
        public override bool SkipEncryption { get => true; set { } }

        public PlayerClientType ClientType { get; set; }
        public byte[] MachineId { get; set; }

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            ClientType = (PlayerClientType)reader.ReadInt32();
            MachineId = reader.ReadBytes(6);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            writer.Write((int)ClientType);
            writer.Write(MachineId ?? new byte[6]);
        }
    }
}