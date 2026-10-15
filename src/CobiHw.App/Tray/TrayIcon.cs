using System.IO;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CobiHw.App.Tray;

internal sealed class TrayIcon : IDisposable
{
    private const string TooltipText = "cobihw";

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
    private readonly Forms.ToolStripMenuItem _settingsItem;
    private readonly Forms.ToolStripMenuItem _resetNetworkItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private bool _speedTestRunning;

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
        _resetNetworkItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ResetNetworkRequested?.Invoke(this, EventArgs.Empty));
        _exitItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _layoutMenu = new Forms.ToolStripMenuItem(string.Empty);
        _layoutMenu.DropDownItems.AddRange(new Forms.ToolStripItem[]
        {
            _fullLayoutItem,
            _horizontalLayoutItem,
            _verticalLayoutItem,
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
            _settingsItem,
            _speedTestItem,
            _resetNetworkItem,
            new Forms.ToolStripSeparator(),
            _exitItem,
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = TooltipText,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        ApplyTexts();
    }

    public event EventHandler? ToggleVisibilityRequested;
    public event EventHandler<WidgetMode>? ModeSelected;
    public event EventHandler<ViewLayout>? LayoutSelected;
    public event EventHandler<bool>? SnapToEdgesToggled;
    public event EventHandler<bool>? LockPositionToggled;
    public event EventHandler<bool>? ClickThroughToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? SpeedTestRequested;
    public event EventHandler? ResetNetworkRequested;
    public event EventHandler? ExitRequested;

    public void ApplyTheme(bool dark)
    {
        foreach (Forms.ToolStrip strip in new Forms.ToolStrip[] { _menu, _layoutMenu.DropDown })
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
        _speedTestItem.Text = _speedTestRunning ? Strings.Tray_SpeedTestRunning : Strings.Tray_SpeedTest;
        _resetNetworkItem.Text = Strings.Tray_ResetNetwork;
        _exitItem.Text = Strings.Tray_Exit;
    }

    public void SetSpeedTestRunning(bool running)
    {
        _speedTestRunning = running;
        _speedTestItem.Enabled = !running;
        ApplyTexts();
    }

    public void ShowNotification(string title, string text, bool isError) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, isError ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

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

    private static Drawing.Icon LoadIcon(Uri iconResource)
    {
        System.Windows.Resources.StreamResourceInfo resource = System.Windows.Application.GetResourceStream(iconResource)
            ?? throw new InvalidOperationException($"Ícone não encontrado nos recursos: {iconResource}");

        using Stream stream = resource.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
