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
    private OverlayDocument activeOverlayDocument;

    private TextBox overlayCodeTextBox;
    private Button loadByCodeButton;
    private Label codeLoadStatusLabel;
    private ComboBox processList;
    private Button startOverlayButton;
    private Button closeOverlayButton;
    private Button loginGoogleButton;
    private Button logoutButton;
    private Button refreshLibraryButton;
    private Label currentUserLabel;
    private Label loginStatusLabel;
    private Label activeOverlayNameLabel;
    private Label activeOverlayMetaLabel;
    private Label selectedOverlayStatusLabel;
    private Label overlayStateLabel;
    private FlowLayoutPanel libraryListPanel;
    private FlowLayoutPanel fullLibraryListPanel;
    private TextBox librarySearchBox;
    private TextBox fullLibrarySearchBox;
    private ComboBox categoryFilter;
    private ComboBox platformFilter;
    private Panel activePreviewCanvas;
    private Panel homePage;
    private Panel libraryPage;
    private Panel settingsPage;
    private Control homeNavButton;
    private Control libraryNavButton;
    private Control settingsNavButton;

    private string loadedOverlayStorePath;
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
        accessToken = appSettingsService.Current.AccessToken;

        BuildLayout();
        LoadRunningProcesses();
        LoadCachedOverlayCards();
        _ = RestoreLoginAsync();
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

        var gameStatus = CreateGameStatusCard();
        gameStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        gameStatus.Left = 32;
        gameStatus.Top = 108;
        gameStatus.Width = Math.Max(0, content.ClientSize.Width - 64);
        gameStatus.Height = 90;
        content.Controls.Add(gameStatus);

        var activeOverlay = CreateActiveOverlayCard();
        activeOverlay.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        activeOverlay.Left = 32;
        activeOverlay.Top = 222;
        activeOverlay.Width = Math.Max(560, content.ClientSize.Width - 472);
        activeOverlay.Height = Math.Max(430, content.ClientSize.Height - 254);
        content.Controls.Add(activeOverlay);

        var rightColumn = CreateRightColumn();
        rightColumn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        rightColumn.Left = content.ClientSize.Width - 408;
        rightColumn.Top = 222;
        rightColumn.Width = 376;
        rightColumn.Height = Math.Max(430, content.ClientSize.Height - 254);
        content.Controls.Add(rightColumn);

        content.Resize += (s, e) => {
            header.Width = Math.Max(0, content.ClientSize.Width - 64);
            gameStatus.Width = Math.Max(0, content.ClientSize.Width - 64);
            rightColumn.Left = content.ClientSize.Width - rightColumn.Width - 32;
            rightColumn.Height = Math.Max(430, content.ClientSize.Height - rightColumn.Top - 32);
            activeOverlay.Width = Math.Max(560, rightColumn.Left - activeOverlay.Left - 24);
            activeOverlay.Height = Math.Max(430, content.ClientSize.Height - activeOverlay.Top - 32);
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
        var header = CreateHeader("Library", "Browse saved overlays with platform and category filters.");
        header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        header.Left = 32;
        header.Top = 24;
        header.Width = Math.Max(0, content.ClientSize.Width - 64);
        header.Height = 58;
        content.Controls.Add(header);

        var libraryCard = CreateCard();
        libraryCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        libraryCard.Left = 32;
        libraryCard.Top = 108;
        libraryCard.Width = Math.Max(0, content.ClientSize.Width - 64);
        libraryCard.Height = Math.Max(420, content.ClientSize.Height - 140);
        libraryCard.Controls.Add(CreateLabel("My Library", 24, 22, 180, 26, 13f, FontStyle.Bold, TextPrimary));

        fullLibrarySearchBox = new TextBox {
            Left = 24,
            Top = 64,
            Width = 260,
            Height = 34,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };
        fullLibrarySearchBox.PlaceholderTextCompat(SearchPlaceholder);
        fullLibrarySearchBox.TextChanged += (s, e) => PopulateOverlayList(fullLibraryListPanel);
        libraryCard.Controls.Add(fullLibrarySearchBox);

        categoryFilter = CreateComboBox(304, 64, 170);
        categoryFilter.Items.AddRange(new object[] { "All categories", "Local", "Cloud" });
        categoryFilter.SelectedIndex = 0;
        categoryFilter.SelectedIndexChanged += (s, e) => PopulateOverlayList(fullLibraryListPanel);
        libraryCard.Controls.Add(categoryFilter);

        platformFilter = CreateComboBox(492, 64, 150);
        platformFilter.Items.AddRange(new object[] { "All platforms", "Windows" });
        platformFilter.SelectedIndex = 0;
        platformFilter.SelectedIndexChanged += (s, e) => PopulateOverlayList(fullLibraryListPanel);
        libraryCard.Controls.Add(platformFilter);

        fullLibraryListPanel = new FlowLayoutPanel {
            Left = 24,
            Top = 116,
            Width = libraryCard.Width - 48,
            Height = libraryCard.Height - 184,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = CardBackground
        };
        libraryCard.Controls.Add(fullLibraryListPanel);

        var refreshButton = CreateButton("Refresh Library", 24, libraryCard.Height - 52, 180, 36, Color.White, TextPrimary);
        refreshButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        refreshButton.Click += RefreshLibraryButton_Click;
        libraryCard.Controls.Add(refreshButton);

        libraryCard.Resize += (s, e) => {
            fullLibraryListPanel.Width = libraryCard.Width - 48;
            fullLibraryListPanel.Height = Math.Max(160, libraryCard.Height - 184);
            ResizeLibraryRows();
        };

        content.Controls.Add(libraryCard);
        content.Resize += (s, e) => {
            header.Width = Math.Max(0, content.ClientSize.Width - 64);
            libraryCard.Width = Math.Max(0, content.ClientSize.Width - 64);
            libraryCard.Height = Math.Max(420, content.ClientSize.Height - 140);
        };
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

        var hotkeyBox = new TextBox {
            Left = 24,
            Top = 126,
            Width = 180,
            Height = 34,
            Text = "Alt + Shift + S",
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };
        hotkeyCard.Controls.Add(hotkeyBox);
        hotkeyCard.Controls.Add(CreateLabel("Custom key assignment is a UI placeholder until HotkeyManager supports configurable keys.", 224, 126, 260, 40, 8f, FontStyle.Regular, TextMuted));

        content.Controls.Add(accountCard);
        content.Controls.Add(hotkeyCard);
        content.Resize += (s, e) => header.Width = Math.Max(0, content.ClientSize.Width - 64);
    }

    private Control CreateHeader(string title, string subtitle)
    {
        var header = new Panel { BackColor = AppBackground };
        header.Controls.Add(CreateLabel(title, 0, 4, 260, 30, 18f, FontStyle.Bold, TextPrimary));
        header.Controls.Add(CreateLabel(subtitle, 1, 34, 620, 20, 9f, FontStyle.Regular, TextMuted));
        return header;
    }

    private Control CreateGameStatusCard()
    {
        var card = CreateCard();

        var gameIcon = new RoundedPanel {
            Left = 16,
            Top = 16,
            Width = 58,
            Height = 58,
            Radius = 10,
            BackColor = RailBackground,
            BorderColor = RailBackground
        };
        gameIcon.Controls.Add(CreateAccentBar(14, 16, 30, 4, Color.FromArgb(6, 182, 212)));
        gameIcon.Controls.Add(CreateAccentBar(14, 30, 22, 4, PrimaryColor));
        card.Controls.Add(gameIcon);

        card.Controls.Add(CreateLabel("DETECTED GAME", 92, 18, 160, 15, 7.5f, FontStyle.Bold, TextMuted));
        processList = CreateComboBox(92, 38, 250);
        processList.DropDown += (s, e) => LoadRunningProcesses();
        card.Controls.Add(processList);

        overlayStateLabel = CreateStatusPill("Overlay loaded", 360, 32, 110, SuccessBackground, SuccessText);
        card.Controls.Add(overlayStateLabel);
        card.Controls.Add(CreateStatusPill("Windows only", 480, 32, 106, ChipBackground, PrimaryColor));

        closeOverlayButton = CreateButton("Stop Overlay", 706, 26, 128, 42, Color.White, TextPrimary);
        startOverlayButton = CreateButton("Start Overlay", 846, 26, 126, 42, PrimaryColor, Color.White);
        closeOverlayButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        startOverlayButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeOverlayButton.Click += CloseOverlay;
        startOverlayButton.Click += StartOverlay;
        card.Controls.Add(closeOverlayButton);
        card.Controls.Add(startOverlayButton);
        card.Resize += (s, e) => {
            startOverlayButton.Left = card.Width - 142;
            closeOverlayButton.Left = startOverlayButton.Left - 140;
        };

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

        var changeButton = CreateButton("Choose", 486, 22, 86, 36, Color.White, TextPrimary);
        changeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        changeButton.Click += (s, e) => ShowSection(AppSection.Library);
        card.Controls.Add(changeButton);
        card.Resize += (s, e) => changeButton.Left = card.Width - 110;

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

        var codeCard = CreateCard();
        codeCard.Left = 0;
        codeCard.Top = 0;
        codeCard.Width = 376;
        codeCard.Height = 154;
        codeCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        codeCard.Controls.Add(CreateLabel("Load by code", 20, 18, 200, 24, 12f, FontStyle.Bold, TextPrimary));
        codeCard.Controls.Add(CreateLabel("Paste a 6-character shared overlay code.", 20, 44, 300, 18, 8f, FontStyle.Regular, TextMuted));

        overlayCodeTextBox = new TextBox {
            Left = 20,
            Top = 78,
            Width = 214,
            Height = 36,
            BorderStyle = BorderStyle.FixedSingle,
            CharacterCasing = CharacterCasing.Upper,
            MaxLength = 6,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular)
        };
        loadByCodeButton = CreateButton("Load", 246, 76, 110, 38, PrimaryColor, Color.White);
        loadByCodeButton.Click += LoadByCodeButton_Click;
        codeLoadStatusLabel = CreateLabel("Invalid codes show a short status message here.", 20, 122, 330, 16, 7.5f, FontStyle.Regular, TextMuted);
        codeCard.Controls.Add(overlayCodeTextBox);
        codeCard.Controls.Add(loadByCodeButton);
        codeCard.Controls.Add(codeLoadStatusLabel);

        var libraryCard = CreateCard();
        libraryCard.Left = 0;
        libraryCard.Top = 176;
        libraryCard.Width = 376;
        libraryCard.Height = 370;
        libraryCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        libraryCard.Controls.Add(CreateLabel("My Library", 20, 18, 140, 24, 12f, FontStyle.Bold, TextPrimary));
        librarySearchBox = new TextBox {
            Left = 168,
            Top = 16,
            Width = 188,
            Height = 36,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Text = ""
        };
        librarySearchBox.PlaceholderTextCompat(SearchPlaceholder);
        librarySearchBox.TextChanged += (s, e) => PopulateOverlayList(libraryListPanel);
        libraryCard.Controls.Add(librarySearchBox);

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
            librarySearchBox.Left = libraryCard.Width - 208;
            libraryListPanel.Width = libraryCard.Width - 32;
            libraryListPanel.Height = Math.Max(120, libraryCard.Height - 150);
            refreshLibraryButton.Top = libraryCard.Height - 64;
            refreshLibraryButton.Width = libraryCard.Width - 32;
            ResizeLibraryRows();
        };

        column.Controls.Add(codeCard);
        column.Controls.Add(libraryCard);
        return column;
    }

    private void ResizeRightColumn(Control rightColumn)
    {
        foreach (Control child in rightColumn.Controls) {
            child.Width = rightColumn.Width;
            if (child.Top > 0) {
                child.Height = Math.Max(260, rightColumn.Height - child.Top);
            }
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
        await LoadLibraryAsync();
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
            foreach (var item in resp.Data) {
                if (item?.Overlay == null) {
                    continue;
                }

                overlayItems.Add(OverlaySelectionItem.FromLibrary(item, settings.ServerBaseUrl));
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
                overlayStateLabel.Text = "Overlay loaded";
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

        if (!string.IsNullOrWhiteSpace(overlay.Platform)
            && !string.Equals(overlay.Platform.Trim(), "windows", StringComparison.OrdinalIgnoreCase)) {
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

    private async Task ApplyLibraryOverlayAsync(OverlaySelectionItem item)
    {
        if (item == null || item.OverlayDatabaseId <= 0) {
            return;
        }

        if (string.IsNullOrWhiteSpace(accessToken)) {
            UpdateSelectedOverlayUi("Login is required.");
            return;
        }

        UpdateSelectedOverlayUi("Applying overlay...");
        var settings = appSettingsService.Current ?? appSettingsService.LoadOrCreate();

        using (var api = new MspApiClient(settings.ServerBaseUrl))
        {
            var resp = await api.GetOverlayDetailAsync(item.OverlayDatabaseId, accessToken).ConfigureAwait(true);
            if (resp == null || !resp.Success || resp.Data == null) {
                UpdateSelectedOverlayUi(resp?.Message ?? "Overlay detail request failed.");
                return;
            }

            try {
                ApplyOverlayResponse(resp.Data, item.Code);
                AddOrSelectLoadedOverlay(resp.Data);
                UpdateSelectedOverlayUi("Selected: " + item.DisplayName);
                overlayStateLabel.Text = "Overlay loaded";
            }
            catch (Exception ex) {
                UpdateSelectedOverlayUi(BuildFriendlyApplyError(ex));
                ErrorLogger.LogError("E221", "Library overlay apply failed: " + ex);
            }
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

                string[] parts = raw.Split(new[] { '|' }, 3);
                if (parts.Length < 2) {
                    continue;
                }

                overlayItems.Add(new OverlaySelectionItem {
                    Source = OverlaySource.LocalCache,
                    Code = Unescape(parts[0]),
                    DisplayName = string.IsNullOrWhiteSpace(Unescape(parts[1])) ? "(Untitled)" : Unescape(parts[1]),
                    OverlayId = parts.Length > 2 ? Unescape(parts[2]) : null
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
        if (overlay == null) {
            return;
        }

        string code = string.IsNullOrWhiteSpace(overlay.Code) ? "UNKNOWN" : overlay.Code.Trim().ToUpperInvariant();
        string title = string.IsNullOrWhiteSpace(overlay.Name) ? "(Untitled)" : overlay.Name.Trim();

        overlayItems.RemoveAll(item => item.Source == OverlaySource.LocalCache && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

        var loadedItem = new OverlaySelectionItem {
            Source = OverlaySource.LocalCache,
            Code = code,
            DisplayName = title,
            OverlayId = overlay.OverlayId
        };

        overlayItems.Insert(0, loadedItem);
        SaveLoadedOverlaysToStore();
        SelectOverlayItem(loadedItem);
        RenderOverlayPreviewCards();
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

                lines.Add(Escape(item.Code) + "|" + Escape(item.DisplayName) + "|" + Escape(item.OverlayId));
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
        PopulateOverlayList(fullLibraryListPanel);
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

        var items = GetFilteredItemsForPanel(panel);

        if (items.Count == 0) {
            if (overlayItems.Count == 0) {
                panel.Controls.Add(CreateLibraryRowShell("No overlays yet", "Load a code or refresh library.", null, false));
            }
            else {
                panel.Controls.Add(CreateLibraryRowShell("No matching overlays", "Try a different search or filter.", null, false));
            }
        }
        else {
            foreach (var item in items) {
                panel.Controls.Add(CreateLibraryRow(item));
            }
        }

        panel.ResumeLayout();
        ResizeLibraryRows(panel);
    }

    private List<OverlaySelectionItem> GetFilteredItemsForPanel(FlowLayoutPanel panel)
    {
        if (panel == fullLibraryListPanel) {
            return FilterOverlayItems(
                fullLibrarySearchBox?.Text,
                categoryFilter?.SelectedItem as string,
                platformFilter?.SelectedItem as string);
        }

        if (panel == libraryListPanel) {
            return FilterOverlayItems(librarySearchBox?.Text, null, null);
        }

        return overlayItems.ToList();
    }

    private List<OverlaySelectionItem> FilterOverlayItems(string search, string category, string platform)
    {
        IEnumerable<OverlaySelectionItem> items = overlayItems;

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

        if (string.Equals(platform, "Windows", StringComparison.Ordinal)) {
            items = items.Where(i => string.IsNullOrWhiteSpace(i.Platform)
                || string.Equals(i.Platform.Trim(), "windows", StringComparison.OrdinalIgnoreCase));
        }

        return items.ToList();
    }

    private Control CreateLibraryRow(OverlaySelectionItem item)
    {
        var selected = ReferenceEquals(item, selectedOverlayItem);
        var row = CreateLibraryRowShell(item.DisplayName, item.BuildShortMetaText(), item, selected);
        row.Click += async (s, e) => await SelectAndApplyOverlayItemAsync(item);

        foreach (Control control in row.Controls) {
            control.Click += async (s, e) => await SelectAndApplyOverlayItemAsync(item);
        }

        return row;
    }

    private RoundedPanel CreateLibraryRowShell(string title, string meta, OverlaySelectionItem item, bool selected)
    {
        var row = new RoundedPanel {
            Width = 344,
            Height = 66,
            Radius = 9,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = selected ? SelectedBackground : CardBackground,
            BorderColor = selected ? Color.FromArgb(147, 197, 253) : BorderColor,
            Cursor = item == null ? Cursors.Default : Cursors.Hand,
            Tag = item
        };

        var preview = new RoundedPanel {
            Left = 12,
            Top = 10,
            Width = 70,
            Height = 46,
            Radius = 6,
            BackColor = item != null && item.Source == OverlaySource.Library ? Color.FromArgb(219, 234, 254) : Color.FromArgb(224, 242, 254),
            BorderColor = Color.Transparent
        };
        preview.Tag = item;
        preview.Paint += DrawMiniPreview;
        row.Controls.Add(preview);

        row.Controls.Add(CreateLabel(title, 94, 10, 168, 18, 8.5f, FontStyle.Bold, TextPrimary));
        row.Controls.Add(CreateLabel(meta, 94, 31, 168, 14, 7.5f, FontStyle.Regular, TextMuted));

        if (selected) {
            row.Controls.Add(CreateStatusPill("ACTIVE", 270, 21, 60, PrimaryColor, Color.White));
        }

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
            overlayStateLabel.Text = "Overlay loaded";
        }
        catch (Exception ex) {
            UpdateSelectedOverlayUi(BuildFriendlyApplyError(ex));
            ErrorLogger.LogError("E221", "Local overlay apply failed: " + ex.Message);
        }
    }

    private void ResizeLibraryRows()
    {
        ResizeLibraryRows(libraryListPanel);
        ResizeLibraryRows(fullLibraryListPanel);
    }

    private void ResizeLibraryRows(FlowLayoutPanel panel)
    {
        if (panel == null) {
            return;
        }

        foreach (Control control in panel.Controls) {
            control.Width = Math.Max(260, panel.ClientSize.Width - 4);
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

    private void StartOverlay(object sender, EventArgs e)
    {
        if (processList.SelectedItem == null) {
            UpdateSelectedOverlayUi("Choose a game window first.");
            return;
        }

        var selectedProcess = (ProcessItem)processList.SelectedItem;
        OverlayForm.ShowOverlay(selectedProcess.ProcessName);
        overlayStateLabel.Text = "Overlay running";
    }

    private void CloseOverlay(object sender, EventArgs e)
    {
        OverlayForm.Instance?.Close();
        overlayStateLabel.Text = selectedOverlayItem == null ? "No overlay" : "Overlay loaded";
    }

    private void UpdateAuthUi()
    {
        bool loggedIn = currentUser != null && !string.IsNullOrWhiteSpace(accessToken);
        currentUserLabel.Text = loggedIn ? TrimText(currentUser.Name, 12) : "Guest";
        loginStatusLabel.Text = loggedIn ? "Signed in" : "Signed out";
        loginGoogleButton.Visible = !loggedIn;
        logoutButton.Visible = loggedIn;
        refreshLibraryButton.Enabled = loggedIn;
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
            if (overlayStateLabel != null) {
                overlayStateLabel.Text = "No overlay";
            }
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

        if (activeOverlayDocument != null) {
            Renderer.DrawOverlayDocument(g, bounds, activeOverlayDocument);
            return;
        }

        DrawActivePreviewPlaceholder(g, bounds);
    }

    private void DrawActivePreviewPlaceholder(Graphics g, Rectangle bounds)
    {
        using (var primary = new SolidBrush(PrimaryColor))
        using (var cyan = new SolidBrush(Color.FromArgb(6, 182, 212)))
        using (var marker = new SolidBrush(Color.FromArgb(217, 70, 239)))
        {
            g.FillRoundedRectangle(primary, bounds.Left + bounds.Width / 5, bounds.Top + 48, bounds.Width / 3, 6, 3);
            g.FillRoundedRectangle(cyan, bounds.Left + bounds.Width * 3 / 5, bounds.Top + 54, bounds.Width / 5, 30, 4);
            g.FillRoundedRectangle(marker, bounds.Left + 36, bounds.Top + bounds.Height / 2 - 7, 14, 14, 4);
            g.FillRoundedRectangle(marker, bounds.Left + bounds.Width / 2 - 7, bounds.Top + bounds.Height / 2 - 7, 14, 14, 4);
            g.FillRoundedRectangle(marker, bounds.Right - 54, bounds.Top + bounds.Height / 2 - 7, 14, 14, 4);
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
        return new ComboBox {
            Left = left,
            Top = top,
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
    }

    private Button CreateButton(string text, int left, int top, int width, int height, Color backColor, Color foreColor)
    {
        var button = new Button {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = backColor == Color.White ? BorderColor : backColor;
        button.FlatAppearance.MouseOverBackColor = backColor == PrimaryColor ? PrimaryHoverColor : Color.FromArgb(248, 250, 252);
        return button;
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

    private sealed class PillLabel : Label
    {
        public int Radius { get; set; } = 12;

        public PillLabel()
        {
            AutoSize = false;
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
