namespace CobiHw.Core.Fps;

/// <summary>Frame rate of the foreground app.</summary>
/// <param name="FramesPerSecond">Frames presented in the last second.</param>
/// <param name="ProcessName">Executable name without ".exe", or null if it couldn't be read.</param>
public sealed record FpsSnapshot(double FramesPerSecond, string? ProcessName);
