using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Views;

public partial class IdeaDiffViewer : UserControl
{
    public static readonly DependencyProperty HorizontalScrollValueProperty =
        DependencyProperty.Register(nameof(HorizontalScrollValue), typeof(double), typeof(IdeaDiffViewer),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnHorizontalScrollValueChanged));

    public static readonly DependencyProperty MaxHorizontalScrollProperty =
        DependencyProperty.Register(nameof(MaxHorizontalScroll), typeof(double), typeof(IdeaDiffViewer),
            new FrameworkPropertyMetadata(0.0));

    public static readonly DependencyProperty ViewportWidthProperty =
        DependencyProperty.Register(nameof(ViewportWidth), typeof(double), typeof(IdeaDiffViewer),
            new FrameworkPropertyMetadata(300.0));

    public static readonly DependencyProperty NegativeHorizontalOffsetProperty =
        DependencyProperty.Register(nameof(NegativeHorizontalOffset), typeof(double), typeof(IdeaDiffViewer),
            new FrameworkPropertyMetadata(0.0));

    public double HorizontalScrollValue
    {
        get => (double)GetValue(HorizontalScrollValueProperty);
        set => SetValue(HorizontalScrollValueProperty, value);
    }

    public double MaxHorizontalScroll
    {
        get => (double)GetValue(MaxHorizontalScrollProperty);
        set => SetValue(MaxHorizontalScrollProperty, value);
    }

    public double ViewportWidth
    {
        get => (double)GetValue(ViewportWidthProperty);
        set => SetValue(ViewportWidthProperty, value);
    }

    public double NegativeHorizontalOffset
    {
        get => (double)GetValue(NegativeHorizontalOffsetProperty);
        set => SetValue(NegativeHorizontalOffsetProperty, value);
    }

    private static void OnHorizontalScrollValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IdeaDiffViewer viewer)
        {
            viewer.NegativeHorizontalOffset = -(double)e.NewValue;
        }
    }

    private HwndSource? _hwndSource;

    public IdeaDiffViewer()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateHorizontalScrollExtent();
        SizeChanged += (_, _) => UpdateHorizontalScrollExtent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateHorizontalScrollExtent();
        if (_hwndSource == null)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (msg == WM_MOUSEHWHEEL && MaxHorizontalScroll > 0)
        {
            short tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            if (tilt != 0)
            {
                var mousePos = Mouse.GetPosition(this);
                if (mousePos.X >= 0 && mousePos.X <= ActualWidth && mousePos.Y >= 0 && mousePos.Y <= ActualHeight)
                {
                    // Positive tilt = tilt right = scroll right
                    double step = (tilt / 120.0) * 48.0;
                    HorizontalScrollValue = Math.Clamp(HorizontalScrollValue + step, 0, MaxHorizontalScroll);
                    handled = true;
                }
            }
        }
        return IntPtr.Zero;
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && MaxHorizontalScroll > 0)
        {
            // Shift + Mouse Wheel = Synchronized horizontal scrolling
            double delta = e.Delta < 0 ? 50.0 : -50.0;
            HorizontalScrollValue = Math.Clamp(HorizontalScrollValue + delta, 0, MaxHorizontalScroll);
            e.Handled = true;
        }
    }

    private static double CalculateVisualLineWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        double width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\t')
            {
                width += 48.0; // 4-space tab in 12pt monospaced font
            }
            else if (c >= 0x4E00 && c <= 0x9FFF || c >= 0x3000 && c <= 0x303F || c >= 0xFF00 && c <= 0xFFEF)
            {
                width += 13.0; // Full-width CJK character
            }
            else
            {
                width += 7.2; // Monospace ASCII character
            }
        }
        return width;
    }

    public void UpdateHorizontalScrollExtent()
    {
        if (DataContext is not IdeaFileDiffResult result || result.Hunks == null || result.Hunks.Count == 0)
        {
            MaxHorizontalScroll = 0;
            HorizontalScrollValue = 0;
            return;
        }

        double maxLineWidth = 0;
        foreach (var hunk in result.Hunks)
        {
            foreach (var l in hunk.LeftLines)
            {
                double w = CalculateVisualLineWidth(l.Text);
                if (w > maxLineWidth) maxLineWidth = w;
            }
            foreach (var l in hunk.RightLines)
            {
                double w = CalculateVisualLineWidth(l.Text);
                if (w > maxLineWidth) maxLineWidth = w;
            }
        }

        // Available width of single side code column:
        // Container width minus middle gutter (124px) minus vertical scrollbar (~18px), split in half
        double totalWidth = HunkListBox != null && HunkListBox.ActualWidth > 150 ? HunkListBox.ActualWidth : ActualWidth;
        double colWidth = Math.Max(100, (totalWidth - 124 - 18) / 2.0);
        // Code line text padding is (Left: 8px, Right: 4px)
        double viewport = Math.Max(50, colWidth - 16);

        ViewportWidth = viewport;

        // Provide 60px of breathing room past the longest line
        double maxScroll = Math.Max(0, maxLineWidth + 60 - viewport);
        MaxHorizontalScroll = Math.Ceiling(maxScroll);

        if (HorizontalScrollValue > MaxHorizontalScroll)
        {
            HorizontalScrollValue = MaxHorizontalScroll;
        }
    }
}
