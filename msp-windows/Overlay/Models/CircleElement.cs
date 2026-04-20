namespace msp_windows.Overlay.Models
{
    public class CircleElement : OverlayElementBase
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }

        public string FillColor { get; set; }
        public string StrokeColor { get; set; }
        public double StrokeWidth { get; set; }
    }
}
