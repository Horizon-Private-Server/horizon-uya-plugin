using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class DataDownloadRequestMessage : BasePluginMessage
    {
        public const int MAX_DATA_SIZE = 1362;

        public override byte CustomMsgId => 1;
        public override bool SkipEncryption { get => true; set { } }

        public int Id { get; set; }
        public uint TargetAddress { get; set; }
        public int TotalSize { get; set; }
        public int DataOffset { get; set; }
        public short Chunk { get; set; }
        public byte[] Data {get;set;}

        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);

            Id = reader.ReadInt32();
            TargetAddress = reader.ReadUInt32();
            TotalSize = reader.ReadInt32();
            DataOffset = reader.ReadInt32();
            Chunk = reader.ReadInt16();
            var len = reader.ReadInt16();
            Data = reader.ReadBytes(len);
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);

            // cap length at MAX_DATA_SIZE
            var len = 0;
            if (Data != null)
                len = Data.Length > MAX_DATA_SIZE ? MAX_DATA_SIZE : Data.Length;

            writer.Write(Id);
            writer.Write(TargetAddress);
            writer.Write(TotalSize);
            writer.Write(DataOffset);
            writer.Write(Chunk);
            writer.Write((short)len);
            if (Data != null)
                writer.Write(Data, 0, len);
        }
    }
}
