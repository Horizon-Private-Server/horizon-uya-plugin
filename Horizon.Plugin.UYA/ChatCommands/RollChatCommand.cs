using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.ChatCommands
{
    public class RollChatCommand : BaseChatCommand
    {
        private static readonly Random _rng = new Random();

        public override string Command => "roll";
        public override string Description => "Rolls a random number between 1 and 100.";

        public override Task Run(ClientObject source, string[] args)
        {
            var value = _rng.Next(0, 100) + 1;

            source.CurrentChannel.BroadcastSystemMessage(source.CurrentChannel.Clients, $"A{source.AccountName} rolled {value}");

            return Task.CompletedTask;
        }
    }
}
