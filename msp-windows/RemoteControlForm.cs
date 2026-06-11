using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using msp_windows.Api;
using msp_windows.Api.Dtos;
using msp_windows.Overlay;
using msp_windows.Overlay.Models;
using msp_windows.Settings;

public class ProcessItem
{
    public string ProcessName { get; set; }
    public string WindowTitle { get; set; }

    public override string ToString() => WindowTitle;
}

public class RemoteControlForm : Form
{
    private static readonly Color AppBackground = Color.FromArgb(243, 246, 250);
    private static readonly Color RailBackground = Color.FromArgb(17, 24, 39);
    private static readonly Color CardBackground = Color.White;
    private static readonly Color MutedBackground = Color.FromArgb(248, 250, 252);
    private static readonly Color PreviewBackground = Color.FromArgb(231, 237, 245);
    private static readonly Color BorderColor = Color.FromArgb(216, 224, 234);
    private static readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);
    private static readonly Color PrimaryHoverColor = Color.FromArgb(29, 78, 216);
    private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
    private static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
    private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
    private static readonly Color SelectedBackground = Color.FromArgb(219, 234, 254);
    private static readonly Color SuccessBackground = Color.FromArgb(220, 252, 231);
    private static readonly Color SuccessText = Color.FromArgb(21, 128, 61);
    private static readonly Color ChipBackground = Color.FromArgb(219, 234, 254);

    private const int LoginCallbackPort = 51321;
    private const int RailWidth = 88;
    private const string SearchPlaceholder = "Search overlays";
    private static readonly Regex OverlayCodeRegex = new Regex("^[A-Z0-9]{6}$", RegexOptions.Compiled);

    private readonly AppSettingsService appSettingsService = new AppSettingsService();
    private readonly OverlayCacheService overlayCacheService = new OverlayCacheService();
    private readonly List<OverlaySelectionItem> overlayItems = new List<OverlaySelectionItem>();
    private readonly Dictionary<string, OverlayDocument> overlayDocumentCache = new Dictionary<string, OverlayDocument>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> favoriteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private OverlayDocument activeOverlayDocument;

    private TextBox overlayCodeTextBox;
    private Button loadByCodeButton;
    private Label codeLoadStatusLabel;
    private ComboBox processList;
    private RoundedButton overlayToggleButton;
    private Button loginGoogleButton;
    private Button logoutButton;
    private Button refreshLibraryButton;
    private Label currentUserLabel;
    private Label loginStatusLabel;
    private Label activeOverlayNameLabel;
    private Label activeOverlayMetaLabel;
    private Label selectedOverlayStatusLabel;
    private FlowLayoutPanel libraryListPanel;
    private FlowLayoutPanel cloudListPanel;
    private FlowLayoutPanel localListPanel;
    private Label cloudCountLabel;
    private Label localCountLabel;
    private TextBox librarySearchBox;
    private TextBox fullLibrarySearchBox;
    private Panel activePreviewCanvas;
    private Panel homePage;
    private Panel libraryPage;
    private Panel settingsPage;
    private Control homeNavButton;
    private Control libraryNavButton;
    private Control settingsNavButton;

    private string loadedOverlayStorePath;
    private string favoritesStorePath;
    private string accessToken;
    private UserMeResponse currentUser;
    private OverlaySelectionItem selectedOverlayItem;
    private AppSection currentSection = AppSection.Home;

    public RemoteControlForm()
    {
        Text = "MSP Overlay";
        MinimumSize = new Size(1040, 720);
        Size = new Size(1180, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppBackground;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        appSettingsService.LoadOrCreate();
        loadedOverlayStorePath = Path.Combine(appSettingsService.SettingsDirectoryPath, "loaded-overlays.txt");
        favoritesStorePath = Path.Combine(appSettingsService.SettingsDirectoryPath, "favorite-overlays.txt");
        accessToken = appSettingsService.Current.AccessToken;

        LoadFavorites();
        BuildLayout();
        LoadRunningProcesses();
        LoadCachedOverlayCards();
        _ = RestoreLoginAsync();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ReapplyResponsiveLayout();
    }

    // The responsive Resize handlers initially run with the form's design-time
    // size, before the window is shown and DPI auto-scaling is applied. Once the
    // form is visible we know the real client size, so trigger one more layout
    // pass to fix the initial alignment (previously only a manual resize did this).
    private void ReapplyResponsiveLayout()
    {
        if (!IsHandleCreated) {
            return;
        }

        var client = ClientSize;
        if (client.Width <= 0 || client.Height <= 0) {
            return;
        }

        SuspendLayout();
        try {
            ClientSize = new Size(client.Width, client.Height + 1);
            ClientSize = client;
        }
        finally {
            ResumeLayout(true);
        }
    }

    private void BuildLayout()
    {
        Controls.Clear();

        var rail = CreateNavigationRail();
        rail.Left = 0;
        rail.Top = 0;
        rail.Width = RailWidth;
        rail.Height = ClientSize.Height;
        rail.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(rail);

        var content = new Panel {
            Left = RailWidth,
            Top = 0,
            Width = Math.Max(0, ClientSize.Width - RailWidth),
            Height = ClientSize.Height,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = AppBackground,
            Padding = new Padding(0)
        };
        Controls.Add(content);

        homePage = new Panel { Dock = DockStyle.Fill, BackColor = AppBackground };
        libraryPage = new Panel { Dock = DockStyle.Fill, BackColor = AppBackground, Visible = false };
        settingsPage = new Panel { Dock = DockStyle.Fill, BackColor = AppBackground, Visible = false };
        content.Controls.Add(settingsPage);
        content.Controls.Add(libraryPage);
        content.Controls.Add(homePage);

        BuildHomePage(homePage);
        BuildLibraryPage(libraryPage);
        BuildSettingsPage(settingsPage);
        ShowSection(AppSection.Home);

        Resize += (s, e) => {
            rail.Height = ClientSize.Height;
            content.Left = rail.Width;
            content.Width = Math.Max(0, ClientSize.Width - rail.Width);
            content.Height = ClientSize.Height;
        };
    }

    private void BuildHomePage(Panel content)
    {
        var header = CreateHeader("Home", "Choose a game window, select an overlay, and run it.");
        header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        header.Left = 32;
        header.Top = 24;
        header.Width = Math.Max(0, content.ClientSize.Width - 64);
        header.Height = 58;
        content.Controls.Add(header);

        const int bottomBarHeight = 96;
        const int runCardWidth = 376; // matches the My Library column width above it
        const int columnGap = 24;
        const int bottomMargin = 24;
        const int gap = 16;
        const int contentTop = 108;
        int bottomTop = Math.Max(contentTop + 200, content.ClientSize.Height - bottomMargin - bottomBarHeight);

        var activeOverlay = CreateActiveOverlayCard();
        activeOverlay.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        activeOverlay.Left = 32;
        activeOverlay.Top = contentTop;
        activeOverlay.Width = Math.Max(560, content.ClientSize.Width - 472);
        activeOverlay.Height = Math.Max(200, bottomTop - gap - contentTop);
        content.Controls.Add(activeOverlay);

        var rightColumn = CreateRightColumn();
        rightColumn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        rightColumn.Left = content.ClientSize.Width - 408;
        rightColumn.Top = contentTop;
        rightColumn.Width = 376;
        rightColumn.Height = Math.Max(200, bottomTop - gap - contentTop);
        content.Controls.Add(rightColumn);

        // Program selection and the overlay run button live in a separate bar at
        // the bottom of the screen, split into two independent cards.
        var selectionCard = CreateGameSelectionCard();
        selectionCard.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        selectionCard.Left = 32;
        selectionCard.Top = bottomTop;
        selectionCard.Width = Math.Max(300, content.ClientSize.Width - 64 - runCardWidth - columnGap);
        selectionCard.Height = bottomBarHeight;
        content.Controls.Add(selectionCard);

        var runCard = CreateOverlayRunCard();
        runCard.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        runCard.Left = content.ClientSize.Width - 32 - runCardWidth;
        runCard.Top = bottomTop;
        runCard.Width = runCardWidth;
        runCard.Height = bottomBarHeight;
        content.Controls.Add(runCard);

        content.Resize += (s, e) => {
            int barTop = Math.Max(contentTop + 200, content.ClientSize.Height - bottomMargin - bottomBarHeight);
            header.Width = Math.Max(0, content.ClientSize.Width - 64);

            rightColumn.Left = content.ClientSize.Width - rightColumn.Width - 32;
            rightColumn.Top = contentTop;
            rightColumn.Height = Math.Max(200, barTop - gap - contentTop);

            activeOverlay.Top = contentTop;
            activeOverlay.Width = Math.Max(420, rightColumn.Left - activeOverlay.Left - 24);
            activeOverlay.Height = Math.Max(200, barTop - gap - contentTop);

            selectionCard.Top = barTop;
            selectionCard.Width = Math.Max(300, content.ClientSize.Width - 64 - runCardWidth - columnGap);
            selectionCard.Height = bottomBarHeight;

            runCard.Top = barTop;
            runCard.Left = content.ClientSize.Width - 32 - runCardWidth;
            runCard.Height = bottomBarHeight;

            ResizeActiveOverlayCard(activeOverlay);
            ResizeRightColumn(rightColumn);
        };
    }

    private Control CreateNavigationRail()
    {
        var rail = new Panel {
            Width = RailWidth,
            BackColor = RailBackground
        };

        var logo = CreateLabel("MSP", 0, 24, RailWidth, 26, 12f, FontStyle.Bold, Color.White);
        logo.TextAlign = ContentAlignment.MiddleCenter;
        rail.Controls.Add(logo);
        int railButtonLeft = (RailWidth - 48) / 2;
        homeNavButton = CreateRailButton(RailIconKind.Home, railButtonLeft, 82, true);
        libraryNavButton = CreateRailButton(RailIconKind.Library, railButtonLeft, 142, false);
        settingsNavButton = CreateRailButton(RailIconKind.Settings, railButtonLeft, 202, false);
        WireNavButton(homeNavButton, AppSection.Home);
        WireNavButton(libraryNavButton, AppSection.Library);
        WireNavButton(settingsNavButton, AppSection.Settings);
        rail.Controls.Add(homeNavButton);
        rail.Controls.Add(libraryNavButton);
        rail.Controls.Add(settingsNavButton);
        return rail;
    }

    private void WireNavButton(Control control, AppSection section)
    {
        control.Click += (s, e) => ShowSection(section);
        foreach (Control child in control.Controls) {
            WireNavButton(child, section);
        }
    }

    private void ShowSection(AppSection section)
    {
        currentSection = section;
        if (homePage != null) {
            homePage.Visible = section == AppSection.Home;
        }
        if (libraryPage != null) {
            libraryPage.Visible = section == AppSection.Library;
        }
        if (settingsPage != null) {
            settingsPage.Visible = section == AppSection.Settings;
        }

        SetRailButtonActive(homeNavButton, section == AppSection.Home);
        SetRailButtonActive(libraryNavButton, section == AppSection.Library);
        SetRailButtonActive(settingsNavButton, section == AppSection.Settings);
    }

    private void SetRailButtonActive(Control control, bool active)
    {
        if (control is RailMenuButton railButton) {
            railButton.IsActive = active;
            railButton.Invalidate();
            return;
        }

        if (control is RoundedPanel panel) {
            panel.BackColor = active ? PrimaryColor : Color.FromArgb(31, 41, 55);
            panel.BorderColor = active ? PrimaryColor : Color.FromArgb(31, 41, 55);
            panel.Invalidate();
        }

        foreach (Control child in control.Controls) {
            child.ForeColor = active ? Color.White : Color.FromArgb(148, 163, 184);
        }
    }

    private Control CreateRailButton(RailIconKind iconKind, int left, int top, bool active)
    {
        return new RailMenuButton {
            Left = left,
            Top = top,
            Width = 48,
            Height = 48,
            Radius = 18,
            IconKind = iconKind,
            IsActive = active,
            Cursor = Cursors.Hand
        };
    }

    private void BuildLibraryPage(Panel content)
    {
        var header = CreateHeader("Library", "Browse your saved Windows overlays.");
        header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        header.Left = 32;
        header.Top = 24;
        header.Width = Math.Max(0, content.ClientSize.Width - 64);
        header.Height = 58;
        content.Controls.Add(header);

        var codeCard = CreateCard();
        codeCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        codeCard.Left = 32;
        codeCard.Top = 102;
        codeCard.Width = Math.Max(0, content.ClientSize.Width - 64);
        codeCard.Height = 116;
        codeCard.Controls.Add(CreateLabel("Load by code", 24, 18, 200, 24, 12f, FontStyle.Bold, TextPrimary));
        codeCard.Controls.Add(CreateLabel("Paste a 6-character shared overlay code.", 24, 44, 320, 18, 8f, FontStyle.Regular, TextMuted));

        var codeHost = CreateRoundedTextBox(out overlayCodeTextBox, 24, 72, 214, 36, 8, 10f);
        overlayCodeTextBox.CharacterCasing = CharacterCasing.Upper;
        overlayCodeTextBox.MaxLength = 6;
        loadByCodeButton = CreateButton("Load", 250, 70, 110, 38, PrimaryColor, Color.White);
        loadByCodeButton.Click += LoadByCodeButton_Click;
        codeLoadStatusLabel = CreateLabel("Invalid codes show a short status message here.", 376, 80, 360, 18, 8f, FontStyle.Regular, TextMuted);
        codeCard.Controls.Add(codeHost);
        codeCard.Controls.Add(loadByCodeButton);
        codeCard.Controls.Add(codeLoadStatusLabel);
        content.Controls.Add(codeCard);

        var libraryCard = CreateCard();
        libraryCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        libraryCard.Left = 32;
        libraryCard.Top = 238;
        libraryCard.Width = Math.Max(0, content.ClientSize.Width - 64);
        libraryCard.Height = Math.Max(320, content.ClientSize.Height - 270);
        libraryCard.Controls.Add(CreateLabel("My Library", 24, 22, 180, 26, 13f, FontStyle.Bold, TextPrimary));

        var fullSearchHost = CreateRoundedTextBox(out fullLibrarySearchBox, 24, 64, 260, 36, 8, 9f);
        fullLibrarySearchBox.PlaceholderTextCompat(SearchPlaceholder);
        fullLibrarySearchBox.TextChanged += (s, e) => RefreshLibraryColumns();
        libraryCard.Controls.Add(fullSearchHost);

        // CLOUD (left) and LOCAL (right) are shown as two separate columns.
        var cloudHeader = BuildColumnHeader("Cloud", "Saved to your account", out cloudCountLabel);
        libraryCard.Controls.Add(cloudHeader);
        cloudListPanel = CreateColumnListPanel();
        libraryCard.Controls.Add(cloudListPanel);

        var localHeader = BuildColumnHeader("Local", "Downloaded to this PC", out localCountLabel);
        libraryCard.Controls.Add(localHeader);
        localListPanel = CreateColumnListPanel();
        libraryCard.Controls.Add(localListPanel);

        var refreshButton = CreateButton("Refresh Library", 24, libraryCard.Height - 52, 180, 36, Color.White, TextPrimary);
        refreshButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        refreshButton.Click += RefreshLibraryButton_Click;
        libraryCard.Controls.Add(refreshButton);

        LayoutLibraryColumns(libraryCard, cloudHeader, localHeader);
        libraryCard.Resize += (s, e) => {
            LayoutLibraryColumns(libraryCard, cloudHeader, localHeader);
            ResizeLibraryRows();
        };

        content.Controls.Add(libraryCard);
        content.Resize += (s, e) => {
            header.Width = Math.Max(0, content.ClientSize.Width - 64);
            codeCard.Width = Math.Max(0, content.ClientSize.Width - 64);
            libraryCard.Width = Math.Max(0, content.ClientSize.Width - 64);
            libraryCard.Height = Math.Max(320, content.ClientSize.Height - 270);
        };
    }

    private FlowLayoutPanel CreateColumnListPanel()
    {
        return new FlowLayoutPanel {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = CardBackground
        };
    }

    private RoundedPanel BuildColumnHeader(string title, string subtitle, out Label countLabel)
    {
        var bar = new RoundedPanel {
            Height = 40,
            Radius = 8,
            BackColor = MutedBackground,
            BorderColor = BorderColor
        };

        var titleLabel = CreateLabel(title.ToUpperInvariant(), 14, 11, 70, 18, 9.5f, FontStyle.Bold, TextPrimary);
        bar.Controls.Add(titleLabel);

        var pill = new PillLabel {
            Text = "0",
            Left = titleLabel.Right + 4,
            Top = 11,
            Width = 30,
            Height = 18,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = TextSecondary,
            BackColor = CardBackground,
            Radius = 9,
            TextAlign = ContentAlignment.MiddleCenter
        };
        bar.Controls.Add(pill);
        bar.Controls.Add(CreateLabel(subtitle, pill.Right + 10, 12, 220, 16, 8f, FontStyle.Regular, TextMuted));

        countLabel = pill;
        return bar;
    }

    private void LayoutLibraryColumns(Control card, Control cloudHeader, Control localHeader)
    {
        if (cloudListPanel == null || localListPanel == null || cloudHeader == null || localHeader == null) {
            return;
        }

        const int pad = 24;
        const int gap = 20;
        const int topY = 112;
        const int headerH = 40;
        const int listTop = 162;
        const int bottomReserve = 64;

        int colW = Math.Max(220, (card.Width - pad * 2 - gap) / 2);
        int leftX = pad;
        int rightX = pad + colW + gap;
        int listH = Math.Max(120, card.Height - listTop - bottomReserve);

        cloudHeader.SetBounds(leftX, topY, colW, headerH);
        cloudListPanel.SetBounds(leftX, listTop, colW, listH);
        localHeader.SetBounds(rightX, topY, colW, headerH);
        localListPanel.SetBounds(rightX, listTop, colW, listH);
    }

    private void RefreshLibraryColumns()
    {
        PopulateOverlayList(cloudListPanel);
        PopulateOverlayList(localListPanel);
        ResizeLibraryRows();
    }

    private void BuildSettingsPage(Panel content)
    {
        var header = CreateHeader("Settings", "Manage account and overlay hotkey preferences.");
        header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        header.Left = 32;
        header.Top = 24;
        header.Width = Math.Max(0, content.ClientSize.Width - 64);
        header.Height = 58;
        content.Controls.Add(header);

        var accountCard = CreateCard();
        accountCard.Left = 32;
        accountCard.Top = 108;
        accountCard.Width = 520;
        accountCard.Height = 190;
        accountCard.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        accountCard.Controls.Add(CreateLabel("Account", 24, 22, 180, 26, 13f, FontStyle.Bold, TextPrimary));

        var avatar = new RoundedPanel {
            Left = 24,
            Top = 62,
            Width = 44,
            Height = 44,
            Radius = 22,
            BackColor = ChipBackground,
            BorderColor = ChipBackground
        };
        var avatarText = CreateLabel("J", 0, 10, 44, 20, 11f, FontStyle.Bold, PrimaryColor);
        avatarText.TextAlign = ContentAlignment.MiddleCenter;
        avatar.Controls.Add(avatarText);
        accountCard.Controls.Add(avatar);

        currentUserLabel = CreateLabel("Guest", 84, 62, 240, 22, 10.5f, FontStyle.Bold, TextPrimary);
        loginStatusLabel = CreateLabel("Signed out", 84, 86, 240, 20, 8.5f, FontStyle.Regular, TextMuted);
        accountCard.Controls.Add(currentUserLabel);
        accountCard.Controls.Add(loginStatusLabel);

        loginGoogleButton = CreateButton("Google Login", 24, 130, 150, 38, PrimaryColor, Color.White);
        logoutButton = CreateButton("Logout", 186, 130, 110, 38, Color.White, TextPrimary);
        loginGoogleButton.Click += LoginGoogleButton_Click;
        logoutButton.Click += LogoutButton_Click;
        accountCard.Controls.Add(loginGoogleButton);
        accountCard.Controls.Add(logoutButton);

        var hotkeyCard = CreateCard();
        hotkeyCard.Left = 32;
        hotkeyCard.Top = 322;
        hotkeyCard.Width = 520;
        hotkeyCard.Height = 210;
        hotkeyCard.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        hotkeyCard.Controls.Add(CreateLabel("Hotkey", 24, 22, 180, 26, 13f, FontStyle.Bold, TextPrimary));
        hotkeyCard.Controls.Add(CreateLabel("Current overlay close hotkey is managed by the overlay window.", 24, 52, 420, 20, 8.5f, FontStyle.Regular, TextMuted));

        var enabledCheck = new CheckBox {
            Left = 24,
            Top = 86,
            Width = 180,
            Height = 24,
            Text = "Enable hotkey",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = CardBackground
        };
        hotkeyCard.Controls.Add(enabledCheck);

        var hotkeyHost = CreateRoundedTextBox(out var hotkeyBox, 24, 126, 180, 36, 8, 9f);
        hotkeyBox.Text = "Alt + Shift + S";
        hotkeyBox.ReadOnly = true;
        hotkeyCard.Controls.Add(hotkeyHost);
        hotkeyCard.Controls.Add(CreateLabel("Custom key assignment is a UI placeholder until HotkeyManager supports configurable keys.", 224, 126, 260, 40, 8f, FontStyle.Regular, TextMuted));

        content.Controls.Add(accountCard);
        content.Controls.Add(hotkeyCard);
        content.Resize += (s, e) => header.Width = Math.Max(0, content.ClientSize.Width - 64);
    }

    private Control CreateHeader(string title, string subtitle)
    {
        // Indent the title/subtitle by the card inner padding (24) so the header text
        // lines up vertically with the text inside the cards below it.
        var header = new Panel { BackColor = AppBackground };
        header.Controls.Add(CreateLabel(title, 24, 4, 260, 30, 18f, FontStyle.Bold, TextPrimary));
        header.Controls.Add(CreateLabel(subtitle, 24, 34, 620, 20, 9f, FontStyle.Regular, TextMuted));
        return header;
    }

    private Control CreateGameSelectionCard()
    {
        var card = CreateCard();

        var gameIcon = new RoundedPanel {
            Left = 16,
            Top = 18,
            Width = 58,
            Height = 58,
            Radius = 10,
            BackColor = RailBackground,
            BorderColor = RailBackground
        };
        gameIcon.Controls.Add(CreateAccentBar(14, 16, 30, 4, Color.FromArgb(6, 182, 212)));
        gameIcon.Controls.Add(CreateAccentBar(14, 30, 22, 4, PrimaryColor));
        card.Controls.Add(gameIcon);

        card.Controls.Add(CreateLabel("DETECTED GAME", 92, 20, 160, 15, 7.5f, FontStyle.Bold, TextMuted));
        processList = CreateComboBox(92, 40, 250);
        processList.DropDown += (s, e) => LoadRunningProcesses();
        card.Controls.Add(processList);
        card.Resize += (s, e) => processList.Width = Math.Max(180, card.Width - 92 - 24);

        return card;
    }

    private Control CreateOverlayRunCard()
    {
        var card = CreateCard();

        overlayToggleButton = (RoundedButton)CreateButton("Start Overlay", 16, 26, 188, 44, PrimaryColor, Color.White);
        overlayToggleButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        overlayToggleButton.Click += ToggleOverlay;
        card.Controls.Add(overlayToggleButton);
        card.Resize += (s, e) => {
            overlayToggleButton.Width = Math.Max(120, card.Width - 32);
            overlayToggleButton.Top = Math.Max(0, (card.Height - overlayToggleButton.Height) / 2);
        };
        UpdateOverlayToggleButton();

        return card;
    }

    private Control CreateActiveOverlayCard()
    {
        var card = CreateCard();

        int titleWidth;
        using (var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold)) {
            titleWidth = TextRenderer.MeasureText("Active overlay", titleFont).Width;
        }
        var titleLabel = CreateLabel("Active overlay", 24, 20, titleWidth + 4, 24, 12f, FontStyle.Bold, TextPrimary);
        card.Controls.Add(titleLabel);
        card.Controls.Add(CreateStatusPill("WINDOWS", titleLabel.Right + 8, 22, 78, ChipBackground, PrimaryColor));

        activeOverlayNameLabel = CreateLabel("No overlay selected", 24, 56, 300, 22, 10.5f, FontStyle.Bold, TextPrimary);
        activeOverlayMetaLabel = CreateLabel("Load by code or select from My Library.", 24, 80, 360, 18, 8.5f, FontStyle.Regular, TextMuted);
        card.Controls.Add(activeOverlayNameLabel);
        card.Controls.Add(activeOverlayMetaLabel);

        activePreviewCanvas = new RoundedPanel {
            Left = 24,
            Top = 120,
            Width = 540,
            Height = 270,
            Radius = 8,
            BackColor = PreviewBackground,
            BorderColor = BorderColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        activePreviewCanvas.Paint += DrawActivePreview;
        card.Controls.Add(activePreviewCanvas);

        card.Resize += (s, e) => ResizeActiveOverlayCard(card);
        return card;
    }

    private void ResizeActiveOverlayCard(Control card)
    {
        if (activePreviewCanvas != null) {
            activePreviewCanvas.Width = Math.Max(320, card.Width - 48);
            activePreviewCanvas.Height = Math.Max(220, card.Height - 144);
        }
    }

    private Control CreateRightColumn()
    {
        var column = new Panel { BackColor = AppBackground };

        var libraryCard = CreateCard();
        libraryCard.Left = 0;
        libraryCard.Top = 0;
        libraryCard.Width = 376;
        libraryCard.Height = 546;
        libraryCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        libraryCard.Controls.Add(CreateLabel("My Library", 20, 18, 140, 24, 12f, FontStyle.Bold, TextPrimary));
        var searchHost = CreateRoundedTextBox(out librarySearchBox, 168, 16, 188, 36, 8, 9f);
        librarySearchBox.PlaceholderTextCompat(SearchPlaceholder);
        librarySearchBox.TextChanged += (s, e) => PopulateOverlayList(libraryListPanel);
        libraryCard.Controls.Add(searchHost);

        libraryListPanel = new FlowLayoutPanel {
            Left = 16,
            Top = 68,
            Width = 344,
            Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = CardBackground
        };
        libraryCard.Controls.Add(libraryListPanel);

        refreshLibraryButton = CreateButton("Refresh Library", 16, 306, 344, 40, Color.White, TextPrimary);
        refreshLibraryButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        refreshLibraryButton.Click += RefreshLibraryButton_Click;
        libraryCard.Controls.Add(refreshLibraryButton);
        libraryCard.Resize += (s, e) => {
            searchHost.Left = libraryCard.Width - 208;
            libraryListPanel.Width = libraryCard.Width - 32;
            libraryListPanel.Height = Math.Max(120, libraryCard.Height - 150);
            refreshLibraryButton.Top = libraryCard.Height - 64;
            refreshLibraryButton.Width = libraryCard.Width - 32;
            ResizeLibraryRows();
        };

        column.Controls.Add(libraryCard);
        return column;
    }

    private void ResizeRightColumn(Control rightColumn)
    {
        foreach (Control child in rightColumn.Controls) {
            child.Width = rightColumn.Width;
            child.Height = Math.Max(260, rightColumn.Height - child.Top);
        }
        ResizeLibraryRows();
    }

    private async void LoginGoogleButton_Click(object sender, EventArgs e)
    {
        await LoginWithGoogleAsync();
    }

    private void LogoutButton_Click(object sender, EventArgs e)
    {
        accessToken = null;
        currentUser = null;
        appSettingsService.UpdateAccessToken(null);
        overlayItems.RemoveAll(item => item.Source == OverlaySource.Library);
        selectedOverlayItem = null;
        activeOverlayDocument = null;
        RenderOverlayPreviewCards();
        UpdateAuthUi();
        UpdateSelectedOverlayUi("Logged out.");
    }

    private async void RefreshLibraryButton_Click(object sender, EventArgs e)
    {
        await RefreshAllAsync();
    }

    private async void LoadByCodeButton_Click(object sender, EventArgs e)
    {
        await LoadByCodeAsync();
    }

    private async Task RestoreLoginAsync()
    {
        UpdateAuthUi();

        if (string.IsNullOrWhiteSpace(accessToken)) {
            return;
        }

        loginStatusLabel.Text = "Checking session";
        await LoadCurrentUserAsync();
        if (currentUser != null) {
            await LoadLibraryAsync();
        }
    }

    private async Task LoginWithGoogleAsync()
    {
        var settings = appSettingsService.Current ?? appSettingsService.LoadOrCreate();
        string state = Guid.NewGuid().ToString("N");
        string callbackUrl = $"http://127.0.0.1:{LoginCallbackPort}/auth/callback/";
        string loginUrl = settings.ServerBaseUrl.TrimEnd('/') +
            "/api/auth/windows/google/start?callbackUrl=" +
            Uri.EscapeDataString(callbackUrl) +
            "&state=" + Uri.EscapeDataString(state);

        loginStatusLabel.Text = "Waiting login";
        loginGoogleButton.Enabled = false;

        try {
            var tokenTask = WaitForLoginCallbackAsync(state);
            Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });

            accessToken = await tokenTask;
            appSettingsService.UpdateAccessToken(accessToken);
            await LoadCurrentUserAsync();
            await LoadLibraryAsync();
        }
        catch (Exception ex) {
            loginStatusLabel.Text = "Login failed";
            UpdateSelectedOverlayUi("Login failed: " + ex.Message);
        }
        finally {
            loginGoogleButton.Enabled = true;
            UpdateAuthUi();
        }
    }

    private async Task<string> WaitForLoginCallbackAsync(string expectedState)
    {
        using (var listener = new HttpListener())
        {
            listener.Prefixes.Add($"http://127.0.0.1:{LoginCallbackPort}/auth/callback/");
            listener.Start();

            var context = await listener.GetContextAsync().ConfigureAwait(true);
            var query = ParseQueryString(context.Request.Url.Query);
            string state = GetQueryValue(query, "state");
            string token = GetQueryValue(query, "accessToken");
            string error = GetQueryValue(query, "error");

            string body = "<html><body><h3>MSP Overlay login complete.</h3><p>You can close this window.</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(true);
            context.Response.OutputStream.Close();

            if (!string.Equals(state, expectedState, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Invalid login state.");
            }

            if (!string.IsNullOrWhiteSpace(error)) {
                throw new InvalidOperationException(error);
            }

            if (string.IsNullOrWhiteSpace(token)) {
                throw new InvalidOperationException("Access token was not returned.");
            }

            return token;
        }
    }

    private async Task LoadCurrentUserAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken)) {
            currentUser = null;
            UpdateAuthUi();
            return;
        }

        var settings = appSettingsService.Current ?? appSettingsService.LoadOrCreate();
        using (var api = new MspApiClient(settings.ServerBaseUrl))
        {
            var resp = await api.GetMeAsync(accessToken).ConfigureAwait(true);
            if (resp != null && resp.Success && resp.Data != null) {
                currentUser = resp.Data;
            }
            else {
                currentUser = null;
                accessToken = null;
                appSettingsService.UpdateAccessToken(null);
                UpdateSelectedOverlayUi(resp?.Message ?? "Session check failed.");
            }
        }

        UpdateAuthUi();
    }

    private async Task LoadLibraryAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken)) {
            UpdateSelectedOverlayUi("Login is required.");
            return;
        }

        UpdateSelectedOverlayUi("Loading library...");
        var settings = appSettingsService.Current ?? appSettingsService.LoadOrCreate();

        using (var api = new MspApiClient(settings.ServerBaseUrl))
        {
            var resp = await api.GetMyLibraryAsync(accessToken).ConfigureAwait(true);
            if (resp == null || !resp.Success || resp.Data == null) {
                UpdateSelectedOverlayUi(resp?.Message ?? "Library request failed.");
                return;
            }

            overlayItems.RemoveAll(item => item.Source == OverlaySource.Library);
            int savedOrder = 0;
            foreach (var item in resp.Data) {
                if (item?.Overlay == null || !IsWindowsPlatform(item.Overlay.Platform)) {
                    continue;
                }

                var cloudItem = OverlaySelectionItem.FromLibrary(item, settings.ServerBaseUrl);
                cloudItem.SavedOrder = savedOrder++;
                overlayItems.Add(cloudItem);
            }
        }

        RenderOverlayPreviewCards();
        UpdateSelectedOverlayUi(overlayItems.Count == 0 ? "No saved overlays." : "Choose an overlay from My Library.");
    }

    private async Task LoadByCodeAsync()
    {
        string code = (overlayCodeTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();

        if (!OverlayCodeRegex.IsMatch(code)) {
            codeLoadStatusLabel.Text = "Enter a valid 6-character overlay code.";
            return;
        }

        overlayCodeTextBox.Text = code;
        codeLoadStatusLabel.Text = "Loading...";

        var settings = appSettingsService.Current ?? appSettingsService.LoadOrCreate();

        using (var api = new MspApiClient(settings.ServerBaseUrl))
        {
            var resp = await api.GetOverlayByCodeAsync(code).ConfigureAwait(true);

            if (resp == null || !resp.Success || resp.Data == null) {
                string msg = (resp != null && !string.IsNullOrWhiteSpace(resp.Message)) ? resp.Message : "Failed to load overlay.";
                codeLoadStatusLabel.Text = msg;
                ErrorLogger.LogError("E210", msg);
                return;
            }

            try {
                ApplyOverlayResponse(resp.Data, code);
                AddOrSelectLoadedOverlay(resp.Data);
                codeLoadStatusLabel.Text = "Loaded: " + resp.Data.Code;
            }
            catch (Exception ex) {
                codeLoadStatusLabel.Text = BuildFriendlyApplyError(ex);
                ErrorLogger.LogError("E220", "Overlay parse/apply failed: " + ex);
            }
        }
    }

    private void ApplyOverlayResponse(OverlayDetailResponse overlay, string fallbackCode)
    {
        if (overlay == null || string.IsNullOrWhiteSpace(overlay.OverlayJson)) {
            throw new InvalidDataException("Server response does not include overlayJson.");
        }

        if (!IsWindowsPlatform(overlay.Platform)) {
            throw new InvalidDataException("Unsupported overlay platform.");
        }

        string cacheCode = string.IsNullOrWhiteSpace(overlay.Code) ? fallbackCode : overlay.Code.Trim().ToUpperInvariant();
        overlayCacheService.SaveOverlayJson(cacheCode, overlay.OverlayJson);

        var parser = new OverlayJsonParser();
        var doc = parser.Parse(overlay.OverlayJson);
        var applyService = new OverlayApplyService();
        applyService.Apply(doc);

        if (!string.IsNullOrWhiteSpace(cacheCode)) {
            overlayDocumentCache[cacheCode] = doc;
        }

        overlay.Code = cacheCode;
        appSettingsService.UpdateLastSelectedOverlayId(GetOverlaySelectionId(overlay, doc.OverlayId));
    }

    // Fetches a cloud overlay through the same code endpoint as "Load by code".
    private async Task<OverlayDetailResponse> FetchOverlayByCodeAsync(OverlaySelectionItem item)
    {
        string code = (item?.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (!OverlayCodeRegex.IsMatch(code)) {
            UpdateSelectedOverlayUi("This overlay has no valid share code.");
            return null;
        }

        var settings = appSettingsService.Current ?? appSettingsService.LoadOrCreate();

        using (var api = new MspApiClient(settings.ServerBaseUrl))
        {
            var resp = await api.GetOverlayByCodeAsync(code).ConfigureAwait(true);
            if (resp == null || !resp.Success || resp.Data == null) {
                UpdateSelectedOverlayUi(resp?.Message ?? "Failed to load overlay.");
                return null;
            }

            return resp.Data;
        }
    }

    // Cloud Run: downloads (saves) the overlay by code, then applies it. Returns false on failure.
    private async Task<bool> ApplyLibraryOverlayAsync(OverlaySelectionItem item)
    {
        if (item == null) {
            return false;
        }

        UpdateSelectedOverlayUi("Applying overlay...");
        var detail = await FetchOverlayByCodeAsync(item).ConfigureAwait(true);
        if (detail == null) {
            return false;
        }

        try {
            ApplyOverlayResponse(detail, item.Code);
            AddOrSelectLoadedOverlay(detail);
            UpdateSelectedOverlayUi("Selected: " + item.DisplayName);
            return true;
        }
        catch (Exception ex) {
            UpdateSelectedOverlayUi(BuildFriendlyApplyError(ex));
            ErrorLogger.LogError("E221", "Library overlay apply failed: " + ex);
            return false;
        }
    }

    private string BuildFriendlyApplyError(Exception ex)
    {
        if (ex is InvalidDataException || ex is ArgumentException) {
            return ex.Message;
        }

        return "Overlay apply failed.";
    }

    private void LoadCachedOverlayCards()
    {
        overlayItems.RemoveAll(item => item.Source == OverlaySource.LocalCache);

        if (string.IsNullOrWhiteSpace(loadedOverlayStorePath) || !File.Exists(loadedOverlayStorePath)) {
            RenderOverlayPreviewCards();
            return;
        }

        try {
            foreach (string raw in File.ReadAllLines(loadedOverlayStorePath)) {
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }

                string[] parts = raw.Split(new[] { '|' }, 5);
                if (parts.Length < 2) {
                    continue;
                }

                DateTime? lastUsed = null;
                if (parts.Length > 3 && long.TryParse(Unescape(parts[3]), out long ticks) && ticks > 0) {
                    try { lastUsed = new DateTime(ticks, DateTimeKind.Utc); } catch { lastUsed = null; }
                }

                int runCount = 0;
                if (parts.Length > 4) {
                    int.TryParse(Unescape(parts[4]), out runCount);
                }

                overlayItems.Add(new OverlaySelectionItem {
                    Source = OverlaySource.LocalCache,
                    Code = Unescape(parts[0]),
                    DisplayName = string.IsNullOrWhiteSpace(Unescape(parts[1])) ? "(Untitled)" : Unescape(parts[1]),
                    OverlayId = parts.Length > 2 ? Unescape(parts[2]) : null,
                    LastUsedUtc = lastUsed,
                    RunCount = runCount
                });
            }
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E213", "Loaded overlay list read failed: " + ex.Message);
        }

        RenderOverlayPreviewCards();
    }

    private void AddOrSelectLoadedOverlay(OverlayDetailResponse overlay)
    {
        AddLocalOverlay(overlay, select: true);
    }

    private OverlaySelectionItem AddLocalOverlay(OverlayDetailResponse overlay, bool select)
    {
        if (overlay == null) {
            return null;
        }

        string code = string.IsNullOrWhiteSpace(overlay.Code) ? "UNKNOWN" : overlay.Code.Trim().ToUpperInvariant();
        string title = string.IsNullOrWhiteSpace(overlay.Name) ? "(Untitled)" : overlay.Name.Trim();

        // Preserve usage stats if a local copy with the same code already exists.
        var existing = overlayItems.FirstOrDefault(item => item.Source == OverlaySource.LocalCache
            && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
        overlayItems.RemoveAll(item => item.Source == OverlaySource.LocalCache && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

        var loadedItem = new OverlaySelectionItem {
            Source = OverlaySource.LocalCache,
            Code = code,
            DisplayName = title,
            OverlayId = overlay.OverlayId,
            Platform = overlay.Platform,
            LastUsedUtc = existing?.LastUsedUtc,
            RunCount = existing?.RunCount ?? 0
        };

        overlayItems.Insert(0, loadedItem);
        SaveLoadedOverlaysToStore();
        if (select) {
            SelectOverlayItem(loadedItem);
        }
        RenderOverlayPreviewCards();
        return loadedItem;
    }

    // ---- Library item actions -------------------------------------------------

    private async Task RefreshAllAsync()
    {
        LoadCachedOverlayCards();

        if (!string.IsNullOrWhiteSpace(accessToken)) {
            await LoadLibraryAsync();
        }
        else {
            RenderOverlayPreviewCards();
            UpdateSelectedOverlayUi("Login to load cloud overlays.");
        }
    }

    private async Task RunOverlayItemAsync(OverlaySelectionItem item)
    {
        if (item == null) {
            return;
        }

        SelectOverlayItem(item);

        string usageCode = item.Code;
        if (item.Source == OverlaySource.Library) {
            // Cloud Run: save the overlay locally first, then start it.
            if (!await ApplyLibraryOverlayAsync(item)) {
                return;
            }
        }
        else {
            ApplyCachedOverlay(item);
        }

        MarkLocalUsed(usageCode);
        RenderOverlayPreviewCards();
        StartOverlay();
    }

    // Records that a local overlay was run, for the Local "recently used" sort order.
    private void MarkLocalUsed(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) {
            return;
        }

        var local = overlayItems.FirstOrDefault(i => i.Source == OverlaySource.LocalCache
            && string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));
        if (local == null) {
            return;
        }

        local.LastUsedUtc = DateTime.UtcNow;
        local.RunCount += 1;
        SaveLoadedOverlaysToStore();
    }

    private void DeleteLocalOverlay(OverlaySelectionItem item)
    {
        if (item == null || item.Source != OverlaySource.LocalCache) {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete the local overlay \"{item.DisplayName}\"?\nThe downloaded copy will be removed from this PC.",
            "Delete local overlay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Code)) {
            overlayCacheService.DeleteOverlayJson(item.Code);
            overlayDocumentCache.Remove(item.Code);
        }

        overlayItems.RemoveAll(i => i.Source == OverlaySource.LocalCache
            && string.Equals(i.Code, item.Code, StringComparison.OrdinalIgnoreCase));
        SaveLoadedOverlaysToStore();

        if (ReferenceEquals(selectedOverlayItem, item)) {
            selectedOverlayItem = null;
            activeOverlayDocument = null;
        }

        RenderOverlayPreviewCards();
        UpdateSelectedOverlayUi("Deleted: " + item.DisplayName);
    }

    // Cloud Download: fetches the overlay by its share code and keeps a local copy (no apply).
    private async Task DownloadCloudOverlayAsync(OverlaySelectionItem item)
    {
        if (item == null || item.Source != OverlaySource.Library) {
            return;
        }

        UpdateSelectedOverlayUi("Downloading: " + item.DisplayName);
        var detail = await FetchOverlayByCodeAsync(item).ConfigureAwait(true);
        if (detail == null) {
            return;
        }

        try {
            if (string.IsNullOrWhiteSpace(detail.OverlayJson)) {
                throw new InvalidDataException("Server response does not include overlayJson.");
            }

            if (!IsWindowsPlatform(detail.Platform)) {
                throw new InvalidDataException("Unsupported overlay platform.");
            }

            string cacheCode = string.IsNullOrWhiteSpace(detail.Code)
                ? (item.Code ?? string.Empty).Trim().ToUpperInvariant()
                : detail.Code.Trim().ToUpperInvariant();

            overlayCacheService.SaveOverlayJson(cacheCode, detail.OverlayJson);
            overlayDocumentCache.Remove(cacheCode);
            detail.Code = cacheCode;

            AddLocalOverlay(detail, select: false);
            UpdateSelectedOverlayUi("Downloaded: " + item.DisplayName);
        }
        catch (Exception ex) {
            UpdateSelectedOverlayUi(BuildFriendlyApplyError(ex));
            ErrorLogger.LogError("E222", "Cloud overlay download failed: " + ex.Message);
        }
    }

    // ---- Favorites ------------------------------------------------------------

    private static string FavoriteKey(OverlaySelectionItem item)
    {
        if (item == null) {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.OverlayId)) {
            return "id:" + item.OverlayId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.Code)) {
            return "code:" + item.Code.Trim().ToUpperInvariant();
        }

        return null;
    }

    private bool IsFavorite(OverlaySelectionItem item)
    {
        string key = FavoriteKey(item);
        return key != null && favoriteKeys.Contains(key);
    }

    private void ToggleFavorite(OverlaySelectionItem item)
    {
        string key = FavoriteKey(item);
        if (key == null) {
            return;
        }

        if (!favoriteKeys.Remove(key)) {
            favoriteKeys.Add(key);
        }

        SaveFavorites();
        RenderOverlayPreviewCards();
    }

    private void LoadFavorites()
    {
        favoriteKeys.Clear();

        if (string.IsNullOrWhiteSpace(favoritesStorePath) || !File.Exists(favoritesStorePath)) {
            return;
        }

        try {
            foreach (string raw in File.ReadAllLines(favoritesStorePath)) {
                string key = Unescape(raw)?.Trim();
                if (!string.IsNullOrWhiteSpace(key)) {
                    favoriteKeys.Add(key);
                }
            }
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E215", "Favorites read failed: " + ex.Message);
        }
    }

    private void SaveFavorites()
    {
        if (string.IsNullOrWhiteSpace(favoritesStorePath)) {
            return;
        }

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(favoritesStorePath));
            var lines = favoriteKeys.Select(Escape).ToArray();
            File.WriteAllLines(favoritesStorePath, lines);
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E216", "Favorites save failed: " + ex.Message);
        }
    }

    private void SaveLoadedOverlaysToStore()
    {
        if (string.IsNullOrWhiteSpace(loadedOverlayStorePath)) {
            return;
        }

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(loadedOverlayStorePath));

            var lines = new List<string>();
            foreach (var item in overlayItems) {
                if (item.Source != OverlaySource.LocalCache || string.IsNullOrWhiteSpace(item.Code)) {
                    continue;
                }

                string lastUsedTicks = item.LastUsedUtc.HasValue ? item.LastUsedUtc.Value.Ticks.ToString() : "0";
                lines.Add(string.Join("|",
                    Escape(item.Code),
                    Escape(item.DisplayName),
                    Escape(item.OverlayId),
                    Escape(lastUsedTicks),
                    Escape(item.RunCount.ToString())));
            }

            File.WriteAllLines(loadedOverlayStorePath, lines.ToArray());
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E214", "Loaded overlay list save failed: " + ex.Message);
        }
    }

    private void RenderOverlayPreviewCards()
    {
        PopulateOverlayList(libraryListPanel);
        PopulateOverlayList(cloudListPanel);
        PopulateOverlayList(localListPanel);
        ResizeLibraryRows();
        UpdateActiveOverlayLabels();
        activePreviewCanvas?.Invalidate();
    }

    private void PopulateOverlayList(FlowLayoutPanel panel)
    {
        if (panel == null) {
            return;
        }

        panel.SuspendLayout();
        panel.Controls.Clear();

        if (panel == cloudListPanel) {
            PopulateCloudColumn(panel);
        }
        else if (panel == localListPanel) {
            PopulateLocalColumn(panel);
        }
        else {
            PopulateHomeQuickList(panel);
        }

        panel.ResumeLayout();
        ResizeLibraryRows(panel);
    }

    // Home quick-run list: locally downloaded overlays only, most-recently-used first.
    private void PopulateHomeQuickList(FlowLayoutPanel panel)
    {
        var local = SortLocal(FilterOverlayItems(librarySearchBox?.Text, "Local"));

        if (local.Count == 0) {
            panel.Controls.Add(overlayItems.Count == 0
                ? CreateMessageRow("No overlays yet", "Downloaded overlays appear here.")
                : CreateMessageRow("No local overlays", "Download an overlay from the Library tab to run it here."));
            return;
        }

        foreach (var item in local) {
            panel.Controls.Add(CreateLibraryRow(item, management: false));
        }
    }

    // Library tab left column: cloud (account) overlays without a local copy.
    private void PopulateCloudColumn(FlowLayoutPanel panel)
    {
        var cloud = SortCloud(FilterOverlayItems(fullLibrarySearchBox?.Text, null)
            .Where(i => i.Source == OverlaySource.Library && !HasLocalCopy(i)));

        if (cloudCountLabel != null) {
            cloudCountLabel.Text = cloud.Count.ToString();
        }

        if (cloud.Count == 0) {
            panel.Controls.Add(CreateMessageRow("No cloud overlays", "Sign in and refresh to load your account library."));
            return;
        }

        foreach (var item in cloud) {
            panel.Controls.Add(CreateLibraryRow(item, management: true));
        }
    }

    // Library tab right column: overlays downloaded to this PC.
    private void PopulateLocalColumn(FlowLayoutPanel panel)
    {
        var local = SortLocal(FilterOverlayItems(fullLibrarySearchBox?.Text, null)
            .Where(i => i.Source == OverlaySource.LocalCache));

        if (localCountLabel != null) {
            localCountLabel.Text = local.Count.ToString();
        }

        if (local.Count == 0) {
            panel.Controls.Add(CreateMessageRow("No local overlays", "Download a cloud overlay to keep it here."));
            return;
        }

        foreach (var item in local) {
            panel.Controls.Add(CreateLibraryRow(item, management: true));
        }
    }

    private bool HasLocalCopy(OverlaySelectionItem cloudItem)
    {
        if (cloudItem == null) {
            return false;
        }

        return overlayItems.Any(i => i.Source == OverlaySource.LocalCache && (
            (!string.IsNullOrWhiteSpace(cloudItem.Code) && string.Equals(i.Code, cloudItem.Code, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(cloudItem.OverlayId) && string.Equals(i.OverlayId, cloudItem.OverlayId, StringComparison.OrdinalIgnoreCase))));
    }

    // Local: favorites first, then most-recently-used, then most-run, then name.
    private List<OverlaySelectionItem> SortLocal(IEnumerable<OverlaySelectionItem> items)
    {
        return items
            .OrderByDescending(i => IsFavorite(i))
            .ThenByDescending(i => i.LastUsedUtc ?? DateTime.MinValue)
            .ThenByDescending(i => i.RunCount)
            .ThenBy(i => i.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Cloud: favorites first, then the order the library saved them, then name.
    private List<OverlaySelectionItem> SortCloud(IEnumerable<OverlaySelectionItem> items)
    {
        return items
            .OrderByDescending(i => IsFavorite(i))
            .ThenBy(i => i.SavedOrder)
            .ThenBy(i => i.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // The Windows client only handles Windows overlays, so non-Windows items are always excluded.
    private List<OverlaySelectionItem> FilterOverlayItems(string search, string category)
    {
        IEnumerable<OverlaySelectionItem> items = overlayItems.Where(i => IsWindowsPlatform(i.Platform));

        if (!string.IsNullOrWhiteSpace(search) && !string.Equals(search, SearchPlaceholder, StringComparison.Ordinal)) {
            string needle = search.Trim().ToLowerInvariant();
            items = items.Where(i =>
                (i.DisplayName ?? string.Empty).ToLowerInvariant().Contains(needle) ||
                (i.Code ?? string.Empty).ToLowerInvariant().Contains(needle) ||
                (i.GameName ?? string.Empty).ToLowerInvariant().Contains(needle));
        }

        if (string.Equals(category, "Local", StringComparison.Ordinal)) {
            items = items.Where(i => i.Source == OverlaySource.LocalCache);
        }
        else if (string.Equals(category, "Cloud", StringComparison.Ordinal)) {
            items = items.Where(i => i.Source == OverlaySource.Library);
        }

        return items.ToList();
    }

    private static bool IsWindowsPlatform(string platform)
    {
        return string.IsNullOrWhiteSpace(platform)
            || string.Equals(platform.Trim(), "windows", StringComparison.OrdinalIgnoreCase);
    }

    private Control CreateMessageRow(string title, string meta)
    {
        var row = new RoundedPanel {
            Width = 344,
            Height = 66,
            Radius = 9,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = CardBackground,
            BorderColor = BorderColor,
            Cursor = Cursors.Default
        };

        row.Controls.Add(CreateLabel(title, 16, 14, 300, 18, 9f, FontStyle.Bold, TextPrimary));
        row.Controls.Add(CreateLabel(meta, 16, 35, 312, 16, 8f, FontStyle.Regular, TextMuted));
        return row;
    }

    private Control CreateLibraryRow(OverlaySelectionItem item, bool management)
    {
        bool selected = ReferenceEquals(item, selectedOverlayItem);
        bool isLocal = item.Source == OverlaySource.LocalCache;
        bool favorite = IsFavorite(item);

        var row = new RoundedPanel {
            Width = 344,
            Height = 74,
            Radius = 9,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = selected ? SelectedBackground : CardBackground,
            BorderColor = selected ? Color.FromArgb(147, 197, 253) : BorderColor,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var preview = new RoundedPanel {
            Left = 12,
            Top = 12,
            Width = 64,
            Height = 50,
            Radius = 6,
            BackColor = isLocal ? Color.FromArgb(224, 242, 254) : Color.FromArgb(219, 234, 254),
            BorderColor = Color.Transparent,
            Tag = item
        };
        preview.Paint += DrawMiniPreview;
        row.Controls.Add(preview);

        // Source badge: distinguishes locally downloaded vs cloud (account) overlays.
        var badge = new PillLabel {
            Text = isLocal ? "LOCAL" : "CLOUD",
            Left = 88,
            Top = 37,
            Width = 52,
            Height = 18,
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = isLocal ? SuccessText : PrimaryColor,
            BackColor = isLocal ? SuccessBackground : ChipBackground,
            Radius = 9,
            TextAlign = ContentAlignment.MiddleCenter
        };
        row.Controls.Add(badge);

        // Action buttons sit in fixed columns (same x on every row) anchored to the
        // right edge, so the layout never looks ragged between Local and Cloud rows.
        const int rightPad = 16;
        const int favW = 36;
        const int runW = 56;
        const int manageW = 92;
        const int gapA = 8;
        int actionTop = (74 - 30) / 2;

        int manageLeft = 344 - rightPad - manageW;
        int runLeft = management ? manageLeft - gapA - runW : 344 - rightPad - runW;
        int favLeft = runLeft - gapA - favW;

        void AddActionAt(int left, string text, int w, Color back, Color fore, Action onClick)
        {
            var b = (RoundedButton)CreateButton(text, left, actionTop, w, 30, back, fore);
            b.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            b.Click += (s, e) => onClick();
            row.Controls.Add(b);
        }

        if (management) {
            if (isLocal) {
                AddActionAt(manageLeft, "Delete", manageW, Color.White, Color.FromArgb(220, 38, 38), () => DeleteLocalOverlay(item));
            }
            else {
                AddActionAt(manageLeft, "Download", manageW, PrimaryColor, Color.White, () => { _ = DownloadCloudOverlayAsync(item); });
            }
        }

        AddActionAt(runLeft, "Run", runW, isLocal ? PrimaryColor : Color.White, isLocal ? Color.White : TextPrimary, () => { _ = RunOverlayItemAsync(item); });
        AddActionAt(favLeft, favorite ? "★" : "☆", favW, Color.White, favorite ? Color.FromArgb(245, 158, 11) : TextMuted, () => ToggleFavorite(item));

        // Title + meta fill the remaining space to the left of the action columns.
        int textRight = favLeft - gapA;
        var titleLabel = CreateLabel(item.DisplayName, 88, 13, Math.Max(60, textRight - 88), 18, 8.5f, FontStyle.Bold, TextPrimary);
        titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        row.Controls.Add(titleLabel);

        var meta = CreateLabel(item.BuildShortMetaText(), 148, 38, Math.Max(40, textRight - 148), 14, 7.5f, FontStyle.Regular, TextMuted);
        meta.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        row.Controls.Add(meta);

        void Select(object s, EventArgs e) { _ = SelectAndApplyOverlayItemAsync(item); }
        row.Click += Select;
        preview.Click += Select;
        titleLabel.Click += Select;
        badge.Click += Select;
        meta.Click += Select;

        return row;
    }

    private async Task SelectAndApplyOverlayItemAsync(OverlaySelectionItem item)
    {
        SelectOverlayItem(item);
        RenderOverlayPreviewCards();

        if (item.Source == OverlaySource.Library) {
            await ApplyLibraryOverlayAsync(item);
            return;
        }

        ApplyCachedOverlay(item);
    }

    private void SelectOverlayItem(OverlaySelectionItem item)
    {
        selectedOverlayItem = item;
        activeOverlayDocument = TryLoadOverlayDocument(item?.Code);
        UpdateActiveOverlayLabels();
        if (item != null) {
            UpdateSelectedOverlayUi("Selected: " + item.DisplayName);
        }
        activePreviewCanvas?.Invalidate();
    }

    private OverlayDocument TryLoadOverlayDocument(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) {
            return null;
        }

        if (overlayDocumentCache.TryGetValue(code, out var cached)) {
            return cached;
        }

        string json = overlayCacheService.TryLoadOverlayJson(code);
        if (string.IsNullOrWhiteSpace(json)) {
            return null;
        }

        try {
            var doc = new OverlayJsonParser().Parse(json);
            overlayDocumentCache[code] = doc;
            return doc;
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E230", "Preview parse failed for " + code + ": " + ex.Message);
            return null;
        }
    }

    private void ApplyCachedOverlay(OverlaySelectionItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Code)) {
            return;
        }

        try {
            string json = overlayCacheService.TryLoadOverlayJson(item.Code);
            if (string.IsNullOrWhiteSpace(json)) {
                UpdateSelectedOverlayUi("Cached overlay JSON was not found.");
                return;
            }

            var parser = new OverlayJsonParser();
            var doc = parser.Parse(json);
            var applyService = new OverlayApplyService();
            applyService.Apply(doc);
            appSettingsService.UpdateLastSelectedOverlayId(string.IsNullOrWhiteSpace(item.OverlayId) ? item.Code : item.OverlayId);
            UpdateSelectedOverlayUi("Selected: " + item.DisplayName);
        }
        catch (Exception ex) {
            UpdateSelectedOverlayUi(BuildFriendlyApplyError(ex));
            ErrorLogger.LogError("E221", "Local overlay apply failed: " + ex.Message);
        }
    }

    private void ResizeLibraryRows()
    {
        ResizeLibraryRows(libraryListPanel);
        ResizeLibraryRows(cloudListPanel);
        ResizeLibraryRows(localListPanel);
    }

    private void ResizeLibraryRows(FlowLayoutPanel panel)
    {
        if (panel == null) {
            return;
        }

        int width = panel.ClientSize.Width - 4;

        // Leave room for the vertical scrollbar so rows never spill past the right
        // edge and trigger a horizontal scrollbar.
        if (panel.VerticalScroll.Visible) {
            width -= SystemInformation.VerticalScrollBarWidth;
        }

        foreach (Control control in panel.Controls) {
            control.Width = Math.Max(260, width);
        }
    }

    private void LoadRunningProcesses()
    {
        processList.Items.Clear();
        foreach (Process process in Process.GetProcesses()) {
            if (!string.IsNullOrEmpty(process.MainWindowTitle)) {
                processList.Items.Add(new ProcessItem {
                    ProcessName = process.ProcessName,
                    WindowTitle = process.MainWindowTitle
                });
            }
        }

        if (processList.Items.Count > 0 && processList.SelectedIndex < 0) {
            processList.SelectedIndex = 0;
        }
    }

    private void ToggleOverlay(object sender, EventArgs e)
    {
        if (IsOverlayRunning()) {
            StopOverlay();
        }
        else {
            StartOverlay();
        }
    }

    private void StartOverlay()
    {
        if (processList.SelectedItem == null) {
            UpdateSelectedOverlayUi("Choose a game window first.");
            return;
        }

        var selectedProcess = (ProcessItem)processList.SelectedItem;
        OverlayForm.ShowOverlay(selectedProcess.ProcessName);

        var instance = OverlayForm.Instance;
        if (instance != null) {
            // Keep the toggle in sync when the overlay closes by any means (e.g. hotkey).
            instance.FormClosed += OverlayClosed;
        }

        UpdateOverlayToggleButton();
    }

    private void StopOverlay()
    {
        OverlayForm.Instance?.Close();
        UpdateOverlayToggleButton();
    }

    private void OverlayClosed(object sender, FormClosedEventArgs e)
    {
        if (sender is Form form) {
            form.FormClosed -= OverlayClosed;
        }

        if (IsHandleCreated && !IsDisposed) {
            BeginInvoke((Action)UpdateOverlayToggleButton);
        }
    }

    private static bool IsOverlayRunning()
    {
        var instance = OverlayForm.Instance;
        return instance != null && !instance.IsDisposed;
    }

    private void UpdateOverlayToggleButton()
    {
        if (overlayToggleButton == null) {
            return;
        }

        if (IsOverlayRunning()) {
            overlayToggleButton.Text = "Stop Overlay";
            overlayToggleButton.BackColor = Color.White;
            overlayToggleButton.ForeColor = TextPrimary;
            overlayToggleButton.BorderColor = BorderColor;
            overlayToggleButton.HoverBackColor = Color.FromArgb(248, 250, 252);
        }
        else {
            overlayToggleButton.Text = "Start Overlay";
            overlayToggleButton.BackColor = PrimaryColor;
            overlayToggleButton.ForeColor = Color.White;
            overlayToggleButton.BorderColor = PrimaryColor;
            overlayToggleButton.HoverBackColor = PrimaryHoverColor;
        }

        overlayToggleButton.Invalidate();
    }

    private void UpdateAuthUi()
    {
        bool loggedIn = currentUser != null && !string.IsNullOrWhiteSpace(accessToken);
        currentUserLabel.Text = loggedIn ? TrimText(currentUser.Name, 12) : "Guest";
        loginStatusLabel.Text = loggedIn ? "Signed in" : "Signed out";
        loginGoogleButton.Visible = !loggedIn;
        logoutButton.Visible = loggedIn;
        // Refresh also reloads locally downloaded overlays, so it works signed out too.
        refreshLibraryButton.Enabled = true;
    }

    private void UpdateSelectedOverlayUi(string status)
    {
        selectedOverlayStatusLabel = selectedOverlayStatusLabel ?? activeOverlayMetaLabel;
        if (selectedOverlayStatusLabel != null && selectedOverlayItem == null) {
            selectedOverlayStatusLabel.Text = status;
        }
    }

    private void UpdateActiveOverlayLabels()
    {
        if (activeOverlayNameLabel == null || activeOverlayMetaLabel == null) {
            return;
        }

        if (selectedOverlayItem == null) {
            activeOverlayNameLabel.Text = "No overlay selected";
            activeOverlayMetaLabel.Text = "Load by code or select from My Library.";
            return;
        }

        activeOverlayNameLabel.Text = selectedOverlayItem.DisplayName;
        activeOverlayMetaLabel.Text = selectedOverlayItem.BuildMetaText();
    }

    private void DrawActivePreview(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = activePreviewCanvas.ClientRectangle;
        bounds.Inflate(-12, -12);

        // Always draw the basic grid; no placeholder markers.
        using (var gridPen = new Pen(Color.FromArgb(203, 213, 225), 1))
        {
            for (int i = 1; i < 6; i++) {
                int x = bounds.Left + (bounds.Width * i / 6);
                g.DrawLine(gridPen, x, bounds.Top, x, bounds.Bottom);
            }

            for (int i = 1; i < 5; i++) {
                int y = bounds.Top + (bounds.Height * i / 5);
                g.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
            }
        }

        // Draw the overlay on top of the grid only when one is selected.
        if (activeOverlayDocument != null) {
            Renderer.DrawOverlayDocument(g, bounds, activeOverlayDocument);
        }
    }

    private void DrawMiniPreview(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var panel = sender as Control;
        var item = panel?.Tag as OverlaySelectionItem;
        var doc = item != null ? TryLoadOverlayDocument(item.Code) : null;

        if (doc != null && panel != null) {
            var bounds = panel.ClientRectangle;
            bounds.Inflate(-6, -6);
            Renderer.DrawOverlayDocument(g, bounds, doc);
            return;
        }

        using (var primary = new SolidBrush(Color.FromArgb(37, 99, 235)))
        using (var cyan = new SolidBrush(Color.FromArgb(6, 182, 212)))
        {
            g.FillRoundedRectangle(primary, 12, 16, 34, 4, 2);
            g.FillRoundedRectangle(cyan, 22, 26, 34, 10, 3);
        }
    }

    private Control CreateAccentBar(int left, int top, int width, int height, Color color)
    {
        return new RoundedPanel {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Radius = height / 2,
            BackColor = color,
            BorderColor = color
        };
    }

    private RoundedPanel CreateCard()
    {
        return new RoundedPanel {
            BackColor = CardBackground,
            BorderColor = BorderColor,
            Radius = 10
        };
    }

    private Label CreateStatusPill(string label, int left, int top, int width, Color background, Color foreground)
    {
        var pill = new PillLabel {
            Text = label,
            Left = left,
            Top = top,
            Width = width,
            Height = 24,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = foreground,
            BackColor = background,
            Radius = 12,
            TextAlign = ContentAlignment.MiddleCenter
        };
        return pill;
    }

    private Label CreateLabel(string text, int left, int top, int width, int height, float fontSize, FontStyle fontStyle, Color color)
    {
        return new Label {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Font = new Font("Segoe UI", fontSize, fontStyle),
            ForeColor = color,
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
    }

    private ComboBox CreateComboBox(int left, int top, int width)
    {
        var combo = new ComboBox {
            Left = left,
            Top = top,
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        MakeRounded(combo, 8);
        return combo;
    }

    // Hosts a borderless TextBox inside a RoundedPanel so the rounded border is
    // painted as one smooth, continuous outline. (Clipping a FixedSingle TextBox
    // with a Region cut the straight border into disconnected segments.)
    private RoundedPanel CreateRoundedTextBox(out TextBox textBox, int left, int top, int width, int height, int radius, float fontSize)
    {
        var host = new RoundedPanel {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Radius = radius,
            BackColor = Color.White,
            BorderColor = BorderColor,
            Cursor = Cursors.IBeam
        };

        var inner = new TextBox {
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Font = new Font("Segoe UI", fontSize, FontStyle.Regular)
        };
        host.Controls.Add(inner);

        void LayoutInner()
        {
            inner.Left = 12;
            inner.Width = Math.Max(0, host.Width - 24);
            inner.Top = Math.Max(0, (host.Height - inner.Height) / 2);
        }

        host.HandleCreated += (s, e) => LayoutInner();
        inner.HandleCreated += (s, e) => LayoutInner();
        host.Resize += (s, e) => LayoutInner();
        host.Click += (s, e) => inner.Focus();
        LayoutInner();

        textBox = inner;
        return host;
    }

    private Button CreateButton(string text, int left, int top, int width, int height, Color backColor, Color foreColor)
    {
        return new RoundedButton {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Radius = 8,
            BorderColor = backColor == Color.White ? BorderColor : backColor,
            HoverBackColor = backColor == PrimaryColor ? PrimaryHoverColor : Color.FromArgb(248, 250, 252),
            Cursor = Cursors.Hand
        };
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control == null || control.Width <= 0 || control.Height <= 0) {
            return;
        }

        using (var path = RoundedPanel.CreateRoundRectPath(new Rectangle(0, 0, control.Width, control.Height), radius)) {
            control.Region = new Region(path);
        }
    }

    private static void MakeRounded(Control control, int radius)
    {
        ApplyRoundedRegion(control, radius);
        control.Resize += (s, e) => ApplyRoundedRegion(control, radius);
        control.HandleCreated += (s, e) => ApplyRoundedRegion(control, radius);
    }

    private static string GetOverlaySelectionId(OverlayDetailResponse overlay, string parsedOverlayId)
    {
        if (overlay != null && !string.IsNullOrWhiteSpace(overlay.OverlayId)) {
            return overlay.OverlayId;
        }

        if (!string.IsNullOrWhiteSpace(parsedOverlayId)) {
            return parsedOverlayId;
        }

        if (overlay != null && !string.IsNullOrWhiteSpace(overlay.Code)) {
            return overlay.Code;
        }

        return null;
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value ?? string.Empty);
    }

    private static string Unescape(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        return Uri.UnescapeDataString(value);
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query)) {
            return values;
        }

        string trimmed = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        foreach (string pair in trimmed.Split('&')) {
            if (string.IsNullOrWhiteSpace(pair)) {
                continue;
            }

            string[] parts = pair.Split(new[] { '=' }, 2);
            string key = Uri.UnescapeDataString(parts[0].Replace("+", " "));
            string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace("+", " ")) : string.Empty;
            values[key] = value;
        }

        return values;
    }

    private static string GetQueryValue(Dictionary<string, string> query, string key)
    {
        return query != null && query.TryGetValue(key, out var value) ? value : null;
    }

    private static string TrimText(string value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return "User";
        }

        value = value.Trim();
        return value.Length <= length ? value : value.Substring(0, length - 1) + "...";
    }

    private enum OverlaySource
    {
        Library,
        LocalCache
    }

    private enum AppSection
    {
        Home,
        Library,
        Settings
    }

    private enum RailIconKind
    {
        Home,
        Library,
        Settings
    }

    private sealed class OverlaySelectionItem
    {
        public OverlaySource Source { get; set; }
        public long OverlayDatabaseId { get; set; }
        public string OverlayId { get; set; }
        public string Code { get; set; }
        public string DisplayName { get; set; }
        public string GameName { get; set; }
        public string Platform { get; set; }
        public string ThumbnailUrl { get; set; }

        // Local usage stats (used for the Local sort order).
        public DateTime? LastUsedUtc { get; set; }
        public int RunCount { get; set; }

        // Order the cloud library returned this item (used for the Cloud sort order).
        public int SavedOrder { get; set; }

        public static OverlaySelectionItem FromLibrary(LibraryItemResponse item, string serverBaseUrl)
        {
            var overlay = item.Overlay;
            return new OverlaySelectionItem {
                Source = OverlaySource.Library,
                OverlayDatabaseId = overlay.Id,
                OverlayId = overlay.OverlayId,
                Code = overlay.Code,
                DisplayName = string.IsNullOrWhiteSpace(overlay.Name) ? "(Untitled)" : overlay.Name,
                GameName = overlay.Game,
                Platform = overlay.Platform,
                ThumbnailUrl = BuildAbsoluteUrl(overlay.ThumbnailUrl, serverBaseUrl)
            };
        }

        public string BuildMetaText()
        {
            string source = Source == OverlaySource.Library ? "Library" : "Local";
            string code = string.IsNullOrWhiteSpace(Code) ? "No code" : Code;
            string game = string.IsNullOrWhiteSpace(GameName) ? "Unknown game" : GameName;
            return source + " | " + code + " | " + game;
        }

        public string BuildShortMetaText()
        {
            string code = string.IsNullOrWhiteSpace(Code) ? "No code" : Code;
            string game = string.IsNullOrWhiteSpace(GameName) ? (Source == OverlaySource.LocalCache ? "Local" : "Unknown") : GameName;
            return code + " | " + game;
        }

        private static string BuildAbsoluteUrl(string url, string serverBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(url)) {
                return null;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)) {
                return absolute.ToString();
            }

            if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var baseUri)) {
                return url;
            }

            return new Uri(baseUri, url.TrimStart('/')).ToString();
        }
    }

    private sealed class RoundedPanel : Panel
    {
        public Color BorderColor { get; set; } = Color.FromArgb(216, 224, 234);
        public int Radius { get; set; } = 8;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Fill the whole control (including the corner area outside the rounded
            // path) with the parent's background so the rounded corners blend into
            // whatever is behind the panel instead of showing the panel's own color.
            if (Parent != null) {
                e.Graphics.Clear(Parent.BackColor);
            }
            else {
                base.OnPaintBackground(e);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var brush = new SolidBrush(BackColor))
            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            base.OnPaint(e);
        }

        public static GraphicsPath CreateRoundRectPath(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class RoundedButton : Button
    {
        public int Radius { get; set; } = 8;
        public Color BorderColor { get; set; } = Color.FromArgb(216, 224, 234);
        public Color HoverBackColor { get; set; } = Color.FromArgb(248, 250, 252);

        private bool hovered;

        public RoundedButton()
        {
            SetStyle(
                ControlStyles.UserPaint
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override bool ShowFocusCues => false;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
        }

        private void ApplyRoundedRegion()
        {
            // Clip the button to its rounded shape so the rectangular focus/default
            // border the framework paints does not poke out past the rounded corners.
            if (Width <= 0 || Height <= 0) {
                return;
            }

            using (var path = RoundedPanel.CreateRoundRectPath(new Rectangle(0, 0, Width, Height), Radius)) {
                Region = new Region(path);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            if (Parent != null) {
                pevent.Graphics.Clear(Parent.BackColor);
            }
            else {
                base.OnPaintBackground(pevent);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color fillColor = !Enabled
                ? Color.FromArgb(241, 245, 249)
                : (hovered ? HoverBackColor : BackColor);
            Color textColor = Enabled ? ForeColor : Color.FromArgb(148, 163, 184);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedPanel.CreateRoundRectPath(bounds, Radius))
            using (var fill = new SolidBrush(fillColor))
            using (var border = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class PillLabel : Label
    {
        public int Radius { get; set; } = 12;

        public PillLabel()
        {
            AutoSize = false;
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Paint the corner area outside the rounded path with the parent's
            // background so the pill's corners are not rendered as opaque squares
            // of the pill color.
            if (Parent != null) {
                e.Graphics.Clear(Parent.BackColor);
            }
            else {
                base.OnPaintBackground(e);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedPanel.CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class RailMenuButton : Control
    {
        public RailIconKind IconKind { get; set; }
        public bool IsActive { get; set; }
        public int Radius { get; set; } = 18;

        public RailMenuButton()
        {
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Keep the rounded corners transparent to the navigation rail behind
            // the button rather than filling them with the button's own color.
            if (Parent != null) {
                e.Graphics.Clear(Parent.BackColor);
            }
            else {
                base.OnPaintBackground(e);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color background = IsActive ? PrimaryColor : Color.FromArgb(31, 41, 55);
            Color foreground = IsActive ? Color.White : Color.FromArgb(148, 163, 184);
            Color iconBackground = IsActive ? Color.FromArgb(59, 130, 246) : Color.FromArgb(15, 23, 42);

            using (var path = RoundedPanel.CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var brush = new SolidBrush(background))
            {
                e.Graphics.FillPath(brush, path);
            }

            var iconBounds = new Rectangle((Width - 28) / 2, (Height - 28) / 2, 28, 28);
            using (var iconBackBrush = new SolidBrush(iconBackground))
            using (var iconPath = RoundedPanel.CreateRoundRectPath(iconBounds, 10))
            {
                e.Graphics.FillPath(iconBackBrush, iconPath);
            }

            using (var iconPen = new Pen(foreground, 2f))
            using (var iconBrush = new SolidBrush(foreground))
            {
                DrawIcon(e.Graphics, iconBounds, iconPen, iconBrush);
            }
        }

        private void DrawIcon(Graphics graphics, Rectangle bounds, Pen pen, Brush brush)
        {
            switch (IconKind) {
                case RailIconKind.Home:
                    Point[] roof = {
                        new Point(bounds.Left + 6, bounds.Top + 14),
                        new Point(bounds.Left + 14, bounds.Top + 7),
                        new Point(bounds.Left + 22, bounds.Top + 14)
                    };
                    graphics.DrawLines(pen, roof);
                    graphics.DrawRectangle(pen, bounds.Left + 8, bounds.Top + 14, 16, 12);
                    break;
                case RailIconKind.Library:
                    graphics.DrawRectangle(pen, bounds.Left + 8, bounds.Top + 7, 14, 18);
                    graphics.DrawLine(pen, bounds.Left + 12, bounds.Top + 11, bounds.Left + 22, bounds.Top + 11);
                    graphics.DrawLine(pen, bounds.Left + 12, bounds.Top + 16, bounds.Left + 22, bounds.Top + 16);
                    graphics.DrawLine(pen, bounds.Left + 12, bounds.Top + 21, bounds.Left + 19, bounds.Top + 21);
                    break;
                case RailIconKind.Settings:
                    graphics.DrawEllipse(pen, bounds.Left + 9, bounds.Top + 9, 14, 14);
                    graphics.FillEllipse(brush, bounds.Left + 14, bounds.Top + 14, 4, 4);
                    graphics.DrawLine(pen, bounds.Left + 16, bounds.Top + 5, bounds.Left + 16, bounds.Top + 9);
                    graphics.DrawLine(pen, bounds.Left + 16, bounds.Top + 23, bounds.Left + 16, bounds.Top + 27);
                    graphics.DrawLine(pen, bounds.Left + 5, bounds.Top + 16, bounds.Left + 9, bounds.Top + 16);
                    graphics.DrawLine(pen, bounds.Left + 23, bounds.Top + 16, bounds.Left + 27, bounds.Top + 16);
                    break;
            }
        }
    }
}

internal static class ControlExtensions
{
    public static void PlaceholderTextCompat(this TextBox textBox, string placeholder)
    {
        if (textBox == null || string.IsNullOrWhiteSpace(placeholder)) {
            return;
        }

        textBox.Text = placeholder;
        textBox.ForeColor = Color.FromArgb(100, 116, 139);
        textBox.GotFocus += (s, e) => {
            if (textBox.Text == placeholder) {
                textBox.Text = string.Empty;
                textBox.ForeColor = Color.FromArgb(15, 23, 42);
            }
        };
        textBox.LostFocus += (s, e) => {
            if (string.IsNullOrWhiteSpace(textBox.Text)) {
                textBox.Text = placeholder;
                textBox.ForeColor = Color.FromArgb(100, 116, 139);
            }
        };
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, int x, int y, int width, int height, int radius)
    {
        using (var path = CreateRoundRectPath(new Rectangle(x, y, width, height), radius))
        {
            graphics.FillPath(brush, path);
        }
    }

    private static GraphicsPath CreateRoundRectPath(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
