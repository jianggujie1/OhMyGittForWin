using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public IdeaDiffViewer()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateHorizontalScrollExtent();
        SizeChanged += (_, _) => UpdateHorizontalScrollExtent();
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

    public void UpdateHorizontalScrollExtent()
    {
        if (DataContext is not IdeaFileDiffResult result || result.Hunks == null || result.Hunks.Count == 0)
        {
            MaxHorizontalScroll = 0;
            HorizontalScrollValue = 0;
            return;
        }

        int maxChars = 0;
        foreach (var hunk in result.Hunks)
        {
            foreach (var l in hunk.LeftLines)
            {
                if (l.Text.Length > maxChars) maxChars = l.Text.Length;
            }
            foreach (var l in hunk.RightLines)
            {
                if (l.Text.Length > maxChars) maxChars = l.Text.Length;
            }
        }

        // Font is JetBrains Mono 12pt (~7.6px per char) + 30px padding
        double estimatedTextWidth = maxChars * 7.6 + 30;
        double viewportWidth = Math.Max(100, (ActualWidth - 124) / 2.0);

        MaxHorizontalScroll = Math.Max(0, estimatedTextWidth - viewportWidth);
        if (HorizontalScrollValue > MaxHorizontalScroll)
        {
            HorizontalScrollValue = MaxHorizontalScroll;
        }
    }
}
