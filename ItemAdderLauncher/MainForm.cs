using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ItemAdderLauncher.Core;
using ItemAdderLauncher.UI;

namespace ItemAdderLauncher;

public sealed class MainForm : Form
{
    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const int NormalWidth = 710;
    private const int NormalHeight = 574;

    private readonly GitHubService _gitHub = new();
    private readonly CancellationTokenSource _lifetime = new();

    private readonly RoundedTextBox _pathInput;
    private readonly RoundedButton _browseButton;
    private readonly RoundedPanel _statusCard;
    private readonly RoundedPanel _stateBadge;
    private readonly Label _stateBadgeLabel;
    private readonly Label _installedValue;
    private readonly Label _installedFile;
    private readonly Label _latestValue;
    private readonly Label _bepInExValue;
    private readonly TextBox _logBox;
    private readonly RoundedButton _clearLogButton;
    private readonly RoundedButton _installButton;
    private readonly RoundedButton _checkButton;
    private readonly RoundedButton _localDllButton;
    private readonly RoundedButton _launchButton;
    private readonly RoundedButton _vanillaButton;
    private readonly RoundedButton _openFolderButton;
    private readonly RoundedButton _releasesButton;
    private readonly RoundedButton _maximizeButton;
    private readonly Label _appTitle;
    private readonly RoundedPanel _versionBadge;
    private readonly Panel _titleBar;
    private readonly ToolTip _toolTip = new();

    private bool _busy;
    private InstalledMod? _installedMod;
    private ReleaseInfo? _latestRelease;

    public MainForm()
    {
        Text = "ItemAdder Launcher";
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(NormalWidth, NormalHeight);
        MinimumSize = new Size(NormalWidth, NormalHeight);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Ui(9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        KeyPreview = true;
        Icon = SystemIcons.Application;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _titleBar = BuildTitleBar(out _appTitle, out _versionBadge, out _maximizeButton);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_titleBar, 0, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty,
            Padding = new Padding(20, 6, 20, 10)
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        Panel header = BuildHeader(out _releasesButton);
        content.Controls.Add(header, 0, 0);

        RoundedPanel pathCard = BuildPathCard(out _pathInput, out _browseButton);
        pathCard.Margin = new Padding(0, 0, 0, 8);
        content.Controls.Add(pathCard, 0, 1);

        _statusCard = BuildStatusCard(
            out _stateBadge,
            out _stateBadgeLabel,
            out _installedValue,
            out _installedFile,
            out _latestValue,
            out _bepInExValue);
        _statusCard.Margin = new Padding(0, 0, 0, 8);
        content.Controls.Add(_statusCard, 0, 2);

        TableLayoutPanel actions = BuildActions(
            out _installButton,
            out _checkButton,
            out _localDllButton,
            out _launchButton,
            out _vanillaButton,
            out _openFolderButton);
        actions.Margin = new Padding(0, 0, 0, 8);
        content.Controls.Add(actions, 0, 3);

        RoundedPanel journal = BuildJournal(out _logBox, out _clearLogButton);
        content.Controls.Add(journal, 0, 4);

        content.Controls.Add(BuildFooter(), 0, 5);
        root.Controls.Add(content, 0, 1);
        Controls.Add(root);

        _browseButton.Click += BrowseButton_Click;
        _installButton.Click += InstallButton_Click;
        _checkButton.Click += CheckButton_Click;
        _localDllButton.Click += LocalDllButton_Click;
        _launchButton.Click += LaunchButton_Click;
        _vanillaButton.Click += VanillaButton_Click;
        _openFolderButton.Click += OpenFolderButton_Click;
        _releasesButton.Click += (_, _) => OpenUrl(GitHubService.ReleasesUrl);
        _clearLogButton.Click += (_, _) => _logBox.Clear();
        _maximizeButton.Click += (_, _) => ToggleMaximize();

        _toolTip.SetToolTip(_browseButton, "Choisir le dossier qui contient Supermarket Together.exe");
        _toolTip.SetToolTip(_localDllButton, "Installer ou réparer le mod depuis un DLL déjà compilé");
        _toolTip.SetToolTip(_launchButton, "Lancer Supermarket Together avec BepInEx et ItemAdder");
        _toolTip.SetToolTip(_vanillaButton, "Lancer le jeu sans BepInEx ni ItemAdder");
        _toolTip.SetToolTip(_openFolderButton, "Ouvrir BepInEx\\plugins dans l'Explorateur");
        _toolTip.SetToolTip(_stateBadge, "État actuel de l'installation");

        LoadSavedGamePath();
        Shown += MainForm_Shown;
        FormClosing += (_, _) =>
        {
            _lifetime.Cancel();
        };
    }


    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateWindowRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateWindowRegion();

        if (_maximizeButton is not null)
        {
            _maximizeButton.IconGlyph =
                WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
        }

        LayoutTitleBar();
        LayoutStatusColumns();
    }

    private Panel BuildTitleBar(
        out Label appTitle,
        out RoundedPanel versionBadge,
        out RoundedButton maximizeButton)
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.TitleBar,
            Margin = Padding.Empty
        };

        var logo = new RoundedPanel
        {
            CornerRadius = 5,
            BackColor = Theme.Accent,
            Location = new Point(12, 8),
            Size = new Size(20, 20)
        };
        var logoGlyph = new Label
        {
            Text = "\uE7B8",
            Dock = DockStyle.Fill,
            Font = Theme.Icon(9f),
            ForeColor = Theme.Background,
            TextAlign = ContentAlignment.MiddleCenter
        };
        logo.Controls.Add(logoGlyph);
        titleBar.Controls.Add(logo);

        appTitle = new Label
        {
            Text = "ItemAdder Launcher",
            AutoSize = true,
            Font = Theme.Ui(9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(40, 10)
        };
        titleBar.Controls.Add(appTitle);

        versionBadge = new RoundedPanel
        {
            CornerRadius = 5,
            BackColor = Theme.Surface,
            BorderColor = Theme.BorderSoft,
            BorderThickness = 1,
            Size = new Size(54, 18),
            Location = new Point(168, 9)
        };
        var versionLabel = new Label
        {
            Text = GetLauncherVersion(),
            Dock = DockStyle.Fill,
            Font = Theme.Ui(7.5f),
            ForeColor = Theme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        };
        versionBadge.Controls.Add(versionLabel);
        titleBar.Controls.Add(versionBadge);

        RoundedButton minimizeButton = CreateTitleButton("\uE921", 0);
        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        titleBar.Controls.Add(minimizeButton);

        maximizeButton = CreateTitleButton("\uE922", 1);
        titleBar.Controls.Add(maximizeButton);

        RoundedButton closeButton = CreateTitleButton("\uE8BB", 2);
        closeButton.HoverBackColor = Color.FromArgb(127, 29, 29);
        closeButton.PressedBackColor = Color.FromArgb(153, 27, 27);
        closeButton.Click += (_, _) => Close();
        titleBar.Controls.Add(closeButton);

        var bottomBorder = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Theme.BorderSoft
        };
        titleBar.Controls.Add(bottomBorder);

        titleBar.Resize += (_, _) => LayoutTitleBar();
        EnableDrag(titleBar);
        EnableDrag(appTitle);
        EnableDrag(logo);
        EnableDrag(logoGlyph);
        EnableDrag(versionBadge);
        EnableDrag(versionLabel);

        return titleBar;
    }

    private static RoundedButton CreateTitleButton(string glyph, int slot)
    {
        var button = new RoundedButton
        {
            Text = string.Empty,
            IconGlyph = glyph,
            IconFont = Theme.Icon(8.5f),
            IconColor = Color.FromArgb(148, 163, 184),
            BackColor = Theme.TitleBar,
            HoverBackColor = Theme.Surface,
            PressedBackColor = Theme.SurfaceRaised,
            CornerRadius = 4,
            Size = new Size(30, 26),
            TabStop = false,
            Tag = slot
        };
        return button;
    }

    private Panel BuildHeader(out RoundedButton releasesButton)
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Margin = new Padding(0, 0, 0, 4)
        };

        var title = new Label
        {
            Text = "ItemAdder",
            AutoSize = true,
            Font = Theme.Ui(20f, FontStyle.Bold),
            ForeColor = Theme.Text,
            Location = new Point(8, 7)
        };
        header.Controls.Add(title);

        var pill = new RoundedPanel
        {
            CornerRadius = 14,
            BackColor = Theme.BlueSoft,
            BorderColor = Color.FromArgb(35, 69, 110),
            BorderThickness = 1,
            Location = new Point(160, 10),
            Size = new Size(166, 28)
        };
        var pillDot = new Panel
        {
            BackColor = Theme.Blue,
            Location = new Point(11, 10),
            Size = new Size(7, 7)
        };
        using (var dotPath = new GraphicsPath())
        {
            dotPath.AddEllipse(0, 0, 7, 7);
            pillDot.Region = new Region(dotPath);
        }
        pill.Controls.Add(pillDot);
        var pillText = new Label
        {
            Text = "Supermarket Together",
            Font = Theme.Ui(8.5f),
            ForeColor = Color.FromArgb(191, 219, 254),
            AutoSize = false,
            Location = new Point(24, 0),
            Size = new Size(132, 28),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pill.Controls.Add(pillText);
        header.Controls.Add(pill);

        var subtitle = new Label
        {
            Text = "Installe et met à jour le mod Supermarket Together depuis GitHub.",
            AutoSize = false,
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.TextMuted,
            Location = new Point(8, 41),
            Size = new Size(480, 20)
        };
        header.Controls.Add(subtitle);

        releasesButton = new RoundedButton
        {
            Text = "Releases GitHub",
            IconGlyph = "\uE8A7",
            IconFont = Theme.Icon(8.5f),
            IconColor = Theme.Accent,
            BackColor = Theme.Surface,
            ForeColor = Theme.Accent,
            BorderColor = Color.FromArgb(96, 65, 16),
            BorderThickness = 1,
            CornerRadius = 7,
            Font = Theme.Ui(8.5f),
            Location = new Point(546, 4),
            Size = new Size(124, 36)
        };
        header.Controls.Add(releasesButton);
        RoundedButton releasesButtonLocal = releasesButton;

        header.Resize += (_, _) =>
        {
            pill.Left = Math.Max(140, title.Right + 2);
            releasesButtonLocal.Left = Math.Max(
                330,
                header.ClientSize.Width - releasesButtonLocal.Width - 2);
            subtitle.Width = Math.Max(240, header.ClientSize.Width - 170);
        };

        return header;
    }

    private static RoundedPanel BuildPathCard(
        out RoundedTextBox pathInput,
        out RoundedButton browseButton)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 10
        };

        var folderIcon = new Label
        {
            Text = "\uE8B7",
            Font = Theme.Icon(9f),
            ForeColor = Theme.Accent,
            AutoSize = false,
            Location = new Point(14, 8),
            Size = new Size(18, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        card.Controls.Add(folderIcon);

        var label = new Label
        {
            Text = "Dossier du jeu",
            AutoSize = false,
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.TextMuted,
            Location = new Point(34, 8),
            Size = new Size(220, 18)
        };
        card.Controls.Add(label);

        pathInput = new RoundedTextBox
        {
            Location = new Point(14, 27),
            Size = new Size(500, 30)
        };
        card.Controls.Add(pathInput);

        browseButton = new RoundedButton
        {
            Text = "Parcourir...",
            IconGlyph = "\uE8B7",
            IconFont = Theme.Icon(9f),
            IconColor = Theme.TextMuted,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(34, 52, 78),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            CornerRadius = 7,
            Font = Theme.Ui(8.5f),
            Location = new Point(564, 27),
            Size = new Size(92, 30)
        };
        card.Controls.Add(browseButton);
        RoundedButton browseButtonLocal = browseButton;
        RoundedTextBox pathInputLocal = pathInput;

        card.Resize += (_, _) =>
        {
            browseButtonLocal.Left = Math.Max(
                120,
                card.ClientSize.Width - 6 - browseButtonLocal.Width);
            pathInputLocal.Width = Math.Max(
                120,
                browseButtonLocal.Left - 6 - pathInputLocal.Left);
        };

        return card;
    }

    private static RoundedPanel BuildStatusCard(
        out RoundedPanel stateBadge,
        out Label stateBadgeLabel,
        out Label installedValue,
        out Label installedFile,
        out Label latestValue,
        out Label bepInExValue)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 10,
            ShowDividers = true
        };

        var shield = new Label
        {
            Text = "\uE83D",
            Font = Theme.Icon(9f),
            ForeColor = Theme.Accent,
            AutoSize = false,
            Location = new Point(14, 10),
            Size = new Size(18, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        card.Controls.Add(shield);

        var heading = new Label
        {
            Text = "ÉTAT DE L'INSTALLATION",
            AutoSize = false,
            Font = Theme.Ui(8f, FontStyle.Bold),
            ForeColor = Theme.Accent,
            Location = new Point(34, 10),
            Size = new Size(260, 18)
        };
        card.Controls.Add(heading);

        stateBadge = new RoundedPanel
        {
            CornerRadius = 12,
            BackColor = Theme.GreenSoft,
            BorderColor = Color.FromArgb(75, 150, 105),
            BorderThickness = 1,
            Size = new Size(96, 24),
            Location = new Point(560, 9),
            Tag = "stateBadge"
        };
        stateBadgeLabel = new Label
        {
            Text = "●  Prêt",
            Dock = DockStyle.Fill,
            Font = Theme.Ui(7.5f, FontStyle.Bold),
            ForeColor = Theme.Green,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        stateBadge.Controls.Add(stateBadgeLabel);
        card.Controls.Add(stateBadge);

        Label installedHeader = MakeColumnHeader("Version installée");
        Label latestHeader = MakeColumnHeader("Dernière release");
        Label bepInExHeader = MakeColumnHeader("BepInEx");
        installedValue = MakeColumnValue();
        latestValue = MakeColumnValue();
        bepInExValue = MakeColumnValue();
        installedFile = MakeColumnNote();
        Label latestNote = MakeColumnNote();
        Label bepInExNote = MakeColumnNote();

        card.Controls.Add(installedHeader);
        card.Controls.Add(latestHeader);
        card.Controls.Add(bepInExHeader);
        card.Controls.Add(installedValue);
        card.Controls.Add(latestValue);
        card.Controls.Add(bepInExValue);
        card.Controls.Add(installedFile);
        card.Controls.Add(latestNote);
        card.Controls.Add(bepInExNote);

        card.Resize += (_, _) => LayoutStatusColumns(card);

        installedHeader.Tag = "header";
        latestHeader.Tag = "header";
        bepInExHeader.Tag = "header";
        installedValue.Tag = "value";
        latestValue.Tag = "value";
        bepInExValue.Tag = "value";
        installedFile.Tag = "note";
        latestNote.Tag = "note";
        bepInExNote.Tag = "note";
        installedFile.Text = string.Empty;
        latestNote.Text = string.Empty;
        bepInExNote.Text = string.Empty;

        LayoutStatusColumns(card);
        return card;
    }

    private static Label MakeColumnHeader(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Font = Theme.Ui(7.5f),
            ForeColor = Theme.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static Label MakeColumnValue()
    {
        return new Label
        {
            Text = "—",
            AutoSize = false,
            Font = Theme.Ui(9.5f, FontStyle.Bold),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static Label MakeColumnNote()
    {
        return new Label
        {
            Text = string.Empty,
            AutoSize = false,
            Font = Theme.Ui(7.5f),
            ForeColor = Theme.TextFaint,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static TableLayoutPanel BuildActions(
        out RoundedButton installButton,
        out RoundedButton checkButton,
        out RoundedButton localDllButton,
        out RoundedButton launchButton,
        out RoundedButton vanillaButton,
        out RoundedButton openFolderButton)
    {
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var firstRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        for (int i = 0; i < 3; i++)
        {
            firstRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        }

        installButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Installer / Mettre à jour",
            IconGlyph = "\uE896",
            IconFont = Theme.Icon(9.5f),
            IconColor = Color.FromArgb(17, 24, 39),
            BackColor = Theme.Accent,
            HoverBackColor = Color.FromArgb(251, 191, 36),
            PressedBackColor = Color.FromArgb(217, 119, 6),
            ForeColor = Color.FromArgb(17, 24, 39),
            CornerRadius = 8,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        checkButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Vérifier les mises à jour",
            IconGlyph = "\uE72C",
            IconFont = Theme.Icon(9f),
            IconColor = Theme.Text,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(34, 52, 78),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            CornerRadius = 8,
            Font = Theme.Ui(8.5f, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        localDllButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Installer un DLL local...",
            IconGlyph = "\uE7B8",
            IconFont = Theme.Icon(9f),
            IconColor = Theme.Text,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(34, 52, 78),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            CornerRadius = 8,
            Font = Theme.Ui(8.5f, FontStyle.Bold),
            Margin = Padding.Empty
        };
        firstRow.Controls.Add(installButton, 0, 0);
        firstRow.Controls.Add(checkButton, 1, 0);
        firstRow.Controls.Add(localDllButton, 2, 0);

        var secondRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0),
            Padding = Padding.Empty
        };
        secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));

        launchButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Lancer le jeu",
            IconGlyph = "\uE768",
            IconFont = Theme.Icon(8.5f),
            IconColor = Color.FromArgb(134, 239, 172),
            BackColor = Theme.GreenSoft,
            HoverBackColor = Color.FromArgb(14, 55, 52),
            PressedBackColor = Color.FromArgb(8, 35, 34),
            ForeColor = Color.FromArgb(134, 239, 172),
            CornerRadius = 8,
            Font = Theme.Ui(8.5f, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        vanillaButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Lancer en vanilla",
            IconGlyph = "\uE768",
            IconFont = Theme.Icon(8.5f),
            IconColor = Theme.TextMuted,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(34, 52, 78),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            CornerRadius = 8,
            Font = Theme.Ui(8.5f, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        openFolderButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Ouvrir BepInEx",
            IconGlyph = "\uE8B7",
            IconFont = Theme.Icon(9f),
            IconColor = Theme.TextMuted,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(34, 52, 78),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            CornerRadius = 8,
            Font = Theme.Ui(8.5f, FontStyle.Bold),
            Margin = Padding.Empty
        };
        secondRow.Controls.Add(launchButton, 0, 0);
        secondRow.Controls.Add(vanillaButton, 1, 0);
        secondRow.Controls.Add(openFolderButton, 2, 0);

        actions.Controls.Add(firstRow, 0, 0);
        actions.Controls.Add(secondRow, 0, 1);
        return actions;
    }

    private static RoundedPanel BuildJournal(out TextBox logBox, out RoundedButton clearButton)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Journal,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 10,
            Padding = new Padding(14, 0, 14, 12)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Theme.Journal,
            Margin = Padding.Empty
        };

        var terminalIcon = new Label
        {
            Text = "\uE756",
            Font = Theme.Icon(8.5f),
            ForeColor = Theme.Accent,
            AutoSize = false,
            Location = new Point(0, 8),
            Size = new Size(18, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        header.Controls.Add(terminalIcon);

        var title = new Label
        {
            Text = "JOURNAL",
            AutoSize = false,
            Font = Theme.Ui(7.5f, FontStyle.Bold),
            ForeColor = Theme.TextMuted,
            Location = new Point(23, 8),
            Size = new Size(180, 18)
        };
        header.Controls.Add(title);

        clearButton = new RoundedButton
        {
            Text = "Effacer",
            IconGlyph = "\uE74D",
            IconFont = Theme.Icon(7.5f),
            IconColor = Theme.TextFaint,
            BackColor = Theme.Journal,
            HoverBackColor = Theme.SurfaceRaised,
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.TextFaint,
            CornerRadius = 5,
            Font = Theme.Ui(7.5f),
            Size = new Size(74, 22),
            Location = new Point(0, 4)
        };
        header.Controls.Add(clearButton);
        RoundedButton clearButtonLocal = clearButton;
        header.Resize += (_, _) =>
        {
            clearButtonLocal.Left = Math.Max(
                100,
                header.ClientSize.Width - clearButtonLocal.Width);
        };

        logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.None,
            BackColor = Theme.Input,
            ForeColor = Color.FromArgb(203, 213, 225),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 8f),
            WordWrap = false,
            Margin = Padding.Empty
        };

        card.Controls.Add(logBox);
        card.Controls.Add(header);
        return card;
    }

    private static Panel BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Margin = Padding.Empty
        };

        var icon = new Label
        {
            Text = "\uE82F",
            AutoSize = true,
            Font = Theme.Icon(8f),
            ForeColor = Theme.Accent,
            BackColor = Color.Transparent
        };
        var label = new Label
        {
            Text = "Les releases GitHub sont vérifiées à l'ouverture. Ferme le jeu avant toute installation.",
            AutoSize = true,
            Font = Theme.Ui(7.5f),
            ForeColor = Theme.TextFaint,
            BackColor = Color.Transparent
        };
        footer.Controls.Add(icon);
        footer.Controls.Add(label);

        footer.Resize += (_, _) =>
        {
            int totalWidth = icon.Width + 6 + label.Width;
            int left = Math.Max(0, (footer.ClientSize.Width - totalWidth) / 2);
            icon.Location = new Point(left, (footer.ClientSize.Height - icon.Height) / 2);
            label.Location = new Point(icon.Right + 6, (footer.ClientSize.Height - label.Height) / 2);
        };

        return footer;
    }

    private void LayoutTitleBar()
    {
        if (_titleBar is null || _appTitle is null || _versionBadge is null)
        {
            return;
        }

        _versionBadge.Left = _appTitle.Right + 9;

        var buttons = _titleBar.Controls
            .OfType<RoundedButton>()
            .OrderBy(button => (int)(button.Tag ?? 0))
            .ToList();

        int x = _titleBar.ClientSize.Width - 8;
        for (int i = buttons.Count - 1; i >= 0; i--)
        {
            x -= buttons[i].Width;
            buttons[i].Left = x;
            buttons[i].Top = 5;
            x -= 4;
        }
    }

    private void LayoutStatusColumns()
    {
        LayoutStatusColumns(_statusCard);
    }

    private static void LayoutStatusColumns(RoundedPanel? card)
    {
        if (card is null)
        {
            return;
        }

        int padding = 14;
        int width = card.ClientSize.Width;
        int[] xs =
        {
            padding,
            Math.Max(padding + 100, width * 35 / 100),
            Math.Max(padding + 200, width * 69 / 100)
        };
        int[] widths =
        {
            Math.Max(80, xs[1] - xs[0] - 10),
            Math.Max(80, xs[2] - xs[1] - 10),
            Math.Max(80, width - xs[2] - padding)
        };

        var headers = card.Controls.OfType<Label>()
            .Where(label => Equals(label.Tag, "header"))
            .ToList();
        var values = card.Controls.OfType<Label>()
            .Where(label => Equals(label.Tag, "value"))
            .ToList();
        var notes = card.Controls.OfType<Label>()
            .Where(label => Equals(label.Tag, "note"))
            .ToList();

        for (int i = 0; i < 3; i++)
        {
            if (i < headers.Count)
            {
                headers[i].Location = new Point(xs[i], 36);
                headers[i].Size = new Size(widths[i], 16);
            }

            if (i < values.Count)
            {
                values[i].Location = new Point(xs[i], 52);
                values[i].Size = new Size(widths[i], 20);
            }

            if (i < notes.Count)
            {
                notes[i].Location = new Point(xs[i], 71);
                notes[i].Size = new Size(widths[i], 15);
            }
        }

        foreach (RoundedPanel badge in card.Controls.OfType<RoundedPanel>())
        {
            if (Equals(badge.Tag, "stateBadge"))
            {
                badge.Left = card.ClientSize.Width - 6 - badge.Width;
                badge.Top = 9;
            }
        }
    }

    private void LoadSavedGamePath()
    {
        LauncherSettings settings = SettingsStore.Load();
        string? detected = GameLocator.FindGamePath(settings.GamePath);
        _pathInput.Value = detected ?? settings.GamePath ?? string.Empty;
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        UpdateWindowRegion();
        await RunOperationAsync(
            "Recherche du jeu et vérification des releases...",
            () => RefreshStatusAsync(checkRelease: true));
    }

    private async void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Sélectionne le dossier de Supermarket Together (celui qui contient le .exe).",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _pathInput.Value = dialog.SelectedPath;
        await RunOperationAsync(
            "Vérification du dossier sélectionné...",
            () => RefreshStatusAsync(checkRelease: true));
    }

    private async void CheckButton_Click(object? sender, EventArgs e)
    {
        await RunOperationAsync(
            "Vérification des mises à jour...",
            () => RefreshStatusAsync(checkRelease: true));
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        await RunOperationAsync("Installation du mod...", InstallLatestReleaseAsync);
    }

    private async void LocalDllButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choisir le DLL du mod",
            Filter = "Plugin BepInEx (*.dll)|*.dll|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string selectedDll = dialog.FileName;
        await RunOperationAsync(
            "Installation depuis un DLL local...",
            async () =>
            {
                string gamePath = RequireGamePath();
                var progress = new Progress<string>(Log);
                await ModService.EnsureBepInExAsync(gamePath, progress, _lifetime.Token);
                await ModService.InstallFromDllAsync(
                    gamePath,
                    selectedDll,
                    progress,
                    _lifetime.Token);
                await RefreshStatusAsync(checkRelease: false);
            });
    }

    private void LaunchButton_Click(object? sender, EventArgs e)
    {
        LaunchGame(withoutMods: false);
    }

    private void VanillaButton_Click(object? sender, EventArgs e)
    {
        LaunchGame(withoutMods: true);
    }

    private void LaunchGame(bool withoutMods)
    {
        try
        {
            string gamePath = RequireGamePath();

            if (GameLocator.IsGameRunning())
            {
                Log("Supermarket Together est déjà lancé.");
                return;
            }

            GameLocator.LaunchGame(gamePath, withoutMods);
            Log(withoutMods
                ? "Lancement de Supermarket Together en vanilla (BepInEx désactivé)..."
                : "Lancement de Supermarket Together...");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OpenFolderButton_Click(object? sender, EventArgs e)
    {
        try
        {
            string gamePath = RequireGamePath();
            string plugins = GameLocator.GetPluginsPath(gamePath);
            Directory.CreateDirectory(plugins);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{plugins}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task InstallLatestReleaseAsync()
    {
        string gamePath = RequireGamePath();

        if (GameLocator.IsGameRunning())
        {
            throw new InvalidOperationException(
                "Ferme Supermarket Together avant d'installer ou de mettre à jour le mod.");
        }

        ReleaseInfo? release;
        try
        {
            release = await _gitHub.GetLatestReleaseAsync(_lifetime.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Impossible de récupérer la dernière release GitHub : " + ex.Message,
                ex);
        }

        if (release is null)
        {
            throw new InvalidOperationException(
                "Aucune release GitHub n'est publiée pour le moment.\n\n" +
                "Tu peux utiliser « Installer un DLL local... » avec un build du mod, " +
                "ou publier une release sur GitHub.");
        }

        _latestRelease = release;
        SetState("Installation", Theme.Blue);

        await ModService.EnsureBepInExAsync(
            gamePath,
            new Progress<string>(Log),
            _lifetime.Token);

        InstalledMod installed = await ModService.InstallFromReleaseAsync(
            gamePath,
            release,
            new Progress<string>(Log),
            _lifetime.Token);

        Log($"Release {release.TagName} installée : {installed.FileName} ({installed.Version}).");
        await RefreshStatusAsync(checkRelease: false);
    }

    private async Task RefreshStatusAsync(bool checkRelease)
    {
        if (IsDisposed)
        {
            return;
        }

        string? gamePath = GameLocator.FindGamePath(_pathInput.Value);

        if (gamePath is null)
        {
            _installedMod = null;
            _installedValue.Text = "—";
            _installedFile.Text = "Jeu non détecté";
            _latestValue.Text = "—";
            _bepInExValue.Text = "—";
            _bepInExValue.ForeColor = Theme.TextMuted;
            SetState("Jeu introuvable", Theme.Accent);
            return;
        }

        _pathInput.Value = gamePath;
        SettingsStore.Save(new LauncherSettings { GamePath = gamePath });

        _installedMod = ModService.GetInstalledMod(gamePath);
        _installedValue.Text = _installedMod?.Version ?? "aucune";
        _installedFile.Text = _installedMod is null
            ? "Aucun DLL dans BepInEx\\plugins"
            : $"({_installedMod.FileName})";

        bool bepInExInstalled = File.Exists(
            Path.Combine(gamePath, "BepInEx", "core", "BepInEx.dll"));
        _bepInExValue.Text = bepInExInstalled
            ? $"● Installé ({ModService.BepInExVersion})"
            : "● Absent";
        _bepInExValue.ForeColor = bepInExInstalled ? Theme.Green : Theme.TextMuted;

        if (!checkRelease)
        {
            UpdateLatestValue();
            UpdateInstallationState();
            return;
        }

        _latestValue.Text = "vérification...";
        _latestValue.ForeColor = Theme.TextMuted;
        SetState("Vérification", Theme.Blue);
        Log("Connexion à l'API GitHub (repository ItemAdder)...");

        try
        {
            _latestRelease = await _gitHub.GetLatestReleaseAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _latestRelease = null;
            _latestValue.Text = "erreur";
            _latestValue.ForeColor = Theme.Red;
            SetState("Erreur GitHub", Theme.Red);
            Log("Erreur GitHub : " + ex.Message);
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        UpdateLatestValue();
        UpdateInstallationState();
    }

    private void UpdateLatestValue()
    {
        if (_latestRelease is null)
        {
            _latestValue.Text = "aucune";
            _latestValue.ForeColor = Theme.TextMuted;
            Log("Aucune release GitHub disponible pour le moment.");
            return;
        }

        _latestValue.Text = _latestRelease.TagName;
        _latestValue.ForeColor = Theme.Text;
        Log($"Dernière release vérifiée : {_latestRelease.TagName}.");
    }

    private void UpdateInstallationState()
    {
        if (_latestRelease is null)
        {
            SetState(
                _installedMod is null ? "Aucune release" : "Prêt",
                _installedMod is null ? Theme.Accent : Theme.Green);
            return;
        }

        Version? latestVersion = VersionHelper.Parse(_latestRelease.TagName);

        if (_installedMod is null)
        {
            SetState("Prêt", Theme.Blue);
            return;
        }

        if (_installedMod.ParsedVersion is null || latestVersion is null)
        {
            SetState("À vérifier", Theme.Accent);
            return;
        }

        SetState(
            _installedMod.ParsedVersion < latestVersion ? "Mise à jour" : "Prêt",
            _installedMod.ParsedVersion < latestVersion ? Theme.Accent : Theme.Green);
    }

    private void SetState(string message, Color color)
    {
        if (IsDisposed)
        {
            return;
        }

        _stateBadgeLabel.Text = "●  " + message;
        _stateBadgeLabel.ForeColor = color;
        int badgeWidth = TextRenderer.MeasureText(
            _stateBadgeLabel.Text,
            _stateBadgeLabel.Font,
            Size.Empty,
            TextFormatFlags.NoPadding).Width + 24;
        _stateBadge.Width = Math.Clamp(badgeWidth, 76, 150);
        _stateBadge.Left = _statusCard.ClientSize.Width - 6 - _stateBadge.Width;
        _stateBadge.BackColor = color == Theme.Green
            ? Theme.GreenSoft
            : color == Theme.Red
                ? Theme.RedSoft
                : color == Theme.Blue
                    ? Theme.BlueSoft
                    : color == Theme.TextMuted
                        ? Theme.SurfaceRaised
                        : Theme.AccentSoft;
        _stateBadge.BorderColor = color == Theme.TextMuted
            ? Theme.BorderSoft
            : Color.FromArgb(95, color);
    }

    private string RequireGamePath()
    {
        string? gamePath = GameLocator.FindGamePath(_pathInput.Value);
        if (gamePath is null || !GameLocator.IsValidGamePath(gamePath))
        {
            throw new InvalidOperationException(
                "Sélectionne le dossier qui contient Supermarket Together.exe.");
        }

        _pathInput.Value = gamePath;
        SettingsStore.Save(new LauncherSettings { GamePath = gamePath });
        return gamePath;
    }

    private async Task RunOperationAsync(string title, Func<Task> operation)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        SetState("En cours", Theme.Blue);
        Log(title);

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            SetState("Annulé", Theme.TextMuted);
            Log("Opération annulée.");
        }
        catch (Exception ex)
        {
            SetState("Erreur", Theme.Red);
            Log("Erreur : " + ex.Message);
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        if (IsDisposed)
        {
            return;
        }

        _busy = busy;
        _pathInput.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _installButton.Enabled = !busy;
        _checkButton.Enabled = !busy;
        _localDllButton.Enabled = !busy;
        _launchButton.Enabled = !busy;
        _vanillaButton.Enabled = !busy;
        _openFolderButton.Enabled = !busy;
        _clearLogButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void Log(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_logBox.TextLength > 0)
        {
            _logBox.AppendText(Environment.NewLine);
        }

        _logBox.AppendText(line);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void ShowError(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        MessageBox.Show(
            this,
            message,
            "ItemAdder Launcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ToggleMaximize()
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        UpdateWindowRegion();
    }

    private void UpdateWindowRegion()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        if (WindowState == FormWindowState.Maximized || WindowState == FormWindowState.Minimized)
        {
            Region = null;
            return;
        }

        using var path = new GraphicsPath();
        var rect = new Rectangle(0, 0, Width, Height);
        const int radius = 12;
        float diameter = radius * 2f;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    private void EnableDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        };

        control.DoubleClick += (_, _) => ToggleMaximize();
    }

    private static string GetLauncherVersion()
    {
        Version? version = typeof(MainForm).Assembly.GetName().Version;
        return version is null
            ? "v1.2.0"
            : $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Le lien reste disponible dans le README si l'ouverture échoue.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
