using System.Drawing.Drawing2D;

namespace ItemAdderLauncher.UI;

/// <summary>
/// Bouton arrondi dessiné à la main : coins réellement transparents (le fond du
/// parent est repeint sous les angles), dégradé léger pour les actions
/// principales, transition douce au survol et icône Segoe MDL2 alignée sur le
/// texte.
/// </summary>
public class RoundedButton : Button
{
    private static readonly TextFormatFlags MeasureFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    private static readonly TextFormatFlags DrawFlags =
        TextFormatFlags.NoPadding |
        TextFormatFlags.SingleLine |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.Left |
        TextFormatFlags.VerticalCenter;

    private readonly System.Windows.Forms.Timer _transition;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;
    private float _hoverAmount;
    private float _pressAmount;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        TabStop = true;
        DoubleBuffered = true;

        _transition = new System.Windows.Forms.Timer { Interval = 15 };
        _transition.Tick += (_, _) => StepTransition();
    }

    public int CornerRadius { get; set; } = 10;

    /// <summary>Intensité du dégradé vertical (0 = plat).</summary>
    public float Sheen { get; set; }

    public string IconGlyph { get; set; } = string.Empty;

    public Font? IconFont { get; set; }

    public Color IconColor { get; set; } = Color.Empty;

    public int IconNudge { get; set; } = 1;

    public Color BorderColor { get; set; } = Color.Transparent;

    public Color HoverBorderColor { get; set; } = Color.Empty;

    public int BorderThickness { get; set; }

    public Color HoverBackColor { get; set; } = Color.Empty;

    public Color PressedBackColor { get; set; } = Color.Empty;

    public Color DisabledBackColor { get; set; } = Color.Empty;

    /// <summary>Fond neutre commun aux boutons inactifs, quel que soit leur rôle.</summary>
    private static readonly Color DisabledFillColor = Color.FromArgb(20, 27, 39);

    private static readonly Color DisabledBorderColor = Color.FromArgb(35, 47, 66);

    public int IconGap { get; set; } = 9;

    public int ContentPadding { get; set; } = 16;

    public Color SelectedBackColor { get; set; } = Color.Empty;

    public bool ShowSelectionBar { get; set; }

    public Color SelectionBarColor { get; set; } = Theme.Accent;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        StartTransition();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        StartTransition();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            StartTransition();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        StartTransition();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            _hovered = false;
            _pressed = false;
        }

        _transition.Stop();
        _hoverAmount = 0f;
        _pressAmount = 0f;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using GraphicsPath shape = CreatePath(0);
        PaintParentBehind(graphics, shape);

        Color fill = ResolveBackground();
        if (Sheen > 0.01f && Enabled)
        {
            var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(2, Height));
            using var gradient = new LinearGradientBrush(
                bounds,
                Mix(fill, Color.White, Sheen),
                Mix(fill, Color.Black, Sheen * 0.32f),
                LinearGradientMode.Vertical);
            graphics.FillPath(gradient, shape);
        }
        else
        {
            using var brush = new SolidBrush(fill);
            graphics.FillPath(brush, shape);
        }

        if (BorderThickness > 0 && BorderColor.A > 0)
        {
            Color border = BorderColor;
            if (_hoverAmount > 0.01f)
            {
                Color hoverBorder = HoverBorderColor == Color.Empty
                    ? Mix(BorderColor, Color.White, 0.22f)
                    : HoverBorderColor;
                border = Mix(BorderColor, hoverBorder, _hoverAmount);
            }

            if (!Enabled)
            {
                border = DisabledBorderColor;
            }

            using GraphicsPath borderPath = CreatePath(BorderThickness);
            using var pen = new Pen(border, BorderThickness);
            graphics.DrawPath(pen, borderPath);
        }

        if (Selected && ShowSelectionBar)
        {
            GraphicsState state = graphics.Save();
            graphics.SetClip(shape, CombineMode.Replace);
            using var bar = new SolidBrush(SelectionBarColor);
            int barHeight = Math.Min(20, Height - 12);
            graphics.FillRectangle(bar, 0, (Height - barHeight) / 2, 4, barHeight);
            graphics.Restore(state);
        }

        DrawContent(graphics);

        if (Focused && ShowFocusCues)
        {
            using GraphicsPath ring = CreatePath(3);
            using var pen = new Pen(Color.FromArgb(150, Theme.Accent), 1) { DashStyle = DashStyle.Dot };
            graphics.DrawPath(pen, ring);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transition.Stop();
            _transition.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawContent(Graphics graphics)
    {
        bool hasText = !string.IsNullOrEmpty(Text);
        bool hasIcon = !string.IsNullOrEmpty(IconGlyph);
        if (!hasText && !hasIcon)
        {
            return;
        }

        Color foreground = Enabled ? ForeColor : Theme.TextFaint;
        Color glyphColor = IconColor == Color.Empty ? foreground : IconColor;
        if (!Enabled)
        {
            glyphColor = Theme.TextFaint;
        }

        int pressOffset = _pressAmount > 0.5f ? 1 : 0;
        Font textFont = Font;
        Font iconFont = IconFont ?? Theme.Icon(10f);

        Size textSize = hasText
            ? TextRenderer.MeasureText(Text, textFont, Size.Empty, MeasureFlags)
            : Size.Empty;
        Size iconSize = hasIcon
            ? TextRenderer.MeasureText(IconGlyph, iconFont, Size.Empty, MeasureFlags)
            : Size.Empty;

        int gap = hasText && hasIcon ? IconGap : 0;
        int contentWidth = textSize.Width + (hasIcon ? iconSize.Width + gap : 0);
        int padding = Math.Max(4, Math.Min(ContentPadding, (Width - contentWidth) / 2));

        int x = TextAlign switch
        {
            ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft =>
                padding,
            ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight =>
                Math.Max(2, Width - padding - contentWidth),
            _ => Math.Max(2, (Width - contentWidth) / 2)
        };

        if (hasIcon)
        {
            TextRenderer.DrawText(
                graphics,
                IconGlyph,
                iconFont,
                new Rectangle(x, IconNudge + pressOffset, iconSize.Width + 3, Height),
                glyphColor,
                DrawFlags);
            x += iconSize.Width + gap;
        }

        if (hasText)
        {
            int available = Math.Max(8, Width - x - 6);
            bool clipped = textSize.Width > available;
            TextFormatFlags flags = clipped ? DrawFlags | TextFormatFlags.EndEllipsis : DrawFlags;
            TextRenderer.DrawText(
                graphics,
                Text,
                textFont,
                new Rectangle(x, pressOffset, Math.Min(available, textSize.Width + 6), Height),
                foreground,
                flags);
        }
    }

    private void PaintParentBehind(Graphics graphics, GraphicsPath shape)
    {
        using var outside = new Region(ClientRectangle);
        outside.Exclude(shape);

        Control? parent = Parent;
        if (parent is null || parent.Width <= 0 || parent.Height <= 0)
        {
            using var fallback = new SolidBrush(Theme.Background);
            graphics.FillRegion(fallback, outside);
            return;
        }

        GraphicsState state = graphics.Save();
        graphics.SetClip(outside, CombineMode.Replace);
        graphics.TranslateTransform(-Left, -Top);

        var bounds = new Rectangle(Left, Top, Width, Height);
        using var args = new PaintEventArgs(graphics, bounds);
        try
        {
            InvokePaintBackground(parent, args);
            InvokePaint(parent, args);
        }
        catch
        {
            using var fallback = new SolidBrush(Theme.Background);
            graphics.FillRegion(fallback, outside);
        }

        graphics.Restore(state);
    }

    private Color ResolveBackground()
    {
        if (!Enabled)
        {
            return DisabledBackColor == Color.Empty ? DisabledFillColor : DisabledBackColor;
        }

        Color baseColor = Selected
            ? (SelectedBackColor == Color.Empty ? ControlPaint.Light(BackColor, 0.1f) : SelectedBackColor)
            : BackColor;

        Color hover = HoverBackColor == Color.Empty
            ? ControlPaint.Light(baseColor, 0.12f)
            : HoverBackColor;
        Color pressed = PressedBackColor == Color.Empty
            ? ControlPaint.Dark(baseColor, 0.1f)
            : PressedBackColor;

        Color result = _hoverAmount > 0.01f ? Mix(baseColor, hover, _hoverAmount) : baseColor;
        return _pressAmount > 0.01f ? Mix(result, pressed, _pressAmount) : result;
    }

    private void StartTransition()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        if (!_transition.Enabled)
        {
            _transition.Start();
        }
    }

    private void StepTransition()
    {
        float targetHover = _hovered && Enabled ? 1f : 0f;
        float targetPress = _pressed && Enabled ? 1f : 0f;

        _hoverAmount = Approach(_hoverAmount, targetHover);
        _pressAmount = Approach(_pressAmount, targetPress);
        Invalidate();

        if (Math.Abs(_hoverAmount - targetHover) < 0.01f
            && Math.Abs(_pressAmount - targetPress) < 0.01f)
        {
            _hoverAmount = targetHover;
            _pressAmount = targetPress;
            _transition.Stop();
        }
    }

    private static float Approach(float current, float target)
    {
        return current < target
            ? Math.Min(target, current + 0.16f)
            : Math.Max(target, current - 0.16f);
    }

    private static Color Mix(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            from.A,
            (int)Math.Round(from.R + ((to.R - from.R) * amount)),
            (int)Math.Round(from.G + ((to.G - from.G) * amount)),
            (int)Math.Round(from.B + ((to.B - from.B) * amount)));
    }

    private GraphicsPath CreatePath(int inset)
    {
        var path = new GraphicsPath();
        float borderInset = inset > 0 ? BorderThickness / 2f : 0f;
        float offset = inset + borderInset;
        var rect = new RectangleF(
            offset,
            offset,
            Math.Max(0, Width - (offset * 2f)),
            Math.Max(0, Height - (offset * 2f)));

        float radius = Math.Min(
            Math.Max(0, CornerRadius - inset),
            Math.Min(rect.Width, rect.Height) / 2f);

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
