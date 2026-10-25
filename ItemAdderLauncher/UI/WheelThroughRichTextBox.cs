using System.Runtime.InteropServices;

namespace ItemAdderLauncher.UI;

/// <summary>
/// Zone de texte en lecture seule dont la molette fait défiler la liste
/// parente au lieu de rester bloquée dans un contrôle qui n'a pas de barre de
/// défilement : sans ça, la molette ne fait rien au-dessus du texte des cartes.
/// </summary>
public sealed class WheelThroughRichTextBox : RichTextBox
{
    private const int WmMouseWheel = 0x020A;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseWheel)
        {
            ScrollableControl? host = FindScrollHost();
            if (host is not null)
            {
                SendMessage(host.Handle, WmMouseWheel, m.WParam, m.LParam);
                return;
            }
        }

        base.WndProc(ref m);
    }

    private ScrollableControl? FindScrollHost()
    {
        for (Control? current = Parent; current is not null; current = current.Parent)
        {
            if (current is ScrollableControl host && host.VerticalScroll.Visible)
            {
                return host;
            }
        }

        return null;
    }
}
