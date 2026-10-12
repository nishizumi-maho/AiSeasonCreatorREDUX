using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection;

namespace AiSeasonCreatorREDUX.Helpers
{
    /// <summary>
    /// Checks GitHub for a newer *app* version. Season data no longer needs app updates
    /// (it is downloaded at runtime), so this only matters for new features and fixes.
    /// </summary>
    public class Updater
    {
        public const string Repository = "nishizumi-maho/AiSeasonCreatorREDUX";
        public static string ReleasesPage => $"https://github.com/{Repository}/releases";

        /// <summary>Silent unless an update exists: being offline or having no releases is not an error.</summary>
        public async void CheckForUpdatesInBackground(IWin32Window owner)
        {
            JObject? latestReleaseInfo;
            try
            {
                latestReleaseInfo = await GetLatestReleaseInfo();
            }
            catch (Exception ex)
            {
                Log.Info($"Update check skipped: {ex.Message}");
                return;
            }

            if (latestReleaseInfo == null || !IsUpdateAvailable(latestReleaseInfo))
                return;

            // UpdateForm downloads and starts the installer itself.
            using var updateForm = new UpdateForm(latestReleaseInfo);
            updateForm.ShowDialog(owner);
        }

        public string? FindInstallerAsset(JObject latestReleaseInfo)
        {
            if (latestReleaseInfo["assets"] is not JArray assets)
                return null;

            foreach (var asset in assets)
            {
                var assetName = asset["name"]?.ToString() ?? "";
                if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    return asset["browser_download_url"]?.ToString();
            }

            return null;
        }

        private static async Task<JObject?> GetLatestReleaseInfo()
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "AiSeasonCreatorREDUX");

            using var response = await httpClient.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
            if (!response.IsSuccessStatusCode)
                return null; // 404 = no release published yet

            var responseBody = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseBody);
        }

        private static bool IsUpdateAvailable(JObject latestReleaseInfo)
        {
            var tagName = latestReleaseInfo["tag_name"]?.ToString() ?? "";
            var versionString = new string(tagName.TrimStart('v', 'V').TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());

            if (!Version.TryParse(versionString, out var latestVersion))
                return false;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            return Normalize(latestVersion) > Normalize(currentVersion);
        }

        private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

        public async Task DownloadAndRunInstaller(string installerUrl)
        {
            using var httpClient = new HttpClient();

            var installerPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(new Uri(installerUrl).LocalPath));

            using (var response = await httpClient.GetAsync(installerUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var fileStream = File.Create(installerPath);
                await response.Content.CopyToAsync(fileStream);
            }

            Process.Start(new ProcessStartInfo("msiexec.exe", $"/i \"{installerPath}\"") { UseShellExecute = true });
        }
    }
}
