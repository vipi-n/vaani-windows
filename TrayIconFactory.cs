using System.Drawing;
using System.Drawing.Drawing2D;

namespace Vaani;

/// <summary>Draws the Vaani waveform mark as a tray icon (System.Drawing).</summary>
public static class TrayIconFactory
{
    public static Icon Create(Color tint)
    {
        int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            double[] heights = { 0.34, 0.6, 0.85, 1.0, 0.85, 0.6, 0.34 };
            int count = heights.Length;
            float barW = size * 0.075f;
            float gap = size * 0.058f;
            float totalW = count * barW + (count - 1) * gap;
            float startX = (size - totalW) / 2f;
            float maxH = size * 0.72f;
            float cy = size / 2f;

            using var brush = new SolidBrush(tint);
            for (int i = 0; i < count; i++)
            {
                float h = (float)(maxH * heights[i]);
                float x = startX + i * (barW + gap);
                var rect = new RectangleF(x, cy - h / 2f, barW, h);
                using var path = Rounded(rect, barW / 2f);
                g.FillPath(brush, path);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
