using System.Drawing.Drawing2D;

namespace ItemAdderLauncher.UI;

public class RoundedPanel : Panel
{
    private int _cornerRadius = 10;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            UpdateRegion();
            Invalidate();
        }
    }

    public Color BorderColor { get; set; } = Color.Transparent;

    public int BorderThickness { get; set; } = 1;

    public bool ShowDividers { get; set; }

    public Color DividerColor { get; set; } = Theme.BorderSoft;

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = CreatePath();
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (GraphicsPath path = CreatePath())
        {
            if (BorderThickness > 0 && BorderColor.A > 0)
            {
                using var pen = new Pen(BorderColor, BorderThickness);
                e.Graphics.DrawPath(pen, path);
            }
        }

        if (ShowDividers && Width > 3)
        {
            using var pen = new Pen(DividerColor);
            int top = Math.Min(44, Height);
            int bottom = Math.Max(top, Height - 12);
            for (int i = 1; i <= 2; i++)
            {
                int x = Width * i / 3;
                e.Graphics.DrawLine(pen, x, top, x, bottom);
            }
        }
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using GraphicsPath path = CreatePath();
        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
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
