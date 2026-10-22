using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CobiHw.App.Tray;

/// <summary>Dark look for the tray menu, which WinForms doesn't theme on its own.</summary>
public sealed class DarkMenuRenderer() : Forms.ToolStripProfessionalRenderer(new DarkMenuColors())
{
    private static readonly Drawing.Color Text = Drawing.Color.FromArgb(0xF2, 0xF2, 0xF2);
    private static readonly Drawing.Color DisabledText = Drawing.Color.FromArgb(0x8A, 0x8A, 0x8A);

    /// <inheritdoc />
    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Text : DisabledText;
        base.OnRenderItemText(e);
    }

    /// <inheritdoc />
    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Text;
        base.OnRenderArrow(e);
    }

    /// <summary>Draws a plain check mark instead of the default boxed one.</summary>
    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        Drawing.Rectangle box = e.ImageRectangle;
        using var pen = new Drawing.Pen(Text, 1.6f);
        e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen,
        [
            new Drawing.PointF(box.Left + box.Width * 0.2f, box.Top + box.Height * 0.55f),
            new Drawing.PointF(box.Left + box.Width * 0.42f, box.Top + box.Height * 0.75f),
            new Drawing.PointF(box.Left + box.Width * 0.8f, box.Top + box.Height * 0.3f),
        ]);
    }

    /// <summary>Background, hover and line colors of the dark menu.</summary>
    private sealed class DarkMenuColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Background = Drawing.Color.FromArgb(0x2B, 0x2B, 0x2B);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(0x3D, 0x3D, 0x3D);
        private static readonly Drawing.Color Line = Drawing.Color.FromArgb(0x4A, 0x4A, 0x4A);

        // Each gradient uses a single color, so the menu looks flat.
        public override Drawing.Color ToolStripDropDownBackground => Background;
        public override Drawing.Color ImageMarginGradientBegin => Background;
        public override Drawing.Color ImageMarginGradientMiddle => Background;
        public override Drawing.Color ImageMarginGradientEnd => Background;
        public override Drawing.Color MenuBorder => Line;
        public override Drawing.Color MenuItemBorder => Hover;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
        public override Drawing.Color MenuItemPressedGradientBegin => Hover;
        public override Drawing.Color MenuItemPressedGradientEnd => Hover;
        public override Drawing.Color CheckBackground => Background;
        public override Drawing.Color CheckSelectedBackground => Hover;
        public override Drawing.Color CheckPressedBackground => Hover;
        public override Drawing.Color SeparatorDark => Line;
        public override Drawing.Color SeparatorLight => Background;
    }
}
