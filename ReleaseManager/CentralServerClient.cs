using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WindowsRemoteTools.ReleaseManager
{
    /// <summary>
    /// Thin REST client for the CentralServer auto-update endpoints. Handles
    /// the JWT-token-in-`token`-header auth scheme that the server's other
    /// REST APIs already use.
    /// </summary>
    public class CentralServerClient : IDisposable
    {
        private readonly HttpClient _http;
        private string? _token;

        public string BaseUrl { get; }

        public bool HasToken => !string.IsNullOrEmpty(_token);

        public CentralServerClient(string baseUrl)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        }

        public void Dispose() => _http.Dispose();

        /// <summary>
        /// POST /signIn — returns the JWT in the `token` response header.
        /// </summary>
        public async Task LoginAsync(string username, string password)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/signIn")
            {
                Content = JsonContent(new { username, password })
            };
            using var resp = await _http.SendAsync(req).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Login failed ({(int)resp.StatusCode}): {body}");
            }
            if (resp.Headers.TryGetValues("token", out var values))
            {
                foreach (var v in values) { _token = v; break; }
            }
            if (string.IsNullOrEmpty(_token))
            {
                throw new InvalidOperationException("Login succeeded but server returned no token header");
            }
        }

        /// <summary>
        /// GET /api/updates/status — admin only; returns published manifest +
        /// public key state. Throws on auth/server errors.
        /// </summary>
        public async Task<JObject> GetStatusAsync()
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/updates/status");
            AddAuth(req);
            using var resp = await _http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"status failed ({(int)resp.StatusCode}): {body}");
            return JObject.Parse(body);
        }

        /// <summary>
        /// POST /api/updates/keys/generate — one-shot keypair creation. Server
        /// refuses if keys already exist (409).
        /// </summary>
        public async Task<JObject> GenerateKeysAsync()
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/updates/keys/generate");
            AddAuth(req);
            using var resp = await _http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"generate-keys failed ({(int)resp.StatusCode}): {body}");
            return JObject.Parse(body);
        }

        /// <summary>
        /// POST /api/updates/publish — multipart upload of the MSI plus
        /// version / notes form fields.
        /// </summary>
        public async Task<JObject> PublishAsync(string msiPath, string version, string? notes, string? minPriorVersion)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(version), "version");
            if (!string.IsNullOrWhiteSpace(notes))
                content.Add(new StringContent(notes!), "notes");
            if (!string.IsNullOrWhiteSpace(minPriorVersion))
                content.Add(new StringContent(minPriorVersion!), "minPriorVersion");

            var fileStream = File.OpenRead(msiPath);
            try
            {
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msi");
                content.Add(fileContent, "msi", Path.GetFileName(msiPath));

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/updates/publish") { Content = content };
                AddAuth(req);
                using var resp = await _http.SendAsync(req).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"publish failed ({(int)resp.StatusCode}): {body}");
                return JObject.Parse(body);
            }
            finally
            {
                fileStream.Dispose();
            }
        }

        /// <summary>
        /// GET the public manifest (no auth required) — used to display the
        /// currently-published version even when not logged in.
        /// </summary>
        public async Task<JObject?> GetPublicManifestAsync(string manifestUrl)
        {
            try
            {
                var body = await _http.GetStringAsync(manifestUrl).ConfigureAwait(false);
                return JObject.Parse(body);
            }
            catch
            {
                return null;
            }
        }

        private void AddAuth(HttpRequestMessage req)
        {
            if (string.IsNullOrEmpty(_token))
                throw new InvalidOperationException("not logged in — call LoginAsync first");
            req.Headers.Add("token", _token);
        }

        private static StringContent JsonContent(object payload) =>
            new(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
    }
}
