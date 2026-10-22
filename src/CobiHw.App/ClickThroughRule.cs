using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Keys that make a click-through widget clickable again while held.</summary>
public static class ClickThroughRule
{
    /// <summary>VK_CONTROL.</summary>
    public const int ControlKey = 0x11;
    /// <summary>VK_SHIFT.</summary>
    public const int ShiftKey = 0x10;
    /// <summary>VK_MENU (Alt).</summary>
    public const int AltKey = 0x12;

    /// <summary>Virtual-key codes that must all be held; empty for "None".</summary>
    /// <param name="key">Configured key.</param>
    public static IReadOnlyList<int> KeysOf(InteractionKey key) => key switch
    {
        InteractionKey.Ctrl => [ControlKey],
        InteractionKey.Shift => [ShiftKey],
        InteractionKey.Alt => [AltKey],
        InteractionKey.CtrlShift => [ControlKey, ShiftKey],
        InteractionKey.CtrlAlt => [ControlKey, AltKey],
        _ => [],
    };

    /// <summary>Text shown for the key, e.g. "Ctrl + Shift".</summary>
    /// <param name="key">Configured key.</param>
    public static string LabelOf(InteractionKey key) => key switch
    {
        InteractionKey.Ctrl => "Ctrl",
        InteractionKey.Shift => "Shift",
        InteractionKey.Alt => "Alt",
        InteractionKey.CtrlShift => "Ctrl + Shift",
        InteractionKey.CtrlAlt => "Ctrl + Alt",
        _ => Resources.Strings.Key_None,
    };

    /// <summary>True while every key of the combination is down.</summary>
    /// <param name="key">Configured key.</param>
    /// <param name="isKeyDown">Checks one virtual-key code; passed in so the rule can be unit tested.</param>
    public static bool IsHolding(InteractionKey key, Func<int, bool> isKeyDown)
    {
        IReadOnlyList<int> keys = KeysOf(key);
        return keys.Count > 0 && keys.All(isKeyDown);
    }
}
