using AiSeasonCreator.Helpers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiSeasonCreator.Online
{
    public class IRacingAuthRequiredException : Exception
    {
        public IRacingAuthRequiredException() : base("You need to sign in to iRacing to download season data.") { }
    }

    public class IRacingDataException : Exception
    {
        public IRacingDataException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Reads iRacing /data endpoints through the user's web UI session.
    /// The proxy answers with a short-lived signed S3 link; the payload itself is downloaded with HttpClient.
    /// </summary>
    public class IRacingDataClient
    {
        private static readonly HttpClient Http = CreateHttpClient();

        private readonly IRacingWebSession _session;

        public IRacingDataClient(IRacingWebSession session)
        {
            _session = session;
        }

        public string? ConnectedDisplayName { get; private set; }
        public bool IsConnected => ConnectedDisplayName != null;

        /// <summary>Raised (on the UI thread) whenever the signed-in state changes.</summary>
        public event EventHandler? ConnectionChanged;

        public IRacingWebSession Session => _session;

        /// <summary>
        /// Checks whether the stored web session is still valid. When <paramref name="allowSilentLogin"/> is set
        /// and the session expired, tries to renew it without user interaction.
        /// </summary>
        public async Task<bool> EnsureConnectedAsync(bool allowSilentLogin, CancellationToken cancellationToken)
        {
            if (await ProbeAsync(cancellationToken))
                return true;

            if (allowSilentLogin && await _session.TrySilentLoginAsync(() => ProbeAsync(cancellationToken), cancellationToken))
                return true;

            SetConnected(null);
            return false;
        }

        /// <summary>Called by the sign-in window once it detected a valid session.</summary>
        public async Task<bool> RefreshConnectionStateAsync(CancellationToken cancellationToken)
        {
            _session.CloseBackgroundBrowser(); // pick up the new cookies with a fresh page
            return await ProbeAsync(cancellationToken);
        }

        public async Task SignOutAsync(CancellationToken cancellationToken)
        {
            await _session.SignOutAsync(cancellationToken);
            SetConnected(null);
        }

        /// <summary>Downloads one /data endpoint and returns its JSON payload.</summary>
        public async Task<string> GetJsonAsync(string endpoint, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken)
        {
            var response = await _session.GetProxyDataAsync(endpoint, query, cancellationToken);

            if (response.IsUnauthorized)
            {
                SetConnected(null);
                throw new IRacingAuthRequiredException();
            }

            if (!response.IsSuccess)
                throw new IRacingDataException($"iRacing returned HTTP {response.Status} for '{endpoint}'. {Truncate(response.Body)}");

            return await ResolvePayloadAsync(endpoint, response.Body, cancellationToken);
        }

        /// <summary>Downloads a public/signed URL (weather forecasts, logos...).</summary>
        public static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            using var response = await Http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public static async Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken)
        {
            using var response = await Http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        private async Task<bool> ProbeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var json = await GetJsonAsync("member/info", null, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var name = doc.RootElement.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                SetConnected(string.IsNullOrWhiteSpace(name) ? "iRacing member" : name);
                return true;
            }
            catch (IRacingAuthRequiredException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn($"Connection probe failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> ResolvePayloadAsync(string endpoint, string body, CancellationToken cancellationToken)
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(body);
            }
            catch (JsonException ex)
            {
                throw new IRacingDataException($"Unexpected response for '{endpoint}'.", ex);
            }

            // Standard /data behaviour: { "link": "<signed S3 url>", "expires": "..." }
            if (root is JsonObject obj && obj["link"]?.GetValue<string>() is { } link)
            {
                var payload = await DownloadStringAsync(link, cancellationToken);
                return await ResolveChunksAsync(payload, cancellationToken);
            }

            return body;
        }

        /// <summary>Large result sets are split in chunks; stitch them back into one JSON array.</summary>
        private static async Task<string> ResolveChunksAsync(string payload, CancellationToken cancellationToken)
        {
            JsonNode? node;
            try { node = JsonNode.Parse(payload); }
            catch (JsonException) { return payload; }

            // Most payloads are plain arrays (series, cars, tracks...): nothing to stitch.
            if (node is not JsonObject obj)
                return payload;

            var chunkInfo = (obj["chunk_info"] ?? (obj["data"] as JsonObject)?["chunk_info"]) as JsonObject;
            if (chunkInfo?["chunk_file_names"] is not JsonArray files || chunkInfo["base_download_url"]?.GetValue<string>() is not { } baseUrl)
                return payload;

            var sb = new StringBuilder("[");
            var first = true;
            foreach (var file in files)
            {
                var chunk = await DownloadStringAsync(baseUrl + file!.GetValue<string>(), cancellationToken);
                var trimmed = chunk.Trim();
                if (trimmed.StartsWith('[')) trimmed = trimmed[1..^1];
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (!first) sb.Append(',');
                sb.Append(trimmed);
                first = false;
            }
            sb.Append(']');
            return sb.ToString();
        }

        private void SetConnected(string? displayName)
        {
            if (ConnectedDisplayName == displayName)
                return;
            ConnectedDisplayName = displayName;
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
            var version = typeof(IRacingDataClient).Assembly.GetName().Version?.ToString(3) ?? "dev";
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AiSeasonCreator", version));
            return client;
        }

        private static string Truncate(string s) => s.Length > 200 ? s[..200] + "..." : s;
    }
}
