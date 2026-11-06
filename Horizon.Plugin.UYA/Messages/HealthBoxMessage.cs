using Server.Common.Stream;

namespace Horizon.Plugin.UYA.Messages
{
	public enum HealthBoxAction : uint
	{
		Hello, Ready, Request, Grant, Applied, Failed, Busy, CatchUp, Consume, Respawn
	}

	// Wire payload: seven little-endian 32-bit words after the custom-message header.
	public class HealthBoxMessage : BasePluginMessage
	{
		public const byte Id = 37;
		public override byte CustomMsgId => Id;
		public override bool SkipEncryption { get => true; set { } }
		public HealthBoxAction Action;
		public uint Session, BoxUid, PlayerUid, Token, PreviousToken;
		public int RespawnMs;

		public override void Deserialize(MessageReader reader)
		{
			base.Deserialize(reader);
			Action = (HealthBoxAction)reader.ReadUInt32();
			Session = reader.ReadUInt32();
			BoxUid = reader.ReadUInt32();
			PlayerUid = reader.ReadUInt32();
			Token = reader.ReadUInt32();
			PreviousToken = reader.ReadUInt32();
			RespawnMs = reader.ReadInt32();
		}

		public override void Serialize(MessageWriter writer)
		{
			base.Serialize(writer);
			writer.Write((uint)Action);
			writer.Write(Session);
			writer.Write(BoxUid);
			writer.Write(PlayerUid);
			writer.Write(Token);
			writer.Write(PreviousToken);
			writer.Write(RespawnMs);
		}
	}
}
