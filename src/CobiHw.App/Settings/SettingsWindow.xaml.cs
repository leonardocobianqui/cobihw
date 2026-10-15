using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

public partial class SettingsWindow : Window
{
    private MetricRowViewModel? _pressedRow;
    private Point _pressPoint;

    public SettingsWindow(AppTheme theme)
    {
        ApplyTheme(theme);
        InitializeComponent();
        Closing += OnClosing;
    }

    public void ApplyTheme(AppTheme theme)
    {
#pragma warning disable WPF0001
        ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
#pragma warning restore WPF0001
    }

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.HasPendingChanges)
        {
            return;
        }

        if (!ViewModel.CanApplyAll)
        {
            string invalid = Localizer.Format(Strings.Format_UnappliedInvalid, ViewModel.PendingTabNames);
            MessageBoxResult discard = MessageBox.Show(this, invalid, Strings.Settings_UnappliedTitle, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            e.Cancel = discard != MessageBoxResult.OK;
            return;
        }

        string message = Localizer.Format(Strings.Format_UnappliedMessage, ViewModel.PendingTabNames);

        switch (MessageBox.Show(this, message, Strings.Settings_UnappliedTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
        {
            case MessageBoxResult.Yes:
                ViewModel.ApplyAll();
                break;
            case MessageBoxResult.No:
                break;
            default:
                e.Cancel = true;
                break;
        }
    }

    private void OnChooseColorClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ColorChoiceViewModel choice ||
            !HexColor.TryParse(choice.Color, out byte red, out byte green, out byte blue))
        {
            return;
        }

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(red, green, blue),
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            choice.Color = HexColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        }
    }

    private void OnMetricPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedRow = IsInside<ToggleButton>(e.OriginalSource) ? null : RowOf(e.OriginalSource);
        _pressPoint = e.GetPosition(MetricList);
    }

    private void OnMetricPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedRow is null || e.LeftButton != MouseButtonState.Pressed)
        {
            _pressedRow = null;
            return;
        }

        Vector moved = e.GetPosition(MetricList) - _pressPoint;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        MetricRowViewModel row = _pressedRow;
        _pressedRow = null;
        int originalIndex = ViewModel.Metrics.IndexOf(row);
        row.IsDragging = true;

        try
        {
            DragDropEffects result = DragDrop.DoDragDrop(MetricList, new DataObject(typeof(MetricRowViewModel), row), DragDropEffects.Move);

            if (result == DragDropEffects.None)
            {
                ViewModel.MoveMetric(row, originalIndex);
            }
        }
        finally
        {
            row.IsDragging = false;
        }
    }

    private void OnMetricDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (e.Data.GetData(typeof(MetricRowViewModel)) is not MetricRowViewModel dragged)
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;

        if (RowOf(e.OriginalSource) is MetricRowViewModel target && target != dragged)
        {
            ViewModel.MoveMetric(dragged, ViewModel.Metrics.IndexOf(target));
        }
    }

    private void OnMetricDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = e.Data.GetDataPresent(typeof(MetricRowViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private static MetricRowViewModel? RowOf(object source) =>
        (source as FrameworkElement)?.DataContext as MetricRowViewModel;

    private static bool IsInside<T>(object source)
        where T : DependencyObject
    {
        for (DependencyObject? current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            if (current is T)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
