using System.Collections.Generic;

namespace msp_windows.Overlay.Models
{
    public class OverlayDocument
    {
        public string SchemaVersion { get; set; }
        public string OverlayId { get; set; }
        public string Name { get; set; }
        public string Platform { get; set; }

        public OverlayGame Game { get; set; }
        public OverlayCanvas Canvas { get; set; }
        public OverlaySettings OverlaySettings { get; set; }

        public List<OverlayElementBase> Elements { get; set; } = new List<OverlayElementBase>();
        public OverlayMeta Meta { get; set; }
    }
}
