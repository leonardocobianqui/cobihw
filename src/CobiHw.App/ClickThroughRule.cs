using CobiHw.App.Settings;

namespace CobiHw.App;

public static class ClickThroughRule
{
    public const int ControlKey = 0x11;
    public const int ShiftKey = 0x10;
    public const int AltKey = 0x12;

    public static IReadOnlyList<int> KeysOf(InteractionKey key) => key switch
    {
        InteractionKey.Ctrl => [ControlKey],
        InteractionKey.Shift => [ShiftKey],
        InteractionKey.Alt => [AltKey],
        InteractionKey.CtrlShift => [ControlKey, ShiftKey],
        InteractionKey.CtrlAlt => [ControlKey, AltKey],
        _ => [],
    };

    public static string LabelOf(InteractionKey key) => key switch
    {
        InteractionKey.Ctrl => "Ctrl",
        InteractionKey.Shift => "Shift",
        InteractionKey.Alt => "Alt",
        InteractionKey.CtrlShift => "Ctrl + Shift",
        InteractionKey.CtrlAlt => "Ctrl + Alt",
        _ => Resources.Strings.Key_None,
    };

    public static bool IsHolding(InteractionKey key, Func<int, bool> isKeyDown)
    {
        IReadOnlyList<int> keys = KeysOf(key);
        return keys.Count > 0 && keys.All(isKeyDown);
    }
}
