namespace msp_windows.Overlay.Models
{
    public class LineElement : OverlayElementBase
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public string StrokeColor { get; set; }
        public double StrokeWidth { get; set; }
        public string DashStyle { get; set; }
    }
}
