using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
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
using Amazon;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Runtime;
using System.Linq;

namespace Horizon.Plugin.UYA
{
    public class Bot
    {

        public static Plugin Plugin = null;
        public static Plugin Host = null;

        private static Dictionary<int, HashSet<int>> BotProfilesLoggedIn = new Dictionary<int, HashSet<int>>();
        private static Dictionary<int, HashSet<int>> profileDifficulty = new Dictionary<int, HashSet<int>>();

        AmazonECSClient ecsClient = null;

        public Bot(Plugin host)
        {
            Host = host;

            Host.DebugLog("Initializing bots!");
            ecsClient = new AmazonECSClient(
                new BasicAWSCredentials(Environment.GetEnvironmentVariable("BOT_ACCESS_KEY"), Environment.GetEnvironmentVariable("BOT_SECRET_KEY")),
                Amazon.RegionEndpoint.USWest2
            );
            populateProfileDifficulty();
        }

        public List<int> getTrainingProfiles(int numProfiles, int world_id) {
            List<int> result = new List<int>();
            Random random = new Random();
            int randomNumber = 0;

            int maxProfileNum = 160;

            while (result.Count != numProfiles) {
                randomNumber = random.Next(1, maxProfileNum);

                while (result.Contains(randomNumber)) {
                    randomNumber = random.Next(1, maxProfileNum);
                }
                result.Add(randomNumber);
            }

            return result;
        }

        public List<int> getDynamicProfiles(int numProfiles, int skillLevel, int world_id) {
            List<int> result = new List<int>();

            int profile = 0;

            for (int i = 0; i < numProfiles; i++) {
                profile = getProfileMatchDifficulty(world_id, skillLevel);
                Host.DebugLog($"Picked Profile: {profile}");
                BotProfilesLoggedIn[world_id].Add(profile);
                result.Add(profile);
            }

            return result;
        }


        public int getProfileMatchDifficulty(int world_id, int skillLevel) {
            HashSet<int> current = BotProfilesLoggedIn[world_id];
            HashSet<int> profilesDifficulty = new HashSet<int>(profileDifficulty[skillLevel]);

            profilesDifficulty.ExceptWith(current);

            if (profilesDifficulty.Count > 0) {
                Random rand = new Random();
                return profilesDifficulty.ElementAt(rand.Next(profilesDifficulty.Count));
            }
            else { // Get a match with lower skill level
                return getProfileMatchDifficulty(world_id, skillLevel - 1);
            }
        }

        public void Trigger(List<string> accountNames, List<int> accountIds, int profile, string bot_mode, int skillLevel, int world_id) {
            Host.DebugLog($"CPU Triggering: {accountNames.Count} | {accountIds.Count} | {profile} | {bot_mode} | {skillLevel} | {world_id}");

            if (!BotProfilesLoggedIn.ContainsKey(world_id)) {
                BotProfilesLoggedIn[world_id] = new HashSet<int>();
            }

            List<int> profiles = null;

            if (bot_mode == "training passive" || bot_mode == "training idle") {
                profiles = getTrainingProfiles(accountNames.Count, world_id);
            }
            else if (bot_mode == "dynamic") {
                profiles = getDynamicProfiles(accountNames.Count, skillLevel, world_id);
            }


            for (int i = 0; i < accountNames.Count; i++) {
                int thisProfile = profiles[i];
                string accountName = accountNames[i];
                int accountId = accountIds[i];
                TriggerSingle(accountName, accountId, thisProfile, bot_mode, world_id);
            }

        }


        public void TriggerSingle(string accountName, int accountId, int profile, string bot_mode, int world_id) {
            Host.DebugLog($"TRIGGER SINGLE: {accountName},{accountId},{profile},{bot_mode},{world_id}!");

            RunTaskRequest request = new RunTaskRequest
            {
                Cluster = Environment.GetEnvironmentVariable("BOT_CLUSTER"),
                TaskDefinition = Environment.GetEnvironmentVariable("BOT_TASK"),
                LaunchType = LaunchType.FARGATE,
                NetworkConfiguration = new NetworkConfiguration
                {
                    AwsvpcConfiguration = new AwsVpcConfiguration
                    {
                        Subnets = new List<string> { Environment.GetEnvironmentVariable("BOT_SUBNET") },
                        SecurityGroups = new List<string> { Environment.GetEnvironmentVariable("BOT_SECURITYGROUP") },
                        AssignPublicIp = AssignPublicIp.ENABLED
                    }
                },
                Overrides = new TaskOverride
                {
                    ContainerOverrides = new List<ContainerOverride>
                    {
                        new ContainerOverride
                        {
                            Name = Environment.GetEnvironmentVariable("BOT_CONTAINER"),
                            Environment = new List<Amazon.ECS.Model.KeyValuePair>
                            {
                                new Amazon.ECS.Model.KeyValuePair { Name = "BOT_MODE", Value = bot_mode },
                                new Amazon.ECS.Model.KeyValuePair { Name = "ACCOUNT_ID", Value = accountId.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "PROFILE_ID", Value = profile.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "USERNAME", Value = accountName },
                                new Amazon.ECS.Model.KeyValuePair { Name = "PASSWORD", Value = Environment.GetEnvironmentVariable("BOT_PASSWORD") },
                                new Amazon.ECS.Model.KeyValuePair { Name = "WORLD_ID", Value = world_id.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MAS_IP", Value = Environment.GetEnvironmentVariable("BOT_SERVER_IP") },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MAS_PORT", Value = Environment.GetEnvironmentVariable("BOT_MAS_PORT") },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MLS_IP", Value = Environment.GetEnvironmentVariable("BOT_SERVER_IP") },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MLS_PORT", Value = Environment.GetEnvironmentVariable("BOT_MLS_PORT") }
                            }
                        }
                    }
                }
            };

            _ = ecsClient.RunTaskAsync(request);

            // System.Threading.Tasks.Task.Run(async () =>
            // {
            //     RunTaskResponse response = await ecsClient.RunTaskAsync(request);

            //     // Process the response or perform other operations
            // }).GetAwaiter().GetResult();
        }

        public void populateProfileDifficulty() {
            profileDifficulty[10] = new HashSet<int> { 1, 2, 3, 4, 5 };
            profileDifficulty[9] = new HashSet<int> { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };
            profileDifficulty[8] = new HashSet<int> { 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50 };
            profileDifficulty[7] = new HashSet<int> { 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83 };
            profileDifficulty[6] = new HashSet<int> { 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106 };
            profileDifficulty[5] = new HashSet<int> { 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122 };
            profileDifficulty[4] = new HashSet<int> { 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137 };
            profileDifficulty[3] = new HashSet<int> { 138, 139, 140, 142, 143, 145, 146, 147 };
            profileDifficulty[1] = new HashSet<int> { 141, 155, 156, 157, 158, 159, 160, 161 };
            profileDifficulty[2] = new HashSet<int> { 144, 148, 149, 150, 151, 152, 153, 154 };
        }    
    }
}

