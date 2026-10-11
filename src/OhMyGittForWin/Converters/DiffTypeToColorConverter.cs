using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Converters;

public class DiffTypeToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush InsertedBrush = new(Color.FromArgb(50, 46, 160, 67));
    private static readonly SolidColorBrush DeletedBrush = new(Color.FromArgb(50, 248, 81, 73));
    private static readonly SolidColorBrush InsertedForeground = new(Color.FromRgb(126, 231, 135));
    private static readonly SolidColorBrush DeletedForeground = new(Color.FromRgb(255, 123, 114));
    private static readonly SolidColorBrush DefaultForeground = new(Color.FromRgb(209, 213, 219));
    private static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiffLineType type)
        {
            string mode = parameter as string ?? "Background";
            if (mode == "Foreground")
            {
                return type switch
                {
                    DiffLineType.Inserted => InsertedForeground,
                    DiffLineType.Deleted => DeletedForeground,
                    _ => DefaultForeground
                };
            }
            else
            {
                return type switch
                {
                    DiffLineType.Inserted => InsertedBrush,
                    DiffLineType.Deleted => DeletedBrush,
                    _ => TransparentBrush
                };
            }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
