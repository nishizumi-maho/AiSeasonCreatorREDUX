using System.Text.Json.Serialization;

namespace AiSeasonCreator.DefaultUserSettings
{
    public class DataSyncSettings
    {
        /// <summary>Check iRacing for new season data when the app starts.</summary>
        [JsonPropertyName("autoUpdate")]
        public bool AutoUpdate { get; set; } = true;
    }
}
