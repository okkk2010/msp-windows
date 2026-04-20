using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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

        double scaleX = bounds.Width / document.Canvas.BaseWidth;
        double scaleY = bounds.Height / document.Canvas.BaseHeight;

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
                    DrawRect(g, rect, scaleX, scaleY, finalOpacity);
                    break;
                case CircleElement circle:
                    DrawCircle(g, circle, scaleX, scaleY, finalOpacity);
                    break;
                case LineElement line:
                    DrawLine(g, line, scaleX, scaleY, finalOpacity);
                    break;
            }
        }
    }

    private static void DrawRect(Graphics g, RectElement rect, double scaleX, double scaleY, double opacity)
    {
        var r = new RectangleF(
            (float)(rect.X * scaleX),
            (float)(rect.Y * scaleY),
            (float)(rect.Width * scaleX),
            (float)(rect.Height * scaleY));

        DrawWithRotation(g, r, rect.Rotation, () =>
        {
            if (!string.IsNullOrWhiteSpace(rect.FillColor)) {
                using (var b = new SolidBrush(ApplyOpacity(ParseColor(rect.FillColor), opacity))) {
                    g.FillRectangle(b, r);
                }
            }

            if (!string.IsNullOrWhiteSpace(rect.StrokeColor) && rect.StrokeWidth > 0) {
                using (var p = new Pen(ApplyOpacity(ParseColor(rect.StrokeColor), opacity), (float)(rect.StrokeWidth * ((scaleX + scaleY) / 2.0)))) {
                    g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                }
            }
        });
    }

    private static void DrawCircle(Graphics g, CircleElement circle, double scaleX, double scaleY, double opacity)
    {
        var r = new RectangleF(
            (float)(circle.X * scaleX),
            (float)(circle.Y * scaleY),
            (float)(circle.Width * scaleX),
            (float)(circle.Height * scaleY));

        DrawWithRotation(g, r, circle.Rotation, () =>
        {
            if (!string.IsNullOrWhiteSpace(circle.FillColor)) {
                using (var b = new SolidBrush(ApplyOpacity(ParseColor(circle.FillColor), opacity))) {
                    g.FillEllipse(b, r);
                }
            }

            if (!string.IsNullOrWhiteSpace(circle.StrokeColor) && circle.StrokeWidth > 0) {
                using (var p = new Pen(ApplyOpacity(ParseColor(circle.StrokeColor), opacity), (float)(circle.StrokeWidth * ((scaleX + scaleY) / 2.0)))) {
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
}
