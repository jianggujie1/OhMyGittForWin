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

    // IDEA Style Ribbon Brushes (Translucent tinted ribbons with crisp boundary stroke)
    private static readonly Brush InsertedFill = new SolidColorBrush(Color.FromArgb(90, 46, 125, 50));
    private static readonly Pen InsertedPen = new(new SolidColorBrush(Color.FromArgb(200, 76, 175, 80)), 1);

    private static readonly Brush ModifiedFill = new SolidColorBrush(Color.FromArgb(90, 33, 85, 145));
    private static readonly Pen ModifiedPen = new(new SolidColorBrush(Color.FromArgb(200, 66, 133, 244)), 1);

    private static readonly Brush DeletedFill = new SolidColorBrush(Color.FromArgb(90, 110, 45, 50));
    private static readonly Pen DeletedPen = new(new SolidColorBrush(Color.FromArgb(200, 211, 47, 47)), 1);

    static HunkRibbonConnector()
    {
        InsertedFill.Freeze();
        InsertedPen.Freeze();
        ModifiedFill.Freeze();
        ModifiedPen.Freeze();
        DeletedFill.Freeze();
        DeletedPen.Freeze();
    }

    public HunkRibbonConnector()
    {
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (HunkType == ChangeType.Unchanged) return;

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double yLeftTop = 0;
        double yRightTop = 0;
        double yLeftBottom;
        double yRightBottom;

        const double NotchHeight = 2.0;
        const double LineHeight = 22.0;

        if (HunkType == ChangeType.Inserted || LeftLineCount == 0)
        {
            // Pure insertion on right: Left starts as a 2px anchor notch at top, Right spans full height h
            yLeftBottom = Math.Min(NotchHeight, h);
            yRightBottom = h;
        }
        else if (HunkType == ChangeType.Deleted || RightLineCount == 0)
        {
            // Pure deletion on left: Left spans full height h, Right tapers to a 2px anchor notch at top
            yLeftBottom = h;
            yRightBottom = Math.Min(NotchHeight, h);
        }
        else
        {
            // Modified: Left and Right both have lines. Each side's bottom matches its actual line count height
            yLeftBottom = Math.Min(h, LeftLineCount * LineHeight);
            yRightBottom = Math.Min(h, RightLineCount * LineHeight);
        }

        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(new Point(0, yLeftTop), true, true);

            // Top boundary: straight horizontal line from (0, yLeftTop) to (w, yRightTop)
            ctx.LineTo(new Point(w, yRightTop), true, true);

            // Right boundary: straight vertical line to (w, yRightBottom)
            ctx.LineTo(new Point(w, yRightBottom), true, true);

            // Bottom boundary: smooth S-curve returning from (w, yRightBottom) to (0, yLeftBottom)
            if (Math.Abs(yLeftBottom - yRightBottom) < 0.5)
            {
                ctx.LineTo(new Point(0, yLeftBottom), true, true);
            }
            else
            {
                ctx.BezierTo(
                    new Point(w * 0.5, yRightBottom),
                    new Point(w * 0.5, yLeftBottom),
                    new Point(0, yLeftBottom),
                    true, true);
            }

            // Left boundary: straight vertical line returning to (0, yLeftTop)
            ctx.LineTo(new Point(0, yLeftTop), true, true);
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
            default:
                fill = DeletedFill;
                pen = DeletedPen;
                break;
        }

        dc.DrawGeometry(fill, pen, geom);
    }
}
