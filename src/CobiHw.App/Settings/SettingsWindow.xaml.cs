using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

/// <summary>
/// Settings window. Logic lives in <see cref="SettingsViewModel"/>; this class handles the close confirmation and dragging rows in the item list.
/// </summary>
public partial class SettingsWindow : Window
{
    // Row under the mouse when the button went down; a drag starts only after the mouse moves far enough.
    private MetricRowViewModel? _pressedRow;
    private Point _pressPoint;

    /// <summary>Creates the window with the given theme.</summary>
    public SettingsWindow(AppTheme theme)
    {
        ApplyTheme(theme);
        InitializeComponent();
        Closing += OnClosing;
    }

    /// <summary>Switches the window theme (light, dark or Windows').</summary>
    public void ApplyTheme(AppTheme theme) => WindowTheme.Apply(this, theme);

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // Pending changes: apply, discard or keep editing. Invalid changes can only be discarded.
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

    private void OnMetricPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking the checkbox toggles it instead of starting a drag.
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

            // Dropped outside the list: back to where it was.
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

        // Rows move live while dragging, so the list shows where the item will land.
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

    // Row of the element under the mouse.
    private static MetricRowViewModel? RowOf(object source) =>
        (source as FrameworkElement)?.DataContext as MetricRowViewModel;

    // True when the element is inside a control of type T (walks up the visual and logical trees).
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
