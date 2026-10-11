using System;
using System.Windows;
using System.Windows.Media;
using DiffPlex.DiffBuilder.Model;

namespace OhMyGittForWin.Controls;

public class HunkRibbonConnector : FrameworkElement
{
    public static readonly DependencyProperty HunkTypeProperty =
        DependencyProperty.Register(nameof(HunkType), typeof(ChangeType), typeof(HunkRibbonConnector),
            new FrameworkPropertyMetadata(ChangeType.Unchanged, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LeftLineCountProperty =
        DependencyProperty.Register(nameof(LeftLineCount), typeof(int), typeof(HunkRibbonConnector),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RightLineCountProperty =
        DependencyProperty.Register(nameof(RightLineCount), typeof(int), typeof(HunkRibbonConnector),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineHeightProperty =
        DependencyProperty.Register(nameof(LineHeight), typeof(double), typeof(HunkRibbonConnector),
            new FrameworkPropertyMetadata(22.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public ChangeType HunkType
    {
        get => (ChangeType)GetValue(HunkTypeProperty);
        set => SetValue(HunkTypeProperty, value);
    }

    public int LeftLineCount
    {
        get => (int)GetValue(LeftLineCountProperty);
        set => SetValue(LeftLineCountProperty, value);
    }

    public int RightLineCount
    {
        get => (int)GetValue(RightLineCountProperty);
        set => SetValue(RightLineCountProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    // IDEA Style Ribbon Brushes
    private static readonly Brush InsertedFill = new SolidColorBrush(Color.FromArgb(90, 46, 125, 50));
    private static readonly Pen InsertedPen = new(new SolidColorBrush(Color.FromArgb(180, 76, 175, 80)), 1);

    private static readonly Brush ModifiedFill = new SolidColorBrush(Color.FromArgb(90, 33, 85, 145));
    private static readonly Pen ModifiedPen = new(new SolidColorBrush(Color.FromArgb(180, 66, 133, 244)), 1);

    private static readonly Brush DeletedFill = new SolidColorBrush(Color.FromArgb(90, 110, 45, 50));
    private static readonly Pen DeletedPen = new(new SolidColorBrush(Color.FromArgb(180, 211, 47, 47)), 1);

    static HunkRibbonConnector()
    {
        InsertedFill.Freeze();
        InsertedPen.Freeze();
        ModifiedFill.Freeze();
        ModifiedPen.Freeze();
        DeletedFill.Freeze();
        DeletedPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (HunkType == ChangeType.Unchanged) return;

        double w = ActualWidth;
        if (w <= 0) return;

        double hLeft = Math.Max(LeftLineCount * LineHeight, LeftLineCount > 0 ? LineHeight : 2);
        double hRight = Math.Max(RightLineCount * LineHeight, RightLineCount > 0 ? LineHeight : 2);

        // If one side has 0 lines, taper to a point in the middle
        double yLeftTop = 0;
        double yLeftBottom = LeftLineCount > 0 ? hLeft : 2;

        double yRightTop = 0;
        double yRightBottom = RightLineCount > 0 ? hRight : 2;

        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(new Point(0, yLeftTop), true, true);
            // Top Bezier curve across middle gutter
            ctx.BezierTo(
                new Point(w * 0.45, yLeftTop),
                new Point(w * 0.55, yRightTop),
                new Point(w, yRightTop),
                true, true);

            // Right line
            ctx.LineTo(new Point(w, yRightBottom), true, true);

            // Bottom Bezier curve returning to left
            ctx.BezierTo(
                new Point(w * 0.55, yRightBottom),
                new Point(w * 0.45, yLeftBottom),
                new Point(0, yLeftBottom),
                true, true);
        }
        geom.Freeze();

        Brush fill;
        Pen pen;

        switch (HunkType)
        {
            case ChangeType.Inserted:
                fill = InsertedFill;
                pen = InsertedPen;
                break;
            case ChangeType.Modified:
                fill = ModifiedFill;
                pen = ModifiedPen;
                break;
            default: // Deleted
                fill = DeletedFill;
                pen = DeletedPen;
                break;
        }

        dc.DrawGeometry(fill, pen, geom);
    }
}
