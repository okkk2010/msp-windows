using System;
using System.Collections.Generic;
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

                        ErrorLogger.LogError("E202", $"{relativeUrl} invalid response: {status}\n{body}");

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
                    return (ApiResponse<T>)serializer.ReadObject(ms);
                }
            }
            catch {
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
