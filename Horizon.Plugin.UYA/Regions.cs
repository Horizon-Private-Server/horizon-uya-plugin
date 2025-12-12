using RT.Models;
using Server.Common;
using Server.Common.Stream;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA
{
    public static class Regions
    {
        public const uint EXCEPTION_HANDLER = 0x000c8000;
        public const uint UNPATCH = 0x000ce000;
        public const uint MODULE_DEFINITIONS = 0x000cf000;
        public const uint PATCH = 0x000d0000;
        public const uint CUSTOM_GAME_MODE = 0x000f0000;

        // changes if PATCH changes
        public const uint PATCH_HASH = PATCH - 0x20;
        public const uint PATCH_CONFIG = PATCH + 0x8;

    }
}