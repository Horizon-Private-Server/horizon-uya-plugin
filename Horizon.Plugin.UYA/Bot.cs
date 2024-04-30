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
        
        
        public void Trigger(int account_id, string bot_class, string username, int world_id, int bolt) {
            Host.DebugLog("CPU Triggering!");
            
            return;

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
                                new Amazon.ECS.Model.KeyValuePair { Name = "BOT_CLASS", Value = bot_class },
                                new Amazon.ECS.Model.KeyValuePair { Name = "ACCOUNT_ID", Value = account_id.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "USERNAME", Value = username },
                                new Amazon.ECS.Model.KeyValuePair { Name = "PASSWORD", Value = Environment.GetEnvironmentVariable("BOT_PASSWORD") },
                                new Amazon.ECS.Model.KeyValuePair { Name = "WORLD_ID", Value = world_id.ToString() },
                                new Amazon.ECS.Model.KeyValuePair { Name = "BOLT", Value = bolt.ToString() },
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

            System.Threading.Tasks.Task.Run(async () =>
            {
                RunTaskResponse response = await ecsClient.RunTaskAsync(request);

                // Process the response or perform other operations
            }).GetAwaiter().GetResult();

        }
        
    }
}

