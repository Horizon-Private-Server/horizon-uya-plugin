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
using ECSTask = Amazon.ECS.Model.Task;

namespace Horizon.Plugin.UYA
{
    public class Bot
    {

        public static Plugin Plugin = null;
        public static Plugin Host = null;

        private static Dictionary<int, HashSet<int>> BotProfilesLoggedIn = new Dictionary<int, HashSet<int>>();
        private static Dictionary<int, HashSet<int>> profileDifficulty = new Dictionary<int, HashSet<int>>();

        AmazonECSClient ecsClient = null;

        // Names of every environment variable required to launch a bot task.
        private static readonly string[] RequiredEnvVars = new string[] {
            "BOT_ACCESS_KEY", "BOT_SECRET_KEY", "BOT_CLUSTER", "BOT_TASK", "BOT_SUBNET",
            "BOT_SECURITYGROUP", "BOT_CONTAINER", "BOT_PASSWORD", "BOT_SERVER_IP",
            "BOT_MAS_PORT", "BOT_MLS_PORT"
        };

        public Bot(Plugin host)
        {
            Host = host;

            Host.DebugLog("Initializing bots!");

            List<string> missing = RequiredEnvVars
                .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                .ToList();

            if (missing.Count > 0)
                Host.DebugLog($"BOT CONFIG ERROR: missing/empty environment variables: {string.Join(", ", missing)}. Bot tasks will fail to launch.");
            else
                Host.DebugLog("BOT CONFIG: all required environment variables are set.");

            try
            {
                ecsClient = new AmazonECSClient(
                    new BasicAWSCredentials(Environment.GetEnvironmentVariable("BOT_ACCESS_KEY"), Environment.GetEnvironmentVariable("BOT_SECRET_KEY")),
                    Amazon.RegionEndpoint.USWest2
                );
            }
            catch (Exception ex)
            {
                Host.DebugLog($"BOT CONFIG ERROR: failed to create ECS client: {ex.GetType().Name}: {ex.Message}");
            }

            populateProfileDifficulty();
        }

        // Reads an environment variable, logging when it is missing or empty.
        private string GetEnvVar(string name, string context)
        {
            string value = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(value))
                Host.DebugLog($"BOT ENV MISSING: {name} is not set | {context}");

            return value;
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

            if (bot_mode == "training passive" || bot_mode == "training idle" || bot_mode == "static") {
                profiles = getTrainingProfiles(accountNames.Count, world_id);
            }
            else if (bot_mode == "dynamic") {
                profiles = getDynamicProfiles(accountNames.Count, skillLevel, world_id);
            }
            else if (bot_mode == "god") {
                profiles = getDynamicProfiles(accountNames.Count, 7, world_id);
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

            string context = $"{accountName},{accountId},{profile},{bot_mode},{world_id}";

            if (ecsClient == null)
            {
                Host.DebugLog($"BOT TASK ERROR: ECS client was never created, cannot launch task | {context}");
                return;
            }

            RunTaskRequest request = new RunTaskRequest
            {
                Cluster = GetEnvVar("BOT_CLUSTER", context),
                TaskDefinition = GetEnvVar("BOT_TASK", context),
                LaunchType = LaunchType.FARGATE,
                NetworkConfiguration = new NetworkConfiguration
                {
                    AwsvpcConfiguration = new AwsVpcConfiguration
                    {
                        Subnets = new List<string> { GetEnvVar("BOT_SUBNET", context) },
                        SecurityGroups = new List<string> { GetEnvVar("BOT_SECURITYGROUP", context) },
                        AssignPublicIp = AssignPublicIp.ENABLED
                    }
                },
                Overrides = new TaskOverride
                {
                    ContainerOverrides = new List<ContainerOverride>
                    {
                        new ContainerOverride
                        {
                            Name = GetEnvVar("BOT_CONTAINER", context),
                            Environment = new List<Amazon.ECS.Model.KeyValuePair>
                            {
                                new Amazon.ECS.Model.KeyValuePair { Name = "BOT_MODE", Value = bot_mode },
                                new Amazon.ECS.Model.KeyValuePair { Name = "ACCOUNT_ID", Value = accountId.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "PROFILE_ID", Value = profile.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "USERNAME", Value = accountName },
                                new Amazon.ECS.Model.KeyValuePair { Name = "PASSWORD", Value = GetEnvVar("BOT_PASSWORD", context) },
                                new Amazon.ECS.Model.KeyValuePair { Name = "WORLD_ID", Value = world_id.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MAS_IP", Value = GetEnvVar("BOT_SERVER_IP", context) },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MAS_PORT", Value = GetEnvVar("BOT_MAS_PORT", context) },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MLS_IP", Value = GetEnvVar("BOT_SERVER_IP", context) },
                                new Amazon.ECS.Model.KeyValuePair { Name = "MLS_PORT", Value = GetEnvVar("BOT_MLS_PORT", context) }
                            }
                        }
                    }
                }
            };

            _ = RunTaskLoggedAsync(request, context);
        }

        private async System.Threading.Tasks.Task RunTaskLoggedAsync(RunTaskRequest request, string context)
        {
            try
            {
                RunTaskResponse response = await ecsClient.RunTaskAsync(request);

                // ECS can return HTTP 200 while still failing to place the task
                if (response.Failures != null && response.Failures.Count > 0)
                {
                    foreach (Failure failure in response.Failures)
                        Host.DebugLog($"BOT TASK FAILED: {context} | arn={failure.Arn} | reason={failure.Reason} | detail={failure.Detail}");
                }

                if (response.Tasks != null && response.Tasks.Count > 0)
                {
                    foreach (ECSTask task in response.Tasks)
                        Host.DebugLog($"BOT TASK STARTED: {context} | taskArn={task.TaskArn} | status={task.LastStatus}");
                }
                else if (response.Failures == null || response.Failures.Count == 0)
                {
                    Host.DebugLog($"BOT TASK FAILED: {context} | no tasks and no failures returned (http={response.HttpStatusCode})");
                }
            }
            catch (Exception ex)
            {
                Host.DebugLog($"BOT TASK ERROR: {context} | {ex.GetType().Name}: {ex.Message}");
            }
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

