using RT.Common;
using RT.Models;
using Server.Common;
using Server.Common.Stream;
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA.Messages
{
    public class BotInviteRequestMessage : BasePluginMessage
    {
        public override byte CustomMsgId => 23;
        public override bool SkipEncryption { get => true; set { } }

        public int NumBotsToInvite = 0;
        public string BotMode = "training idle";
        public int Difficulty = 0;
        public int Profile = 0;
        
        public override void Deserialize(MessageReader reader)
        {
            base.Deserialize(reader);
            byte RawNumBotsToInvite = reader.ReadByte();
            byte RawBotMode = reader.ReadByte();
            byte RawDiffulty = reader.ReadByte();
            byte RawProfile = reader.ReadByte();
            
            NumBotsToInvite = (int)RawNumBotsToInvite;

            switch (RawBotMode) {
                case 0:
                    BotMode = "dynamic";
                    break;
                case 1:
                    BotMode = "training idle";
                    break;
                case 2:
                    BotMode = "training passive";
                    break;
            }

            Difficulty = (int)RawDiffulty+1;
            // if (RawDiffulty <= 5) {
            //     Difficulty = (int)RawDiffulty + 5;
            // }
            // else {
            //     Difficulty = (int)RawDiffulty - 5;
            // }

            Profile = (int)RawProfile;
        }

        public override void Serialize(MessageWriter writer)
        {
            base.Serialize(writer);
        }

        public override string ToString()
        {
            return $"BotInviteRequestMessage: NumBotsToInvite:{NumBotsToInvite}; BotMode:{BotMode}; Difficulty:{Difficulty}; Profile:{Profile}; ";
        }
    }
}
