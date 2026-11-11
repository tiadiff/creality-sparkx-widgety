using System.Windows;
using System.Windows.Media;

namespace CrealitySparkXWidget.Helpers;

public static class TaskbarPositioner
{
    public static void PositionAboveTaskbar(Window window)
    {
        // 1. Get mouse position in physical screen coordinates
        var cursorPoint = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursorPoint);

        // 2. Obtain DPI scale factors for the window/screen
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }
        else
        {
            // Fallback from system
            using var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            dpiScaleX = graphics.DpiX / 96.0;
            dpiScaleY = graphics.DpiY / 96.0;
        }

        // Convert physical bounds to WPF DIPs
        double workAreaLeft = screen.WorkingArea.Left / dpiScaleX;
        double workAreaTop = screen.WorkingArea.Top / dpiScaleY;
        double workAreaWidth = screen.WorkingArea.Width / dpiScaleX;
        double workAreaHeight = screen.WorkingArea.Height / dpiScaleY;
        double workAreaRight = workAreaLeft + workAreaWidth;
        double workAreaBottom = workAreaTop + workAreaHeight;

        double cursorX = cursorPoint.X / dpiScaleX;
        double cursorY = cursorPoint.Y / dpiScaleY;

        if (window.ActualHeight <= 0 || double.IsNaN(window.ActualHeight))
        {
            window.Measure(new System.Windows.Size(window.Width, double.PositiveInfinity));
        }

        double windowWidth = window.ActualWidth > 0 ? window.ActualWidth : (window.DesiredSize.Width > 0 ? window.DesiredSize.Width : window.Width);
        double windowHeight = window.ActualHeight > 0 ? window.ActualHeight : (window.DesiredSize.Height > 0 ? window.DesiredSize.Height : 520);
        if (windowHeight <= 0) windowHeight = 520;

        const double margin = 12.0;

        // Determine taskbar edge
        // Taskbar is at the bottom if screen.Bounds.Bottom != screen.WorkingArea.Bottom
        bool taskbarAtBottom = screen.Bounds.Bottom != screen.WorkingArea.Bottom;
        bool taskbarAtTop = screen.Bounds.Top != screen.WorkingArea.Top;
        bool taskbarAtLeft = screen.Bounds.Left != screen.WorkingArea.Left;
        bool taskbarAtRight = screen.Bounds.Right != screen.WorkingArea.Right;

        double targetLeft;
        double targetTop;

        if (taskbarAtTop)
        {
            targetTop = workAreaTop + margin;
            targetLeft = cursorX - (windowWidth / 2.0);
        }
        else if (taskbarAtLeft)
        {
            targetLeft = workAreaLeft + margin;
            targetTop = cursorY - (windowHeight / 2.0);
        }
        else if (taskbarAtRight)
        {
            targetLeft = workAreaRight - windowWidth - margin;
            targetTop = cursorY - (windowHeight / 2.0);
        }
        else
        {
            // Default: Bottom taskbar (Windows standard)
            targetTop = workAreaBottom - windowHeight - margin;
            targetLeft = cursorX - (windowWidth / 2.0);
        }

        // Clamp to work area with margins
        if (targetLeft + windowWidth > workAreaRight - margin)
            targetLeft = workAreaRight - windowWidth - margin;
        if (targetLeft < workAreaLeft + margin)
            targetLeft = workAreaLeft + margin;

        if (targetTop + windowHeight > workAreaBottom - margin)
            targetTop = workAreaBottom - windowHeight - margin;
        if (targetTop < workAreaTop + margin)
            targetTop = workAreaTop + margin;

        window.Left = targetLeft;
        window.Top = targetTop;
    }
}

