using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Horizon.Plugin.UYA
{
    public class AppSettings
    {
        /// <summary>
        /// This settings respective app id.
        /// </summary>
        public int AppId { get; }

        /// <summary>
        /// 
        /// </summary>
        public DateTimeOffset? ScavengerHuntBeginDate { get; private set; } = null;

        /// <summary>
        /// 
        /// </summary>
        public DateTimeOffset? ScavengerHuntEndDate { get; private set; } = null;

        /// <summary>
        /// 
        /// </summary>
        public float ScavengerHuntSpawnRateFactor { get; private set; } = 1f;

        public AppSettings(int appId)
        {
            AppId = appId;
        }

        public void SetSettings(Dictionary<string, string> settings)
        {
            string prefix = Server.Medius.Program.Database.GetUsername();
            string value = null;
            DateTimeOffset dt;

            // ScavengerHuntBeginDate
            if (settings.TryGetValue($"{prefix}_ScavengerHuntBeginDate", out value) && DateTimeOffset.TryParse(value, out dt))
                ScavengerHuntBeginDate = dt;
            else
                ScavengerHuntBeginDate = null;
            // ScavengerHuntEndDate
            if (settings.TryGetValue($"{prefix}_ScavengerHuntEndDate", out value) && DateTimeOffset.TryParse(value, out dt))
                ScavengerHuntEndDate = dt;
            else
                ScavengerHuntEndDate = null;
            // ScavengerHuntSpawnRateFactor
            if (settings.TryGetValue($"{prefix}_ScavengerHuntSpawnRateFactor", out value) && float.TryParse(value, out var spawnRate))
                ScavengerHuntSpawnRateFactor = spawnRate;
        }

        public Dictionary<string, string> GetSettings()
        {
            string prefix = Server.Medius.Program.Database.GetUsername();
            return new Dictionary<string, string>()
            {
                { $"{prefix}_ScavengerHuntBeginDate", ScavengerHuntBeginDate?.ToString() },
                { $"{prefix}_ScavengerHuntEndDate", ScavengerHuntEndDate?.ToString() },
                { $"{prefix}_ScavengerHuntSpawnRateFactor", ScavengerHuntSpawnRateFactor.ToString() },
            };
        }
    }
}
