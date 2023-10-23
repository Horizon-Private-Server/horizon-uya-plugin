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
            new SpleefCustomMode()
        };

        public static BaseCustomMode FindCustomModeById(CustomModeId id)
        {
            return CustomModes.FirstOrDefault(x => x.Id == id);
        }

    }


    public enum CustomModeId : sbyte
    {
        // custom mode ids
        // CMODE_ID_EXAMPLE = 1,

        /*
        CMODE_ID_GUN_GAME = 1,
        CMODE_ID_INFECTED = CMODE_ID_GUN_GAME + 1,
        CMODE_ID_INFINITE_CLIMBER = CMODE_ID_INFECTED + 1,
        CMODE_ID_PAYLOAD = CMODE_ID_INFINITE_CLIMBER + 1,
        CMODE_ID_SEARCH_AND_DESTROY = CMODE_ID_PAYLOAD + 1,
        CMODE_ID_SURVIVAL = CMODE_ID_SEARCH_AND_DESTROY + 1,
        CMODE_ID_THOUSAND_KILLS = CMODE_ID_SURVIVAL + 1,
        CMODE_ID_GRIDIRON = CMODE_ID_THOUSAND_KILLS + 1,
        CMODE_ID_TEAM_DEFENDER = CMODE_ID_GRIDIRON + 1,

        // reserved for custom maps
        CMODE_ID_DUCK_HUNT = -1,
        CMODE_ID_SPLEEF = -2,
        CMODE_ID_HOVERBIKE_RACE = -3,
        */
        CMODE_ID_SPLEEF = -1,
    }

}
