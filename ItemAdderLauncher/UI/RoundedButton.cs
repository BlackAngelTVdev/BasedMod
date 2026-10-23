using System.Drawing.Drawing2D;

namespace ItemAdderLauncher.UI;

public class RoundedButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public int CornerRadius { get; set; } = 8;

    public string IconGlyph { get; set; } = string.Empty;

    public Font? IconFont { get; set; }

    public Color IconColor { get; set; } = Color.Empty;

    public Color BorderColor { get; set; } = Color.Transparent;

    public int BorderThickness { get; set; }

    public Color HoverBackColor { get; set; } = Color.Empty;

    public Color PressedBackColor { get; set; } = Color.Empty;

    public Color DisabledBackColor { get; set; } = Color.Empty;

    public int IconGap { get; set; } = 8;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Color background = ResolveBackground();
        using (GraphicsPath path = CreatePath())
        {
            using var brush = new SolidBrush(background);
            graphics.FillPath(brush, path);

            if (BorderThickness > 0 && BorderColor.A > 0)
            {
                using var pen = new Pen(BorderColor, BorderThickness);
                graphics.DrawPath(pen, path);
            }
        }

        Color foreground = Enabled ? ForeColor : Color.FromArgb(125, ForeColor);
        Color iconColor = !Enabled
            ? Color.FromArgb(110, IconColor == Color.Empty ? ForeColor : IconColor)
            : (IconColor == Color.Empty ? ForeColor : IconColor);

        Font textFont = Font;
        Font iconFont = IconFont ?? Theme.Icon(10f);
        Size textSize = string.IsNullOrEmpty(Text)
            ? Size.Empty
            : TextRenderer.MeasureText(Text, textFont, Size.Empty, TextFormatFlags.NoPadding);
        Size iconSize = string.IsNullOrEmpty(IconGlyph)
            ? Size.Empty
            : TextRenderer.MeasureText(IconGlyph, iconFont, Size.Empty, TextFormatFlags.NoPadding);

        int gap = !string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(IconGlyph) ? IconGap : 0;
        int contentWidth = textSize.Width + iconSize.Width + gap;
        int x = Math.Max(2, (Width - contentWidth) / 2);

        if (!string.IsNullOrEmpty(IconGlyph))
        {
            TextRenderer.DrawText(
                graphics,
                IconGlyph,
                iconFont,
                new Rectangle(x, 0, iconSize.Width, Height),
                iconColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += iconSize.Width + gap;
        }

        if (!string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(
                graphics,
                Text,
                textFont,
                new Rectangle(x, 0, textSize.Width + 2, Height),
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        if (Focused && ShowFocusCues)
        {
            using var focusPen = new Pen(Color.FromArgb(140, Theme.Accent), 1) { DashStyle = DashStyle.Dot };
            graphics.DrawRectangle(focusPen, 3, 3, Width - 7, Height - 7);
        }
    }

    private Color ResolveBackground()
    {
        if (!Enabled)
        {
            return DisabledBackColor == Color.Empty
                ? Color.FromArgb(120, BackColor)
                : DisabledBackColor;
        }

        if (_pressed)
        {
            return PressedBackColor == Color.Empty
                ? ControlPaint.Dark(BackColor, 0.08f)
                : PressedBackColor;
        }

        if (_hovered)
        {
            return HoverBackColor == Color.Empty
                ? ControlPaint.Light(BackColor, 0.1f)
                : HoverBackColor;
        }

        return BackColor;
    }

    private GraphicsPath CreatePath()
    {
        var path = new GraphicsPath();
        float inset = BorderThickness > 0 ? BorderThickness / 2f : 0f;
        var rect = new RectangleF(
            inset,
            inset,
            Math.Max(0, Width - BorderThickness),
            Math.Max(0, Height - BorderThickness));
        float radius = Math.Min(CornerRadius, Math.Min(rect.Width, rect.Height) / 2f);

        if (radius <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float diameter = radius * 2f;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
