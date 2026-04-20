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

        public void SaveOverlayJson(string overlayId, string overlayJson)
        {
            if (string.IsNullOrWhiteSpace(overlayId)) throw new ArgumentException("overlayId is required.", nameof(overlayId));
            if (string.IsNullOrWhiteSpace(overlayJson)) throw new ArgumentException("overlayJson is required.", nameof(overlayJson));

            string dir = Path.Combine(RootCacheDirectoryPath, overlayId);
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "overlay.json");
            File.WriteAllText(path, overlayJson, Encoding.UTF8);
        }

        public string TryLoadOverlayJson(string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId)) return null;

            string path = Path.Combine(RootCacheDirectoryPath, overlayId, "overlay.json");
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
