using System;
using System.IO;
using System.Text;

namespace msp_windows.Overlay
{
    public class OverlayCacheService
    {
        private const string AppFolderName = "msp-overlay";

        public string RootCacheDirectoryPath { get; }

        public OverlayCacheService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            RootCacheDirectoryPath = Path.Combine(appData, AppFolderName, "cache", "overlays");
        }

        public void SaveOverlayJson(string code, string overlayJson)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(overlayJson)) throw new ArgumentException("overlayJson is required.", nameof(overlayJson));

            Directory.CreateDirectory(RootCacheDirectoryPath);

            string path = Path.Combine(RootCacheDirectoryPath, $"{code}.json");
            File.WriteAllText(path, overlayJson, Encoding.UTF8);
        }

        public bool DeleteOverlayJson(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;

            string path = Path.Combine(RootCacheDirectoryPath, $"{code}.json");
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                    return true;
                }
            }
            catch {
                return false;
            }

            return false;
        }

        public string TryLoadOverlayJson(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            string path = Path.Combine(RootCacheDirectoryPath, $"{code}.json");
            if (!File.Exists(path)) return null;

            try {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch {
                return null;
            }
        }
    }
}
