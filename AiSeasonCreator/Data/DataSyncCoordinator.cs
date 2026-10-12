using AiSeasonCreator.Helpers;
using AiSeasonCreator.Online;

namespace AiSeasonCreator.Data
{
    /// <summary>
    /// Decides *when* to talk to iRacing and drives the sign-in UI. Used by the status bar and the Settings tab.
    /// </summary>
    public class DataSyncCoordinator
    {
        private readonly SeasonDataManager _dataManager;
        private readonly IRacingDataClient _client;
        private readonly LoadedData _loadedData;

        public DataSyncCoordinator(SeasonDataManager dataManager, IRacingDataClient client, LoadedData loadedData)
        {
            _dataManager = dataManager;
            _client = client;
            _loadedData = loadedData;

            _dataManager.StatusChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
            _dataManager.DataChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
            _client.ConnectionChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Anything shown in the status bar / settings changed.</summary>
        public event EventHandler? StateChanged;

        public SeasonDataManager DataManager => _dataManager;
        public IRacingDataClient Client => _client;

        public bool AutoUpdate
        {
            get => _loadedData.UserDefaultSettings.DataSync.AutoUpdate;
            set => _loadedData.UserDefaultSettings.DataSync.AutoUpdate = value;
        }

        /// <summary>One-line description of the data in use, e.g. "2026 Season 4 · updated 2 hours ago".</summary>
        public string DescribeData()
        {
            if (!_dataManager.HasLiveData)
                return $"Offline data bundled with the app ({_dataManager.SeasonLabel}) — sign in to get the current season";

            var text = $"{_dataManager.SeasonLabel} · {_dataManager.Manifest.SeasonCount} series";
            if (_dataManager.Manifest.FetchedAtUtc is { } fetched)
                text += $" · updated {Ago(fetched)}";
            if (_dataManager.IsSeasonOver)
                text += " · a new season is available";
            return text;
        }

        /// <summary>Called once the main window is visible.</summary>
        public async Task OnStartupAsync(IWin32Window owner)
        {
            if (!_dataManager.HasLiveData)
            {
                var answer = MessageBox.Show(owner,
                    "AiSeasonCreator can download the current iRacing series, schedules and weather automatically, " +
                    "so it is always up to date — even when a new season starts.\n\n" +
                    "To do that, sign in once with your iRacing account (on iRacing's own website, inside the app).\n\n" +
                    "Sign in now?\n\n(Choose \"No\" to keep using the offline data bundled with the app. You can sign in later from the status bar.)",
                    "Get the current iRacing season", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (answer == DialogResult.Yes)
                    await UpdateNowAsync(owner, interactive: true);
                return;
            }

            if (!AutoUpdate || !_dataManager.IsRefreshDue)
                return;

            var result = await _dataManager.RefreshAsync(allowSilentLogin: true);

            if (result == RefreshResult.NeedsSignIn && _dataManager.IsSeasonOver)
            {
                var answer = MessageBox.Show(owner,
                    "A new iRacing season has started, but your iRacing session has expired.\n\nSign in now to download the new series and schedules?",
                    "New season available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (answer == DialogResult.Yes)
                    await UpdateNowAsync(owner, interactive: true);
            }
        }

        /// <summary>Downloads fresh data, asking the user to sign in when needed.</summary>
        public async Task UpdateNowAsync(IWin32Window owner, bool interactive)
        {
            if (_dataManager.IsRefreshing)
                return;

            var result = await _dataManager.RefreshAsync(allowSilentLogin: true);

            if (result == RefreshResult.NeedsSignIn && interactive && await ConnectAsync(owner))
                result = await _dataManager.RefreshAsync(allowSilentLogin: false);

            if (result == RefreshResult.Failed && interactive)
            {
                MessageBox.Show(owner,
                    "Could not download the season data from iRacing.\n\n" +
                    $"{_dataManager.LastError}\n\n" +
                    "The previously saved data is still being used. Details were written to:\n" + AppPaths.LogFile,
                    "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Opens the iRacing sign-in window. Returns true once a valid session exists.</summary>
        public async Task<bool> ConnectAsync(IWin32Window owner)
        {
            if (!IRacingWebSession.IsRuntimeAvailable(out _))
            {
                var answer = MessageBox.Show(owner,
                    "Signing in to iRacing needs the Microsoft Edge WebView2 Runtime (normally included with Windows 10/11).\n\nOpen the download page?",
                    "WebView2 Runtime required", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer == DialogResult.Yes)
                    UrlOpener.Open("https://developer.microsoft.com/microsoft-edge/webview2/");
                return false;
            }

            using var form = new IRacingConnectForm(_client.Session);
            if (form.ShowDialog(owner) != DialogResult.OK)
                return false;

            return await _client.RefreshConnectionStateAsync(CancellationToken.None);
        }

        public async Task DisconnectAsync()
        {
            await _client.SignOutAsync(CancellationToken.None);
        }

        private static string Ago(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} h ago";
            return $"{(int)span.TotalDays} day(s) ago";
        }
    }
}
