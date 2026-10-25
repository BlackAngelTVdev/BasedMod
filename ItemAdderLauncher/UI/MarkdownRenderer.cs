using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ItemAdderLauncher.UI;

/// <summary>
/// Rendu d'un sous-ensemble de Markdown GitHub dans un RichTextBox :
/// titres, paragraphes, listes, citations, blocs de code, gras, italique,
/// barré, code en ligne et liens. Les images et les balises HTML sont retirées.
/// </summary>
public static class MarkdownRenderer
{
    private const float BaseSize = 9f;
    private const float CodeSize = 8.4f;

    private static readonly Dictionary<(float Size, FontStyle Style), Font> FontCache = new();
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex TaskItemPattern = new(@"^\[( |x|X)\]\s+", RegexOptions.Compiled);

    public static void Render(RichTextBox box, string markdown)
    {
        box.Clear();
        box.Font = GetFont(BaseSize, FontStyle.Regular);
        box.SelectionIndent = 0;
        box.SelectionRightIndent = 0;
        box.SelectionBackColor = box.BackColor;

        if (string.IsNullOrWhiteSpace(markdown))
        {
            AppendRun(box, "Pas de notes pour cette release.", GetFont(BaseSize, FontStyle.Italic), Theme.TextFaint);
            return;
        }

        string text = StripEmoji(
            HtmlTagPattern.Replace(markdown.Replace("\r\n", "\n").Replace('\r', '\n'), string.Empty));
        string[] lines = text.Split('\n');

        bool inCode = false;
        bool lastWasBlank = true;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                AppendRun(
                    box,
                    (line.Length == 0 ? " " : line) + "\n",
                    GetFont(CodeSize, FontStyle.Regular, monospace: true),
                    Color.FromArgb(163, 205, 194),
                    indent: 10,
                    background: Color.FromArgb(10, 15, 24));
                lastWasBlank = false;
                continue;
            }

            if (line.Length == 0)
            {
                if (!lastWasBlank)
                {
                    AppendRun(box, "\n", GetFont(BaseSize, FontStyle.Regular), Theme.Text);
                    lastWasBlank = true;
                }

                continue;
            }

            if (line.Trim() is "---" or "***" or "___")
            {
                AppendRun(box, "\n", GetFont(BaseSize, FontStyle.Regular), Theme.Text);
                lastWasBlank = true;
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                int level = 0;
                while (level < line.Length && line[level] == '#')
                {
                    level++;
                }

                string heading = line.Substring(level).Trim();
                if (heading.Length > 0)
                {
                    float size = level switch
                    {
                        1 => 13.5f,
                        2 => 11.6f,
                        3 => 10.4f,
                        _ => 9.6f
                    };

                    Color color = level <= 2 ? Theme.Text : Theme.Accent;

                    if (!lastWasBlank)
                    {
                        AppendRun(box, "\n", GetFont(BaseSize, FontStyle.Regular), Theme.Text);
                    }

                    AppendRun(box, heading + "\n", GetFont(size, FontStyle.Bold), color);
                    lastWasBlank = false;
                }

                continue;
            }

            if (line.StartsWith(">", StringComparison.Ordinal))
            {
                string quote = line.TrimStart('>', ' ');
                AppendInline(box, "« " + quote + " »\n", BaseSize, Theme.TextMuted, italic: true, indent: 12);
                lastWasBlank = false;
                continue;
            }

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("|", StringComparison.Ordinal))
            {
                string row = trimmed.Trim('|').Replace("|", " · ").Trim();
                if (row.Length > 0 && !row.All(c => c is '-' or ':' or ' '))
                {
                    AppendInline(box, row + "\n", 8.6f, Theme.TextMuted);
                }

                lastWasBlank = false;
                continue;
            }

            int indent = Math.Max(0, line.Length - trimmed.Length);
            int indentPx = 10 + indent / 2 * 10;

            if (trimmed.StartsWith("- ", StringComparison.Ordinal)
                || trimmed.StartsWith("* ", StringComparison.Ordinal)
                || trimmed.StartsWith("+ ", StringComparison.Ordinal))
            {
                string content = trimmed.Substring(2);
                Match task = TaskItemPattern.Match(content);
                string bullet = task.Success
                    ? (task.Groups[1].Value is "x" or "X" ? "☑  " : "☐  ")
                    : "•  ";

                if (task.Success)
                {
                    content = content.Substring(task.Length);
                }

                AppendInline(box, bullet + content + "\n", BaseSize, Theme.Text, indent: indentPx + 4);
                lastWasBlank = false;
                continue;
            }

            Match numbered = Regex.Match(trimmed, @"^(\d+)[.)]\s+(.*)$");
            if (numbered.Success)
            {
                AppendInline(
                    box,
                    numbered.Groups[1].Value + ".  " + numbered.Groups[2].Value + "\n",
                    BaseSize,
                    Theme.Text,
                    indent: indentPx + 4);
                lastWasBlank = false;
                continue;
            }

            AppendInline(box, trimmed + "\n", BaseSize, Theme.Text, indent: indentPx);
            lastWasBlank = false;
        }

        box.SelectionStart = 0;
        box.SelectionLength = 0;
    }

    /// <summary>
    /// Donne au contrôle une hauteur égale à son contenu replié.
    /// La largeur doit être fixée avant l'appel.
    /// </summary>
    public static int Fit(RichTextBox box)
    {
        _ = box.Handle;

        const int probe = 8000;
        box.Height = probe;

        int lineHeight = (int)Math.Ceiling(GetFont(BaseSize, FontStyle.Regular).GetHeight()) + 4;
        int height = lineHeight;

        if (box.TextLength > 0)
        {
            Point last = box.GetPositionFromCharIndex(box.TextLength - 1);
            height = last.Y + lineHeight;
            if (box.Text.EndsWith("\n", StringComparison.Ordinal))
            {
                height += lineHeight;
            }
        }

        box.Height = Math.Clamp(height + 6, 22, 6000);
        return box.Height;
    }

    private static void AppendInline(
        RichTextBox box,
        string text,
        float size,
        Color color,
        bool bold = false,
        bool italic = false,
        int indent = 0)
    {
        box.SelectionIndent = indent;
        box.SelectionBackColor = box.BackColor;

        var buffer = new StringBuilder();
        bool isBold = bold;
        bool isItalic = italic;
        bool isCode = false;
        bool isStrike = false;

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            FontStyle style = FontStyle.Regular;
            if (isBold)
            {
                style |= FontStyle.Bold;
            }

            if (isItalic)
            {
                style |= FontStyle.Italic;
            }

            if (isStrike)
            {
                style |= FontStyle.Strikeout;
            }

            box.SelectionFont = GetFont(isCode ? CodeSize : size, style, monospace: isCode);
            box.SelectionColor = isCode ? Theme.Accent : color;
            box.SelectionBackColor = isCode
                ? Color.FromArgb(28, 24, 16)
                : box.BackColor;
            box.AppendText(buffer.ToString());
            buffer.Clear();
        }

        int index = 0;
        while (index < text.Length)
        {
            char current = text[index];

            if (current == '`')
            {
                Flush();
                isCode = !isCode;
                index++;
                continue;
            }

            if (current == '*' && index + 1 < text.Length && text[index + 1] == '*')
            {
                Flush();
                isBold = !isBold;
                index += 2;
                continue;
            }

            if (current == '_' && index + 1 < text.Length && text[index + 1] == '_')
            {
                Flush();
                isBold = !isBold;
                index += 2;
                continue;
            }

            if (current == '~' && index + 1 < text.Length && text[index + 1] == '~')
            {
                Flush();
                isStrike = !isStrike;
                index += 2;
                continue;
            }

            if (current is '*' or '_')
            {
                Flush();
                isItalic = !isItalic;
                index++;
                continue;
            }

            if (current == '!' && index + 1 < text.Length && text[index + 1] == '[')
            {
                int imageEnd = FindLinkEnd(text, index + 1);
                if (imageEnd > 0)
                {
                    index = imageEnd + 1;
                    continue;
                }
            }

            if (current == '[')
            {
                int labelEnd = text.IndexOf("](", index, StringComparison.Ordinal);
                int linkEnd = labelEnd > 0 ? text.IndexOf(')', labelEnd) : -1;
                if (labelEnd > 0 && linkEnd > 0)
                {
                    Flush();
                    string label = text.Substring(index + 1, labelEnd - index - 1);
                    box.SelectionFont = GetFont(size, FontStyle.Underline | (isBold ? FontStyle.Bold : FontStyle.Regular));
                    box.SelectionColor = Theme.Blue;
                    box.SelectionBackColor = box.BackColor;
                    box.AppendText(label);
                    index = linkEnd + 1;
                    continue;
                }
            }

            buffer.Append(current);
            index++;
        }

        Flush();
    }

    /// <summary>
    /// GDI+ n'affiche pas les emoji en couleur et rend les plus récents en carrés vides :
    /// on les retire pour garder un texte propre.
    /// </summary>
    private static string StripEmoji(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (char.IsSurrogate(character))
            {
                continue;
            }

            if (character is '\uFE0F' or '\u200D' or '\u20E3')
            {
                continue;
            }

            if (character >= '\u2600' && character <= '\u27BF')
            {
                continue;
            }

            if (character >= '\u2B00' && character <= '\u2BFF')
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static int FindLinkEnd(string text, int openBracket)
    {
        int labelEnd = text.IndexOf("](", openBracket, StringComparison.Ordinal);
        if (labelEnd < 0)
        {
            return -1;
        }

        return text.IndexOf(')', labelEnd);
    }

    private static void AppendRun(
        RichTextBox box,
        string text,
        Font font,
        Color color,
        int indent = 0,
        Color? background = null)
    {
        box.SelectionIndent = indent;
        box.SelectionFont = font;
        box.SelectionColor = color;
        box.SelectionBackColor = background ?? box.BackColor;
        box.AppendText(text);
    }

    private static Font GetFont(float size, FontStyle style, bool monospace = false)
    {
        string family = monospace ? "Consolas" : "Segoe UI";
        var cacheKey = (size + (monospace ? 100f : 0f), style);

        if (!FontCache.TryGetValue(cacheKey, out Font? font))
        {
            font = new Font(family, size, style, GraphicsUnit.Point);
            FontCache[cacheKey] = font;
        }

        return font;
    }
}
