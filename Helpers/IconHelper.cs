using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace CrealitySparkXWidget.Helpers;

public static class IconHelper
{
    public static Icon CreatePrinterIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Dark background rounded box
        using var path = CreateRoundedRectanglePath(new Rectangle(2, 2, 60, 60), 14);
        using (var bgBrush = new LinearGradientBrush(new Point(0, 0), new Point(64, 64),
                   Color.FromArgb(24, 24, 32), Color.FromArgb(15, 15, 20)))
        {
            g.FillPath(bgBrush, path);
        }

        // Cyan / Emerald gradient border
        using (var borderPen = new Pen(Color.FromArgb(0, 210, 106), 3.5f))
        {
            g.DrawPath(borderPen, path);
        }

        // Draw Stylized 3D Printer / Extruder Nozzle
        // Frame top beam
        using (var beamBrush = new SolidBrush(Color.FromArgb(200, 215, 230)))
        {
            g.FillRectangle(beamBrush, 14, 16, 36, 4);
        }

        // Vertical rails
        using (var railPen = new Pen(Color.FromArgb(100, 115, 130), 3f))
        {
            g.DrawLine(railPen, 16, 16, 16, 46);
            g.DrawLine(railPen, 48, 16, 48, 46);
        }

        // Heated Bed (plate)
        using (var bedBrush = new SolidBrush(Color.FromArgb(245, 158, 11))) // warm amber
        {
            g.FillRectangle(bedBrush, 18, 44, 28, 4);
        }

        // Extruder Toolhead block
        using (var toolheadBrush = new SolidBrush(Color.FromArgb(0, 210, 106))) // Creality green
        {
            g.FillRectangle(toolheadBrush, 28, 22, 8, 8);
        }

        // Nozzle cone
        Point[] nozzlePoints = {
            new(29, 30),
            new(35, 30),
            new(32, 35)
        };
        using (var nozzleBrush = new SolidBrush(Color.FromArgb(255, 215, 0)))
        {
            g.FillPolygon(nozzleBrush, nozzlePoints);
        }

        // Laser / Spark dot
        using (var sparkBrush = new SolidBrush(Color.FromArgb(0, 240, 255)))
        {
            g.FillEllipse(sparkBrush, 31, 37, 3, 3);
        }

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public static void EnsureIconFile()
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "app.ico");
            if (!File.Exists(file))
            {
                using var icon = CreatePrinterIcon();
                using var fs = new FileStream(file, FileMode.Create);
                icon.Save(fs);
            }
        }
        catch { }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
    {
        var path = new GraphicsPath();
        int diameter = cornerRadius * 2;
        var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
