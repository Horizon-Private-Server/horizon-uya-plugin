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
using System.Data.SQLite;
using BCrypt.Net;
using System.Collections.Generic;

namespace Horizon.Plugin.UYA
{
    public class SkillBoltFixer
    {

        public SkillBoltFixer(Plugin host)
        {
        }

        public static int getStat(int accountId, int statId, int appId) {
            Task<LeaderboardDTO> task = Server.Medius.Program.Database.GetPlayerLeaderboardIndex(accountId, statId, appId); // Call the async method
            task.Wait(); // Wait for the async method to complete

            if (task.Result == null){
                return 1;
            }
            
            LeaderboardDTO leaderboard = task.Result;
            int statValue = leaderboard.StatValue;
            return statValue;
        }

        public static int getTopStat(int statId, int appId) {

            //         public async Task<LeaderboardDTO[]> GetLeaderboard(int statId, int startIndex, int size, int appId)
            Task<LeaderboardDTO[]> task = Server.Medius.Program.Database.GetLeaderboard(statId, 0, 1, appId); // Call the async method
            task.Wait(); // Wait for the async method to complete

            if (task.Result == null){
                return 1;
            }
            
            LeaderboardDTO leaderboard = task.Result[0];
            int statValue = leaderboard.StatValue;
            return statValue;
        }

        public static double percentStat(int accountId, int statId, int appId) {
            int playerStat = getStat(accountId, statId, appId);
            int topStat = getTopStat(statId, appId);

            double perc = (double) playerStat/topStat;

            return perc;
        }

        public static int calculateBolt(double stat1, double stat2, double stat3, double stat4) {
            double total = stat1 + stat2 + stat3 + stat4;

            if (total < 1) {
                return 1;
            }
            if (total < 2) {
                return 2;
            }
            if (total < 3) {
                return 3;
            }
            if (total <= 4) {
                return 4;
            }

            return 1;
        }



        public static byte[] Fix(Plugin Host, ClientObject player, byte[] stats) {
            int accountId = player.AccountId;
            /*
            OVERALL
            10 - games played (0-1)
            4 - kills (0-1)
            8 - base dmg (0-1)
            1 - wins (0-1)

            min: 0
            max: 4
            0-1 -> 1
            1-2 -> 2
            2-3 -> 3
            3-4 -> 4

            CTF BOLT:
            65 - games played
            58 - ctf kills
            85 - avg flag saves
            61 - ctf base damage
            63 - ctf flags captured


            SIEGE BOLT:
            47 - games played
            45 - base dmg
            42 - siege kills
            46 - nodes

            DM BOLT:
            54 - games played
            48 - dm wins
            51 - dm kills
            70 - avg kills
            */

            double overallGamesPlayed = percentStat(accountId, 12, player.ApplicationId);
            double overallKills = percentStat(accountId, 6, player.ApplicationId);
            double overallBaseDmg = percentStat(accountId, 10, player.ApplicationId);
            double overallWins = percentStat(accountId, 3, player.ApplicationId);

            int overallRank = calculateBolt(overallGamesPlayed, overallKills, overallBaseDmg, overallWins);
        
            double ctfGamesPlayed = percentStat(accountId, 65, player.ApplicationId);
            double ctfKills = percentStat(accountId, 58, player.ApplicationId);
            double ctfAvgFlagSaves = percentStat(accountId, 85, player.ApplicationId);
            double ctfBaseDamage = percentStat(accountId, 61, player.ApplicationId);

            int ctfRank = calculateBolt(ctfGamesPlayed, ctfKills, ctfAvgFlagSaves, ctfBaseDamage);
        
            double siegeGamesPlayed = percentStat(accountId, 47, player.ApplicationId);
            double siegeBaseDamage = percentStat(accountId, 45, player.ApplicationId);
            double siegeKills = percentStat(accountId, 42, player.ApplicationId);
            double siegeNodes = percentStat(accountId, 46, player.ApplicationId);

            int siegeRank = calculateBolt(siegeGamesPlayed, siegeBaseDamage, siegeKills, siegeNodes);

            double dmGamesPlayed = percentStat(accountId, 54, player.ApplicationId);
            double dmWins = percentStat(accountId, 48, player.ApplicationId);
            double dmKills = percentStat(accountId, 51, player.ApplicationId);
            double overallAvgKills = percentStat(accountId, 70, player.ApplicationId);
        
            int dmRank = calculateBolt(dmGamesPlayed, dmWins, dmKills, overallAvgKills);

            // Host.DebugLog($"Got ACCOUNT UPDATE overallGamesPlayed {overallGamesPlayed}");
            // Host.DebugLog($"Got ACCOUNT UPDATE overallKills {overallKills}");
            // Host.DebugLog($"Got ACCOUNT UPDATE overallBaseDmg {overallBaseDmg}");
            // Host.DebugLog($"Got ACCOUNT UPDATE overallWins {overallWins}");

            // Host.DebugLog($"Got ACCOUNT UPDATE overallRank {overallRank}");

            // Host.DebugLog($"Got ACCOUNT UPDATE ctfGamesPlayed {ctfGamesPlayed}");
            // Host.DebugLog($"Got ACCOUNT UPDATE ctfKills {ctfKills}");
            // Host.DebugLog($"Got ACCOUNT UPDATE ctfAvgFlagSaves {ctfAvgFlagSaves}");
            // Host.DebugLog($"Got ACCOUNT UPDATE ctfBaseDamage {ctfBaseDamage}");

            // Host.DebugLog($"Got ACCOUNT UPDATE ctfRank {ctfRank}");

            // Host.DebugLog($"Got ACCOUNT UPDATE siegeGamesPlayed {siegeGamesPlayed}");
            // Host.DebugLog($"Got ACCOUNT UPDATE siegeBaseDamage {siegeBaseDamage}");
            // Host.DebugLog($"Got ACCOUNT UPDATE siegeKills {siegeKills}");
            // Host.DebugLog($"Got ACCOUNT UPDATE siegeNodes {siegeNodes}");

            // Host.DebugLog($"Got ACCOUNT UPDATE siegeRank {siegeRank}");

            // Host.DebugLog($"Got ACCOUNT UPDATE dmGamesPlayed {dmGamesPlayed}");
            // Host.DebugLog($"Got ACCOUNT UPDATE dmWins {dmWins}");
            // Host.DebugLog($"Got ACCOUNT UPDATE dmKills {dmKills}");
            // Host.DebugLog($"Got ACCOUNT UPDATE overallAvgKills {overallAvgKills}");

            // Host.DebugLog($"Got ACCOUNT UPDATE dmRank {dmRank}");


            Dictionary<int, List<string>> boltHexMap = new Dictionary<int, List<string>>
            {
                { 1, new List<string> { "00C0A844", "0000AF43" } },
                { 2, new List<string> { "00C0A844", "00808443" } },
                { 3, new List<string> { "00C0A844", "00000000" } },
                { 4, new List<string> { "C8C8D444", "00808943" } }
            };

            string finalHexResult = "";

            List<int> ranks = new List<int> { siegeRank, dmRank, ctfRank, overallRank };

            for (int i = 0; i < 2; i++)
            {
                foreach (int rank in ranks)
                {
                    string res = boltHexMap[rank][i];
                    finalHexResult += res;
                }
            }

            //Host.DebugLog($"Got ACCOUNT UPDATE STATS {finalHexResult}");

            byte[] newBytes = HexStringToByteArray(finalHexResult);
            for (int i = 0; i < newBytes.Length; i++) {
                stats[i] = newBytes[i];
            }
            return stats;
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

