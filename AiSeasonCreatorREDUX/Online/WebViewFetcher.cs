using Microsoft.Web.WebView2.Core;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AiSeasonCreatorREDUX.Online
{
    public sealed record ProxyResponse(int Status, string Body)
    {
        public bool IsSuccess => Status >= 200 && Status < 300;
        public bool IsUnauthorized => Status == 401 || Status == 403;
    }

    /// <summary>
    /// Runs fetch() calls *inside* an iRacing web page so the request carries the same
    /// session cookie the official iRacing UI uses. The user's password never passes through
    /// this app: the login happens on iRacing's own site inside the embedded browser.
    /// </summary>
    internal sealed class WebViewFetcher : IDisposable
    {
        private readonly CoreWebView2 _core;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<ProxyResponse>> _pending = new();

        public WebViewFetcher(CoreWebView2 core)
        {
            _core = core;
            _core.WebMessageReceived += OnWebMessageReceived;
        }

        public CoreWebView2 Core => _core;

        public bool IsOnWebUiOrigin =>
            Uri.TryCreate(_core.Source, UriKind.Absolute, out var uri) &&
            uri.Host.Equals(IRacingWebSession.WebUiHost, StringComparison.OrdinalIgnoreCase);

        public async Task<ProxyResponse> GetAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!IsOnWebUiOrigin)
                return new ProxyResponse(401, "The embedded browser is not on the iRacing web UI.");

            var id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<ProxyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            try
            {
                await _core.ExecuteScriptAsync(BuildFetchScript(id, url));

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);
                using (timeoutCts.Token.Register(() => tcs.TrySetResult(new ProxyResponse(0, "Request timed out."))))
                {
                    var result = await tcs.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                    return result;
                }
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        /// <summary>
        /// Clicks the "Log in" button shown by the iRacing UI when there is no session.
        /// If iRacing still remembers the user, the OAuth round-trip finishes without any input.
        /// </summary>
        public async Task<bool> ClickLoginButtonAsync()
        {
            var result = await _core.ExecuteScriptAsync(@"
                (function () {
                    const re = /log\s*in|sign\s*in|entrar|iniciar|anmelden|connexion|accedi/i;
                    const el = [...document.querySelectorAll('button, a')].find(b => re.test((b.textContent || '').trim()));
                    if (el) { el.click(); return true; }
                    return false;
                })();");
            return result == "true";
        }

        public async Task PostAsync(string url)
        {
            var script = $@"
                fetch({JsonSerializer.Serialize(url)}, {{
                    method: 'POST',
                    credentials: 'include',
                    headers: {{ 'IR-Client-ID': {JsonSerializer.Serialize(IRacingWebSession.ClientId)} }}
                }}).catch(() => {{}});";
            await _core.ExecuteScriptAsync(script);
        }

        private static string BuildFetchScript(string id, string url) => $@"
            (async () => {{
                const id = {JsonSerializer.Serialize(id)};
                try {{
                    const r = await fetch({JsonSerializer.Serialize(url)}, {{
                        credentials: 'include',
                        headers: {{ 'IR-Client-ID': {JsonSerializer.Serialize(IRacingWebSession.ClientId)} }}
                    }});
                    const body = await r.text();
                    window.chrome.webview.postMessage(JSON.stringify({{ id, status: r.status, body }}));
                }} catch (e) {{
                    window.chrome.webview.postMessage(JSON.stringify({{ id, status: 0, body: String(e) }}));
                }}
            }})();";

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Only trust messages coming from the iRacing web UI origin.
            if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) ||
                !source.Host.Equals(IRacingWebSession.WebUiHost, StringComparison.OrdinalIgnoreCase))
                return;

            string? message;
            try { message = e.TryGetWebMessageAsString(); }
            catch { return; }

            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var idEl) || idEl.GetString() is not { } id)
                    return;

                if (_pending.TryGetValue(id, out var tcs))
                {
                    var status = root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : 0;
                    var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    tcs.TrySetResult(new ProxyResponse(status, body));
                }
            }
            catch (JsonException)
            {
                // Not one of our messages.
            }
        }

        public void Dispose()
        {
            _core.WebMessageReceived -= OnWebMessageReceived;
            foreach (var tcs in _pending.Values)
                tcs.TrySetResult(new ProxyResponse(0, "Browser closed."));
            _pending.Clear();
        }
    }
}
