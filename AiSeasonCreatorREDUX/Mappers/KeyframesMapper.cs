using AiSeasonCreatorREDUX.ScheduleClasses;
using AiSeasonCreatorREDUX.Data;
using AiSeasonCreatorREDUX.Views;

namespace AiSeasonCreatorREDUX.Mappers
{
    public class KeyframesMapper : IMapper<List<Keyframes>>
    {
        private const int StepMinutes = 15;

        private readonly LoadedData _loadedData;
        private readonly ISeasonView _seasonView;
        private readonly WeatherForecastProvider _weather;

        public KeyframesMapper(LoadedData loadedData, ISeasonView seasonView, WeatherForecastProvider weather)
        {
            _loadedData = loadedData;
            _seasonView = seasonView;
            _weather = weather;
        }

        public List<Keyframes> Map(int eventIndex, string eventGuid)
        {
            var season = _loadedData.SelectedSeries!;
            var forecast = _weather.GetKeyframes(season, eventIndex);
            var lkf = new List<Keyframes>();
            if (forecast == null || forecast.Count == 0)
                return lkf;

            var sst = season.Schedules[eventIndex].Weather.SimulatedStartTime;
            var index = 0;

            for (var i = 0; i < forecast.Count; i++)
            {
                var f = forecast[i];

                // AI seasons expect a keyframe every 15 minutes. Hourly forecasts are expanded by holding
                // each value until the next frame; forecasts that are already 15-minute based are copied
                // as-is (expanding those used to create overlapping, duplicated timestamps).
                var gapMinutes = i + 1 < forecast.Count
                    ? (forecast[i + 1].Timestamp - f.Timestamp).TotalMinutes
                    : i > 0 ? (f.Timestamp - forecast[i - 1].Timestamp).TotalMinutes : StepMinutes;
                var steps = Math.Clamp((int)Math.Round(gapMinutes / StepMinutes), 1, 4);

                for (var s = 0; s < steps; s++)
                {
                    var offset = s * StepMinutes;
                    lkf.Add(new Keyframes
                    {
                        Index = index,
                        Timestamp = f.Timestamp.AddMinutes(offset),
                        TimeOffset = f.TimeOffset + offset,
                        AllowPrecip = !_seasonView.NeverRain && f.AllowPrecip,
                        ValidStats = f.ValidStats,
                        AffectsSession = f.AffectsSession,
                        CloudCover = f.CloudCover,
                        AirTemp = f.AirTemp,
                        RelHumidity = f.RelHumidity,
                        Pressure = f.Pressure,
                        WindDir = f.WindDir,
                        WindSpeed = f.WindSpeed,
                        PrecipChance = _seasonView.NeverRain ? 0 : f.PrecipChance,
                        PrecipAmount = _seasonView.NeverRain ? 0 : f.PrecipAmount,
                        RawAirTemp = f.RawAirTemp,
                        IsSunUp = f.IsSunUp,
                        WeatherSeed = null,
                        SimulatedStartTime = index == 0 ? sst.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    });
                    index++;
                }
            }

            return lkf;
        }
    }
}
