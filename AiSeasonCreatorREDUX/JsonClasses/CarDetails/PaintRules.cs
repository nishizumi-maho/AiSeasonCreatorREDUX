using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AiSeasonCreatorREDUX.JsonClasses.CarDetails
{
    public class PaintRules
    {
        [JsonPropertyName("RestrictCustomPaint")]
        public bool RestrictCustomPaint { get; set; }
    }
}
