using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ItemAdderLauncher.Core;
using ItemAdderLauncher.UI;

namespace ItemAdderLauncher;

internal enum LauncherPage
{
    Home,
    News,
    Journal
}

public sealed class MainForm : Form
{
    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const int SidebarWidth = 214;
    private const int TitleBarHeight = 40;
    private const int NormalWidth = 1120;
    private const int NormalHeight = 694;

    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    private readonly GitHubService _gitHub = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ToolTip _toolTip = new();
    private readonly Image? _backgroundArt;
    private readonly Image? _logoArt;

    private Panel _titleBar = null!;
    private Panel _sidebar = null!;
    private BackdropPanel _contentHost = null!;
    private Label _appTitle = null!;
    private RoundedPanel _versionBadge = null!;
    private RoundedButton _maximizeButton = null!;

    private Panel _homePage = null!;
    private Panel _newsPage = null!;
    private Panel _journalPage = null!;

    private RoundedButton _homeNav = null!;
    private RoundedButton _newsNav = null!;
    private RoundedButton _journalNav = null!;
    private RoundedButton _installSelfButton = null!;
    private RoundedButton _releasesButton = null!;

    private RoundedTextBox _pathInput = null!;
    private RoundedButton _browseButton = null!;
    private RoundedPanel _statusCard = null!;
    private RoundedPanel _stateBadge = null!;
    private Label _stateBadgeLabel = null!;
    private Label _installedValue = null!;
    private Label _installedFile = null!;
    private Label _latestValue = null!;
    private Label _bepInExValue = null!;
    private RoundedButton _installButton = null!;
    private RoundedButton _checkButton = null!;
    private RoundedButton _localDllButton = null!;
    private RoundedButton _launchButton = null!;
    private RoundedButton _vanillaButton = null!;
    private RoundedButton _openFolderButton = null!;
    private CheckBox _tobaccoCheck = null!;
    private CheckBox _undergroundCheck = null!;
    private CheckBox _restockCheck = null!;

    private RoundedPanel _teaserCard = null!;
    private Label _teaserTitle = null!;
    private Label _teaserMeta = null!;

    private FlowLayoutPanel _newsFlow = null!;
    private Label _newsStatus = null!;
    private RoundedButton _newsRefreshButton = null!;
    private readonly System.Windows.Forms.Timer _newsLayoutTimer;

    private TextBox _logBox = null!;
    private RoundedButton _clearLogButton = null!;

    private IReadOnlyList<ReleaseInfo> _releases = Array.Empty<ReleaseInfo>();
    private bool _newsLoaded;
    private bool _newsLoading;
    private int _newsRenderedWidth;

    private LauncherPage _page = LauncherPage.Home;
    private bool _busy;
    private bool _autoSetupRan;
    private bool _suppressModuleEvents;
    private bool _launcherUpdateChecked;
    private InstalledMod? _installedMod;
    private ReleaseInfo? _latestRelease;

    public MainForm()
    {
        _backgroundArt = LoadEmbeddedImage("ItemAdder.background.jpg");
        _logoArt = LoadEmbeddedImage("ItemAdder.logo.png");

        Text = "ItemAdder Launcher";
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(NormalWidth, NormalHeight);
        MinimumSize = new Size(1000, 660);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Ui(9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        KeyPreview = true;
        Icon = LoadAppIcon();

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _newsLayoutTimer = new System.Windows.Forms.Timer { Interval = 220 };
        _newsLayoutTimer.Tick += NewsLayoutTimer_Tick;

        BuildTitleBar();
        BuildSidebar();

        _contentHost = new BackdropPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(26, 14, 26, 18),
            Artwork = _backgroundArt
        };

        _homePage = BuildHomePage();
        _newsPage = BuildNewsPage();
        _journalPage = BuildJournalPage();

        foreach (Panel page in new[] { _homePage, _newsPage, _journalPage })
        {
            page.Dock = DockStyle.Fill;
            page.BackColor = Color.Transparent;
            page.Visible = false;
            _contentHost.Controls.Add(page);
        }

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 13, 20),
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(_sidebar, 0, 0);
        body.Controls.Add(_contentHost, 1, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_titleBar, 0, 0);
        root.Controls.Add(body, 0, 1);
        Controls.Add(root);

        WireEvents();
        LoadSavedGamePath();
        UpdateInstallButton();
        LauncherInstaller.RefreshRegistration();
        ShowPage(LauncherPage.Home);

        Shown += MainForm_Shown;
        FormClosing += (_, _) =>
        {
            _newsLayoutTimer.Stop();
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
        LayoutStatusColumns(_statusCard);
    }

    // ---------------------------------------------------------------- chrome

    private void BuildTitleBar()
    {
        _titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 13, 20),
            Margin = Padding.Empty
        };

        var logo = new PictureBox
        {
            Image = _logoArt,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(20, 20),
            Location = new Point(14, 10),
            BackColor = Color.Transparent
        };
        _titleBar.Controls.Add(logo);

        _appTitle = new Label
        {
            Text = "ItemAdder Launcher",
            AutoSize = true,
            Font = Theme.Ui(9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            BackColor = Color.Transparent,
            Location = new Point(42, 11)
        };
        _titleBar.Controls.Add(_appTitle);

        _versionBadge = new RoundedPanel
        {
            CornerRadius = 5,
            BackColor = Color.FromArgb(20, 28, 40),
            BorderColor = Theme.BorderSoft,
            BorderThickness = 1,
            Size = new Size(54, 18),
            Location = new Point(166, 10)
        };
        _versionBadge.Controls.Add(new Label
        {
            Text = GetLauncherVersion(),
            Dock = DockStyle.Fill,
            Font = Theme.Ui(7.5f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        });
        _titleBar.Controls.Add(_versionBadge);

        RoundedButton minimize = CreateTitleButton("\uE921", 0);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _titleBar.Controls.Add(minimize);

        _maximizeButton = CreateTitleButton("\uE922", 1);
        _maximizeButton.Click += (_, _) => ToggleMaximize();
        _titleBar.Controls.Add(_maximizeButton);

        RoundedButton close = CreateTitleButton("\uE8BB", 2);
        close.HoverBackColor = Color.FromArgb(127, 29, 29);
        close.PressedBackColor = Color.FromArgb(153, 27, 27);
        close.Click += (_, _) => Close();
        _titleBar.Controls.Add(close);

        var hairline = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Color.FromArgb(20, 29, 43)
        };
        _titleBar.Controls.Add(hairline);

        _titleBar.Resize += (_, _) => LayoutTitleBar();
        foreach (Control control in new Control[] { _titleBar, _appTitle, logo, _versionBadge })
        {
            EnableDrag(control);
        }
    }

    private static RoundedButton CreateTitleButton(string glyph, int slot)
    {
        return new RoundedButton
        {
            Text = string.Empty,
            IconGlyph = glyph,
            IconFont = Theme.Icon(9f),
            IconColor = Color.FromArgb(148, 163, 184),
            BackColor = Color.FromArgb(9, 13, 20),
            HoverBackColor = Color.FromArgb(24, 34, 48),
            PressedBackColor = Color.FromArgb(31, 44, 62),
            CornerRadius = 5,
            Size = new Size(30, 26),
            TabStop = false,
            Tag = slot
        };
    }

    private void BuildSidebar()
    {
        _sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 13, 20),
            Margin = Padding.Empty
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 13, 20),
            ColumnCount = 1,
            RowCount = 9,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(9, 13, 20) };
        brand.Controls.Add(new PictureBox
        {
            Image = _logoArt,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(42, 42),
            Location = new Point(18, 20),
            BackColor = Color.Transparent
        });
        brand.Controls.Add(new Label
        {
            Text = "ItemAdder",
            AutoSize = true,
            Font = Theme.Ui(13f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(70, 22)
        });
        brand.Controls.Add(new Label
        {
            Text = "Launcher " + GetLauncherVersion(),
            AutoSize = true,
            Font = Theme.Ui(8f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(71, 44)
        });
        layout.Controls.Add(brand, 0, 0);

        var navCaption = new Label
        {
            Text = "NAVIGATION",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = Theme.Ui(7.5f, FontStyle.Bold),
            ForeColor = Theme.TextFaint,
            BackColor = Color.FromArgb(9, 13, 20),
            Padding = new Padding(20, 0, 0, 4)
        };
        layout.Controls.Add(navCaption, 0, 1);

        _homeNav = CreateNavButton("Accueil", "\uE80F");
        _newsNav = CreateNavButton("Actualités", "\uE8A5");
        _journalNav = CreateNavButton("Journal", "\uE81C");
        layout.Controls.Add(_homeNav, 0, 2);
        layout.Controls.Add(_newsNav, 0, 3);
        layout.Controls.Add(_journalNav, 0, 4);

        _installSelfButton = new RoundedButton
        {
            Text = "Installer sur le PC",
            IconGlyph = "\uE896",
            IconFont = Theme.Icon(10f),
            IconColor = Theme.Accent,
            BackColor = Color.FromArgb(18, 25, 37),
            HoverBackColor = Color.FromArgb(26, 36, 51),
            PressedBackColor = Color.FromArgb(31, 44, 62),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 9,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            Margin = new Padding(16, 0, 16, 8)
        };
        layout.Controls.Add(_installSelfButton, 0, 6);

        _releasesButton = new RoundedButton
        {
            Text = "Releases GitHub",
            IconGlyph = "\uE8A7",
            IconFont = Theme.Icon(10f),
            IconColor = Theme.TextMuted,
            BackColor = Color.FromArgb(9, 13, 20),
            HoverBackColor = Color.FromArgb(20, 29, 43),
            PressedBackColor = Color.FromArgb(26, 37, 54),
            ForeColor = Theme.TextMuted,
            CornerRadius = 9,
            Font = Theme.Ui(8.6f),
            Dock = DockStyle.Fill,
            Margin = new Padding(16, 0, 16, 6)
        };
        layout.Controls.Add(_releasesButton, 0, 7);

        _sidebar.Controls.Add(layout);
    }

    private static Label MakeCardIcon(string glyph, int top)
    {
        return new Label
        {
            Text = glyph,
            Font = Theme.Icon(10f),
            ForeColor = Theme.Accent,
            AutoSize = false,
            Location = new Point(15, top),
            Size = new Size(20, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
    }

    private static RoundedButton CreateNavButton(string text, string glyph)
    {
        return new RoundedButton
        {
            Text = text,
            IconGlyph = glyph,
            IconFont = Theme.Icon(10f),
            IconColor = Theme.TextMuted,
            BackColor = Color.FromArgb(9, 13, 20),
            HoverBackColor = Color.FromArgb(18, 26, 38),
            PressedBackColor = Color.FromArgb(24, 34, 49),
            SelectedBackColor = Color.FromArgb(22, 32, 47),
            ForeColor = Theme.TextMuted,
            CornerRadius = 9,
            Font = Theme.Ui(9f),
            TextAlign = ContentAlignment.MiddleLeft,
            IconGap = 11,
            ContentPadding = 16,
            ShowSelectionBar = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(16, 0, 16, 6)
        };
    }

    /// <summary>
    /// Ligne de boutons avec de vrais écarts : les colonnes de séparation
    /// absolues évitent que les marges ne fassent déborder la dernière colonne
    /// hors de la fenêtre.
    /// </summary>
    private static TableLayoutPanel BuildButtonRow(int count, int gap, float[]? fixedWidths = null)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = (count * 2) - 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        for (int i = 0; i < row.ColumnCount; i++)
        {
            if (i % 2 == 1)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, gap));
                continue;
            }

            int index = i / 2;
            float fixedWidth = fixedWidths is not null && index < fixedWidths.Length
                ? fixedWidths[index]
                : 0f;
            row.ColumnStyles.Add(fixedWidth > 0
                ? new ColumnStyle(SizeType.Absolute, fixedWidth)
                : new ColumnStyle(SizeType.Percent, 100f));
        }

        return row;
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
            buttons[i].Top = 6;
            x -= 4;
        }
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
        const int radius = 14;
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

    private void ToggleMaximize()
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        UpdateWindowRegion();
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

    // ------------------------------------------------------------ navigation

    private void ShowPage(LauncherPage page)
    {
        _page = page;
        _homePage.Visible = page == LauncherPage.Home;
        _newsPage.Visible = page == LauncherPage.News;
        _journalPage.Visible = page == LauncherPage.Journal;

        UpdateNavState();

        if (page == LauncherPage.News)
        {
            _ = ShowNewsAsync();
        }
    }

    private void UpdateNavState()
    {
        (_homeNav.Selected, _homeNav.ForeColor, _homeNav.IconColor) = NavStyle(LauncherPage.Home);
        (_newsNav.Selected, _newsNav.ForeColor, _newsNav.IconColor) = NavStyle(LauncherPage.News);
        (_journalNav.Selected, _journalNav.ForeColor, _journalNav.IconColor) = NavStyle(LauncherPage.Journal);

        _homeNav.Invalidate();
        _newsNav.Invalidate();
        _journalNav.Invalidate();
    }

    private (bool Selected, Color Text, Color Icon) NavStyle(LauncherPage page)
    {
        return _page == page
            ? (true, Theme.Text, Theme.Accent)
            : (false, Theme.TextMuted, Theme.TextMuted);
    }

    // ----------------------------------------------------------- page: home

    private Panel BuildHomePage()
    {
        var page = new Panel { BackColor = Color.Transparent, Padding = Padding.Empty };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 8,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        header.Controls.Add(new Label
        {
            Text = "Tableau de bord",
            AutoSize = true,
            Font = Theme.Ui(18f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(2, 2)
        });
        header.Controls.Add(new Label
        {
            Text = "Supermarket Together · mod ItemAdder, BepInEx et sous-sol clandestin",
            AutoSize = true,
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(3, 32)
        });
        layout.Controls.Add(header, 0, 0);

        var pathCard = BuildPathCard(out _pathInput, out _browseButton);
        pathCard.Dock = DockStyle.Fill;
        pathCard.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(pathCard, 0, 1);

        _statusCard = BuildStatusCard(
            out _stateBadge,
            out _stateBadgeLabel,
            out _installedValue,
            out _installedFile,
            out _latestValue,
            out _bepInExValue);
        _statusCard.Dock = DockStyle.Fill;
        _statusCard.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_statusCard, 0, 2);

        var actions = BuildActions(
            out _installButton,
            out _checkButton,
            out _localDllButton);
        actions.Dock = DockStyle.Fill;
        actions.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(actions, 0, 3);

        var modules = BuildModulesCard(
            out _tobaccoCheck,
            out _undergroundCheck,
            out _restockCheck);
        modules.Dock = DockStyle.Fill;
        modules.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(modules, 0, 4);

        _teaserCard = BuildTeaserCard(out _teaserTitle, out _teaserMeta);
        _teaserCard.Dock = DockStyle.Fill;
        _teaserCard.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_teaserCard, 0, 5);

        var spacer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        layout.Controls.Add(spacer, 0, 6);

        var launchRow = BuildLaunchRow(out _launchButton, out _vanillaButton, out _openFolderButton);
        layout.Controls.Add(launchRow, 0, 7);

        page.Controls.Add(layout);
        return page;
    }

    private static RoundedPanel BuildPathCard(
        out RoundedTextBox pathInput,
        out RoundedButton browseButton)
    {
        var card = new RoundedPanel
        {
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 12
        };

        card.Controls.Add(MakeCardIcon("\uE8B7", 9));

        card.Controls.Add(new Label
        {
            Text = "DOSSIER DU JEU",
            AutoSize = false,
            Font = Theme.Ui(7.8f, FontStyle.Bold),
            ForeColor = Theme.Accent,
            Location = new Point(38, 9),
            Size = new Size(220, 18),
            BackColor = Color.Transparent
        });

        pathInput = new RoundedTextBox
        {
            Location = new Point(16, 32),
            Size = new Size(500, 30)
        };
        card.Controls.Add(pathInput);

        browseButton = new RoundedButton
        {
            Text = "Parcourir",
            IconGlyph = "\uE8B7",
            IconFont = Theme.Icon(10f),
            IconColor = Color.FromArgb(203, 213, 225),
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 9,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Location = new Point(530, 32),
            Size = new Size(104, 30)
        };
        card.Controls.Add(browseButton);

        RoundedButton browseLocal = browseButton;
        RoundedTextBox inputLocal = pathInput;
        card.Resize += (_, _) =>
        {
            browseLocal.Left = Math.Max(140, card.ClientSize.Width - 8 - browseLocal.Width);
            inputLocal.Width = Math.Max(120, browseLocal.Left - 10 - inputLocal.Left);
        };

        return card;
    }

    private RoundedPanel BuildStatusCard(
        out RoundedPanel stateBadge,
        out Label stateBadgeLabel,
        out Label installedValue,
        out Label installedFile,
        out Label latestValue,
        out Label bepInExValue)
    {
        var card = new RoundedPanel
        {
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 12,
            ShowDividers = true
        };

        card.Controls.Add(MakeCardIcon("\uE83D", 12));

        card.Controls.Add(new Label
        {
            Text = "ÉTAT DE L'INSTALLATION",
            AutoSize = false,
            Font = Theme.Ui(7.8f, FontStyle.Bold),
            ForeColor = Theme.Accent,
            Location = new Point(38, 12),
            Size = new Size(260, 18),
            BackColor = Color.Transparent
        });

        stateBadge = new RoundedPanel
        {
            CornerRadius = 12,
            BackColor = Theme.GreenSoft,
            BorderColor = Color.FromArgb(75, 150, 105),
            BorderThickness = 1,
            Size = new Size(96, 24),
            Location = new Point(560, 10),
            Tag = "stateBadge"
        };
        stateBadgeLabel = new Label
        {
            Text = "●  Prêt",
            Dock = DockStyle.Fill,
            Font = Theme.Ui(7.5f, FontStyle.Bold),
            ForeColor = Theme.Green,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            BackColor = Color.Transparent
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
        latestNote.Text = string.Empty;
        bepInExNote.Text = string.Empty;
        installedFile.Text = string.Empty;

        card.Controls.Add(installedHeader);
        card.Controls.Add(latestHeader);
        card.Controls.Add(bepInExHeader);
        card.Controls.Add(installedValue);
        card.Controls.Add(latestValue);
        card.Controls.Add(bepInExValue);
        card.Controls.Add(installedFile);
        card.Controls.Add(latestNote);
        card.Controls.Add(bepInExNote);

        installedHeader.Tag = "header";
        latestHeader.Tag = "header";
        bepInExHeader.Tag = "header";
        installedValue.Tag = "value";
        latestValue.Tag = "value";
        bepInExValue.Tag = "value";
        installedFile.Tag = "note";
        latestNote.Tag = "note";
        bepInExNote.Tag = "note";

        card.Resize += (_, _) => LayoutStatusColumns(card);
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
            AutoEllipsis = true,
            BackColor = Color.Transparent
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
            AutoEllipsis = true,
            BackColor = Color.Transparent
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
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
    }

    private static void LayoutStatusColumns(RoundedPanel? card)
    {
        if (card is null)
        {
            return;
        }

        int padding = 16;
        int width = card.ClientSize.Width;
        int[] xs =
        {
            padding,
            Math.Max(padding + 140, width * 33 / 100),
            Math.Max(padding + 280, width * 66 / 100)
        };
        int[] widths =
        {
            Math.Max(80, xs[1] - xs[0] - 12),
            Math.Max(80, xs[2] - xs[1] - 12),
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
                headers[i].Location = new Point(xs[i], 40);
                headers[i].Size = new Size(widths[i], 16);
            }

            if (i < values.Count)
            {
                values[i].Location = new Point(xs[i], 57);
                values[i].Size = new Size(widths[i], 20);
            }

            if (i < notes.Count)
            {
                notes[i].Location = new Point(xs[i], 78);
                notes[i].Size = new Size(widths[i], 15);
            }
        }

        foreach (RoundedPanel badge in card.Controls.OfType<RoundedPanel>())
        {
            if (Equals(badge.Tag, "stateBadge"))
            {
                badge.Left = card.ClientSize.Width - 8 - badge.Width;
                badge.Top = 10;
            }
        }
    }

    private static TableLayoutPanel BuildActions(
        out RoundedButton installButton,
        out RoundedButton checkButton,
        out RoundedButton localDllButton)
    {
        var firstRow = BuildButtonRow(3, 10);

        installButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Installer / Mettre à jour",
            IconGlyph = "\uE896",
            IconFont = Theme.Icon(10f),
            IconColor = Color.FromArgb(38, 26, 4),
            BackColor = Theme.Accent,
            HoverBackColor = Color.FromArgb(251, 191, 36),
            PressedBackColor = Color.FromArgb(217, 119, 6),
            ForeColor = Color.FromArgb(30, 20, 2),
            BorderColor = Color.FromArgb(196, 122, 5),
            BorderThickness = 1,
            CornerRadius = 11,
            Sheen = 0.2f,
            Font = Theme.Ui(9.2f, FontStyle.Bold),
            Margin = Padding.Empty
        };
        checkButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Vérifier les mises à jour",
            IconGlyph = "\uE72C",
            IconFont = Theme.Icon(10f),
            IconColor = Color.FromArgb(203, 213, 225),
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 11,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Margin = Padding.Empty
        };
        localDllButton = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Installer un DLL",
            IconGlyph = "\uE8E5",
            IconFont = Theme.Icon(10f),
            IconColor = Color.FromArgb(203, 213, 225),
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 11,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Margin = Padding.Empty
        };
        firstRow.Controls.Add(installButton, 0, 0);
        firstRow.Controls.Add(checkButton, 2, 0);
        firstRow.Controls.Add(localDllButton, 4, 0);

        return firstRow;
    }

    private static RoundedPanel BuildModulesCard(
        out CheckBox tobacco,
        out CheckBox undergroundToCheck,
        out CheckBox restock)
    {
        var card = new RoundedPanel
        {
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 12
        };

        card.Controls.Add(MakeCardIcon("\uE713", 10));

        card.Controls.Add(new Label
        {
            Text = "MODULES AU LANCEMENT",
            AutoSize = false,
            Font = Theme.Ui(7.8f, FontStyle.Bold),
            ForeColor = Theme.Accent,
            Location = new Point(38, 10),
            Size = new Size(260, 18),
            BackColor = Color.Transparent
        });

        var hint = new Label
        {
            Text = "Appliqué au prochain lancement",
            AutoSize = false,
            Font = Theme.Ui(7.5f),
            ForeColor = Theme.TextFaint,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(400, 10),
            Size = new Size(256, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        card.Controls.Add(hint);

        tobacco = CreateModuleCheckBox("Rayon Tabac", 38, 190);
        undergroundToCheck = CreateModuleCheckBox("Sous-sol clandestin", 250, 210);
        restock = CreateModuleCheckBox("Réassort intelligent", 470, 220);

        card.Controls.Add(tobacco);
        card.Controls.Add(undergroundToCheck);
        card.Controls.Add(restock);
        return card;
    }

    private static CheckBox CreateModuleCheckBox(string text, int left, int width)
    {
        return new RoundedCheckBox
        {
            Text = text,
            Location = new Point(left, 38),
            Size = new Size(width, 24),
            Checked = true
        };
    }

    private RoundedPanel BuildTeaserCard(out Label title, out Label meta)
    {
        var card = new RoundedPanel
        {
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 12
        };

        card.Controls.Add(MakeCardIcon("\uE8EC", 12));

        card.Controls.Add(new Label
        {
            Text = "DERNIÈRE RELEASE",
            AutoSize = false,
            Font = Theme.Ui(7.8f, FontStyle.Bold),
            ForeColor = Theme.Accent,
            Location = new Point(38, 12),
            Size = new Size(280, 18),
            BackColor = Color.Transparent
        });

        title = new Label
        {
            Text = "Aucune release récupérée",
            AutoSize = false,
            Height = 24,
            Font = Theme.Ui(12f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoEllipsis = true,
            Location = new Point(38, 34),
            BackColor = Color.Transparent
        };
        card.Controls.Add(title);

        meta = new Label
        {
            Text = "Lance une vérification pour charger les actualités du dépôt.",
            AutoSize = false,
            Height = 18,
            Font = Theme.Ui(8f),
            ForeColor = Theme.TextMuted,
            AutoEllipsis = true,
            Location = new Point(38, 60),
            BackColor = Color.Transparent
        };
        card.Controls.Add(meta);

        var readButton = new RoundedButton
        {
            Text = "Lire les actualités",
            IconGlyph = "\uE76C",
            IconFont = Theme.Icon(9.5f),
            IconColor = Theme.Accent,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 9,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Size = new Size(172, 34)
        };
        readButton.Click += (_, _) => ShowPage(LauncherPage.News);
        card.Controls.Add(readButton);

        Label titleLocal = title;
        Label metaLocal = meta;
        card.Resize += (_, _) =>
        {
            readButton.Location = new Point(
                Math.Max(300, card.ClientSize.Width - 16 - readButton.Width),
                30);
            int available = Math.Max(180, readButton.Left - 46);
            titleLocal.Width = available;
            metaLocal.Width = available;
        };

        return card;
    }

    private TableLayoutPanel BuildLaunchRow(
        out RoundedButton launch,
        out RoundedButton vanilla,
        out RoundedButton openFolder)
    {
        var row = BuildButtonRow(3, 10, new[] { 0f, 158f, 158f });

        launch = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Lancer le jeu",
            IconGlyph = "\uE768",
            IconFont = Theme.Icon(11f),
            IconColor = Color.FromArgb(8, 40, 22),
            BackColor = Color.FromArgb(34, 197, 94),
            HoverBackColor = Color.FromArgb(74, 222, 128),
            PressedBackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.FromArgb(6, 32, 18),
            BorderColor = Color.FromArgb(21, 128, 61),
            BorderThickness = 1,
            CornerRadius = 11,
            Sheen = 0.22f,
            Font = Theme.Ui(9.6f, FontStyle.Bold),
            Margin = Padding.Empty
        };

        vanilla = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "Vanilla",
            IconGlyph = "\uE7F4",
            IconFont = Theme.Icon(10f),
            IconColor = Color.FromArgb(203, 213, 225),
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 11,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Margin = Padding.Empty
        };

        openFolder = new RoundedButton
        {
            Dock = DockStyle.Fill,
            Text = "BepInEx",
            IconGlyph = "\uE8B7",
            IconFont = Theme.Icon(10f),
            IconColor = Color.FromArgb(203, 213, 225),
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 11,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Margin = Padding.Empty
        };

        row.Controls.Add(launch, 0, 0);
        row.Controls.Add(vanilla, 2, 0);
        row.Controls.Add(openFolder, 4, 0);
        return row;
    }

    // ----------------------------------------------------------- page: news

    private Panel BuildNewsPage()
    {
        var page = new Panel { BackColor = Color.Transparent, Padding = Padding.Empty };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        header.Controls.Add(new Label
        {
            Text = "Actualités",
            AutoSize = true,
            Font = Theme.Ui(18f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(2, 2)
        });
        header.Controls.Add(new Label
        {
            Text = "Notes de version publiées sur GitHub, lues et mises en forme ici.",
            AutoSize = true,
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(3, 32)
        });

        _newsRefreshButton = new RoundedButton
        {
            Text = "Rafraîchir",
            IconGlyph = "\uE72C",
            IconFont = Theme.Icon(9.5f),
            IconColor = Theme.Text,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(36, 54, 80),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 9,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Size = new Size(122, 32),
            Location = new Point(520, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        header.Controls.Add(_newsRefreshButton);

        RoundedButton github = new RoundedButton
        {
            Text = "Ouvrir GitHub",
            IconGlyph = "\uE8A7",
            IconFont = Theme.Icon(9.5f),
            IconColor = Theme.Accent,
            BackColor = Theme.Surface,
            HoverBackColor = Color.FromArgb(30, 41, 57),
            PressedBackColor = Color.FromArgb(31, 44, 62),
            ForeColor = Theme.Accent,
            BorderColor = Color.FromArgb(96, 65, 16),
            BorderThickness = 1,
            CornerRadius = 9,
            Font = Theme.Ui(9f, FontStyle.Bold),
            Size = new Size(142, 32),
            Location = new Point(644, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        github.Click += (_, _) => OpenUrl(GitHubService.ReleasesUrl);
        header.Controls.Add(github);

        header.Resize += (_, _) =>
        {
            github.Left = Math.Max(320, header.ClientSize.Width - github.Width);
            _newsRefreshButton.Left = github.Left - _newsRefreshButton.Width - 8;
        };

        layout.Controls.Add(header, 0, 0);

        _newsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };
        _newsFlow.Resize += NewsFlow_Resize;
        layout.Controls.Add(_newsFlow, 0, 1);

        _newsStatus = new Label
        {
            Text = "Chargement des actualités...",
            AutoSize = false,
            Font = Theme.Ui(8.8f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(4, 70),
            Size = new Size(520, 22)
        };
        page.Controls.Add(layout);
        page.Controls.Add(_newsStatus);
        _newsStatus.BringToFront();

        return page;
    }

    private async Task ShowNewsAsync()
    {
        if (!_newsLoaded && !_newsLoading)
        {
            await LoadNewsAsync();
        }
    }

    private async Task LoadNewsAsync()
    {
        if (_newsLoading)
        {
            return;
        }

        _newsLoading = true;
        _newsRefreshButton.Enabled = false;
        _newsStatus.Visible = true;
        _newsStatus.Text = "Chargement des actualités GitHub...";

        try
        {
            IReadOnlyList<ReleaseInfo> releases = await _gitHub.GetReleasesAsync(6, _lifetime.Token);

            if (IsDisposed)
            {
                return;
            }

            _releases = releases;
            _newsLoaded = true;
            _newsFlow.Controls.Clear();

            if (releases.Count == 0)
            {
                _newsStatus.Visible = true;
                _newsStatus.Text = "Aucune release publiée pour le moment sur le dépôt.";
                return;
            }

            _newsStatus.Visible = false;
            BuildNewsCards();
            Log($"{releases.Count} actualité(s) chargée(s) depuis GitHub.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _newsStatus.Visible = true;
            _newsStatus.Text = "Impossible de charger les actualités : " + ex.Message;
            Log("Erreur GitHub (actualités) : " + ex.Message);
        }
        finally
        {
            _newsRefreshButton.Enabled = true;
            _newsLoading = false;
        }
    }

    private void BuildNewsCards()
    {
        if (IsDisposed)
        {
            return;
        }

        int width = Math.Max(420, _newsFlow.ClientSize.Width - 24);
        _newsRenderedWidth = _newsFlow.ClientSize.Width;

        _newsFlow.SuspendLayout();
        _newsFlow.Controls.Clear();

        foreach (ReleaseInfo release in _releases)
        {
            _newsFlow.Controls.Add(BuildNewsCard(release, width));
        }

        _newsFlow.ResumeLayout();
    }

    private Control BuildNewsCard(ReleaseInfo release, int width)
    {
        int inner = Math.Max(260, width - 40);

        var card = new RoundedPanel
        {
            Width = width,
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 12,
            Margin = new Padding(0, 0, 0, 12)
        };

        int y = 16;

        string tagText = string.IsNullOrWhiteSpace(release.TagName) ? "release" : release.TagName;
        bool showTag = !string.Equals(
            release.TagName,
            release.Title,
            StringComparison.OrdinalIgnoreCase);

        var tag = new RoundedPanel
        {
            CornerRadius = 10,
            BackColor = release.Prerelease ? Theme.BlueSoft : Theme.AccentSoft,
            BorderColor = release.Prerelease
                ? Color.FromArgb(46, 84, 128)
                : Color.FromArgb(96, 65, 16),
            BorderThickness = 1,
            Height = 22
        };
        Size tagSize = TextRenderer.MeasureText(
            tagText,
            Theme.Ui(7.5f, FontStyle.Bold),
            Size.Empty,
            TextFormatFlags.NoPadding);
        tag.Width = Math.Max(52, tagSize.Width + 22);
        tag.Location = new Point(width - 20 - tag.Width, 16);
        tag.Controls.Add(new Label
        {
            Text = tagText,
            Dock = DockStyle.Fill,
            Font = Theme.Ui(7.5f, FontStyle.Bold),
            ForeColor = release.Prerelease ? Theme.Blue : Theme.Accent,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        });

        if (showTag)
        {
            card.Controls.Add(tag);
        }

        card.Controls.Add(new Label
        {
            Text = release.Title,
            AutoSize = false,
            Height = 24,
            Width = Math.Max(140, inner - (showTag ? tag.Width + 16 : 0)),
            Location = new Point(20, y),
            Font = Theme.Ui(11.5f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoEllipsis = true,
            BackColor = Color.Transparent
        });
        y += 27;

        card.Controls.Add(new Label
        {
            Text = BuildReleaseMeta(release),
            AutoSize = false,
            Height = 18,
            Width = inner,
            Location = new Point(20, y),
            Font = Theme.Ui(8f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent
        });
        y += 25;

        var body = new WheelThroughRichTextBox
        {
            Location = new Point(20, y),
            Width = inner,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.None,
            WordWrap = true,
            DetectUrls = false,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.Ui(9f),
            Cursor = Cursors.Default,
            TabStop = false,
            ShortcutsEnabled = true
        };
        MarkdownRenderer.Render(body, release.Body);
        body.Height = Math.Max(26, MarkdownRenderer.Fit(body));
        card.Controls.Add(body);
        y += body.Height + 4;

        var link = new LinkLabel
        {
            Text = "Ouvrir sur GitHub",
            AutoSize = true,
            Location = new Point(19, y),
            Font = Theme.Ui(8.2f),
            LinkColor = Theme.Blue,
            ActiveLinkColor = Theme.Accent,
            VisitedLinkColor = Theme.Blue,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Color.Transparent
        };
        link.LinkClicked += (_, _) => OpenUrl(
            string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? GitHubService.RepositoryUrl
                : release.HtmlUrl);
        card.Controls.Add(link);
        y += link.PreferredHeight + 14;

        card.Height = y;
        return card;
    }

    private static string BuildReleaseMeta(ReleaseInfo release)
    {
        var parts = new List<string>();

        if (release.PublishedAt is DateTimeOffset published)
        {
            parts.Add(published.ToLocalTime().ToString("d MMMM yyyy", FrenchCulture));
        }

        if (!string.IsNullOrWhiteSpace(release.Author))
        {
            parts.Add("par " + release.Author);
        }

        if (release.Prerelease)
        {
            parts.Add("pré-release");
        }

        return parts.Count == 0 ? "date inconnue" : string.Join("   ·   ", parts);
    }

    private void NewsFlow_Resize(object? sender, EventArgs e)
    {
        if (_releases.Count == 0 || _page != LauncherPage.News)
        {
            return;
        }

        _newsLayoutTimer.Stop();
        _newsLayoutTimer.Start();
    }

    private void NewsLayoutTimer_Tick(object? sender, EventArgs e)
    {
        _newsLayoutTimer.Stop();

        if (IsDisposed || _releases.Count == 0 || _page != LauncherPage.News)
        {
            return;
        }

        if (Math.Abs(_newsFlow.ClientSize.Width - _newsRenderedWidth) < 12)
        {
            return;
        }

        BuildNewsCards();
    }

    // -------------------------------------------------------- page: journal

    private Panel BuildJournalPage()
    {
        var page = new Panel { BackColor = Color.Transparent, Padding = Padding.Empty };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        header.Controls.Add(new Label
        {
            Text = "Journal",
            AutoSize = true,
            Font = Theme.Ui(18f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(2, 2)
        });
        header.Controls.Add(new Label
        {
            Text = "Toutes les opérations du launcher, dans l'ordre.",
            AutoSize = true,
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(3, 32)
        });

        _clearLogButton = new RoundedButton
        {
            Text = "Effacer",
            IconGlyph = "\uE74D",
            IconFont = Theme.Icon(9.5f),
            IconColor = Theme.TextMuted,
            BackColor = Theme.SurfaceRaised,
            HoverBackColor = Color.FromArgb(34, 52, 78),
            PressedBackColor = Color.FromArgb(28, 44, 67),
            ForeColor = Theme.Text,
            CornerRadius = 8,
            Font = Theme.Ui(8.2f),
            Size = new Size(104, 32),
            Location = new Point(560, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        header.Controls.Add(_clearLogButton);
        header.Resize += (_, _) =>
        {
            _clearLogButton.Left = Math.Max(200, header.ClientSize.Width - _clearLogButton.Width);
        };

        layout.Controls.Add(header, 0, 0);

        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 17, 26),
            BorderColor = Theme.Border,
            BorderThickness = 1,
            CornerRadius = 12,
            Padding = new Padding(14, 12, 14, 12)
        };

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(12, 17, 26),
            ForeColor = Color.FromArgb(203, 213, 225),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 8.2f),
            WordWrap = false,
            Margin = Padding.Empty
        };
        card.Controls.Add(_logBox);

        layout.Controls.Add(card, 0, 1);
        page.Controls.Add(layout);

        return page;
    }

    // ---------------------------------------------------------------- events

    private void WireEvents()
    {
        _browseButton.Click += BrowseButton_Click;
        _installButton.Click += InstallButton_Click;
        _checkButton.Click += CheckButton_Click;
        _localDllButton.Click += LocalDllButton_Click;
        _launchButton.Click += async (_, _) =>
            await RunOperationAsync(
                "Préparation du lancement...",
                () => LaunchGameAsync(withoutMods: false));
        _vanillaButton.Click += async (_, _) =>
            await RunOperationAsync(
                "Préparation du lancement...",
                () => LaunchGameAsync(withoutMods: true));
        _openFolderButton.Click += OpenFolderButton_Click;
        _clearLogButton.Click += (_, _) => _logBox.Clear();
        _installSelfButton.Click += InstallSelfButton_Click;
        _releasesButton.Click += (_, _) => OpenUrl(GitHubService.ReleasesUrl);
        _newsRefreshButton.Click += async (_, _) =>
        {
            _newsLoaded = false;
            await LoadNewsAsync();
        };

        _homeNav.Click += (_, _) => ShowPage(LauncherPage.Home);
        _newsNav.Click += (_, _) => ShowPage(LauncherPage.News);
        _journalNav.Click += (_, _) => ShowPage(LauncherPage.Journal);

        _tobaccoCheck.CheckedChanged += ModuleCheckBox_CheckedChanged;
        _undergroundCheck.CheckedChanged += ModuleCheckBox_CheckedChanged;
        _restockCheck.CheckedChanged += ModuleCheckBox_CheckedChanged;

        _toolTip.SetToolTip(_browseButton, "Choisir le dossier qui contient Supermarket Together.exe");
        _toolTip.SetToolTip(_localDllButton, "Installer ou réparer le mod depuis un DLL déjà compilé");
        _toolTip.SetToolTip(_launchButton, "Lancer Supermarket Together avec BepInEx et ItemAdder");
        _toolTip.SetToolTip(_vanillaButton, "Lancer le jeu sans BepInEx ni ItemAdder");
        _toolTip.SetToolTip(_openFolderButton, "Ouvrir BepInEx\\plugins dans l'Explorateur");
        _toolTip.SetToolTip(_stateBadge, "État actuel de l'installation");
        _toolTip.SetToolTip(_newsRefreshButton, "Recharger les actualités depuis GitHub");
        _toolTip.SetToolTip(
            _tobaccoCheck,
            "Coche pour lancer avec le rayon tabac et son palier de compétences. " +
            "Garde-la cochée sur une sauvegarde qui contient déjà des produits tabac.");
        _toolTip.SetToolTip(
            _undergroundCheck,
            "Coche pour lancer avec le sous-sol clandestin (culture, police, raids). " +
            "Tous les joueurs doivent avoir le même réglage.");
        _toolTip.SetToolTip(
            _restockCheck,
            "Les employés de réassort rangent les produits restés en réserve " +
            "sur l'étagère compatible la plus proche.");
    }

    private void LoadSavedGamePath()
    {
        LauncherSettings settings = SettingsStore.Load();
        string? detected = GameLocator.FindGamePath(settings.GamePath);
        _pathInput.Value = detected ?? settings.GamePath ?? string.Empty;

        (bool Tobacco, bool Underground, bool Restock) fromConfig = detected is null
            ? (true, true, true)
            : ModConfig.Read(detected);

        _suppressModuleEvents = true;
        _tobaccoCheck.Checked = settings.Tobacco ?? fromConfig.Tobacco;
        _undergroundCheck.Checked = settings.Underground ?? fromConfig.Underground;
        _restockCheck.Checked = settings.Restock ?? fromConfig.Restock;
        _suppressModuleEvents = false;
    }

    private void ModuleCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressModuleEvents)
        {
            return;
        }

        SettingsStore.SaveModules(
            _tobaccoCheck.Checked,
            _undergroundCheck.Checked,
            _restockCheck.Checked);
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        UpdateWindowRegion();

        await RunOperationAsync(
            "Recherche du jeu et vérification des releases...",
            () => RefreshStatusAsync(checkRelease: true));

        await EnsureInstalledAsync();
        await AutoSetupAsync();
    }

    /// <summary>
    /// Installe le launcher sur le PC dès le premier démarrage, sans question :
    /// c'est ce qui rend l'application « tu télécharges, tu double-cliques, c'est prêt ».
    /// Le bouton du menu permet toujours de la retirer ensuite.
    /// </summary>
    private Task EnsureInstalledAsync()
    {
        if (LauncherInstaller.IsInstalled)
        {
            return Task.CompletedTask;
        }

        try
        {
            LauncherInstaller.Install(new Progress<string>(Log));
            UpdateInstallButton();
            Log("Launcher installé sur ce PC : icône sur le Bureau, menu Démarrer et « Applications installées ».");
        }
        catch (Exception ex)
        {
            Log("Installation du launcher impossible : " + ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Première configuration automatique : BepInEx quand il manque, puis le mod
    /// depuis la dernière release GitHub quand il est absent ou plus ancien.
    /// Aucune question : le but est qu'un joueur n'ait rien à cliquer.
    /// </summary>
    private async Task AutoSetupAsync()
    {
        if (_autoSetupRan || IsDisposed)
        {
            return;
        }

        _autoSetupRan = true;

        string? gamePath = GameLocator.FindGamePath(_pathInput.Value);
        if (gamePath is null)
        {
            Log("Installation automatique : jeu non détecté. " +
                "Utilise « Parcourir » pour indiquer le dossier de Supermarket Together.");
            return;
        }

        bool bepInExInstalled = ModService.IsBepInExInstalled(gamePath);
        Version? latest = _latestRelease is null
            ? null
            : VersionHelper.Parse(_latestRelease.TagName);
        bool needsMod = _installedMod is null;
        bool needsUpdate = _installedMod?.ParsedVersion is not null
            && latest is not null
            && _installedMod.ParsedVersion < latest;

        if (bepInExInstalled && !needsMod && !needsUpdate)
        {
            Log("Installation déjà à jour : BepInEx et le mod sont en place.");
            return;
        }

        if (GameLocator.IsGameRunning())
        {
            Log("Installation automatique reportée : ferme Supermarket Together puis relance le launcher.");
            return;
        }

        SetBusy(true);
        SetState("Installation", Theme.Blue);

        try
        {
            var progress = new Progress<string>(Log);

            if (!bepInExInstalled)
            {
                Log($"BepInEx absent : installation automatique de la version {ModService.BepInExVersion}...");
                await ModService.EnsureBepInExAsync(gamePath, progress, _lifetime.Token);
                Log("BepInEx installé.");
            }

            if (_latestRelease is not null && (needsMod || needsUpdate))
            {
                Log($"Installation automatique de la release {_latestRelease.TagName}...");
                InstalledMod installed = await ModService.InstallFromReleaseAsync(
                    gamePath,
                    _latestRelease,
                    progress,
                    _lifetime.Token);
                Log($"Mod installé automatiquement : {installed.FileName} ({installed.Version}).");
            }

            await RefreshStatusAsync(checkRelease: false);
            Log("Installation automatique terminée : le jeu est prêt à être lancé.");
        }
        catch (OperationCanceledException)
        {
            // Fermeture du launcher pendant l'installation : rien à signaler.
        }
        catch (Exception ex)
        {
            Log("Installation automatique impossible : " + ex.Message);
            SetState("Erreur", Theme.Red);
        }
        finally
        {
            SetBusy(false);
        }
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

    /// <summary>
    /// Lance le jeu sans jamais le faire avant que BepInEx et le mod soient en
    /// place. Sur une première installation, BepInEx se téléchargeait en tâche de
    /// fond : un clic sur « Lancer le jeu » pendant ce temps démarrait le jeu en
    /// vanilla (pas de trappe, pas de rayon Tabac), et rien ne le signalait.
    /// </summary>
    private async Task LaunchGameAsync(bool withoutMods)
    {
        string gamePath = RequireGamePath();

        if (GameLocator.IsGameRunning())
        {
            Log("Supermarket Together est déjà lancé : ferme-le, puis relance-le " +
                "depuis le launcher pour être sûr qu'il démarre avec le mod.");
            return;
        }

        if (!withoutMods)
        {
            var progress = new Progress<string>(Log);

            if (!ModService.IsBepInExInstalled(gamePath))
            {
                Log($"BepInEx manquant : installation avant le lancement " +
                    $"({ModService.BepInExVersion})...");
            }

            await ModService.EnsureBepInExAsync(gamePath, progress, _lifetime.Token);
            Log("BepInEx prêt.");

            if (ModService.GetInstalledMod(gamePath) is null)
            {
                if (_latestRelease is null)
                {
                    throw new InvalidOperationException(
                        "Le mod ItemAdder n'est pas installé et aucune release n'a pu être " +
                        "récupérée : le jeu démarrerait sans le mod. Vérifie ta connexion, " +
                        "puis clique à nouveau sur « Lancer le jeu ».");
                }

                Log($"Mod manquant : installation de la release {_latestRelease.TagName} " +
                    "avant le lancement...");
                InstalledMod installed = await ModService.InstallFromReleaseAsync(
                    gamePath,
                    _latestRelease,
                    progress,
                    _lifetime.Token);
                Log($"Mod installé : {installed.FileName} ({installed.Version}).");
            }

            ApplyModuleSettings(gamePath);
        }

        DateTime launchedAt = DateTime.Now.AddSeconds(-1);
        GameLocator.LaunchGame(gamePath, withoutMods);
        Log(withoutMods
            ? "Lancement de Supermarket Together en vanilla (BepInEx désactivé)..."
            : "Lancement de Supermarket Together avec ItemAdder...");
        SetState("Jeu lancé", Theme.Green);

        if (!withoutMods)
        {
            _ = VerifyInjectionAsync(gamePath, launchedAt);
        }
    }

    /// <summary>
    /// Après le lancement, vérifie que BepInEx a réellement chargé le mod et le dit
    /// dans le journal. Sans ce contrôle, un jeu démarré sans injection (Steam
    /// pendant l'installation, BepInEx incomplet...) ne se voit qu'en jeu, par une
    /// étagère vide.
    /// </summary>
    private async Task VerifyInjectionAsync(string gamePath, DateTime launchedAt)
    {
        string logPath = Path.Combine(gamePath, "BepInEx", "LogOutput.log");

        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                await Task.Delay(1500, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (IsDisposed || _lifetime.IsCancellationRequested)
            {
                return;
            }

            string? text = TryReadLauncherLog(logPath);
            if (text is null)
            {
                continue;
            }

            if (File.GetLastWriteTime(logPath) < launchedAt)
            {
                continue;
            }

            if (text.Contains("Loading [ItemAdder", StringComparison.Ordinal))
            {
                Log("Mod chargé : ItemAdder est actif dans le jeu.");
                return;
            }

            if (text.Contains("Chainloader startup complete", StringComparison.Ordinal))
            {
                Log("Attention : BepInEx a démarré mais ItemAdder n'a pas été chargé. " +
                    "Le DLL est peut-être absent de BepInEx\\plugins.");
                SetState("Mod absent", Theme.Red);
                return;
            }
        }

        Log("Attention : le jeu tourne sans le mod (BepInEx n'a pas démarré). " +
            "Ferme le jeu, clique sur « Vérifier les mises à jour » pour réinstaller " +
            "BepInEx, puis relance depuis le launcher.");
        SetState("Sans mod", Theme.Red);
    }

    private static string? TryReadLauncherLog(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ApplyModuleSettings(string gamePath)
    {
        bool tobacco = _tobaccoCheck.Checked;
        bool underground = _undergroundCheck.Checked;
        bool restock = _restockCheck.Checked;
        SettingsStore.SaveModules(tobacco, underground, restock);

        try
        {
            ModConfig.Apply(gamePath, tobacco, underground, restock);
            Log($"Modules : tabac {(tobacco ? "activé" : "désactivé")}, " +
                $"sous-sol {(underground ? "activé" : "désactivé")}, " +
                $"réassort intelligent {(restock ? "activé" : "désactivé")}.");
        }
        catch (Exception ex)
        {
            Log("Configuration du mod non enregistrée : " + ex.Message);
        }
    }

    // -------------------------------------------------------------- launcher

    private async void InstallSelfButton_Click(object? sender, EventArgs e)
    {
        if (LauncherInstaller.IsInstalled)
        {
            DialogResult answer = MessageBox.Show(
                this,
                "Désinstaller ItemAdder Launcher de ce PC ?\n\n" +
                "Les raccourcis du bureau, du menu Démarrer et l'entrée « Applications installées » " +
                "seront supprimés.\n\n" +
                "Le mod installé dans le jeu et tes réglages ne sont pas touchés.",
                "Désinstaller le launcher",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer == DialogResult.Yes)
            {
                await RunOperationAsync("Désinstallation du launcher...", UninstallSelfAsync);
            }

            return;
        }

        await RunOperationAsync("Installation du launcher sur ce PC...", InstallSelfAsync);
    }

    private Task InstallSelfAsync()
    {
        LauncherInstaller.Install(new Progress<string>(Log));
        UpdateInstallButton();
        Log("Le launcher est installé : raccourcis Bureau et menu Démarrer, " +
            "entrée « Applications installées ».");
        Log($"Mises à jour : elles remplaceront {LauncherInstaller.InstalledExecutablePath} " +
            "et les raccourcis resteront valides.");

        if (LauncherInstaller.IsRunningInstalledCopy)
        {
            return Task.CompletedTask;
        }

        DialogResult answer = MessageBox.Show(
            this,
            "ItemAdder Launcher est installé sur ce PC.\n\n" +
            "Ouvrir la version installée maintenant ? C'est elle qui recevra les mises à jour " +
            "automatiques.",
            "Installation terminée",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer == DialogResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = LauncherInstaller.InstalledExecutablePath,
                WorkingDirectory = LauncherInstaller.InstallDirectory,
                UseShellExecute = true
            });
            Close();
        }

        return Task.CompletedTask;
    }

    private Task UninstallSelfAsync()
    {
        LauncherInstaller.Uninstall(new Progress<string>(Log));
        UpdateInstallButton();

        if (LauncherInstaller.IsRunningInstalledCopy)
        {
            Log("Fermeture du launcher pour terminer la suppression du dossier d'installation.");
            Close();
            return Task.CompletedTask;
        }

        Log("Le launcher a été retiré de ce PC. Le mod et les réglages restent en place.");
        return Task.CompletedTask;
    }

    private void UpdateInstallButton()
    {
        bool installed = LauncherInstaller.IsInstalled;

        _installSelfButton.Text = installed ? "Désinstaller le launcher" : "Installer sur le PC";
        _installSelfButton.IconGlyph = installed ? "\uE74D" : "\uE896";
        _installSelfButton.IconColor = installed ? Theme.TextMuted : Theme.Accent;
        _installSelfButton.ForeColor = installed ? Theme.TextMuted : Theme.Text;
        _installSelfButton.Invalidate();

        _toolTip.SetToolTip(
            _installSelfButton,
            installed
                ? "Désinstalle le launcher : raccourcis, entrée Windows et dossier " +
                  "d'installation. Le mod dans le jeu reste en place."
                : "Installe le launcher sur ce PC : raccourci sur le bureau, menu Démarrer " +
                  "et entrée dans « Applications installées ». Les mises à jour remplaceront " +
                  "cette installation.");
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
        SettingsStore.SaveGamePath(gamePath);

        _installedMod = ModService.GetInstalledMod(gamePath);
        _installedValue.Text = _installedMod?.Version ?? "aucune";
        _installedFile.Text = _installedMod is null
            ? "Aucun DLL dans BepInEx\\plugins"
            : $"({_installedMod.FileName})";

        bool bepInExInstalled = ModService.IsBepInExInstalled(gamePath);
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

        await CheckLauncherUpdateAsync();
    }

    private async Task CheckLauncherUpdateAsync()
    {
        if (_launcherUpdateChecked || _latestRelease is null || IsDisposed)
        {
            return;
        }

        _launcherUpdateChecked = true;

        ReleaseAsset? asset = _latestRelease.SelectLauncherAsset();
        if (asset is null)
        {
            Log("Launcher : aucun asset ItemAdderLauncher*.exe dans la release, pas de mise à jour proposée.");
            return;
        }

        Version current = LauncherUpdater.CurrentVersion;
        Version? remote = LauncherUpdater.GetAssetVersion(asset);

        if (remote is null)
        {
            Log($"Launcher : la version n'est pas dans le nom de l'asset " +
                $"({asset.Name}). Renomme l'exécutable {LauncherUpdater.AssetPrefix}-v{current}.exe " +
                "avant de l'ajouter à la release pour activer la vérification.");
            return;
        }

        if (remote <= current)
        {
            Log($"Launcher à jour (v{current}).");
            return;
        }

        DialogResult answer = MessageBox.Show(
            this,
            $"Une nouvelle version du launcher est disponible : v{remote} " +
            $"(actuelle : v{current}).\n\nTélécharger et redémarrer maintenant ?",
            "Mise à jour du launcher",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            Log($"Mise à jour du launcher v{remote} ignorée.");
            return;
        }

        string updateTarget = LauncherInstaller.UpdateTargetPath;
        if (!LauncherUpdater.CanReplaceExecutable(updateTarget, out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        SetState("Mise à jour", Theme.Blue);
        string downloaded = await LauncherUpdater.DownloadAsync(
            asset,
            new Progress<string>(Log),
            _lifetime.Token);

        Version? downloadedVersion = LauncherUpdater.ReadExecutableVersion(downloaded);
        if (downloadedVersion is not null && downloadedVersion <= current)
        {
            Log($"Le fichier téléchargé est en v{downloadedVersion} : mise à jour annulée.");
            return;
        }

        Log($"Installation du launcher v{downloadedVersion?.ToString() ?? remote.ToString()} " +
            $"dans {updateTarget} et redémarrage...");
        LauncherUpdater.ScheduleReplaceAndRestart(updateTarget, downloaded, Environment.ProcessId);
        Close();
    }

    private void UpdateLatestValue()
    {
        if (_latestRelease is null)
        {
            _latestValue.Text = "aucune";
            _latestValue.ForeColor = Theme.TextMuted;
            _teaserTitle.Text = "Aucune release récupérée";
            _teaserMeta.Text = "Lance une vérification pour charger les actualités du dépôt.";
            Log("Aucune release GitHub disponible pour le moment.");
            return;
        }

        _latestValue.Text = _latestRelease.TagName;
        _latestValue.ForeColor = Theme.Text;
        _teaserTitle.Text = _latestRelease.Title;
        _teaserMeta.Text = BuildReleaseMeta(_latestRelease) + "   ·   " + _latestRelease.TagName;
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
        _stateBadge.Left = _statusCard.ClientSize.Width - 8 - _stateBadge.Width;
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
        SettingsStore.SaveGamePath(gamePath);
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
        _installSelfButton.Enabled = !busy;
        _clearLogButton.Enabled = !busy;
        _newsRefreshButton.Enabled = !busy;
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

    // --------------------------------------------------------------- helpers

    private static string GetLauncherVersion()
    {
        Version version = LauncherUpdater.CurrentVersion;
        return $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                Icon? icon = Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // L'icône générique suffit si l'exécutable n'en contient pas.
        }

        return SystemIcons.Application;
    }

    private static Image? LoadEmbeddedImage(string name)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream is null)
            {
                return null;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;
            return Image.FromStream(memory);
        }
        catch
        {
            return null;
        }
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
