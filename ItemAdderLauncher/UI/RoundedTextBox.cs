namespace ItemAdderLauncher.UI;

public class RoundedTextBox : RoundedPanel
{
    private readonly TextBox _textBox;
    private readonly Label _chevron;

    public RoundedTextBox()
    {
        CornerRadius = 7;
        BackColor = Theme.Input;
        BorderColor = Theme.BorderSoft;
        BorderThickness = 1;

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            Font = Theme.Ui(9f),
            AutoSize = false
        };
        Controls.Add(_textBox);

        _chevron = new Label
        {
            Text = "\uE70D",
            Font = Theme.Icon(7.5f),
            ForeColor = Theme.TextFaint,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        Controls.Add(_chevron);
    }

    public TextBox InnerTextBox => _textBox;

    public string Value
    {
        get => _textBox.Text;
        set => _textBox.Text = value ?? string.Empty;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        LayoutChildren();
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        LayoutChildren();
    }

    private void LayoutChildren()
    {
        if (_textBox is null || _chevron is null)
        {
            return;
        }

        int chevronWidth = 24;
        _textBox.Location = new Point(12, 7);
        _textBox.Size = new Size(
            Math.Max(20, Width - 12 - chevronWidth - 2),
            20);
        _chevron.Location = new Point(Width - chevronWidth, 0);
        _chevron.Size = new Size(chevronWidth - 4, Height);
    }
}
