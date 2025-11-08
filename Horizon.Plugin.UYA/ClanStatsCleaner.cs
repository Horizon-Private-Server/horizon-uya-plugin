using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Common.Internal.Logging;
using Newtonsoft.Json;
using Server.Medius;
using Server.Plugins.Interface;
using Server.Common;
using System.Text.Json;
using Server.Database.Models;
using Server.Medius.Models;
using BCrypt.Net;
using System.Collections.Generic;

namespace Horizon.Plugin.UYA
{
    public class ClanStatsCleaner
    {

        public ClanStatsCleaner(Plugin host)
        {
        }

        public static byte[] CleanStats(Plugin Host, string clanMessage, byte[] clanStats) {
            int clanTagIdxStart = 32;
            int clanTagIdxEnd = 48;
            int clanTagLength = clanTagIdxEnd - clanTagIdxStart;

            string hexString = BitConverter.ToString(clanStats).Replace("-", "");

            // Host.DebugLog($"HEX INPUT: {clanMessage} | {hexString}");

            string clanTag = hexString.Substring(clanTagIdxStart,clanTagLength);

            string[] clanTagChars = SplitClanTagIntoCharacters(clanTag);

            Dictionary<string, string> colors_map = null;

            if (clanMessage == "Colors 1") {
                colors_map = new Dictionary<string, string>
                {
                    { "3331", "3038" },
                    { "3332", "3039" },
                    { "3333", "3041" },
                    { "3334", "3042" },
                    { "3335", "3043" },
                    { "3336", "3044" },
                    { "3337", "3045" }
                };
            }
            else if (clanMessage == "Colors 2") {
                colors_map = new Dictionary<string, string>
                {
                    { "3631", "3038" },
                    { "3632", "3039" },
                    { "3633", "3041" },
                    { "3634", "3042" },
                    { "3635", "3043" },
                    { "3636", "3044" },
                    { "3637", "3045" }
                };
            }
            else if (clanMessage == "Colors 3") {
                colors_map = new Dictionary<string, string>
                {
                    { "3431", "3038" },
                    { "3432", "3039" },
                    { "3433", "3041" },
                    { "3434", "3042" },
                    { "3435", "3043" },
                    { "3436", "3044" },
                    { "3437", "3045" }
                };
            }

            if (colors_map != null) {
                // For each string character, see if it's in the map.
                for (int i = 0; i < clanTagChars.Length; i++)
                {
                    if (colors_map.ContainsKey(clanTagChars[i])) {
                        clanTagChars[i] = colors_map[clanTagChars[i]];
                    }
                }
            }

            string fixedClanTag = String.Join("", clanTagChars);


            string newHexStats = hexString.Substring(0,clanTagIdxStart) + fixedClanTag + hexString.Substring(clanTagIdxEnd,hexString.Length-clanTagIdxEnd);
            // Host.DebugLog($"Prv Hex stats: {clanMessage} | {hexString}");
            // Host.DebugLog($"Prv Hex stats: {clanMessage} | {fixedClanTag} | {hexString}");
            // Host.DebugLog($"New Hex stats: {newHexStats.Length} | {clanTag} | {newHexStats}");

            byte[] finalResult = HexStringToByteArray(newHexStats);

            return finalResult;
        }

        public static byte[] HexStringToByteArray(string hexString)
        {
            byte[] byteArray = new byte[hexString.Length / 2];
            
            for (int i = 0; i < hexString.Length; i += 2)
            {
                byteArray[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            
            return byteArray;
        }


        public static string[] SplitClanTagIntoCharacters(string clanTag) {
            // Define the length of each substring
            int substringLength = 4;

            // Calculate the number of substrings
            int numSubstrings = clanTag.Length / substringLength;

            // Create an array to hold the substrings
            string[] substrings = new string[numSubstrings];

            // Split the string into substrings
            for (int i = 0; i < numSubstrings; i++)
            {
                substrings[i] = clanTag.Substring(i * substringLength, substringLength);
            }

            return substrings;
        }
    }


}

