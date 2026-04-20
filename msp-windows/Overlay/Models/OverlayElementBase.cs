namespace msp_windows.Overlay.Models
{
    public abstract class OverlayElementBase
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public double Opacity { get; set; } = 1.0;
        public int ZIndex { get; set; }
        public bool Visible { get; set; } = true;
        public bool Locked { get; set; }
    }
}
