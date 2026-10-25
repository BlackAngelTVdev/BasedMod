using System.Drawing.Drawing2D;

namespace ItemAdderLauncher.UI;

/// <summary>
/// Panneau de fond : dessine l'illustration (magasin flou, lumières chaudes)
/// puis un voile sombre dégradé, mis en cache pour que les contrôles
/// transparents posés dessus restent rapides à repeindre.
/// </summary>
public sealed class BackdropPanel : Panel
{
    private Image? _artwork;
    private Bitmap? _cache;

    public Image? Artwork
    {
        get => _artwork;
        set
        {
            _artwork = value;
            RebuildCache();
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        RebuildCache();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_cache is null)
        {
            e.Graphics.Clear(BackColor);
            return;
        }

        e.Graphics.DrawImageUnscaled(_cache, 0, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cache?.Dispose();
            _cache = null;
        }

        base.Dispose(disposing);
    }

    private void RebuildCache()
    {
        _cache?.Dispose();
        _cache = null;

        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        _cache = new Bitmap(Width, Height);

        using Graphics graphics = Graphics.FromImage(_cache);
        graphics.Clear(BackColor);

        if (_artwork is not null)
        {
            float scale = Math.Max((float)Width / _artwork.Width, (float)Height / _artwork.Height);
            int width = (int)Math.Ceiling(_artwork.Width * scale);
            int height = (int)Math.Ceiling(_artwork.Height * scale);

            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                _artwork,
                new Rectangle((Width - width) / 2, (Height - height) / 2, width, height));
        }

        using var veil = new LinearGradientBrush(
            new Rectangle(0, 0, Width, Height),
            Color.FromArgb(94, 7, 11, 18),
            Color.FromArgb(162, 4, 7, 12),
            LinearGradientMode.Vertical);
        graphics.FillRectangle(veil, ClientRectangle);
    }
}
