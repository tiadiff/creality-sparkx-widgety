using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using BrushConverter = System.Windows.Media.BrushConverter;

namespace CrealitySparkXWidget.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            bool b = v == Visibility.Visible;
            return Invert ? !b : b;
        }
        return false;
    }
}

public class StringToBrushConverter : IValueConverter
{
    private static readonly BrushConverter _converter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var brush = _converter.ConvertFromString(hex) as MediaBrush;
                if (brush != null)
                {
                    brush.Freeze();
                    return brush;
                }
            }
            catch { }
        }
        return MediaBrushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ValueToPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return $"{d:F0}%";
        if (value is int i)
            return $"{i}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToActiveBorderBrushConverter : IValueConverter
{
    private static readonly BrushConverter _converter = new();
    private static readonly MediaBrush _activeBrush = (MediaBrush)_converter.ConvertFromString("#10B981")!;
    private static readonly MediaBrush _inactiveBrush = (MediaBrush)_converter.ConvertFromString("#2A2C33")!;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? _activeBrush : _inactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToActiveTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "IN USO" : "PRONTO";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToActiveForegroundConverter : IValueConverter
{
    private static readonly BrushConverter _converter = new();
    private static readonly MediaBrush _activeBrush = (MediaBrush)_converter.ConvertFromString("#10B981")!;
    private static readonly MediaBrush _inactiveBrush = (MediaBrush)_converter.ConvertFromString("#6B7280")!;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? _activeBrush : _inactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
