using System.IO;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CobiHw.App.Tray;

/// <summary>
/// Tray icon with its menu, tooltip and notifications.
/// Menu clicks are raised as events; <see cref="App"/> applies them.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    // ShowBalloonTip requires a timeout, but Windows ignores it since Vista: the display time comes from the accessibility settings.
    private const int NotificationTimeoutIgnored = 5000;

    private readonly Drawing.Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _alwaysOnTopItem;
    private readonly Forms.ToolStripMenuItem _desktopItem;
    private readonly Forms.ToolStripMenuItem _fullLayoutItem;
    private readonly Forms.ToolStripMenuItem _horizontalLayoutItem;
    private readonly Forms.ToolStripMenuItem _verticalLayoutItem;
    private readonly Forms.ToolStripMenuItem _snapToEdgesItem;
    private readonly Forms.ToolStripMenuItem _lockPositionItem;
    private readonly Forms.ToolStripMenuItem _clickThroughItem;
    private readonly Forms.ToolStripMenuItem _speedTestItem;
    private readonly Forms.ToolStripMenuItem _layoutMenu;
    private readonly Forms.ToolStripMenuItem _networkMenu;
    private readonly Forms.ToolStripMenuItem _settingsItem;
    private readonly Forms.ToolStripMenuItem _aboutItem;
    private readonly Forms.ToolStripMenuItem _networkDevicesItem;
    private readonly Forms.ToolStripMenuItem _resetNetworkItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private bool _speedTestRunning;

    /// <summary>Creates the icon and its menu; texts come from <see cref="ApplyTexts"/>.</summary>
    /// <param name="iconResource">Icon inside the app resources.</param>
    public TrayIcon(Uri iconResource)
    {
        _icon = LoadIcon(iconResource);

        _alwaysOnTopItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ModeSelected?.Invoke(this, WidgetMode.AlwaysOnTop));
        _desktopItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ModeSelected?.Invoke(this, WidgetMode.Desktop));
        _fullLayoutItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => LayoutSelected?.Invoke(this, ViewLayout.Full));
        _horizontalLayoutItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => LayoutSelected?.Invoke(this, ViewLayout.CompactHorizontal));
        _verticalLayoutItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => LayoutSelected?.Invoke(this, ViewLayout.CompactVertical));
        _snapToEdgesItem = new Forms.ToolStripMenuItem(string.Empty, null, OnSnapToEdgesClick);
        _lockPositionItem = new Forms.ToolStripMenuItem(string.Empty, null, OnLockPositionClick);
        _clickThroughItem = new Forms.ToolStripMenuItem(string.Empty, null, OnClickThroughClick);
        _speedTestItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => SpeedTestRequested?.Invoke(this, EventArgs.Empty));

        _settingsItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _aboutItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty));
        _networkDevicesItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => NetworkDevicesRequested?.Invoke(this, EventArgs.Empty));
        _resetNetworkItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ResetNetworkRequested?.Invoke(this, EventArgs.Empty));
        _exitItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _layoutMenu = new Forms.ToolStripMenuItem(string.Empty);
        _layoutMenu.DropDownItems.AddRange(new Forms.ToolStripItem[]
        {
            _fullLayoutItem,
            _horizontalLayoutItem,
            _verticalLayoutItem,
        });

        _networkMenu = new Forms.ToolStripMenuItem(string.Empty);
        _networkMenu.DropDownItems.AddRange(new Forms.ToolStripItem[]
        {
            _networkDevicesItem,
            _speedTestItem,
            _resetNetworkItem,
        });

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _alwaysOnTopItem,
            _desktopItem,
            new Forms.ToolStripSeparator(),
            _layoutMenu,
            _snapToEdgesItem,
            _lockPositionItem,
            _clickThroughItem,
            new Forms.ToolStripSeparator(),
            _networkMenu,
            new Forms.ToolStripSeparator(),
            _settingsItem,
            new Forms.ToolStripSeparator(),
            _aboutItem,
            new Forms.ToolStripSeparator(),
            _exitItem,
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = TrayTooltip.Title,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
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
    /// <summary>"Speed test" clicked.</summary>
    public event EventHandler? SpeedTestRequested;
    /// <summary>"Reset network counter" clicked.</summary>
    public event EventHandler? ResetNetworkRequested;
    /// <summary>"Exit" clicked.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Switches the menu between the dark look and the default light one.</summary>
    /// <param name="dark">True for dark.</param>
    public void ApplyTheme(bool dark)
    {
        foreach (Forms.ToolStrip strip in new Forms.ToolStrip[] { _menu, _layoutMenu.DropDown, _networkMenu.DropDown })
        {
            if (dark)
            {
                strip.Renderer = new DarkMenuRenderer();
            }
            else
            {
                strip.RenderMode = Forms.ToolStripRenderMode.ManagerRenderMode;
            }
        }
    }

    /// <summary>Updates the check marks from the settings.</summary>
    /// <param name="settings">Current settings.</param>
    public void UpdateMenu(AppSettings settings)
    {
        _alwaysOnTopItem.Checked = settings.Mode == WidgetMode.AlwaysOnTop;
        _desktopItem.Checked = settings.Mode == WidgetMode.Desktop;
        _fullLayoutItem.Checked = settings.Layout == ViewLayout.Full;
        _horizontalLayoutItem.Checked = settings.Layout == ViewLayout.CompactHorizontal;
        _verticalLayoutItem.Checked = settings.Layout == ViewLayout.CompactVertical;
        _snapToEdgesItem.Checked = settings.SnapToEdges;
        _lockPositionItem.Checked = settings.PositionLocked;
        _clickThroughItem.Checked = settings.ClickThrough;
    }

    /// <summary>Sets every menu text in the current language.</summary>
    public void ApplyTexts()
    {
        _alwaysOnTopItem.Text = Strings.Tray_AlwaysOnTop;
        _desktopItem.Text = Strings.Tray_Desktop;
        _layoutMenu.Text = Strings.Tray_Layout;
        _fullLayoutItem.Text = Strings.Layout_Full;
        _horizontalLayoutItem.Text = Strings.Layout_Horizontal;
        _verticalLayoutItem.Text = Strings.Layout_Vertical;
        _snapToEdgesItem.Text = Strings.Tray_SnapToEdges;
        _lockPositionItem.Text = Strings.Tray_LockPosition;
        _clickThroughItem.Text = Strings.Tray_ClickThrough;
        _settingsItem.Text = Strings.Tray_Settings;
        _aboutItem.Text = Strings.Tray_About;
        _networkMenu.Text = Strings.Tray_Network;
        _networkDevicesItem.Text = Strings.Tray_NetworkDevices;
        _speedTestItem.Text = _speedTestRunning ? Strings.Tray_SpeedTestRunning : Strings.Tray_SpeedTest;
        _resetNetworkItem.Text = Strings.Tray_ResetNetwork;
        _exitItem.Text = Strings.Tray_Exit;
    }

    /// <summary>Sets the icon tooltip; Windows limits it to 127 characters.</summary>
    /// <param name="text">Tooltip text.</param>
    public void SetTooltip(string text) => _notifyIcon.Text = text;

    /// <summary>Disables "Speed test" and changes its text while a test runs.</summary>
    /// <param name="running">True while testing.</param>
    public void SetSpeedTestRunning(bool running)
    {
        _speedTestRunning = running;
        _speedTestItem.Enabled = !running;
        ApplyTexts();
    }

    /// <summary>Shows a Windows notification.</summary>
    /// <param name="title">Title.</param>
    /// <param name="text">Message.</param>
    /// <param name="isError">Uses the warning icon.</param>
    public void ShowNotification(string title, string text, bool isError) =>
        _notifyIcon.ShowBalloonTip(NotificationTimeoutIgnored, title, text, isError ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);

    /// <summary>Removes the icon from the tray and frees it.</summary>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    // Sends the new value; the check mark changes when App calls UpdateMenu.
    private void OnSnapToEdgesClick(object? sender, EventArgs e) =>
        SnapToEdgesToggled?.Invoke(this, !_snapToEdgesItem.Checked);

    private void OnLockPositionClick(object? sender, EventArgs e) =>
        LockPositionToggled?.Invoke(this, !_lockPositionItem.Checked);

    private void OnClickThroughClick(object? sender, EventArgs e) =>
        ClickThroughToggled?.Invoke(this, !_clickThroughItem.Checked);

    private void OnNotifyIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // Loads the size Windows uses for small icons, such as the tray.
    private static Drawing.Icon LoadIcon(Uri iconResource)
    {
        System.Windows.Resources.StreamResourceInfo resource = System.Windows.Application.GetResourceStream(iconResource)
            ?? throw new InvalidOperationException($"Icon not found in the resources: {iconResource}");

        using Stream stream = resource.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
