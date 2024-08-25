using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA.ChatCommands
{
    public abstract class BaseChatCommand
    {
        public abstract string Command { get; }
        public abstract string Description { get; }

        public abstract Task Run(ClientObject source, string[] args);
    }
}
