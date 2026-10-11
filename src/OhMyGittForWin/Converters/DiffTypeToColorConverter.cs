using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DiffPlex.DiffBuilder.Model;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Converters;

public class DiffTypeToColorConverter : IValueConverter
{
    // IDEA Dark Theme (New UI / Darcula) Diff Colors
    private static readonly SolidColorBrush InsertedBg = new(Color.FromArgb(160, 26, 64, 38));       // #1A4026
    private static readonly SolidColorBrush DeletedBg = new(Color.FromArgb(160, 74, 28, 32));        // #4A1C20
    private static readonly SolidColorBrush ModifiedBg = new(Color.FromArgb(160, 24, 48, 76));       // #18304C
    private static readonly SolidColorBrush ImaginaryBg = new(Color.FromArgb(100, 16, 18, 22));      // #101216 (Hollow)
    private static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    private static readonly SolidColorBrush InsertedFg = new(Color.FromRgb(163, 230, 172));          // Soft Neon Green
    private static readonly SolidColorBrush DeletedFg = new(Color.FromRgb(248, 113, 113));           // Soft Salmon Red
    private static readonly SolidColorBrush ModifiedFg = new(Color.FromRgb(147, 197, 253));          // Soft Ice Blue
    private static readonly SolidColorBrush DefaultFg = new(Color.FromRgb(229, 231, 235));           // Light Gray
    private static readonly SolidColorBrush MutedFg = new(Color.FromRgb(107, 114, 128));             // Gutter Gray

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string mode = parameter as string ?? "Background";

        ChangeType changeType = ChangeType.Unchanged;
        if (value is ChangeType ct)
        {
            changeType = ct;
        }
        else if (value is DiffLineType dlt)
        {
            changeType = dlt switch
            {
                DiffLineType.Inserted => ChangeType.Inserted,
                DiffLineType.Deleted => ChangeType.Deleted,
                DiffLineType.Modified => ChangeType.Modified,
                DiffLineType.Imaginary => ChangeType.Imaginary,
                _ => ChangeType.Unchanged
            };
        }

        if (mode == "Foreground")
        {
            return changeType switch
            {
                ChangeType.Inserted => InsertedFg,
                ChangeType.Deleted => DeletedFg,
                ChangeType.Modified => ModifiedFg,
                ChangeType.Imaginary => MutedFg,
                _ => DefaultFg
            };
        }
        else if (mode == "GutterBorder")
        {
            return changeType switch
            {
                ChangeType.Inserted => InsertedFg,
                ChangeType.Deleted => DeletedFg,
                ChangeType.Modified => ModifiedFg,
                _ => TransparentBrush
            };
        }
        else // Background
        {
            return changeType switch
            {
                ChangeType.Inserted => InsertedBg,
                ChangeType.Deleted => DeletedBg,
                ChangeType.Modified => ModifiedBg,
                ChangeType.Imaginary => ImaginaryBg,
                _ => TransparentBrush
            };
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
