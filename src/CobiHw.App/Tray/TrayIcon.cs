using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using CobiHw.App.Interop;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CobiHw.App.Tray;

/// <summary>
/// Tray icon with its menu, tooltip and notifications. The menu is WPF, with the app's own look, the same on Windows 10 and 11.
/// Menu clicks are raised as events; <see cref="App"/> applies them.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    // ShowBalloonTip requires a timeout, but Windows ignores it since Vista: the display time comes from the accessibility settings.
    private const int NotificationTimeoutIgnored = 5000;
    // Single-choice items show a dot instead of a check (MenuItemTemplate in Controls.xaml).
    private const string RadioTag = "Radio";
    // Transparent space the menu template keeps around the card for the shadow; the offsets put the card's corner at the mouse.
    private const double ShadowLeft = 12;
    private const double ShadowTop = 4;

    private readonly Drawing.Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly ContextMenu _menu;
    private readonly MenuItem _alwaysOnTopItem;
    private readonly MenuItem _desktopItem;
    private readonly MenuItem _fullLayoutItem;
    private readonly MenuItem _horizontalLayoutItem;
    private readonly MenuItem _verticalLayoutItem;
    private readonly MenuItem _snapToEdgesItem;
    private readonly MenuItem _lockPositionItem;
    private readonly MenuItem _clickThroughItem;
    private readonly MenuItem _speedTestItem;
    private readonly MenuItem _layoutMenu;
    private readonly MenuItem _networkMenu;
    private readonly MenuItem _settingsItem;
    private readonly MenuItem _aboutItem;
    private readonly MenuItem _networkDevicesItem;
    private readonly MenuItem _resetNetworkItem;
    private readonly MenuItem _exitItem;
    private bool _speedTestRunning;

    /// <summary>Creates the icon and its menu; texts come from <see cref="ApplyTexts"/> and the colors from <see cref="ApplyTheme"/>.</summary>
    /// <param name="iconResource">Icon inside the app resources.</param>
    public TrayIcon(Uri iconResource)
    {
        _icon = LoadIcon(iconResource);

        _alwaysOnTopItem = Item(() => ModeSelected?.Invoke(this, WidgetMode.AlwaysOnTop), RadioTag);
        _desktopItem = Item(() => ModeSelected?.Invoke(this, WidgetMode.Desktop), RadioTag);
        _fullLayoutItem = Item(() => LayoutSelected?.Invoke(this, ViewLayout.Full), RadioTag);
        _horizontalLayoutItem = Item(() => LayoutSelected?.Invoke(this, ViewLayout.CompactHorizontal), RadioTag);
        _verticalLayoutItem = Item(() => LayoutSelected?.Invoke(this, ViewLayout.CompactVertical), RadioTag);
        _snapToEdgesItem = Item(OnSnapToEdgesClick);
        _lockPositionItem = Item(OnLockPositionClick);
        _clickThroughItem = Item(OnClickThroughClick);
        _speedTestItem = Item(() => SpeedTestRequested?.Invoke(this, EventArgs.Empty));

        _settingsItem = Item(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _aboutItem = Item(() => AboutRequested?.Invoke(this, EventArgs.Empty));
        _networkDevicesItem = Item(() => NetworkDevicesRequested?.Invoke(this, EventArgs.Empty));
        _resetNetworkItem = Item(() => ResetNetworkRequested?.Invoke(this, EventArgs.Empty));
        _exitItem = Item(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        _layoutMenu = Submenu(_fullLayoutItem, _horizontalLayoutItem, _verticalLayoutItem);
        _networkMenu = Submenu(_networkDevicesItem, _speedTestItem, _resetNetworkItem);

        // Colors first, then the control styles; the menu isn't inside a window, so it carries both itself.
        _menu = new ContextMenu
        {
            Placement = PlacementMode.MousePoint,
            HorizontalOffset = -ShadowLeft,
            VerticalOffset = -ShadowTop,
        };
        _menu.Resources.MergedDictionaries.Add(WindowTheme.Tokens(dark: true));
        _menu.Resources.MergedDictionaries.Add(WindowTheme.Controls);
        _menu.Style = (Style)WindowTheme.Controls[typeof(ContextMenu)];

        foreach (Control item in new Control[]
        {
            _alwaysOnTopItem,
            _desktopItem,
            new Separator(),
            _layoutMenu,
            _snapToEdgesItem,
            _lockPositionItem,
            _clickThroughItem,
            new Separator(),
            _networkMenu,
            new Separator(),
            _settingsItem,
            new Separator(),
            _aboutItem,
            new Separator(),
            _exitItem,
        })
        {
            _menu.Items.Add(item);
        }

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = TrayTooltip.Title,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
        ApplyTexts();
    }

    /// <summary>Left click on the icon.</summary>
    public event EventHandler? ToggleVisibilityRequested;
    /// <summary>"Always on top" or "Desktop" chosen.</summary>
    public event EventHandler<WidgetMode>? ModeSelected;
    /// <summary>Layout chosen.</summary>
    public event EventHandler<ViewLayout>? LayoutSelected;
    /// <summary>Snap to edges toggled; carries the new value.</summary>
    public event EventHandler<bool>? SnapToEdgesToggled;
    /// <summary>Lock position toggled; carries the new value.</summary>
    public event EventHandler<bool>? LockPositionToggled;
    /// <summary>Click-through toggled; carries the new value.</summary>
    public event EventHandler<bool>? ClickThroughToggled;
    /// <summary>"Settings" clicked.</summary>
    public event EventHandler? SettingsRequested;
    /// <summary>"About" clicked.</summary>
    public event EventHandler? AboutRequested;
    /// <summary>"Network devices" clicked.</summary>
    public event EventHandler? NetworkDevicesRequested;
    /// <summary>"Speed test" clicked: starts a test, or shows the one running.</summary>
    public event EventHandler? SpeedTestRequested;
    /// <summary>"Reset network counter" clicked.</summary>
    public event EventHandler? ResetNetworkRequested;
    /// <summary>"Exit" clicked.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Switches the menu colors between dark and light.</summary>
    /// <param name="dark">True for dark.</param>
    public void ApplyTheme(bool dark)
    {
        ResourceDictionary tokens = WindowTheme.Tokens(dark);
        if (_menu.Resources.MergedDictionaries[0] != tokens)
        {
            _menu.Resources.MergedDictionaries[0] = tokens;
        }
    }

    /// <summary>Updates the check marks from the settings.</summary>
    /// <param name="settings">Current settings.</param>
    public void UpdateMenu(AppSettings settings)
    {
        _alwaysOnTopItem.IsChecked = settings.Mode == WidgetMode.AlwaysOnTop;
        _desktopItem.IsChecked = settings.Mode == WidgetMode.Desktop;
        _fullLayoutItem.IsChecked = settings.Layout == ViewLayout.Full;
        _horizontalLayoutItem.IsChecked = settings.Layout == ViewLayout.CompactHorizontal;
        _verticalLayoutItem.IsChecked = settings.Layout == ViewLayout.CompactVertical;
        _snapToEdgesItem.IsChecked = settings.SnapToEdges;
        _lockPositionItem.IsChecked = settings.PositionLocked;
        _clickThroughItem.IsChecked = settings.ClickThrough;
    }

    /// <summary>Sets every menu text in the current language.</summary>
    public void ApplyTexts()
    {
        _alwaysOnTopItem.Header = Strings.Tray_AlwaysOnTop;
        _desktopItem.Header = Strings.Tray_Desktop;
        _layoutMenu.Header = Strings.Tray_Layout;
        _fullLayoutItem.Header = Strings.Layout_Full;
        _horizontalLayoutItem.Header = Strings.Layout_Horizontal;
        _verticalLayoutItem.Header = Strings.Layout_Vertical;
        _snapToEdgesItem.Header = Strings.Tray_SnapToEdges;
        _lockPositionItem.Header = Strings.Tray_LockPosition;
        _clickThroughItem.Header = Strings.Tray_ClickThrough;
        _settingsItem.Header = Strings.Tray_Settings;
        _aboutItem.Header = Strings.Tray_About;
        _networkMenu.Header = Strings.Tray_Network;
        _networkDevicesItem.Header = Strings.Tray_NetworkDevices;
        _speedTestItem.Header = _speedTestRunning ? Strings.Tray_SpeedTestRunning : Strings.Tray_SpeedTest;
        _resetNetworkItem.Header = Strings.Tray_ResetNetwork;
        _exitItem.Header = Strings.Tray_Exit;
    }

    /// <summary>Sets the icon tooltip; Windows limits it to 127 characters.</summary>
    /// <param name="text">Tooltip text.</param>
    public void SetTooltip(string text) => _notifyIcon.Text = text;

    /// <summary>Changes the "Speed test" text while a test runs; the item then shows the running test.</summary>
    /// <param name="running">True while testing.</param>
    public void SetSpeedTestRunning(bool running)
    {
        _speedTestRunning = running;
        ApplyTexts();
    }

    /// <summary>Shows a Windows notification.</summary>
    /// <param name="title">Title.</param>
    /// <param name="text">Message.</param>
    /// <param name="isError">Uses the warning icon.</param>
    public void ShowNotification(string title, string text, bool isError) =>
        _notifyIcon.ShowBalloonTip(NotificationTimeoutIgnored, title, text, isError ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);

    /// <summary>Closes the menu, removes the icon from the tray and frees it.</summary>
    public void Dispose()
    {
        _menu.IsOpen = false;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    // Plain item; Tag="Radio" draws the single-choice dot when checked.
    private static MenuItem Item(Action click, string? tag = null)
    {
        var item = new MenuItem { Tag = tag };
        item.Click += (_, _) => click();
        return item;
    }

    private static MenuItem Submenu(params MenuItem[] items)
    {
        var submenu = new MenuItem();
        foreach (MenuItem item in items)
        {
            submenu.Items.Add(item);
        }

        return submenu;
    }

    // Sends the new value; the check mark changes when App calls UpdateMenu.
    private void OnSnapToEdgesClick() =>
        SnapToEdgesToggled?.Invoke(this, !_snapToEdgesItem.IsChecked);

    private void OnLockPositionClick() =>
        LockPositionToggled?.Invoke(this, !_lockPositionItem.IsChecked);

    private void OnClickThroughClick() =>
        ClickThroughToggled?.Invoke(this, !_clickThroughItem.IsChecked);

    private void OnNotifyIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // Opens on the right button's release, like the Windows menus; the keyboard (Shift+F10 on the icon) arrives the same way.
    private void OnNotifyIconMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Right)
        {
            return;
        }

        _menu.IsOpen = true;

        // The menu only closes on a click elsewhere while the app is in the foreground, so its popup takes the foreground.
        if (PresentationSource.FromVisual(_menu) is HwndSource popup)
        {
            User32.SetForegroundWindow(popup.Handle);
        }
    }

    // Loads the size Windows uses for small icons, such as the tray.
    private static Drawing.Icon LoadIcon(Uri iconResource)
    {
        System.Windows.Resources.StreamResourceInfo resource = Application.GetResourceStream(iconResource)
            ?? throw new InvalidOperationException($"Icon not found in the resources: {iconResource}");

        using Stream stream = resource.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
