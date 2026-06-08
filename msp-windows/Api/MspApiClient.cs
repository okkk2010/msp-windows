using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using msp_windows.Api.Dtos;

namespace msp_windows.Api
{
    public class MspApiClient : IDisposable
    {
        private const string AppFolderName = "msp-overlay";

        private readonly HttpClient _httpClient;

        public MspApiClient(string serverBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl)) throw new ArgumentException("serverBaseUrl is required.", nameof(serverBaseUrl));

            if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var baseUri)) {
                throw new ArgumentException("Invalid serverBaseUrl.", nameof(serverBaseUrl));
            }

            if (!serverBaseUrl.EndsWith("/", StringComparison.Ordinal)) {
                serverBaseUrl += "/";
            }

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(serverBaseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public Task<ApiResponse<OverlayDetailResponse>> GetOverlayByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code is required.", nameof(code));
            code = code.Trim();
            return GetAsync<OverlayDetailResponse>($"api/overlays/code/{Uri.EscapeDataString(code)}", accessToken: null);
        }

        public Task<ApiResponse<UserMeResponse>> GetMeAsync(string accessToken)
        {
            return GetAsync<UserMeResponse>("api/auth/me", accessToken);
        }

        public Task<ApiResponse<List<LibraryItemResponse>>> GetMyLibraryAsync(string accessToken)
        {
            return GetAsync<List<LibraryItemResponse>>("api/library", accessToken);
        }

        public Task<ApiResponse<OverlayDetailResponse>> GetOverlayDetailAsync(long id, string accessToken = null)
        {
            return GetAsync<OverlayDetailResponse>($"api/overlays/{id}", accessToken);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try {
                using (var resp = await _httpClient.GetAsync("api/platforms").ConfigureAwait(false)) {
                    return resp.IsSuccessStatusCode;
                }
            }
            catch {
                return false;
            }
        }

        private async Task<ApiResponse<T>> GetAsync<T>(string relativeUrl, string accessToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                if (!string.IsNullOrWhiteSpace(accessToken)) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }

                try {
                    using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        string status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

                        if (!response.IsSuccessStatusCode) {
                            var fail = TryDeserializeApiResponse<T>(body);
                            if (fail != null) {
                                if (string.IsNullOrWhiteSpace(fail.Message)) {
                                    fail.Message = status;
                                }
                                else {
                                    fail.Message = $"{fail.Message} ({status})";
                                }

                                ErrorLogger.LogError("E201", $"{relativeUrl} failed: {fail.Message}");
                                return fail;
                            }

                            return new ApiResponse<T>
                            {
                                Success = false,
                                Data = default(T),
                                Message = string.IsNullOrWhiteSpace(body) ? status : $"{status}\n{body}"
                            };
                        }

                        var ok = TryDeserializeApiResponse<T>(body);
                        if (ok != null) {
                            return ok;
                        }

                        LogInvalidResponseDiagnostics(relativeUrl, status, body);

                        return new ApiResponse<T>
                        {
                            Success = false,
                            Data = default(T),
                            Message = string.IsNullOrWhiteSpace(body) ? $"Invalid server response. ({status})" : $"Invalid server response. ({status})\n{body}"
                        };
                    }
                }
                catch (Exception ex) {
                    ErrorLogger.LogError("E203", $"{relativeUrl} exception: {ex.Message}");
                    return new ApiResponse<T>
                    {
                        Success = false,
                        Data = default(T),
                        Message = ex.Message
                    };
                }
            }
        }

        private static ApiResponse<T> TryDeserializeApiResponse<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) {
                return null;
            }

            try {
                var serializer = new DataContractJsonSerializer(typeof(ApiResponse<T>));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var response = (ApiResponse<T>)serializer.ReadObject(ms);
                    if (ShouldTryCompatibleOverlayResponse(response)) {
                        return TryDeserializeCompatibleApiResponse<T>(json) ?? response;
                    }

                    return response;
                }
            }
            catch {
                return TryDeserializeCompatibleApiResponse<T>(json);
            }
        }

        private static bool ShouldTryCompatibleOverlayResponse<T>(ApiResponse<T> response)
        {
            if (typeof(T) != typeof(OverlayDetailResponse) || response == null) {
                return false;
            }

            var overlay = (object)response.Data as OverlayDetailResponse;
            return response.Success && (overlay == null || string.IsNullOrWhiteSpace(overlay.OverlayJson));
        }

        private static ApiResponse<T> TryDeserializeCompatibleApiResponse<T>(string json)
        {
            if (typeof(T) == typeof(OverlayDetailResponse)) {
                return TryDeserializeCompatibleOverlayResponse<T>(json);
            }

            if (typeof(T) == typeof(List<LibraryItemResponse>)) {
                return TryDeserializeCompatibleLibraryResponse<T>(json);
            }

            return null;
        }

        private static ApiResponse<T> TryDeserializeCompatibleOverlayResponse<T>(string json)
        {
            try {
                var root = DeserializeToDictionary(json);
                var response = new ApiResponse<OverlayDetailResponse>
                {
                    Success = GetBoolOrDefault(root, "success", false),
                    Message = GetStringOrNull(root, "message")
                };

                var data = GetDictOrNull(root, "data");
                if (data != null) {
                    response.Data = new OverlayDetailResponse
                    {
                        Id = GetLongOrDefault(data, "id", 0),
                        OverlayId = GetStringOrNull(data, "overlayId"),
                        Code = GetStringOrNull(data, "code"),
                        Name = GetStringOrNull(data, "name"),
                        Platform = NormalizePlatform(GetValueOrNull(data, "platform")),
                        OverlayJson = NormalizeOverlayJson(GetValueOrNull(data, "overlayJson"))
                    };
                }

                return (ApiResponse<T>)(object)response;
            }
            catch (Exception ex) {
                ErrorLogger.LogError("E204", "Compatible response parse failed: " + ex.Message);
                return null;
            }
        }

        private static ApiResponse<T> TryDeserializeCompatibleLibraryResponse<T>(string json)
        {
            try {
                var root = DeserializeToDictionary(json);
                var response = new ApiResponse<List<LibraryItemResponse>>
                {
                    Success = GetBoolOrDefault(root, "success", false),
                    Message = GetStringOrNull(root, "message"),
                    Data = new List<LibraryItemResponse>()
                };

                var data = GetValueOrNull(root, "data") as IEnumerable<object>;
                if (data != null) {
                    foreach (var entry in data) {
                        if (!(entry is Dictionary<string, object> itemDict)) {
                            continue;
                        }

                        var overlayDict = GetDictOrNull(itemDict, "overlay");
                        response.Data.Add(new LibraryItemResponse
                        {
                            LibraryId = GetLongOrDefault(itemDict, "libraryId", 0),
                            SavedAt = GetStringOrNull(itemDict, "savedAt"),
                            Overlay = overlayDict == null ? null : new OverlaySummaryResponse
                            {
                                Id = GetLongOrDefault(overlayDict, "id", 0),
                                OverlayId = GetStringOrNull(overlayDict, "overlayId"),
                                Code = GetStringOrNull(overlayDict, "code"),
                                Name = GetStringOrNull(overlayDict, "name"),
                                Platform = NormalizePlatform(GetValueOrNull(overlayDict, "platform")),
                                Game = NormalizeGame(GetValueOrNull(overlayDict, "game")),
                                ThumbnailUrl = GetStringOrNull(overlayDict, "thumbnailPath")
                                    ?? GetStringOrNull(overlayDict, "thumbnailUrl")
                            }
                        });
                    }
                }

                return (ApiResponse<T>)(object)response;
            }
            catch (Exception ex) {
                ErrorLogger.LogError("E205", "Compatible library parse failed: " + ex.Message);
                return null;
            }
        }

        private static string NormalizeGame(object value)
        {
            if (value == null) {
                return null;
            }

            if (value is Dictionary<string, object> dict) {
                return GetStringOrNull(dict, "name")
                    ?? GetStringOrNull(dict, "slug")
                    ?? GetStringOrNull(dict, "id");
            }

            return value.ToString();
        }

        private static Dictionary<string, object> DeserializeToDictionary(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, object>), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(ms) as Dictionary<string, object>;
            }
        }

        private static string NormalizePlatform(object value)
        {
            if (value == null) {
                return null;
            }

            if (value is Dictionary<string, object> dict) {
                return GetStringOrNull(dict, "slug")
                    ?? GetStringOrNull(dict, "name")
                    ?? GetStringOrNull(dict, "id");
            }

            return value.ToString();
        }

        private static string NormalizeOverlayJson(object value)
        {
            if (value == null) {
                return null;
            }

            if (value is string text) {
                return text;
            }

            return WriteJsonValue(value);
        }

        private static object GetValueOrNull(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.TryGetValue(key, out var value)) {
                return null;
            }

            return value;
        }

        private static Dictionary<string, object> GetDictOrNull(Dictionary<string, object> dict, string key)
        {
            return GetValueOrNull(dict, key) as Dictionary<string, object>;
        }

        private static string GetStringOrNull(Dictionary<string, object> dict, string key)
        {
            var value = GetValueOrNull(dict, key);
            return value?.ToString();
        }

        private static bool GetBoolOrDefault(Dictionary<string, object> dict, string key, bool defaultValue)
        {
            var value = GetValueOrNull(dict, key);
            if (value == null) return defaultValue;
            if (value is bool b) return b;
            if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }

        private static long GetLongOrDefault(Dictionary<string, object> dict, string key, long defaultValue)
        {
            var value = GetValueOrNull(dict, key);
            if (value == null) return defaultValue;
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is double d) return (long)d;
            if (long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return defaultValue;
        }

        private static string WriteJsonValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + EscapeJsonString(s) + "\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal) {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is Dictionary<string, object> dict) {
                var parts = new List<string>();
                foreach (var pair in dict) {
                    parts.Add("\"" + EscapeJsonString(pair.Key) + "\":" + WriteJsonValue(pair.Value));
                }

                return "{" + string.Join(",", parts.ToArray()) + "}";
            }

            if (value is IDictionary dictionary) {
                var parts = new List<string>();
                foreach (DictionaryEntry entry in dictionary) {
                    parts.Add("\"" + EscapeJsonString(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)) + "\":" + WriteJsonValue(entry.Value));
                }

                return "{" + string.Join(",", parts.ToArray()) + "}";
            }

            if (value is IEnumerable enumerable) {
                var parts = new List<string>();
                foreach (var item in enumerable) {
                    parts.Add(WriteJsonValue(item));
                }

                return "[" + string.Join(",", parts.ToArray()) + "]";
            }

            return "\"" + EscapeJsonString(value.ToString()) + "\"";
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value) {
                switch (c) {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 32) {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        private static void LogInvalidResponseDiagnostics(string relativeUrl, string status, string body)
        {
            ErrorLogger.LogError("E202", relativeUrl + " invalid response: " + status + Environment.NewLine + body);

            try {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDirectoryPath = Path.Combine(appData, AppFolderName, "logs");
                Directory.CreateDirectory(logDirectoryPath);

                string logFilePath = Path.Combine(logDirectoryPath, "invalid-server-response.log");
                string logEntry =
                    "---- " + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) + " ----" + Environment.NewLine +
                    "URL: " + relativeUrl + Environment.NewLine +
                    "Status: " + status + Environment.NewLine +
                    "Body:" + Environment.NewLine +
                    body + Environment.NewLine + Environment.NewLine;

                File.AppendAllText(logFilePath, logEntry, Encoding.UTF8);
            }
            catch {
                // Diagnostic file logging must not hide the original API failure.
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
