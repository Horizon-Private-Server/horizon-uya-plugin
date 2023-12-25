using Horizon.Plugin.UYA.CustomModes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Horizon.Plugin.UYA
{
    public static class Modes
    {
        static readonly BaseCustomMode[] CustomModes = new BaseCustomMode[]
        {
            new SpleefCustomMode(),
            new InfectedCustomMode(),
            new JuggernaughtCustomMode()
        };

        public static BaseCustomMode FindCustomModeById(CustomModeId id)
        {
            return CustomModes.FirstOrDefault(x => x.Id == id);
        }

    }

    public enum CustomModeId : sbyte
    {
        // custom mode ids
        CMODE_ID_INFECTED = 1,
        CMODE_ID_JUGGERNAUGHT = 2,

        // reserved for custom maps
        CMODE_ID_SPLEEF = -1,
    }

}
