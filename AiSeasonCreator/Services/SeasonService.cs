using AiSeasonCreator.Data;
using AiSeasonCreator.FormOptions;
using AiSeasonCreator.Helpers;
using AiSeasonCreator.JsonClasses.FullSchedule;
using AiSeasonCreator.Mappers;
using AiSeasonCreator.Online;
using AiSeasonCreator.Repos;
using AiSeasonCreator.ScheduleClasses;
using AiSeasonCreator.Views;
using System.Text.Json;

namespace AiSeasonCreator.Services
{
    public class SeasonService : ISeasonService
    {
        public const string GenerateRosterOption = "Generate Roster";
        public const string ExcludeRosterOption = "Exclude Roster";

        private readonly IJsonRepo _jsonRepo;
        private readonly LoadedData _loadedData;
        private readonly AppSettings _appSettings;
        private readonly SeasonBuilder<SeasonSchedule> _seasonBuilder;
        private readonly AssetImageProvider _assets;
        private readonly WeatherForecastProvider _weather;
        private readonly IRacingDataClient _client;
        private readonly ISeasonView _view;

        public SeasonService(
            IJsonRepo jsonRepo,
            LoadedData loadedData,
            AppSettings appSettings,
            SeasonBuilder<SeasonSchedule> seasonBuilder,
            AssetImageProvider assets,
            WeatherForecastProvider weather,
            IRacingDataClient client,
            ISeasonView view)
        {
            _jsonRepo = jsonRepo;
            _loadedData = loadedData;
            _appSettings = appSettings;
            _seasonBuilder = seasonBuilder;
            _assets = assets;
            _weather = weather;
            _client = client;
            _view = view;
        }

        public void SetSelectedSeason(int seasonId)
        {
            var selectedSeries = _loadedData.FullSchedule.FirstOrDefault(s => s.SeasonId == seasonId);
            _loadedData.SelectedSeries = selectedSeries;
            _loadedData.SelectedSeriesDetails = selectedSeries == null
                ? null
                : _loadedData.SeriesDetails.FirstOrDefault(s => s.SeriesId == selectedSeries.SeriesId);
        }

        public IEnumerable<NameAndAsset> GetAvailableSeries()
        {
            var aiCarIds = _loadedData.CarDetails.Where(c => c.AiEnabled).Select(c => c.CarId).ToHashSet();
            var aiTrackIds = _loadedData.TrackDetails.Where(t => t.AiEnabled).Select(t => t.TrackId).ToHashSet();
            var availableSeries = new List<NameAndAsset>();

            foreach (var season in _loadedData.FullSchedule)
            {
                var carIds = CarIdsInClasses(season.CarClassIds);
                var hasAiCar = carIds.Any(aiCarIds.Contains);
                var aiWeeks = season.Schedules.Count(w => w.Track != null && aiTrackIds.Contains(w.Track.TrackId));

                if (!hasAiCar || aiWeeks == 0)
                    continue;

                availableSeries.Add(new NameAndAsset
                {
                    Id = season.SeasonId,
                    Name = SeriesName(season),
                    Tooltip = $"{season.SeasonName}\n{aiWeeks} of {season.Schedules.Count} weeks on AI-enabled tracks",
                    Asset = _assets.GetSeriesLogo(season.SeriesId),
                });
            }

            return availableSeries.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public IEnumerable<NameAndAsset> GetAvailableCars()
        {
            var season = _loadedData.SelectedSeries;
            if (season == null)
                return Enumerable.Empty<NameAndAsset>();

            var cars = new List<NameAndAsset>();
            foreach (var carId in CarIdsInClasses(season.CarClassIds).Distinct())
            {
                var car = _loadedData.CarDetails.FirstOrDefault(c => c.CarId == carId);
                if (car == null || !car.AiEnabled)
                    continue;

                cars.Add(new NameAndAsset
                {
                    Id = car.CarId,
                    Name = car.CarName,
                    Asset = _assets.GetCarLogo(car.CarId),
                });
            }

            return cars;
        }

        public IEnumerable<NameAndAsset> GetAvailableTracks()
        {
            var season = _loadedData.SelectedSeries;
            if (season == null)
                return Enumerable.Empty<NameAndAsset>();

            var tracks = new List<NameAndAsset>();
            for (var i = 0; i < season.Schedules.Count; i++)
            {
                var week = season.Schedules[i];
                if (week.Track == null)
                    continue;

                var trackDetail = _loadedData.TrackDetails.FirstOrDefault(t => t.TrackId == week.Track.TrackId);
                if (trackDetail == null || !trackDetail.AiEnabled)
                    continue;

                var config = string.IsNullOrWhiteSpace(week.Track.ConfigName) ? "" : $" - {week.Track.ConfigName}";
                tracks.Add(new NameAndAsset
                {
                    Id = i,
                    Name = $"W{week.RaceWeekIndex + 1}  {week.Track.TrackName}",
                    Tooltip = $"Week {week.RaceWeekIndex + 1} (starts {week.StartDate})\n{week.Track.TrackName}{config}\n{DescribeRaceLength(week)}",
                    Asset = _assets.GetTrackLogo(week.Track.TrackId),
                });
            }

            return tracks;
        }

        public int GetDriverCount() => _loadedData.SelectedSeriesDetails?.MaxStarters ?? 20;

        public int GetPracticeLength() => 3;

        public int GetQualiLength(bool qualiAlone)
        {
            var week = _loadedData.SelectedSeries?.Schedules.FirstOrDefault();
            if (week == null)
                return qualiAlone ? 2 : 8;

            if (qualiAlone)
                return Math.Clamp(week.QualifyLaps, 1, 5);
            return Math.Clamp(week.QualifyLength, 1, 30);
        }

        public bool IsLapLimitedRace() => _loadedData.SelectedSeries?.Schedules.FirstOrDefault()?.RaceLapLimit != null;

        /// <summary>Minutes for timed races, or a percentage of the official lap count for lap races.</summary>
        public int GetRaceLength()
        {
            var week = _loadedData.SelectedSeries?.Schedules.FirstOrDefault();
            if (week == null || week.RaceLapLimit != null)
                return 100;

            return Math.Clamp(week.RaceTimeLimit ?? 30, 10, 300);
        }

        public IEnumerable<string> GetAvailableRosters()
        {
            var rosterNames = new List<string> { GenerateRosterOption, ExcludeRosterOption };
            var folderPath = _appSettings.RosterFolderPath;

            if (Directory.Exists(folderPath))
            {
                rosterNames.AddRange(Directory.GetDirectories(folderPath)
                    .Where(r => File.Exists(Path.Combine(r, "roster.json")))
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
            }
            return rosterNames;
        }

        public async Task<SeasonCreationResult> CreateSeasonAsync(string seasonName, IReadOnlyCollection<int> scheduleIndexes)
        {
            var season = _loadedData.SelectedSeries;
            if (season == null)
                return new SeasonCreationResult(false, "Please select a series.");
            if (scheduleIndexes.Count == 0)
                return new SeasonCreationResult(false, "Please keep at least one track selected.");

            // Fail fast: saving anywhere but the iRacing folders means the season never
            // shows up in the sim, so refuse instead of writing to the wrong place.
            if (!IracingFolders.IsExpectedFolder(_appSettings.SeasonFolderPath, "aiseasons"))
                return new SeasonCreationResult(false,
                    $"The seasons folder is \"{_appSettings.SeasonFolderPath}\", but iRacing only reads \"{IracingFolders.DefaultFolder("aiseasons")}\". " +
                    "Fix the Seasons folder on the Settings tab.");
            var generateRoster = _view.RosterName == GenerateRosterOption;
            if (generateRoster && !IracingFolders.IsExpectedFolder(_appSettings.RosterFolderPath, "airosters"))
                return new SeasonCreationResult(false,
                    $"The rosters folder is \"{_appSettings.RosterFolderPath}\", but iRacing only reads \"{IracingFolders.DefaultFolder("airosters")}\". " +
                    "Fix the Rosters folder on the Settings tab.");

            var missingForecasts = 0;
            if (!_view.StaticWeather)
                missingForecasts = await PrefetchWeatherAsync(season, scheduleIndexes);

            var (uniqueName, filePath) = GetUniqueSeasonName(FileNames.Sanitize(seasonName), generateRoster);

            try
            {
                var schedule = _seasonBuilder.BuildSeason(uniqueName);
                _jsonRepo.Save(filePath, schedule);
            }
            catch (Exception ex)
            {
                Log.Error("Season creation failed", ex);
                return new SeasonCreationResult(false, $"Could not create the season: {ex.Message}");
            }

            // Verify the result is really where iRacing looks before reporting success.
            if (!File.Exists(filePath))
                return new SeasonCreationResult(false, $"The season file could not be found after saving: {filePath}");
            if (generateRoster && !Directory.Exists(Path.Combine(_appSettings.RosterFolderPath, uniqueName)))
                return new SeasonCreationResult(false,
                    $"Season \"{uniqueName}\" was saved, but its roster folder was not created. " +
                    "Check the Rosters folder on the Settings tab.");

            var message = $"Season \"{uniqueName}\" created with {scheduleIndexes.Count} race(s). Saved to: {filePath}";
            if (missingForecasts > 0)
                message += $" Real forecast unavailable for {missingForecasts} week(s); the schedule's basic weather was used instead.";

            Log.Info($"Created season {filePath}");
            return new SeasonCreationResult(true, message, filePath);
        }

        /// <summary>
        /// Downloads the real forecast for the chosen weeks. Forecast links expire 7 days after the
        /// schedule was downloaded, so when they do we ask iRacing for fresh links for this season only.
        /// Returns how many weeks still have no forecast.
        /// </summary>
        private async Task<int> PrefetchWeatherAsync(FullSchedule season, IReadOnlyCollection<int> scheduleIndexes)
        {
            try
            {
                var result = await _weather.PrefetchAsync(season, scheduleIndexes, CancellationToken.None);

                if (result.LinksExpired && await _client.EnsureConnectedAsync(allowSilentLogin: true, CancellationToken.None))
                {
                    await RenewWeatherLinksAsync(season);
                    result = await _weather.PrefetchAsync(season, scheduleIndexes, CancellationToken.None);
                }

                return result.Missing;
            }
            catch (Exception ex)
            {
                Log.Warn($"Weather prefetch failed: {ex.Message}");
                return scheduleIndexes.Count;
            }
            finally
            {
                _client.Session.CloseBackgroundBrowser();
            }
        }

        private async Task RenewWeatherLinksAsync(FullSchedule season)
        {
            var json = await _client.GetJsonAsync("series/season_schedule",
                new Dictionary<string, string> { ["season_id"] = season.SeasonId.ToString() }, CancellationToken.None);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("schedules", out var schedulesEl))
                return;

            var fresh = schedulesEl.Deserialize<List<Schedules>>(LenientJson.Options) ?? new List<Schedules>();
            foreach (var week in season.Schedules)
            {
                var match = fresh.FirstOrDefault(f => f.RaceWeekIndex == week.RaceWeekIndex && f.Track?.TrackId == week.Track?.TrackId);
                if (match?.Weather?.WeatherURL != null && week.Weather != null)
                    week.Weather.WeatherURL = match.Weather.WeatherURL;
            }
        }

        private (string SeasonName, string FilePath) GetUniqueSeasonName(string seasonName, bool generateRoster)
        {
            bool IsTaken(string name) =>
                File.Exists(Path.Combine(_appSettings.SeasonFolderPath, $"{name}.json")) ||
                (generateRoster && Directory.Exists(Path.Combine(_appSettings.RosterFolderPath, name)));

            var newSeasonName = seasonName;
            var fileCounter = 0;
            while (IsTaken(newSeasonName))
            {
                fileCounter++;
                newSeasonName = $"{seasonName}({fileCounter})";
            }

            return (newSeasonName, Path.Combine(_appSettings.SeasonFolderPath, $"{newSeasonName}.json"));
        }

        private IEnumerable<int> CarIdsInClasses(IEnumerable<int> carClassIds)
        {
            var ids = carClassIds.ToHashSet();
            return _loadedData.CarClasses
                .Where(cc => ids.Contains(cc.CarClassId))
                .SelectMany(cc => cc.CarsInClass.Select(car => car.CarId));
        }

        private static string SeriesName(FullSchedule season) =>
            season.Schedules.FirstOrDefault()?.SeriesName ?? season.SeasonName ?? $"Series {season.SeriesId}";

        private static string DescribeRaceLength(Schedules week) =>
            week.RaceLapLimit != null ? $"{week.RaceLapLimit} laps" :
            week.RaceTimeLimit != null ? $"{week.RaceTimeLimit} minutes" : "";
    }
}
