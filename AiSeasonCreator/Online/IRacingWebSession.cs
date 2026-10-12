using AiSeasonCreator.Data;
using AiSeasonCreator.Helpers;
using Microsoft.Web.WebView2.Core;

namespace AiSeasonCreator.Online
{
    /// <summary>
    /// Owns the embedded Edge (WebView2) browser profile used to talk to the iRacing web UI.
    ///
    /// Since the public Data API stopped accepting new OAuth clients, the only way for each user
    /// to get fresh season data is through their own iRacing web session. The web UI
    /// (members-ng.iracing.com) proxies the regular /data endpoints through
    /// "/bff/pub/proxy/data/...", authenticated by the session cookie. We sign in once in a
    /// visible browser window (on iRacing's own login page) and afterwards issue the same
    /// requests from a hidden browser that shares the cookie jar.
    ///
    /// All members must be called from the UI thread.
    /// </summary>
    public sealed class IRacingWebSession : IDisposable
    {
        public const string WebUiHost = "members-ng.iracing.com";
        public const string WebUiUrl = "https://members-ng.iracing.com/web/racing/home/dashboard";
        public const string BffBaseUrl = "https://members-ng.iracing.com/bff/pub";
        public const string ClientId = "iracing_ui";

        private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(45);

        private Task<CoreWebView2Environment>? _environmentTask;
        private Form? _hiddenHost;
        private CoreWebView2Controller? _backgroundController;
        private WebViewFetcher? _backgroundFetcher;
        private Task<WebViewFetcher>? _backgroundFetcherTask;
        private int _activeRequests;
        private bool _closeRequested;

        public static string BuildProxyDataUrl(string endpoint, IReadOnlyDictionary<string, string>? query = null)
        {
            var url = $"{BffBaseUrl}/proxy/data/{endpoint.TrimStart('/')}";
            if (query != null && query.Count > 0)
                url += "?" + string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            return url;
        }

        /// <summary>True when the Microsoft Edge WebView2 runtime is installed (it ships with Windows 10/11).</summary>
        public static bool IsRuntimeAvailable(out string? version)
        {
            try
            {
                version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return !string.IsNullOrEmpty(version);
            }
            catch (Exception)
            {
                version = null;
                return false;
            }
        }

        public Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            return _environmentTask ??= CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppPaths.WebViewProfileFolder,
                options: null);
        }

        /// <summary>
        /// Performs a GET against the web UI's data proxy using the hidden browser.
        /// </summary>
        public async Task<ProxyResponse> GetProxyDataAsync(string endpoint, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken)
        {
            _activeRequests++;
            try
            {
                var fetcher = await GetBackgroundFetcherAsync(cancellationToken);
                return await fetcher.GetAsync(BuildProxyDataUrl(endpoint, query), TimeSpan.FromSeconds(60), cancellationToken);
            }
            finally
            {
                _activeRequests--;
                if (_activeRequests == 0 && _closeRequested)
                    CloseBackgroundBrowser();
            }
        }

        /// <summary>
        /// Tries to re-establish an expired web session without user interaction by pressing the
        /// web UI's "Log in" button in the hidden browser. Works when iRacing still remembers the login.
        /// </summary>
        public async Task<bool> TrySilentLoginAsync(Func<Task<bool>> probe, CancellationToken cancellationToken)
        {
            var fetcher = await GetBackgroundFetcherAsync(cancellationToken);
            if (!fetcher.IsOnWebUiOrigin)
                return false;

            var clicked = false;
            for (var attempt = 0; attempt < 3 && !clicked; attempt++)
            {
                clicked = await fetcher.ClickLoginButtonAsync();
                if (!clicked)
                    await Task.Delay(1500, cancellationToken);
            }

            if (!clicked)
                return false;

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(1500, cancellationToken);
                if (fetcher.IsOnWebUiOrigin && await probe())
                    return true;
            }

            return false;
        }

        /// <summary>Signs out of the iRacing web UI and wipes the embedded browser's cookies.</summary>
        public async Task SignOutAsync(CancellationToken cancellationToken)
        {
            try
            {
                var fetcher = await GetBackgroundFetcherAsync(cancellationToken);
                if (fetcher.IsOnWebUiOrigin)
                    await fetcher.PostAsync($"{BffBaseUrl}/proxy/api/revoke/current");
                await Task.Delay(500, cancellationToken);
                fetcher.Core.CookieManager.DeleteAllCookies();
            }
            catch (Exception ex)
            {
                Log.Warn($"Sign out failed: {ex.Message}");
            }
            finally
            {
                CloseBackgroundBrowser();
            }
        }

        /// <summary>The hidden browser is only kept alive while a refresh runs.</summary>
        public void CloseBackgroundBrowser()
        {
            if (_activeRequests > 0)
            {
                // Another operation is mid-request; close once it finishes.
                _closeRequested = true;
                return;
            }

            _closeRequested = false;
            _backgroundFetcherTask = null;
            _backgroundFetcher?.Dispose();
            _backgroundFetcher = null;
            _backgroundController?.Close();
            _backgroundController = null;
            _hiddenHost?.Dispose();
            _hiddenHost = null;
        }

        private async Task<WebViewFetcher> GetBackgroundFetcherAsync(CancellationToken cancellationToken)
        {
            // Share one in-flight creation between concurrent callers.
            var task = _backgroundFetcherTask ??= CreateBackgroundFetcherAsync(cancellationToken);
            try
            {
                return await task;
            }
            catch
            {
                if (_backgroundFetcherTask == task)
                {
                    _backgroundFetcherTask = null;
                    CloseBackgroundBrowser();
                }
                throw;
            }
        }

        private async Task<WebViewFetcher> CreateBackgroundFetcherAsync(CancellationToken cancellationToken)
        {
            var environment = await GetEnvironmentAsync();

            // WebView2 needs a parent window handle; a never-shown form is enough.
            _hiddenHost = new Form { ShowInTaskbar = false, Opacity = 0, Size = new Size(1024, 768) };
            var hwnd = _hiddenHost.Handle;

            _backgroundController = await environment.CreateCoreWebView2ControllerAsync(hwnd);
            _backgroundController.IsVisible = false;
            _backgroundController.Bounds = new Rectangle(0, 0, 1024, 768);

            var core = _backgroundController.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.IsMuted = true;
            core.NewWindowRequested += (_, e) => e.Handled = true;

            var fetcher = new WebViewFetcher(core);
            _backgroundFetcher = fetcher;
            await NavigateAndWaitAsync(core, WebUiUrl, cancellationToken);
            return fetcher;
        }

        internal static async Task NavigateAndWaitAsync(CoreWebView2 core, string url, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);

            core.NavigationCompleted += OnCompleted;
            try
            {
                core.Navigate(url);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(NavigationTimeout);
                using (timeoutCts.Token.Register(() => tcs.TrySetResult(false)))
                {
                    if (!await tcs.Task)
                        Log.Warn($"Navigation to {url} did not complete successfully.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Give the single-page app a moment to boot (it may redirect).
                await Task.Delay(1500, cancellationToken);
            }
            finally
            {
                core.NavigationCompleted -= OnCompleted;
            }
        }

        public void Dispose() => CloseBackgroundBrowser();
    }
}
