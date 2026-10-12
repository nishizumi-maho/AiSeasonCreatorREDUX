using AiSeasonCreator.Helpers;
using AiSeasonCreator.JsonClasses.Assets;
using AiSeasonCreator.JsonClasses.CarDetails;
using AiSeasonCreator.JsonClasses.FullSchedule;
using AiSeasonCreator.JsonClasses.SeriesDetails;
using AiSeasonCreator.JsonClasses.TrackDetails;
using AiSeasonCreator.Online;
using AiSeasonCreator.Repos;
using System.Globalization;
using System.Text.Json;
using CarClassesModel = AiSeasonCreator.JsonClasses.CarClasses.CarClasses;

namespace AiSeasonCreator.Data
{
    public enum DataOrigin
    {
        /// <summary>Old snapshot shipped inside the installer (offline fallback).</summary>
        Bundled,
        /// <summary>Downloaded from iRacing by this user.</summary>
        Live,
    }

    public class DataManifest
    {
        public int FormatVersion { get; set; } = 1;
        public DateTime? FetchedAtUtc { get; set; }
        public int SeasonYear { get; set; }
        public int SeasonQuarter { get; set; }
        public int SeasonCount { get; set; }
        public DateTime? SeasonEndsUtc { get; set; }
        public string? FetchedBy { get; set; }
    }

    public enum RefreshResult
    {
        Success,
        NeedsSignIn,
        Failed,
        AlreadyRunning,
    }

    /// <summary>
    /// Keeps the season data (series, schedules, cars, tracks and their logos) up to date.
    ///
    /// Data is downloaded per user from the iRacing web UI and cached in %LocalAppData%, so a
    /// new iRacing season shows up automatically — no new app release required. The JSON files
    /// shipped with the app are only used until the first successful download (or when offline).
    /// </summary>
    public class SeasonDataManager
    {
        private record Dataset(string FileName, string Endpoint, IReadOnlyDictionary<string, string>? Query = null);

        private static readonly Dataset[] Datasets =
        {
            new("SeasonSchedule.json", "series/seasons", new Dictionary<string, string> { ["include_series"] = "true" }),
            new("SeriesDetails.json", "series/get"),
            new("Cars.json", "car/get"),
            new("CarClasses.json", "carclass/get"),
            new("Tracks.json", "track/get"),
            new("CarAssets.json", "car/assets"),
            new("SeriesAssets.json", "series/assets"),
            new("TrackAssets.json", "track/assets"),
        };

        private const string ManifestFileName = "manifest.json";

        /// <summary>How old the cache may get before a silent background refresh is attempted.</summary>
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);

        private readonly LoadedData _loadedData;
        private readonly IRacingDataClient _client;
        private readonly AssetImageProvider _assets;

        public SeasonDataManager(LoadedData loadedData, IRacingDataClient client, AssetImageProvider assets)
        {
            _loadedData = loadedData;
            _client = client;
            _assets = assets;
        }

        public DataOrigin Origin { get; private set; } = DataOrigin.Bundled;
        public DataManifest Manifest { get; private set; } = new();
        public bool IsRefreshing { get; private set; }
        public string StatusText { get; private set; } = "";
        public string? LastError { get; private set; }

        /// <summary>Raised on the UI thread after new data has been applied to <see cref="LoadedData"/>.</summary>
        public event EventHandler? DataChanged;

        /// <summary>Raised on the UI thread whenever <see cref="StatusText"/> or <see cref="IsRefreshing"/> changes.</summary>
        public event EventHandler? StatusChanged;

        public bool HasLiveData => Origin == DataOrigin.Live;

        public bool IsSeasonOver => Manifest.SeasonEndsUtc is { } end && DateTime.UtcNow > end;

        public bool IsRefreshDue =>
            !HasLiveData ||
            IsSeasonOver ||
            Manifest.FetchedAtUtc is not { } fetched ||
            DateTime.UtcNow - fetched > RefreshInterval;

        public string SeasonLabel => Manifest.SeasonYear > 0
            ? $"{Manifest.SeasonYear} Season {Manifest.SeasonQuarter}"
            : "Unknown season";

        /// <summary>Loads cached live data if present, otherwise the bundled snapshot. Synchronous, call at startup.</summary>
        public void LoadInitial()
        {
            if (TryLoadFrom(AppPaths.DataCacheFolder, DataOrigin.Live))
                return;

            if (!TryLoadFrom(AppPaths.BundledJsonFolder, DataOrigin.Bundled))
                throw new InvalidOperationException("No season data available. Please reinstall AiSeasonCreator.");
        }

        /// <summary>
        /// Downloads everything from iRacing. Must be called on the UI thread.
        /// </summary>
        public async Task<RefreshResult> RefreshAsync(bool allowSilentLogin, CancellationToken cancellationToken = default)
        {
            if (IsRefreshing)
                return RefreshResult.AlreadyRunning;

            IsRefreshing = true;
            LastError = null;
            SetStatus("Connecting to iRacing...");

            try
            {
                if (!await _client.EnsureConnectedAsync(allowSilentLogin, cancellationToken))
                {
                    SetStatus("Sign in to iRacing to download the current season.");
                    return RefreshResult.NeedsSignIn;
                }

                var payloads = new Dictionary<string, string>();
                for (var i = 0; i < Datasets.Length; i++)
                {
                    var dataset = Datasets[i];
                    SetStatus($"Downloading season data ({i + 1}/{Datasets.Length})...");
                    payloads[dataset.FileName] = await _client.GetJsonAsync(dataset.Endpoint, dataset.Query, cancellationToken);
                }

                SetStatus("Processing season data...");
                var snapshot = await Task.Run(() => Parse(payloads.TryGetValue, DataOrigin.Live), cancellationToken);

                await _assets.PrefetchAsync(RequiredLogos(snapshot), new Progress<string>(SetStatus), cancellationToken);

                var manifest = BuildManifest(snapshot, _client.ConnectedDisplayName);
                await Task.Run(() => WriteCache(payloads, manifest), cancellationToken);

                Apply(snapshot, DataOrigin.Live, manifest);
                Log.Info($"Season data refreshed: {SeasonLabel}, {snapshot.Schedules.Length} seasons.");
                SetStatus("");
                return RefreshResult.Success;
            }
            catch (IRacingAuthRequiredException)
            {
                SetStatus("Your iRacing session expired. Sign in again to update.");
                return RefreshResult.NeedsSignIn;
            }
            catch (OperationCanceledException)
            {
                SetStatus("Update cancelled.");
                return RefreshResult.Failed;
            }
            catch (Exception ex)
            {
                Log.Error("Season data refresh failed", ex);
                LastError = ex.Message;
                SetStatus("Could not update season data. Using the last saved data.");
                return RefreshResult.Failed;
            }
            finally
            {
                _client.Session.CloseBackgroundBrowser();
                IsRefreshing = false;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool TryLoadFrom(string folder, DataOrigin origin)
        {
            try
            {
                if (!Datasets.All(d => File.Exists(Path.Combine(folder, d.FileName))))
                    return false;

                var manifestPath = Path.Combine(folder, ManifestFileName);
                if (origin == DataOrigin.Live && !File.Exists(manifestPath))
                    return false; // interrupted download, don't trust a mixed cache

                var snapshot = Parse((string file, out string json) =>
                {
                    json = File.ReadAllText(Path.Combine(folder, file));
                    return true;
                }, origin);

                var manifest = File.Exists(manifestPath)
                    ? JsonSerializer.Deserialize<DataManifest>(File.ReadAllText(manifestPath)) ?? BuildManifest(snapshot, null)
                    : BuildManifest(snapshot, null);

                if (origin == DataOrigin.Bundled)
                    manifest.FetchedAtUtc = null;

                Apply(snapshot, origin, manifest);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Could not load season data from {folder}", ex);
                return false;
            }
        }

        private delegate bool PayloadSource(string fileName, out string json);

        private sealed class Snapshot
        {
            public FullSchedule[] Schedules { get; init; } = Array.Empty<FullSchedule>();
            public SeriesDetails[] Series { get; init; } = Array.Empty<SeriesDetails>();
            public CarDetails[] Cars { get; init; } = Array.Empty<CarDetails>();
            public CarClassesModel[] CarClasses { get; init; } = Array.Empty<CarClassesModel>();
            public TrackDetails[] Tracks { get; init; } = Array.Empty<TrackDetails>();
            public Dictionary<string, CarAssets> CarAssets { get; init; } = new();
            public Dictionary<string, SeriesAssets> SeriesAssets { get; init; } = new();
            public Dictionary<string, TrackAssets> TrackAssets { get; init; } = new();
        }

        private static Snapshot Parse(PayloadSource source, DataOrigin origin)
        {
            T Read<T>(string file)
            {
                if (!source(file, out var json))
                    throw new IRacingDataException($"Missing {file}");
                return JsonSerializer.Deserialize<T>(json, LenientJson.Options)
                       ?? throw new IRacingDataException($"{file} is empty");
            }

            var schedules = Read<FullSchedule[]>("SeasonSchedule.json")
                // Seasons without a published schedule can't be turned into an AI season.
                .Where(s => s.Schedules != null && s.Schedules.Count > 0 && s.CarClassIds != null)
                .ToArray();

            if (schedules.Length == 0)
                throw new IRacingDataException("iRacing returned no series schedules.");

            return new Snapshot
            {
                Schedules = schedules,
                Series = Read<SeriesDetails[]>("SeriesDetails.json"),
                Cars = Read<CarDetails[]>("Cars.json"),
                CarClasses = Read<CarClassesModel[]>("CarClasses.json"),
                Tracks = Read<TrackDetails[]>("Tracks.json"),
                CarAssets = Read<Dictionary<string, CarAssets>>("CarAssets.json"),
                SeriesAssets = Read<Dictionary<string, SeriesAssets>>("SeriesAssets.json"),
                TrackAssets = Read<Dictionary<string, TrackAssets>>("TrackAssets.json"),
            };
        }

        /// <summary>Logos shown in the UI: every series, plus the AI-enabled cars and tracks they use.</summary>
        private static List<(AssetKind Kind, string Logo)> RequiredLogos(Snapshot snapshot)
        {
            var logos = new List<(AssetKind, string)>();

            foreach (var season in snapshot.Schedules)
            {
                if (snapshot.SeriesAssets.TryGetValue(season.SeriesId.ToString(), out var sa) && !string.IsNullOrEmpty(sa.Logo))
                    logos.Add((AssetKind.Series, sa.Logo));
            }

            var classIds = snapshot.Schedules.SelectMany(s => s.CarClassIds).ToHashSet();
            var carIds = snapshot.CarClasses
                .Where(cc => classIds.Contains(cc.CarClassId))
                .SelectMany(cc => cc.CarsInClass.Select(c => c.CarId))
                .ToHashSet();
            foreach (var car in snapshot.Cars.Where(c => c.AiEnabled && carIds.Contains(c.CarId)))
            {
                if (snapshot.CarAssets.TryGetValue(car.CarId.ToString(), out var ca) && !string.IsNullOrEmpty(ca.Logo))
                    logos.Add((AssetKind.Car, ca.Logo));
            }

            var trackIds = snapshot.Schedules.SelectMany(s => s.Schedules).Select(w => w.Track?.TrackId ?? 0).ToHashSet();
            foreach (var track in snapshot.Tracks.Where(t => t.AiEnabled && trackIds.Contains(t.TrackId)))
            {
                if (snapshot.TrackAssets.TryGetValue(track.TrackId.ToString(), out var ta) && !string.IsNullOrEmpty(ta.Logo))
                    logos.Add((AssetKind.Track, ta.Logo));
            }

            return logos.Distinct().ToList();
        }

        private void Apply(Snapshot snapshot, DataOrigin origin, DataManifest manifest)
        {
            _loadedData.FullSchedule = snapshot.Schedules;
            _loadedData.SeriesDetails = snapshot.Series;
            _loadedData.CarDetails = snapshot.Cars;
            _loadedData.CarClasses = snapshot.CarClasses;
            _loadedData.TrackDetails = snapshot.Tracks;
            _loadedData.CarAssets = snapshot.CarAssets;
            _loadedData.SeriesAssets = snapshot.SeriesAssets;
            _loadedData.TrackAssets = snapshot.TrackAssets;
            _loadedData.DataOrigin = origin;

            // Selections refer to the previous data set.
            _loadedData.SelectedSeries = null;
            _loadedData.SelectedSeriesDetails = null;
            _loadedData.RosterSelectedSeries = null;
            _loadedData.RosterSelectedSeriesDetails = null;

            Origin = origin;
            Manifest = manifest;
            _assets.ClearMemoryCache();
            PruneStaleCache(snapshot);
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Deletes cached files that belong to seasons/series that no longer exist, so dead
        /// content doesn't pile up on disk. Everything removed here is re-downloadable while
        /// its season is current (logos) or irrelevant once the season is gone (forecasts).
        /// </summary>
        private static void PruneStaleCache(Snapshot snapshot)
        {
            try
            {
                var liveIds = snapshot.Schedules.Select(s => s.SeasonId.ToString()).ToHashSet();
                var weatherRoot = AppPaths.WeatherCacheFolder;
                if (Directory.Exists(weatherRoot))
                {
                    foreach (var dir in Directory.GetDirectories(weatherRoot))
                    {
                        if (!liveIds.Contains(Path.GetFileName(dir)))
                        {
                            try
                            {
                                Directory.Delete(dir, recursive: true);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"Could not delete stale weather cache {dir}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Weather cache prune failed: {ex.Message}");
            }

            try
            {
                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in snapshot.CarAssets.Values)
                    AddLogoName(referenced, a?.Logo);
                foreach (var a in snapshot.SeriesAssets.Values)
                    AddLogoName(referenced, a?.Logo);
                foreach (var a in snapshot.TrackAssets.Values)
                    AddLogoName(referenced, a?.Logo);

                foreach (var kind in new[] { AssetKind.Series, AssetKind.Car, AssetKind.Track })
                {
                    var dir = Path.Combine(AppPaths.ImageCacheFolder, kind.ToString());
                    if (!Directory.Exists(dir))
                        continue;

                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (!referenced.Contains(Path.GetFileName(file)))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"Could not delete stale logo cache {file}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Logo cache prune failed: {ex.Message}");
            }
        }

        private static void AddLogoName(HashSet<string> referenced, string? logo)
        {
            if (!string.IsNullOrWhiteSpace(logo))
                referenced.Add(Path.GetFileName(logo));
        }

        private static DataManifest BuildManifest(Snapshot snapshot, string? fetchedBy)
        {
            // The most common year/quarter is "the" current season (a few special events may differ).
            var current = snapshot.Schedules
                .GroupBy(s => (s.SeasonYear, s.SeasonQuarter))
                .OrderByDescending(g => g.Count())
                .First().Key;

            return new DataManifest
            {
                FetchedAtUtc = DateTime.UtcNow,
                SeasonYear = current.SeasonYear,
                SeasonQuarter = current.SeasonQuarter,
                SeasonCount = snapshot.Schedules.Length,
                SeasonEndsUtc = ComputeSeasonEnd(snapshot.Schedules.Where(s => s.SeasonYear == current.SeasonYear && s.SeasonQuarter == current.SeasonQuarter)),
                FetchedBy = fetchedBy,
            };
        }

        /// <summary>The season is over one week after the start of its last race week.</summary>
        private static DateTime? ComputeSeasonEnd(IEnumerable<FullSchedule> seasons)
        {
            DateTime? last = null;
            foreach (var week in seasons.SelectMany(s => s.Schedules))
            {
                if (DateTime.TryParse(week.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start) &&
                    (last == null || start > last))
                    last = start;
            }
            return last?.AddDays(7);
        }

        private static void WriteCache(Dictionary<string, string> payloads, DataManifest manifest)
        {
            var folder = AppPaths.DataCacheFolder;
            var staging = Path.Combine(folder, "staging");
            Directory.CreateDirectory(staging);

            foreach (var (file, json) in payloads)
                File.WriteAllText(Path.Combine(staging, file), json);

            // Remove the manifest first: if we crash halfway, the next start won't trust a mixed cache.
            File.Delete(Path.Combine(folder, ManifestFileName));
            foreach (var file in payloads.Keys)
                File.Move(Path.Combine(staging, file), Path.Combine(folder, file), overwrite: true);

            File.WriteAllText(Path.Combine(folder, ManifestFileName),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            Directory.Delete(staging, recursive: true);
        }

        private void SetStatus(string text)
        {
            StatusText = text;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
