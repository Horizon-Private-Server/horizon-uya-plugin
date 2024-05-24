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

namespace Horizon.Plugin.UYA
{
    public class Bot
    {

        public static Plugin Plugin = null;
        public static Plugin Host = null;

        private static Dictionary<int, HashSet<int>> BotProfilesLoggedIn = new Dictionary<int, HashSet<int>>();

        AmazonECSClient ecsClient = null;

        public Bot(Plugin host)
        {
            Host = host;

            Host.DebugLog("Initializing bots!");
            ecsClient = new AmazonECSClient(
                new BasicAWSCredentials(Environment.GetEnvironmentVariable("BOT_ACCESS_KEY"), Environment.GetEnvironmentVariable("BOT_SECRET_KEY")),
                Amazon.RegionEndpoint.USWest2
            );
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

        public void Trigger(List<string> accountNames, List<int> accountIds, int profile, string bot_mode, int skillLevel, int world_id) {

            if (bot_mode == "training passive" || bot_mode == "training idle") {
                List<int> profiles = getTrainingProfiles(accountNames.Count, world_id);

                for (int i = 0; i < accountNames.Count; i++) {
                    int thisProfile = profiles[i];
                    string accountName = accountNames[i];
                    int accountId = accountIds[i];
                    TriggerSingle(accountName, accountId, thisProfile, bot_mode, world_id);
                }
            }



            Host.DebugLog("CPU Triggering!");
            //Host.DebugLog($"CPU ACCOUNT NAMES: {accountNames}!");
            foreach (string accountname in accountNames)
            {
                Host.DebugLog($"CPU ACCOUNT NAME: {accountname}!");
            }
            foreach (int accountid in accountIds)
            {
                Host.DebugLog($"CPU ACCOUNT ID: {accountid}!");
            }
            Host.DebugLog($"CPU PROFILE: {profile}!");
            Host.DebugLog($"CPU BOT MODE: {bot_mode}!");
            Host.DebugLog($"CPU WORLD ID: {world_id}!");
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
                        /*
                            "local": False,
                            "profile_id": int(os.getenv("PROFILE_ID")),
                            "bot_mode": os.getenv("BOT_MODE"),
                            "account_id": int(os.getenv("ACCOUNT_ID")),
                            "account_name": os.getenv("USERNAME"),
                            "password": os.getenv("PASSWORD"),
                            "world_id": int(os.getenv("WORLD_ID")),
                            "mas_ip": os.getenv("MAS_IP"),
                            "mas_port": int(os.getenv("MAS_PORT")),
                            "mls_ip": os.getenv("MLS_IP"),
                            "mls_port": int(os.getenv("MLS_PORT")),
                            "timeout": 180,
                        */
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
                                // Add more environment variable overrides as needed
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

    }
}

