using CobiHw.App.Interop;
using Forms = System.Windows.Forms;

namespace CobiHw.Tests.App;

/// <summary>user32.dll calls and structures of the App, in the architecture running the tests (x64 or x86).</summary>
[Collection(AppState.Name)]
public sealed class User32Tests
{
    [Fact]
    public void ExtendedStyle_CanEnableAndRemoveClickThrough()
    {
        // A real hidden window: in x86 this goes through GetWindowLongW/SetWindowLongW.
        var window = new Forms.NativeWindow();
        window.CreateHandle(new Forms.CreateParams { Caption = "cobihw interop test" });

        try
        {
            nint original = User32.GetWindowLongPtr(window.Handle, User32.GwlExStyle);
            nint passThrough = original | User32.WsExTransparent;

            User32.SetWindowLongPtr(window.Handle, User32.GwlExStyle, passThrough);
            Assert.Equal(passThrough, User32.GetWindowLongPtr(window.Handle, User32.GwlExStyle));

            User32.SetWindowLongPtr(window.Handle, User32.GwlExStyle, original);
            Assert.Equal(original, User32.GetWindowLongPtr(window.Handle, User32.GwlExStyle));
        }
        finally
        {
            window.DestroyHandle();
        }
    }

    [Fact]
    public void Structures_MatchWinuser()
    {
        // Two window handles: 8 bytes each in x64, 4 in x86.
        NativeLayout.AssertSize<User32.WindowPos>(x64: 40, x86: 28);
        NativeLayout.AssertSize<User32.MonitorInfo>(x64: 40, x86: 40);
    }
}
