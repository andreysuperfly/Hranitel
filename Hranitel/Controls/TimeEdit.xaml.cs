using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Hranitel.Controls;

public partial class TimeEdit : UserControl
{
    private static readonly List<string> Hours = Enumerable.Range(0, 24).Select(i => i.ToString("00")).ToList();
    private static readonly List<string> Minutes = Enumerable.Range(0, 60).Select(i => i.ToString("00")).ToList();

    public static readonly DependencyProperty TimeProperty =
        DependencyProperty.Register(nameof(Time), typeof(TimeSpan), typeof(TimeEdit),
            new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

    private void OnSelectionChanged(object? s, SelectionChangedEventArgs e) => UpdateTime();

    public TimeEdit()
    {
        InitializeComponent();

        HourBox.ItemsSource = Hours;
        MinuteBox.ItemsSource = Minutes;

        HourBox.SelectionChanged += OnSelectionChanged;
        MinuteBox.SelectionChanged += OnSelectionChanged;

        Loaded += (_, _) => ApplyTime(Time);

        IsEnabledChanged += (_, e) =>
        {
            var en = (bool)(e.NewValue ?? false);
            HourBox.IsEnabled = en;
            MinuteBox.IsEnabled = en;
        };
    }

    public TimeSpan Time
    {
        get => (TimeSpan)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimeEdit ctrl)
            ctrl.ApplyTime((TimeSpan)e.NewValue);
    }

    private void ApplyTime(TimeSpan t)
    {
        HourBox.SelectionChanged -= OnSelectionChanged;
        MinuteBox.SelectionChanged -= OnSelectionChanged;

        HourBox.SelectedItem = ((int)t.TotalHours % 24).ToString("00");
        MinuteBox.SelectedItem = t.Minutes.ToString("00");

        HourBox.SelectionChanged += OnSelectionChanged;
        MinuteBox.SelectionChanged += OnSelectionChanged;
    }

    private void UpdateTime()
    {
        var h = int.Parse(HourBox.SelectedItem as string ?? "0");
        var m = int.Parse(MinuteBox.SelectedItem as string ?? "0");
        Time = new TimeSpan(h, m, 0);
    }
}
