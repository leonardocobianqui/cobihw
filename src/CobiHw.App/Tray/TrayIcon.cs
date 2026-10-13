using System.IO;
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
    private readonly Forms.ToolStripMenuItem _startWithWindowsItem;

    public TrayIcon(Uri iconResource)
    {
        _icon = LoadIcon(iconResource);

        _alwaysOnTopItem = new Forms.ToolStripMenuItem("Sempre por cima", null, (_, _) => ModeSelected?.Invoke(this, WidgetMode.AlwaysOnTop));
        _desktopItem = new Forms.ToolStripMenuItem("Na área de trabalho", null, (_, _) => ModeSelected?.Invoke(this, WidgetMode.Desktop));
        _fullLayoutItem = new Forms.ToolStripMenuItem("Completa", null, (_, _) => LayoutSelected?.Invoke(this, ViewLayout.Full));
        _horizontalLayoutItem = new Forms.ToolStripMenuItem("Resumida horizontal", null, (_, _) => LayoutSelected?.Invoke(this, ViewLayout.CompactHorizontal));
        _verticalLayoutItem = new Forms.ToolStripMenuItem("Resumida vertical", null, (_, _) => LayoutSelected?.Invoke(this, ViewLayout.CompactVertical));
        _snapToEdgesItem = new Forms.ToolStripMenuItem("Encaixar nas bordas", null, OnSnapToEdgesClick);
        _lockPositionItem = new Forms.ToolStripMenuItem("Travar posição", null, OnLockPositionClick);
        _startWithWindowsItem = new Forms.ToolStripMenuItem("Iniciar com o Windows", null, OnStartWithWindowsClick);

        var layoutMenu = new Forms.ToolStripMenuItem("Visualização");
        layoutMenu.DropDownItems.AddRange(new Forms.ToolStripItem[]
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
            layoutMenu,
            _snapToEdgesItem,
            _lockPositionItem,
            _startWithWindowsItem,
            new Forms.ToolStripSeparator(),
            new Forms.ToolStripMenuItem("Configurações...", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)),
            new Forms.ToolStripMenuItem("Zerar contador de rede", null, (_, _) => ResetNetworkRequested?.Invoke(this, EventArgs.Empty)),
            new Forms.ToolStripSeparator(),
            new Forms.ToolStripMenuItem("Sair", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)),
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = TooltipText,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
    }

    public event EventHandler? ToggleVisibilityRequested;
    public event EventHandler<WidgetMode>? ModeSelected;
    public event EventHandler<ViewLayout>? LayoutSelected;
    public event EventHandler<bool>? SnapToEdgesToggled;
    public event EventHandler<bool>? LockPositionToggled;
    public event EventHandler<bool>? StartWithWindowsToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ResetNetworkRequested;
    public event EventHandler? ExitRequested;

    public void UpdateMenu(AppSettings settings, bool startWithWindows)
    {
        _alwaysOnTopItem.Checked = settings.Mode == WidgetMode.AlwaysOnTop;
        _desktopItem.Checked = settings.Mode == WidgetMode.Desktop;
        _fullLayoutItem.Checked = settings.Layout == ViewLayout.Full;
        _horizontalLayoutItem.Checked = settings.Layout == ViewLayout.CompactHorizontal;
        _verticalLayoutItem.Checked = settings.Layout == ViewLayout.CompactVertical;
        _snapToEdgesItem.Checked = settings.SnapToEdges;
        _lockPositionItem.Checked = settings.PositionLocked;
        _startWithWindowsItem.Checked = startWithWindows;
    }

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

    private void OnStartWithWindowsClick(object? sender, EventArgs e) =>
        StartWithWindowsToggled?.Invoke(this, !_startWithWindowsItem.Checked);

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
