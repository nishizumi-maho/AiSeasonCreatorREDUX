using AiSeasonCreator.ScheduleClasses;
using AiSeasonCreator.FormOptions;
using AiSeasonCreator.Data;
using AiSeasonCreator.Views;

namespace AiSeasonCreator.Mappers
{
    public class WeatherMapper : IMapper<Weather>
    {
        private readonly IMapper<GuidedParameters> _guidedParametersMapper;
        private readonly IMapper<List<Keyframes>> _keyframesMapper;
        private readonly LoadedData _loadedData;
        private readonly ISeasonView _seasonView;
        private readonly WeatherForecastProvider _weather;
        public WeatherMapper(
            IMapper<GuidedParameters> guidedParametersMapper,
            IMapper<List<Keyframes>> keyframesMapper,
            LoadedData loadedData,
            ISeasonView seasonView,
            WeatherForecastProvider weather)
        {
            _weather = weather;
            _guidedParametersMapper = guidedParametersMapper;
            _keyframesMapper = keyframesMapper;
            _loadedData = loadedData;
            _seasonView = seasonView;
        }
        public Weather Map(int eventIndex, string eventGuid)
        {
            var weather = new Weather();
            var season = _loadedData.SelectedSeries!;
            var s = season.Schedules[eventIndex].Weather;

            // Forecast weather needs keyframes; without them fall back to the schedule's fixed conditions.
            var hasForecast = s.ForecastOptions != null && !_seasonView.StaticWeather &&
                              _weather.GetKeyframes(season, eventIndex) is { Count: > 0 };
            var useFixedWeather = !_seasonView.StaticWeather && s.ForecastOptions != null && !hasForecast;

            weather.TempUnits = s.TempUnits;
            weather.WindUnits = s.WindUnits;
            weather.SimulatedTimeMultiplier = s.SimulatedTimeMultiplier;
            weather.SimulatedTimeOffsets = s.SimulatedTimeOffsets?.ToList() ?? new List<int>();
            weather.WeatherVarInitial = s.WeatherVarInitial;
            weather.WeatherVarOngoing = s.WeatherVarOngoing;

            if (_seasonView.StaticWeather)
            {
                weather.Version = 2;
                weather.Type = 3;
                weather.TempValue = 78;
                weather.RelHumidity = 45;
                weather.WindDir = 0;
                weather.WindValue = 2;
                weather.Skies = 0;
                weather.Fog = 0;
                weather.AllowFog = false;
            }
            else if (useFixedWeather)
            {
                weather.Version = 2;
                weather.Type = 3;
                weather.TempValue = s.TempValue;
                weather.RelHumidity = s.RelHumidity;
                weather.WindDir = s.WindDir;
                weather.WindValue = s.WindValue;
                weather.Skies = s.Skies;
                weather.Fog = s.Fog;
                weather.AllowFog = s.AllowFog;
            }
            else
            {
                weather.Version = s.Version;
                weather.Type = s.Type;
                weather.TempValue = s.TempValue;
                weather.RelHumidity = s.RelHumidity;
                weather.WindDir = s.WindDir;
                weather.WindValue = s.WindValue;
                weather.Skies = s.Skies;
                weather.Fog = s.Fog;
                weather.AllowFog = s.AllowFog;
            }

            if (_seasonView.AfternoonRaces)
                weather.SimulatedStartTime = s.SimulatedStartTime.Date.AddHours(14);
            else
                weather.SimulatedStartTime = s.SimulatedStartTime;

            weather.TimeOfDay = 4;
            weather.TrackWater = 0;
            weather.WeatherId = "564678_" + Guid.NewGuid().ToString();
            weather.EventId = eventGuid;
            weather.Loading = false;

            if (hasForecast)
            {
                weather.GuidedParameters = _guidedParametersMapper.Map(eventIndex, eventGuid);
                //weather.WeatherSeed = s.ForecastOptions.WeatherSeed;
                weather.WeatherSeed = null;
                weather.PrecipOption = s.PrecipOption;
                weather.Keyframes = _keyframesMapper.Map(eventIndex, eventGuid);
            }

            return weather;
        }
    }
}
