using System;
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
                new BasicAWSCredentials("YourAccessKey", "YourSecretKey"),
                Amazon.RegionEndpoint.USWest2
            );
        }

        async public void Trigger() {
            RunTaskRequest request = new RunTaskRequest
            {
                Cluster = "YourClusterName",
                TaskDefinition = "YourTaskDefinitionArn",
                LaunchType = LaunchType.FARGATE,
                NetworkConfiguration = new NetworkConfiguration
                {
                    AwsvpcConfiguration = new AwsVpcConfiguration
                    {
                        Subnets = new List<string> { "YourSubnetId" },
                        SecurityGroups = new List<string> { "YourSecurityGroupId" }
                    }
                }
            };

            RunTaskResponse response = await ecsClient.RunTaskAsync(request);
            if (response.Failures.Count > 0)
            {
                // Handle failures
            }
            else
            {
                // Task started successfully
                string taskArn = response.Tasks[0].TaskArn;
                Host.DebugLog("CPU Triggered successfully!");
                // Do something with the task ARN
            }


        }

    }
}

