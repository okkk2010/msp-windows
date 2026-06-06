namespace msp_windows.Overlay.Models
{
    public class RectElement : OverlayElementBase
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Anchor { get; set; } = "top-left";
        public string AnchorSpace { get; set; } = "safeFrame";
        public double Rotation { get; set; }

        public string FillColor { get; set; }
        public string StrokeColor { get; set; }
        public double StrokeWidth { get; set; }
        public double CornerRadius { get; set; }
    }
}
