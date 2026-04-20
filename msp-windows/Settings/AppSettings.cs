using System.Runtime.Serialization;

namespace msp_windows.Settings
{
    [DataContract]
    public class AppSettings
    {
        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                ServerBaseUrl = "http://localhost:8080",
                AccessToken = null,
                LastSelectedOverlayId = null,
                CacheEnabled = true
            };
        }

        [DataMember(Name = "serverBaseUrl", EmitDefaultValue = true)]
        public string ServerBaseUrl { get; set; }

        [DataMember(Name = "accessToken", EmitDefaultValue = true)]
        public string AccessToken { get; set; }

        [DataMember(Name = "lastSelectedOverlayId", EmitDefaultValue = true)]
        public string LastSelectedOverlayId { get; set; }

        [DataMember(Name = "cacheEnabled", EmitDefaultValue = true)]
        public bool CacheEnabled { get; set; }
    }
}
