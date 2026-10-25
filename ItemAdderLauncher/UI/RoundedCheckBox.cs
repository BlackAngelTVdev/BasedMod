using System.Drawing.Drawing2D;

namespace ItemAdderLauncher.UI;

/// <summary>
/// Case à cocher dessinée par le launcher : coche ambre, case arrondie,
/// lisible sur les cartes sombres (la case native reste blanche).
/// </summary>
public sealed class RoundedCheckBox : CheckBox
{
    private bool _hovered;

    public RoundedCheckBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        AutoSize = false;
        Height = 24;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = Theme.Ui(8.8f);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        const int boxSize = 18;
        var box = new Rectangle(1, (Height - boxSize) / 2, boxSize, boxSize);

        using (GraphicsPath path = Rounded(box, 5))
        {
            using var fill = new SolidBrush(
                Checked ? Theme.Accent : Color.FromArgb(17, 24, 35));
            graphics.FillPath(fill, path);

            Color border = Checked
                ? Theme.Accent
                : _hovered
                    ? Color.FromArgb(70, 96, 132)
                    : Theme.Border;
            using var pen = new Pen(border, 1f);
            graphics.DrawPath(pen, path);
        }

        if (Checked)
        {
            TextRenderer.DrawText(
                graphics,
                "\uE73E",
                Theme.Icon(7.5f),
                box,
                Color.FromArgb(12, 18, 28),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        Color textColor = Enabled ? ForeColor : Color.FromArgb(120, ForeColor);
        var textRect = new Rectangle(box.Right + 9, 0, Math.Max(0, Width - box.Right - 10), Height);
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textRect,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (Focused && ShowFocusCues)
        {
            using var focusPen = new Pen(Color.FromArgb(120, Theme.Accent)) { DashStyle = DashStyle.Dot };
            graphics.DrawRectangle(focusPen, 0, 1, Width - 1, Height - 3);
        }
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
