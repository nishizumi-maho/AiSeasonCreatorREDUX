using AiSeasonCreatorREDUX.Helpers;
using AiSeasonCreatorREDUX.Online;
using System.Collections.Concurrent;
using System.Drawing.Drawing2D;

namespace AiSeasonCreatorREDUX.Data
{
    public enum AssetKind
    {
        Series,
        Car,
        Track,
    }

    /// <summary>
    /// Provides series/car/track logos. Logos for new content are downloaded from iRacing's image
    /// CDN and cached locally; the logos shipped with the app are used as a fallback.
    /// A missing logo never breaks the UI — the button simply shows text only.
    /// </summary>
    public class AssetImageProvider
    {
        private const string ImageCdn = "https://images-static.iracing.com";

        private readonly LoadedData _loadedData;
        private readonly ConcurrentDictionary<string, Image?> _memory = new();

        public AssetImageProvider(LoadedData loadedData)
        {
            _loadedData = loadedData;
        }

        public Image? GetSeriesLogo(int seriesId) =>
            _loadedData.SeriesAssets != null && _loadedData.SeriesAssets.TryGetValue(seriesId.ToString(), out var a)
                ? Get(AssetKind.Series, a?.Logo) : null;

        public Image? GetCarLogo(int carId) =>
            _loadedData.CarAssets != null && _loadedData.CarAssets.TryGetValue(carId.ToString(), out var a)
                ? Get(AssetKind.Car, a?.Logo) : null;

        public Image? GetTrackLogo(int trackId) =>
            _loadedData.TrackAssets != null && _loadedData.TrackAssets.TryGetValue(trackId.ToString(), out var a)
                ? Get(AssetKind.Track, a?.Logo) : null;

        public void ClearMemoryCache() => _memory.Clear();

        /// <summary>Downloads the logos that aren't on disk yet (runs during a data refresh).</summary>
        public async Task PrefetchAsync(IReadOnlyCollection<(AssetKind Kind, string Logo)> logos, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var missing = logos.Where(l => FindLocalFile(l.Kind, l.Logo) == null).ToList();
            if (missing.Count == 0)
                return;

            var done = 0;
            using var throttle = new SemaphoreSlim(6);

            var tasks = missing.Select(async item =>
            {
                await throttle.WaitAsync(cancellationToken);
                try
                {
                    var bytes = await IRacingDataClient.DownloadBytesAsync(RemoteUrl(item.Kind, item.Logo), cancellationToken);
                    var target = CachePath(item.Kind, item.Logo);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await File.WriteAllBytesAsync(target, bytes, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Logos are cosmetic; keep going.
                    Log.Warn($"Logo download failed for {item.Logo}: {ex.Message}");
                }
                finally
                {
                    throttle.Release();
                    var count = Interlocked.Increment(ref done);
                    progress?.Report($"Downloading logos ({count}/{missing.Count})...");
                }
            });

            await Task.WhenAll(tasks);
        }

        private Image? Get(AssetKind kind, string? logo)
        {
            if (string.IsNullOrWhiteSpace(logo))
                return null;

            return _memory.GetOrAdd($"{kind}:{logo}", _ =>
            {
                var path = FindLocalFile(kind, logo);
                if (path == null)
                    return null;

                try
                {
                    // Load through a copy so the file isn't locked while the app runs.
                    using var stream = new MemoryStream(File.ReadAllBytes(path));
                    using var original = Image.FromStream(stream);
                    return FitInto(original, MaxSize(kind));
                }
                catch (Exception ex)
                {
                    Log.Warn($"Could not read logo {path}: {ex.Message}");
                    return null;
                }
            });
        }

        private static string? FindLocalFile(AssetKind kind, string logo)
        {
            var cached = CachePath(kind, logo);
            if (File.Exists(cached))
                return cached;

            var bundled = Path.Combine(AppPaths.BundledAssetsFolder, BundledFolder(kind), Path.GetFileName(logo));
            return File.Exists(bundled) ? bundled : null;
        }

        private static string CachePath(AssetKind kind, string logo) =>
            Path.Combine(AppPaths.ImageCacheFolder, kind.ToString(), Path.GetFileName(logo));

        private static string BundledFolder(AssetKind kind) => kind switch
        {
            AssetKind.Series => "SeriesAssets",
            AssetKind.Car => "CarAssets",
            _ => "TrackAssets",
        };

        /// <summary>
        /// The asset endpoints return either a full path ("/img/logos/partners/x.png") or just a file
        /// name ("seriesid_123.png"); both map to iRacing's static image CDN.
        /// </summary>
        private static string RemoteUrl(AssetKind kind, string logo)
        {
            if (logo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return logo;
            if (logo.Contains('/'))
                return ImageCdn + "/" + logo.TrimStart('/');

            var folder = kind switch
            {
                AssetKind.Series => "img/logos/series",
                AssetKind.Car => "img/logos/partners",
                _ => "img/logos/tracks",
            };
            return $"{ImageCdn}/{folder}/{logo}";
        }

        private static Size MaxSize(AssetKind kind) => kind == AssetKind.Car ? new Size(160, 34) : new Size(90, 32);

        private static Image FitInto(Image source, Size max)
        {
            var scale = Math.Min(1f, Math.Min((float)max.Width / source.Width, (float)max.Height / source.Height));
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));

            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, width, height);
            return bitmap;
        }
    }
}
