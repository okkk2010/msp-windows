using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace msp_windows.Settings
{
    public class AppSettingsService
    {
        private const string AppFolderName = "msp-overlay";
        private const string SettingsFileName = "settings.json";

        public string SettingsDirectoryPath { get; }
        public string SettingsFilePath { get; }

        public AppSettings Current { get; private set; }

        public AppSettingsService()
        {
            SettingsDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);
            SettingsFilePath = Path.Combine(SettingsDirectoryPath, SettingsFileName);
        }

        public AppSettings LoadOrCreate()
        {
            Directory.CreateDirectory(SettingsDirectoryPath);

            if (!File.Exists(SettingsFilePath)) {
                Current = AppSettings.CreateDefault();
                Save(Current);
                return Current;
            }

            try {
                Current = Load();
                if (Current == null) {
                    throw new InvalidDataException("settings.json deserialized to null.");
                }

                if (string.IsNullOrWhiteSpace(Current.ServerBaseUrl)) {
                    Current.ServerBaseUrl = AppSettings.CreateDefault().ServerBaseUrl;
                    Save(Current);
                }

                return Current;
            }
            catch {
                BackupCorruptedSettingsFile();
                Current = AppSettings.CreateDefault();
                Save(Current);
                return Current;
            }
        }

        public AppSettings Load()
        {
            using (var stream = File.OpenRead(SettingsFilePath)) {
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                return (AppSettings)serializer.ReadObject(stream);
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Directory.CreateDirectory(SettingsDirectoryPath);

            var serializer = new DataContractJsonSerializer(typeof(AppSettings));
            using (var ms = new MemoryStream()) {
                serializer.WriteObject(ms, settings);
                string json = Encoding.UTF8.GetString(ms.ToArray());

                json = json.Replace(",\"", ",\n  \"");
                json = json.Replace("{\"", "{\n  \"");
                json = json.Replace("\"}", "\"\n}");

                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            }

            Current = settings;
        }

        public void UpdateServerBaseUrl(string serverBaseUrl)
        {
            if (Current == null) {
                LoadOrCreate();
            }

            Current.ServerBaseUrl = serverBaseUrl;
            Save(Current);
        }

        public void UpdateLastSelectedOverlayId(string lastSelectedOverlayId)
        {
            if (Current == null) {
                LoadOrCreate();
            }

            Current.LastSelectedOverlayId = lastSelectedOverlayId;
            Save(Current);
        }

        public void UpdateAccessToken(string accessToken)
        {
            if (Current == null) {
                LoadOrCreate();
            }

            Current.AccessToken = accessToken;
            Save(Current);
        }

        private void BackupCorruptedSettingsFile()
        {
            try {
                if (!File.Exists(SettingsFilePath)) {
                    return;
                }

                string backupPath = SettingsFilePath + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(SettingsFilePath, backupPath, overwrite: true);
            }
            catch {
                // backup 실패는 무시하고 default로 재생성
            }
        }
    }
}
