using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using CobiHw.Presentation.Resources;
using CobiHw.Presentation.Settings;
using CobiHw.Presentation.Tray;

namespace CobiHw.App.Linux.Tray;

/// <summary>
/// Tray icon and its menu. Menu clicks are raised as events; <see cref="App"/> applies them and calls <see cref="Update"/>.
/// </summary>
/// <remarks>
/// On Linux the menu is exported over D-Bus (StatusNotifierItem) and drawn by the desktop, so it takes the desktop's look,
/// not the app's. The desktop also decides what a click on the icon does: GNOME's AppIndicator extension opens the menu
/// on any click, so showing and hiding the widget is the first menu item rather than a click on the icon.
/// The check marks are set from the settings after every change, never toggled locally, so the menu can't drift from
/// what the app applied.
/// </remarks>
internal sealed class TrayMenu : IDisposable
{
    private static readonly Uri IconResource = new("avares://cobihw/Assets/icon.png");

    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _showWidgetItem;
    private readonly NativeMenuItem _alwaysOnTopItem;
    private readonly NativeMenuItem _desktopItem;
    private readonly NativeMenuItem _layoutMenu;
    private readonly NativeMenuItem _fullLayoutItem;
    private readonly NativeMenuItem _horizontalLayoutItem;
    private readonly NativeMenuItem _verticalLayoutItem;
    private readonly NativeMenuItem _snapToEdgesItem;
    private readonly NativeMenuItem _lockPositionItem;
    private readonly NativeMenuItem _clickThroughItem;
    private readonly NativeMenuItem _resetNetworkItem;
    private readonly NativeMenuItem _exitItem;

    /// <summary>Creates the icon and its menu and shows it in the tray of <paramref name="application"/>.</summary>
    /// <param name="application">Application that owns the tray icon.</param>
    public TrayMenu(Application application)
    {
        _showWidgetItem = Item(MenuItemToggleType.CheckBox, () => ShowWidgetToggled?.Invoke(this, EventArgs.Empty));
        _alwaysOnTopItem = Item(MenuItemToggleType.Radio, () => ModeSelected?.Invoke(this, WidgetMode.AlwaysOnTop));
        _desktopItem = Item(MenuItemToggleType.Radio, () => ModeSelected?.Invoke(this, WidgetMode.Desktop));
        _fullLayoutItem = Item(MenuItemToggleType.Radio, () => LayoutSelected?.Invoke(this, ViewLayout.Full));
        _horizontalLayoutItem = Item(MenuItemToggleType.Radio, () => LayoutSelected?.Invoke(this, ViewLayout.CompactHorizontal));
        _verticalLayoutItem = Item(MenuItemToggleType.Radio, () => LayoutSelected?.Invoke(this, ViewLayout.CompactVertical));
        _snapToEdgesItem = Item(MenuItemToggleType.CheckBox, () => SnapToEdgesToggled?.Invoke(this, EventArgs.Empty));
        _lockPositionItem = Item(MenuItemToggleType.CheckBox, () => LockPositionToggled?.Invoke(this, EventArgs.Empty));
        _clickThroughItem = Item(MenuItemToggleType.CheckBox, () => ClickThroughToggled?.Invoke(this, EventArgs.Empty));
        _resetNetworkItem = Item(MenuItemToggleType.None, () => ResetNetworkRequested?.Invoke(this, EventArgs.Empty));
        _exitItem = Item(MenuItemToggleType.None, () => ExitRequested?.Invoke(this, EventArgs.Empty));
        _layoutMenu = new NativeMenuItem { Menu = new NativeMenu { _fullLayoutItem, _horizontalLayoutItem, _verticalLayoutItem } };

        var menu = new NativeMenu
        {
            _showWidgetItem,
            new NativeMenuItemSeparator(),
            _alwaysOnTopItem,
            _desktopItem,
            _layoutMenu,
            new NativeMenuItemSeparator(),
            _snapToEdgesItem,
            _lockPositionItem,
            _clickThroughItem,
            new NativeMenuItemSeparator(),
            _resetNetworkItem,
            new NativeMenuItemSeparator(),
            _exitItem,
        };

        using Stream icon = AssetLoader.Open(IconResource);
        _icon = new TrayIcon
        {
            Icon = new WindowIcon(icon),
            ToolTipText = TrayTooltip.Title,
            Menu = menu,
            IsVisible = true,
        };

        TrayIcon.SetIcons(application, new TrayIcons { _icon });
        ApplyTexts();
    }

    /// <summary>"Show widget" was clicked: the widget hides if shown, shows if hidden.</summary>
    public event EventHandler? ShowWidgetToggled;

    /// <summary>A mode was picked: above or under other windows.</summary>
    public event EventHandler<WidgetMode>? ModeSelected;

    /// <summary>A layout was picked.</summary>
    public event EventHandler<ViewLayout>? LayoutSelected;

    /// <summary>"Snap to edges" was clicked.</summary>
    public event EventHandler? SnapToEdgesToggled;

    /// <summary>"Lock position" was clicked.</summary>
    public event EventHandler? LockPositionToggled;

    /// <summary>"Click-through" was clicked.</summary>
    public event EventHandler? ClickThroughToggled;

    /// <summary>"Reset network" was clicked.</summary>
    public event EventHandler? ResetNetworkRequested;

    /// <summary>"Exit" was clicked.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Sets the texts in the current language (<c>Strings</c> follows <c>Localizer</c>); called again whenever the language changes.</summary>
    public void ApplyTexts()
    {
        _showWidgetItem.Header = Strings.Tray_ShowWidget;
        _alwaysOnTopItem.Header = Strings.Tray_AlwaysOnTop;
        _desktopItem.Header = Strings.Tray_Desktop;
        _layoutMenu.Header = Strings.Tray_Layout;
        _fullLayoutItem.Header = Strings.Layout_Full;
        _horizontalLayoutItem.Header = Strings.Layout_Horizontal;
        _verticalLayoutItem.Header = Strings.Layout_Vertical;
        _snapToEdgesItem.Header = Strings.Tray_SnapToEdges;
        _lockPositionItem.Header = Strings.Tray_LockPosition;
        _clickThroughItem.Header = Strings.Tray_ClickThrough;
        _resetNetworkItem.Header = Strings.Tray_ResetNetwork;
        _exitItem.Header = Strings.Tray_Exit;
    }

    /// <summary>Sets every check mark from the applied settings and the widget's visibility.</summary>
    /// <param name="settings">Settings as the app applied them.</param>
    /// <param name="widgetShown">Whether the widget window is shown.</param>
    public void Update(AppSettings settings, bool widgetShown)
    {
        _showWidgetItem.IsChecked = widgetShown;
        _alwaysOnTopItem.IsChecked = settings.Mode == WidgetMode.AlwaysOnTop;
        _desktopItem.IsChecked = settings.Mode == WidgetMode.Desktop;
        _fullLayoutItem.IsChecked = settings.Layout == ViewLayout.Full;
        _horizontalLayoutItem.IsChecked = settings.Layout == ViewLayout.CompactHorizontal;
        _verticalLayoutItem.IsChecked = settings.Layout == ViewLayout.CompactVertical;
        _snapToEdgesItem.IsChecked = settings.SnapToEdges;
        _lockPositionItem.IsChecked = settings.PositionLocked;
        _clickThroughItem.IsChecked = settings.ClickThrough;
    }

    /// <summary>Sets the tooltip; some desktops don't show tray tooltips at all, so nothing depends on it.</summary>
    /// <param name="text">Tooltip text.</param>
    public void SetTooltip(string text) => _icon.ToolTipText = text;

    /// <summary>Removes the icon from the tray.</summary>
    public void Dispose()
    {
        _icon.IsVisible = false;
        _icon.Dispose();
    }

    private static NativeMenuItem Item(MenuItemToggleType toggle, Action click)
    {
        var item = new NativeMenuItem { ToggleType = toggle };
        item.Click += (_, _) => click();
        return item;
    }
}
