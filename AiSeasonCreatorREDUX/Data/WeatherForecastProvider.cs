using AiSeasonCreatorREDUX.Helpers;
using AiSeasonCreatorREDUX.JsonClasses.FullSchedule;
using AiSeasonCreatorREDUX.Online;
using AiSeasonCreatorREDUX.Repos;
using iRacingWeatherURLParser.WeatherSchedule;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiSeasonCreatorREDUX.Data
{
    public class WeatherPrefetchResult
    {
        public int Missing { get; set; }
        public bool LinksExpired { get; set; }
    }

    /// <summary>
    /// Supplies the real forecast (weather keyframes) for each race week. Every schedule entry has a
    /// signed "weather_url"; forecasts are downloaded the first time a season is created and cached
    /// forever (a given week's forecast never changes).
    /// </summary>
    public class WeatherForecastProvider
    {
        private readonly LoadedData _loadedData;
        private readonly IJsonRepo _jsonRepo;
        private WeatherSchedule? _bundled;

        public WeatherForecastProvider(LoadedData loadedData, IJsonRepo jsonRepo)
        {
            _loadedData = loadedData;
            _jsonRepo = jsonRepo;
        }

        /// <summary>Makes sure the forecasts for the given weeks are on disk.</summary>
        public async Task<WeatherPrefetchResult> PrefetchAsync(FullSchedule season, IEnumerable<int> scheduleIndexes, CancellationToken cancellationToken)
        {
            var result = new WeatherPrefetchResult();

            foreach (var index in scheduleIndexes)
            {
                var week = season.Schedules[index];
                if (week.Weather?.ForecastOptions == null || File.Exists(CachePath(season, week)))
                    continue;

                if (week.Weather.WeatherURL is not { IsAbsoluteUri: true } url)
                {
                    result.Missing++;
                    continue;
                }

                try
                {
                    var json = await IRacingDataClient.DownloadStringAsync(url.AbsoluteUri, cancellationToken);
                    var keyframes = ParseForecast(json);
                    if (keyframes.Count == 0)
                    {
                        result.Missing++;
                        continue;
                    }

                    var path = CachePath(season, week);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(keyframes), cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
                {
                    // Signed links are valid for 7 days after the schedule was downloaded.
                    result.LinksExpired = true;
                    result.Missing++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Weather forecast download failed (season {season.SeasonId}, week {week.RaceWeekIndex}): {ex.Message}");
                    result.Missing++;
                }
            }

            return result;
        }

        /// <summary>Returns the forecast for one race week, or null when it isn't available.</summary>
        public List<Keyframes>? GetKeyframes(FullSchedule season, int scheduleIndex)
        {
            var week = season.Schedules[scheduleIndex];

            var path = CachePath(season, week);
            if (File.Exists(path))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<List<Keyframes>>(File.ReadAllText(path), LenientJson.Options);
                    if (cached is { Count: > 0 })
                        return cached;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Corrupt weather cache {path}: {ex.Message}");
                }
            }

            // Offline fallback: the bundled snapshot only matches the bundled schedule.
            if (_loadedData.DataOrigin == DataOrigin.Bundled)
            {
                var bundled = LoadBundled()?.Series?.FirstOrDefault(s => s.SeriesId == season.SeriesId);
                var evnt = bundled?.Events?.FirstOrDefault(e => e.RaceWeekNum == week.RaceWeekIndex);
                if (evnt?.Keyframes is { Count: > 0 } frames)
                    return frames;
            }

            return null;
        }

        /// <summary>The forecast file is a JSON array of keyframes (older formats wrapped it in an object).</summary>
        private static List<Keyframes> ParseForecast(string json)
        {
            var node = JsonNode.Parse(json);
            JsonArray? array = node as JsonArray;

            if (array == null && node is JsonObject obj)
                array = obj.Select(kv => kv.Value).OfType<JsonArray>().FirstOrDefault();

            if (array == null)
                return new List<Keyframes>();

            var frames = array.Deserialize<List<Keyframes>>(LenientJson.Options) ?? new List<Keyframes>();
            for (var i = 0; i < frames.Count; i++)
                frames[i].Index = i;
            return frames;
        }

        private WeatherSchedule? LoadBundled()
        {
            if (_bundled != null)
                return _bundled;

            var path = Path.Combine(AppPaths.BundledJsonFolder, "WeatherSchedule.json");
            if (!File.Exists(path))
                return null;

            try
            {
                _bundled = _jsonRepo.Load<WeatherSchedule>(path);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not read bundled weather: {ex.Message}");
            }
            return _bundled;
        }

        private static string CachePath(FullSchedule season, Schedules week) =>
            Path.Combine(AppPaths.WeatherCacheFolder, season.SeasonId.ToString(), $"rw{week.RaceWeekIndex}_t{week.Track?.TrackId}.json");
    }
}
