namespace CobiHw.App.Settings;

public sealed record SavedSettings(DisplaySettings Display, ColorSettings Colors, NetworkSettings Network, AppearanceSettings Appearance, string Language);
