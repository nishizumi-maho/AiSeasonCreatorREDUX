using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AiSeasonCreatorREDUX.DefaultUserSettings
{
    public class FolderLocations
    {
        [JsonPropertyName("seasonsFolder")]
        public string SeasonsFolder { get; set; } = "";
        [JsonPropertyName("rostersFolder")]
        public string RostersFolder { get; set; } = "";
    }
}
