using System.Drawing;

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
}
