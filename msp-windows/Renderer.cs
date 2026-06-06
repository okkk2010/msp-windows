using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using msp_windows.Overlay.Models;

public static class Renderer
{
    public static void DrawOverlayUI(Graphics g, Rectangle bounds)
    {
        using (SolidBrush brush = new SolidBrush(SettingsManager.SelectedOverlayColor)) {
            int rectSize = 50;
            int midRectSize = 10;
            int offset = 15;

            g.FillRectangle(brush, bounds.Width / 2 - rectSize / 2, 0, rectSize, rectSize);
            g.FillRectangle(brush, 0, bounds.Height / 2 - rectSize / 2, rectSize, rectSize);
            g.FillRectangle(brush, bounds.Width / 2 - rectSize / 2, bounds.Height - rectSize, rectSize, rectSize);
            g.FillRectangle(brush, bounds.Width - rectSize, bounds.Height / 2 - rectSize / 2, rectSize, rectSize);

            g.FillRectangle(brush, bounds.Width / 2 - midRectSize / 2, bounds.Height / 2 - midRectSize - offset, midRectSize, midRectSize);
            g.FillRectangle(brush, bounds.Width / 2 - midRectSize - offset, bounds.Height / 2 - midRectSize / 2, midRectSize, midRectSize);
            g.FillRectangle(brush, bounds.Width / 2 - midRectSize / 2, bounds.Height / 2 + offset, midRectSize, midRectSize);
            g.FillRectangle(brush, bounds.Width / 2 + offset, bounds.Height / 2 - midRectSize / 2, midRectSize, midRectSize);
        }
    }

    public static void DrawOverlayDocument(Graphics g, Rectangle bounds, OverlayDocument document)
    {
        if (document == null || document.Canvas == null || document.Elements == null) {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (document.Canvas.BaseWidth <= 0 || document.Canvas.BaseHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }

        double scaleX = bounds.Width / document.Canvas.BaseWidth;
        double scaleY = bounds.Height / document.Canvas.BaseHeight;
        var transform = RenderTransform.Contain(bounds, document.Canvas.BaseWidth, document.Canvas.BaseHeight);

        double globalOpacity = Clamp01(document.OverlaySettings?.Opacity ?? 1.0);

        var elements = new System.Collections.Generic.List<OverlayElementBase>(document.Elements);
        elements.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

        foreach (var el in elements) {
            if (el == null) continue;
            if (!el.Visible) continue;

            double elementOpacity = Clamp01(el.Opacity);
            double finalOpacity = Clamp01(globalOpacity * elementOpacity);

            switch (el) {
                case RectElement rect:
                    DrawRect(g, rect, transform, finalOpacity);
                    break;
                case CircleElement circle:
                    DrawCircle(g, circle, transform, finalOpacity);
                    break;
                case LineElement line:
                    DrawLine(g, line, scaleX, scaleY, finalOpacity);
                    break;
            }
        }
    }

    private static void DrawRect(Graphics g, RectElement rect, RenderTransform transform, double opacity)
    {
        var r = new RectangleF(
            transform.X(rect.X, rect.Width, rect.Anchor, rect.AnchorSpace),
            transform.Y(rect.Y, rect.Height, rect.Anchor, rect.AnchorSpace),
            transform.Size(rect.Width),
            transform.Size(rect.Height));
        float scaledCornerRadius = transform.Size(rect.CornerRadius);

        DrawWithRotation(g, r, rect.Rotation, () =>
        {
            using (var path = BuildRoundedRectanglePath(r, scaledCornerRadius)) {
                if (!string.IsNullOrWhiteSpace(rect.FillColor)) {
                    using (var b = new SolidBrush(ApplyOpacity(ParseColor(rect.FillColor), opacity))) {
                        g.FillPath(b, path);
                    }
                }

                if (!string.IsNullOrWhiteSpace(rect.StrokeColor) && rect.StrokeWidth > 0) {
                    using (var p = new Pen(ApplyOpacity(ParseColor(rect.StrokeColor), opacity), transform.Size(rect.StrokeWidth))) {
                        g.DrawPath(p, path);
                    }
                }
            }
        });
    }

    private static void DrawCircle(Graphics g, CircleElement circle, RenderTransform transform, double opacity)
    {
        var r = new RectangleF(
            transform.X(circle.X, circle.Width, circle.Anchor, circle.AnchorSpace),
            transform.Y(circle.Y, circle.Height, circle.Anchor, circle.AnchorSpace),
            transform.Size(circle.Width),
            transform.Size(circle.Height));

        DrawWithRotation(g, r, circle.Rotation, () =>
        {
            if (!string.IsNullOrWhiteSpace(circle.FillColor)) {
                using (var b = new SolidBrush(ApplyOpacity(ParseColor(circle.FillColor), opacity))) {
                    g.FillEllipse(b, r);
                }
            }

            if (!string.IsNullOrWhiteSpace(circle.StrokeColor) && circle.StrokeWidth > 0) {
                using (var p = new Pen(ApplyOpacity(ParseColor(circle.StrokeColor), opacity), transform.Size(circle.StrokeWidth))) {
                    g.DrawEllipse(p, r);
                }
            }
        });
    }

    private static void DrawLine(Graphics g, LineElement line, double scaleX, double scaleY, double opacity)
    {
        if (string.IsNullOrWhiteSpace(line.StrokeColor) || line.StrokeWidth <= 0) {
            return;
        }

        using (var p = new Pen(ApplyOpacity(ParseColor(line.StrokeColor), opacity), (float)(line.StrokeWidth * ((scaleX + scaleY) / 2.0))))
        {
            p.DashStyle = ParseDashStyle(line.DashStyle);
            g.DrawLine(
                p,
                (float)(line.X1 * scaleX),
                (float)(line.Y1 * scaleY),
                (float)(line.X2 * scaleX),
                (float)(line.Y2 * scaleY));
        }
    }

    private static void DrawWithRotation(Graphics g, RectangleF rect, double rotationDeg, System.Action draw)
    {
        if (draw == null) return;
        if (rotationDeg == 0) {
            draw();
            return;
        }

        var state = g.Save();
        try {
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)rotationDeg);
            g.TranslateTransform(-cx, -cy);

            draw();
        }
        finally {
            g.Restore(state);
        }
    }

    private static Color ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Color.Empty;

        value = value.Trim();
        if (value.StartsWith("rgba", System.StringComparison.OrdinalIgnoreCase)) {
            return ParseRgbaColor(value);
        }

        if (value.StartsWith("rgb", System.StringComparison.OrdinalIgnoreCase)) {
            return ParseRgbColor(value);
        }

        if (value.StartsWith("#")) {
            string hex = value.Substring(1);

            if (hex.Length == 6) {
                int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return Color.FromArgb(255, r, g, b);
            }

            if (hex.Length == 8) {
                int a = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                int r = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                return Color.FromArgb(a, r, g, b);
            }
        }

        return Color.Empty;
    }

    private static Color ParseRgbaColor(string value)
    {
        Match match = Regex.Match(
            value,
            @"^rgba\(\s*(?<r>\d+)\s*,\s*(?<g>\d+)\s*,\s*(?<b>\d+)\s*,\s*(?<a>[\d.]+)\s*\)$",
            RegexOptions.IgnoreCase);

        if (!match.Success) {
            return Color.Empty;
        }

        int r = ClampByte(ParseInt(match.Groups["r"].Value));
        int g = ClampByte(ParseInt(match.Groups["g"].Value));
        int b = ClampByte(ParseInt(match.Groups["b"].Value));
        double alpha = ParseDouble(match.Groups["a"].Value);
        int a = ClampByte((int)System.Math.Round(Clamp01(alpha) * 255.0));
        return Color.FromArgb(a, r, g, b);
    }

    private static Color ParseRgbColor(string value)
    {
        Match match = Regex.Match(
            value,
            @"^rgb\(\s*(?<r>\d+)\s*,\s*(?<g>\d+)\s*,\s*(?<b>\d+)\s*\)$",
            RegexOptions.IgnoreCase);

        if (!match.Success) {
            return Color.Empty;
        }

        int r = ClampByte(ParseInt(match.Groups["r"].Value));
        int g = ClampByte(ParseInt(match.Groups["g"].Value));
        int b = ClampByte(ParseInt(match.Groups["b"].Value));
        return Color.FromArgb(255, r, g, b);
    }

    private static Color ApplyOpacity(Color color, double opacity)
    {
        if (color == Color.Empty) return color;
        int a = (int)(color.A * Clamp01(opacity));
        if (a < 0) a = 0;
        if (a > 255) a = 255;
        return Color.FromArgb(a, color.R, color.G, color.B);
    }

    private static DashStyle ParseDashStyle(string dashStyle)
    {
        if (string.IsNullOrWhiteSpace(dashStyle)) return DashStyle.Solid;

        switch (dashStyle.Trim().ToLowerInvariant()) {
            case "dash":
                return DashStyle.Dash;
            case "dot":
                return DashStyle.Dot;
            case "dashdot":
                return DashStyle.DashDot;
            case "dashdotdot":
                return DashStyle.DashDotDot;
            default:
                return DashStyle.Solid;
        }
    }

    private static double Clamp01(double value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }

    private static int ParseInt(string value)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static int ClampByte(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return value;
    }

    private static GraphicsPath BuildRoundedRectanglePath(RectangleF rect, float cornerRadius)
    {
        var path = new GraphicsPath();
        float radius = System.Math.Max(0f, cornerRadius);
        float maxRadius = System.Math.Min(rect.Width, rect.Height) / 2f;
        radius = System.Math.Min(radius, maxRadius);

        if (radius <= 0f) {
            path.AddRectangle(rect);
            path.CloseFigure();
            return path;
        }

        float diameter = radius * 2f;
        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.X;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private struct RenderTransform
    {
        private readonly double scale;
        private readonly Rectangle bounds;
        private readonly double baseWidth;
        private readonly double baseHeight;
        private readonly double safeLeft;
        private readonly double safeTop;
        private readonly double safeRight;
        private readonly double safeBottom;

        private RenderTransform(double scale, Rectangle bounds, double baseWidth, double baseHeight, double safeLeft, double safeTop)
        {
            this.scale = scale;
            this.bounds = bounds;
            this.baseWidth = baseWidth;
            this.baseHeight = baseHeight;
            this.safeLeft = safeLeft;
            this.safeTop = safeTop;
            this.safeRight = safeLeft + baseWidth * scale;
            this.safeBottom = safeTop + baseHeight * scale;
        }

        public static RenderTransform Contain(Rectangle bounds, double baseWidth, double baseHeight)
        {
            double scale = System.Math.Min(bounds.Width / baseWidth, bounds.Height / baseHeight);
            double scaledWidth = baseWidth * scale;
            double scaledHeight = baseHeight * scale;
            double safeLeft = bounds.Left + (bounds.Width - scaledWidth) / 2.0;
            double safeTop = bounds.Top + (bounds.Height - scaledHeight) / 2.0;
            return new RenderTransform(scale, bounds, baseWidth, baseHeight, safeLeft, safeTop);
        }

        public float X(double value, double width, string anchor, string anchorSpace)
        {
            double frameLeft = IsScreenSpace(anchorSpace) ? bounds.Left : safeLeft;
            double frameRight = IsScreenSpace(anchorSpace) ? bounds.Right : safeRight;
            double frameCenter = (frameLeft + frameRight) / 2.0;
            double renderWidth = width * scale;
            double marginRight = baseWidth - value - width;
            double centerOffset = (value + width / 2.0) - baseWidth / 2.0;

            switch (NormalizeAnchor(anchor)) {
                case "top-right":
                case "right":
                case "bottom-right":
                    return (float)(frameRight - marginRight * scale - renderWidth);
                case "top":
                case "center":
                case "bottom":
                    return (float)(frameCenter + centerOffset * scale - renderWidth / 2.0);
                default:
                    return (float)(frameLeft + value * scale);
            }
        }

        public float Y(double value, double height, string anchor, string anchorSpace)
        {
            double frameTop = IsScreenSpace(anchorSpace) ? bounds.Top : safeTop;
            double frameBottom = IsScreenSpace(anchorSpace) ? bounds.Bottom : safeBottom;
            double frameCenter = (frameTop + frameBottom) / 2.0;
            double renderHeight = height * scale;
            double marginBottom = baseHeight - value - height;
            double centerOffset = (value + height / 2.0) - baseHeight / 2.0;

            switch (NormalizeAnchor(anchor)) {
                case "bottom-left":
                case "bottom":
                case "bottom-right":
                    return (float)(frameBottom - marginBottom * scale - renderHeight);
                case "left":
                case "center":
                case "right":
                    return (float)(frameCenter + centerOffset * scale - renderHeight / 2.0);
                default:
                    return (float)(frameTop + value * scale);
            }
        }

        public float Size(double value)
        {
            return (float)(value * scale);
        }

        private static string NormalizeAnchor(string anchor)
        {
            return string.IsNullOrWhiteSpace(anchor) ? "top-left" : anchor.Trim().ToLowerInvariant();
        }

        private static bool IsScreenSpace(string anchorSpace)
        {
            return string.Equals(anchorSpace, "screen", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
