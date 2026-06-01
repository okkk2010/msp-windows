using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using msp_windows.Api;
using msp_windows.Overlay;
using msp_windows.Settings;

public class ProcessItem
{
    public string ProcessName { get; set; }   // 실제 프로세스 이름 (코드에서 사용)
    public string WindowTitle { get; set; }     // 상태표시줄에 나오는 타이틀

    public override string ToString() => WindowTitle;
}

public class RemoteControlForm : Form
{
    private ComboBox processList;
    private Button startOverlayButton;
    private Button closeOverlayButton;

    private TextBox overlayCodeTextBox;
    private Button loadByCodeButton;
    private Label codeLoadStatusLabel;
    private ComboBox loadedOverlayList;

    private Button selectColorButton;
    private FlowLayoutPanel palettePanel; // 저장된 색상 스와치를 보여줄 영역
    private TextBox errorLogTextBox;

    private List<Color> colorPalette = new List<Color>();
    private const int MaxPaletteColors = 10;

    private static readonly Regex OverlayCodeRegex = new Regex("^[A-Z0-9]{6}$", RegexOptions.Compiled);

    private readonly AppSettingsService appSettingsService = new AppSettingsService();
    private readonly OverlayCacheService overlayCacheService = new OverlayCacheService();
    private string loadedOverlayStorePath;

    public RemoteControlForm()
    {
        Text = "Oculo Remote Control";
        Size = new Size(370, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;

        Label processLabel = new Label() { Text = "게임 프로세스 선택:", Top = 20, Left = 20, Width = 320 };
        processList = new ComboBox() {
            Top = 50,
            Left = 20,
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        processList.DropDown += (s, e) => LoadRunningProcesses();

        startOverlayButton = new Button() { Text = "오버레이 시작", Top = 80, Left = 20, Width = 320 };
        closeOverlayButton = new Button() { Text = "오버레이 종료", Top = 110, Left = 20, Width = 320 };

        overlayCodeTextBox = new TextBox() { Top = 140, Left = 20, Width = 210 };
        loadByCodeButton = new Button() { Text = "Code Load", Top = 140, Left = 240, Width = 100 };
        loadByCodeButton.Click += LoadByCodeButton_Click;

        codeLoadStatusLabel = new Label() { Text = "", Top = 170, Left = 20, Width = 320, Height = 20 };

        Label loadedOverlayLabel = new Label() { Text = "불러온 오버레이 (Code | Title)", Top = 195, Left = 20, Width = 320 };
        loadedOverlayList = new ComboBox() {
            Top = 220,
            Left = 20,
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        loadedOverlayList.DropDown += (s, e) => LoadLoadedOverlaysFromStore();
        loadedOverlayList.SelectedIndexChanged += LoadedOverlayList_SelectedIndexChanged;

        selectColorButton = new Button() { Text = "색상 선택", Top = 255, Left = 20, Width = 320 };
        selectColorButton.Click += SelectColorButton_Click;

        palettePanel = new FlowLayoutPanel();
        palettePanel.Location = new Point(20, 295);
        palettePanel.Size = new Size(320, 260); // 5개 x 64px(스와치+라벨+마진) = 320px, 2줄(128~260px)
        palettePanel.BorderStyle = BorderStyle.FixedSingle;
        palettePanel.FlowDirection = FlowDirection.LeftToRight;
        palettePanel.WrapContents = true;

        errorLogTextBox = new TextBox() {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Location = new Point(20, 510),
            Size = new Size(320, 50)
        };

        startOverlayButton.Click += StartOverlay;
        closeOverlayButton.Click += CloseOverlay;

        Controls.Add(processLabel);
        Controls.Add(processList);
        Controls.Add(startOverlayButton);
        Controls.Add(closeOverlayButton);
        Controls.Add(overlayCodeTextBox);
        Controls.Add(loadByCodeButton);
        Controls.Add(codeLoadStatusLabel);
        Controls.Add(loadedOverlayLabel);
        Controls.Add(loadedOverlayList);
        Controls.Add(selectColorButton);
        Controls.Add(palettePanel);
        Controls.Add(errorLogTextBox);

        LoadRunningProcesses();
        LoadColorPalette();

        // 오류 로그 이벤트 구독 (ErrorLogger는 기존 코드에 있음)
        ErrorLogger.OnErrorLogged += (logMessage) => {
            errorLogTextBox.Invoke(new Action(() => {
                errorLogTextBox.AppendText(logMessage + Environment.NewLine);
            }));
        };

        // 프로그램 시작 시 저장된 오버레이 색상 로드
        ApplyOverlayColor(SettingsManager.LoadOverlayColor());

        // settings.json 로드/생성 (없으면 기본값으로 생성)
        appSettingsService.LoadOrCreate();
        loadedOverlayStorePath = Path.Combine(appSettingsService.SettingsDirectoryPath, "loaded-overlays.txt");
        LoadLoadedOverlaysFromStore();
    }

    private async void LoadByCodeButton_Click(object sender, EventArgs e)
    {
        await LoadByCodeAsync();
    }

    private async Task LoadByCodeAsync()
    {
        string code = (overlayCodeTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code)) {
            codeLoadStatusLabel.Text = "올바른 6자리 오버레이 코드를 입력하세요.";
            return;
        }

        if (!OverlayCodeRegex.IsMatch(code)) {
            codeLoadStatusLabel.Text = "Invalid overlay code.";
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

            AddOrSelectLoadedOverlay(resp.Data);

            try {
                if (!string.IsNullOrWhiteSpace(resp.Data.OverlayJson)) {
                    // 1. JSON 캐시에 저장
                    overlayCacheService.SaveOverlayJson(resp.Data.Code, resp.Data.OverlayJson);

                    // 2. JSON 파싱
                    var parser = new OverlayJsonParser();
                    var doc = parser.Parse(resp.Data.OverlayJson);

                    // 3. 오버레이 적용
                    var applyService = new OverlayApplyService();
                    applyService.Apply(doc);

                    codeLoadStatusLabel.Text = $"오버레이 로드 및 적용 완료: {resp.Data.Code} | {doc.Name}";
                } else {
                    codeLoadStatusLabel.Text = $"Overlay selected: {resp.Data.Code} | {resp.Data.Name} (JSON 없음)";
                }
            }
            catch (Exception ex) {
                ErrorLogger.LogError("E220", "오버레이 파싱 또는 적용 실패: " + ex.Message);
                codeLoadStatusLabel.Text = "오버레이 적용 중 오류가 발생했습니다.";
            }
        }
    }

    private void LoadedOverlayList_SelectedIndexChanged(object sender, EventArgs e)
    {
        var item = loadedOverlayList.SelectedItem as LoadedOverlayItem;
        if (item == null || string.IsNullOrWhiteSpace(item.Code)) return;

        try {
            string json = overlayCacheService.TryLoadOverlayJson(item.Code);
            if (!string.IsNullOrWhiteSpace(json)) {
                var parser = new OverlayJsonParser();
                var doc = parser.Parse(json);

                var applyService = new OverlayApplyService();
                applyService.Apply(doc);
            }
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E221", "로컬 오버레이 파싱 또는 적용 실패: " + ex.Message);
        }
    }

    private void AddOrSelectLoadedOverlay(msp_windows.Api.Dtos.OverlayDetailResponse overlay)
    {
        if (overlay == null) {
            return;
        }

        string code = string.IsNullOrWhiteSpace(overlay.Code) ? "UNKNOWN" : overlay.Code.Trim().ToUpperInvariant();
        string title = string.IsNullOrWhiteSpace(overlay.Name) ? "(제목 없음)" : overlay.Name.Trim();

        for (int i = 0; i < loadedOverlayList.Items.Count; i++) {
            LoadedOverlayItem existing = loadedOverlayList.Items[i] as LoadedOverlayItem;
            if (existing == null) {
                continue;
            }

            if (string.Equals(existing.Code, code, StringComparison.OrdinalIgnoreCase)) {
                existing.Title = title;
                existing.OverlayId = overlay.OverlayId;
                loadedOverlayList.Items[i] = existing;
                loadedOverlayList.SelectedIndex = i;
                SaveLoadedOverlaysToStore();
                return;
            }
        }

        LoadedOverlayItem item = new LoadedOverlayItem {
            Code = code,
            Title = title,
            OverlayId = overlay.OverlayId
        };

        loadedOverlayList.Items.Add(item);
        loadedOverlayList.SelectedIndex = loadedOverlayList.Items.Count - 1;
        SaveLoadedOverlaysToStore();
    }

    private void LoadLoadedOverlaysFromStore()
    {
        loadedOverlayList.Items.Clear();

        if (string.IsNullOrWhiteSpace(loadedOverlayStorePath) || !File.Exists(loadedOverlayStorePath)) {
            return;
        }

        try {
            string[] lines = File.ReadAllLines(loadedOverlayStorePath);
            foreach (string raw in lines) {
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }

                string[] parts = raw.Split(new[] { '|' }, 3);
                if (parts.Length < 2) {
                    continue;
                }

                LoadedOverlayItem item = new LoadedOverlayItem {
                    Code = Unescape(parts[0]),
                    Title = Unescape(parts[1]),
                    OverlayId = parts.Length > 2 ? Unescape(parts[2]) : null
                };

                if (string.IsNullOrWhiteSpace(item.Code)) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Title)) {
                    item.Title = "(제목 없음)";
                }

                AddLoadedOverlayListItemIfMissing(item);
            }

            MergeLoadedOverlaysFromCacheDirectory();

            if (loadedOverlayList.Items.Count > 0 && loadedOverlayList.SelectedIndex < 0) {
                loadedOverlayList.SelectedIndex = 0;
            }
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E213", "불러온 오버레이 목록 로드 실패: " + ex.Message);
        }
    }

    private void SaveLoadedOverlaysToStore()
    {
        if (string.IsNullOrWhiteSpace(loadedOverlayStorePath)) {
            return;
        }

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(loadedOverlayStorePath));

            List<string> lines = new List<string>();
            foreach (object obj in loadedOverlayList.Items) {
                LoadedOverlayItem item = obj as LoadedOverlayItem;
                if (item == null || string.IsNullOrWhiteSpace(item.Code)) {
                    continue;
                }

                string line = Escape(item.Code) + "|" + Escape(item.Title) + "|" + Escape(item.OverlayId);
                lines.Add(line);
            }

            File.WriteAllLines(loadedOverlayStorePath, lines.ToArray());
        }
        catch (Exception ex) {
            ErrorLogger.LogError("E214", "불러온 오버레이 목록 저장 실패: " + ex.Message);
        }
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

    private void MergeLoadedOverlaysFromCacheDirectory()
    {
        string cacheRoot = overlayCacheService.RootCacheDirectoryPath;
        if (!Directory.Exists(cacheRoot)) {
            return;
        }

        string[] overlayFiles = Directory.GetFiles(cacheRoot, "*.json");
        foreach (string filePath in overlayFiles) {
            string fileNameCode = Path.GetFileNameWithoutExtension(filePath);

            string code = fileNameCode;
            string title = "(캐시 오버레이)";

            try {
                string json = File.ReadAllText(filePath);
                string parsedCode = TryExtractJsonStringValue(json, "code", "overlayCode");
                string parsedTitle = TryExtractJsonStringValue(json, "name", "title", "overlayName");

                if (!string.IsNullOrWhiteSpace(parsedCode)) {
                    code = parsedCode.Trim().ToUpperInvariant();
                }

                if (!string.IsNullOrWhiteSpace(parsedTitle)) {
                    title = parsedTitle.Trim();
                }
            }
            catch {
            }

            AddLoadedOverlayListItemIfMissing(new LoadedOverlayItem {
                Code = code,
                Title = title,
                OverlayId = code
            });
        }
    }

    private string TryExtractJsonStringValue(string json, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(json) || keys == null || keys.Length == 0) {
            return null;
        }

        foreach (string key in keys) {
            string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<value>[^\"]*)\"";
            Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (match.Success) {
                return match.Groups["value"].Value;
            }
        }

        return null;
    }

    private void AddLoadedOverlayListItemIfMissing(LoadedOverlayItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Code)) {
            return;
        }

        for (int i = 0; i < loadedOverlayList.Items.Count; i++) {
            LoadedOverlayItem existing = loadedOverlayList.Items[i] as LoadedOverlayItem;
            if (existing == null) {
                continue;
            }

            if (string.Equals(existing.Code, item.Code, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
        }

        loadedOverlayList.Items.Add(item);
    }

    private void LoadRunningProcesses()
    {
        processList.Items.Clear();
        foreach (Process process in Process.GetProcesses()) {
            if (!string.IsNullOrEmpty(process.MainWindowTitle)) {
                ProcessItem item = new ProcessItem {
                    ProcessName = process.ProcessName,
                    WindowTitle = process.MainWindowTitle
                };
                processList.Items.Add(item);
            }
        }
    }

    private void LoadColorPalette()
    {
        colorPalette = SettingsManager.LoadColorPalette();
        // Limit to max 10 colors
        if (colorPalette.Count > MaxPaletteColors)
            colorPalette = colorPalette.GetRange(0, MaxPaletteColors);
        UpdatePaletteUI();
    }

    private string ToHex(Color c)
    {
        // Return hex string like #AARRGGBB
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private void UpdatePaletteUI()
    {
        palettePanel.Controls.Clear();

        int swatchWidth = 48;
        int swatchHeight = 48;
        int labelHeight = 24;
        int containerWidth = 64; // 스와치+라벨+마진 넓게
        int containerHeight = swatchHeight + labelHeight + 6;

        int shownCount = 0;
        foreach (var color in colorPalette) {
            if (shownCount >= MaxPaletteColors) break;
            shownCount++;

            Panel container = new Panel();
            container.Size = new Size(containerWidth, containerHeight);
            container.Margin = new Padding(5);
            container.BackColor = SystemColors.Control;

            Panel colorBox = new Panel();
            colorBox.BackColor = color;
            colorBox.Size = new Size(swatchWidth, swatchHeight);
            colorBox.Location = new Point((containerWidth-swatchWidth)/2, 0); // 가운데 정렬
            colorBox.BorderStyle = BorderStyle.FixedSingle;
            colorBox.Cursor = Cursors.Hand;
            colorBox.Click += (s, e) => {
                ApplyOverlayColor(color);
            };

            // Context menu for delete
            ContextMenuStrip cms = new ContextMenuStrip();
            var deleteItem = new ToolStripMenuItem("삭제");
            deleteItem.Click += (s, e) => {
                var result = MessageBox.Show("색상을 삭제하시겠습니까?", "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) {
                    colorPalette.Remove(color);
                    SettingsManager.SaveColorPalette(colorPalette);
                    UpdatePaletteUI();
                }
            };
            cms.Items.Add(deleteItem);
            colorBox.ContextMenuStrip = cms;

            Label hexLabel = new Label();
            hexLabel.Text = ToHex(color);
            hexLabel.Font = new Font(FontFamily.GenericSansSerif, 9f);
            hexLabel.AutoSize = false;
            hexLabel.TextAlign = ContentAlignment.MiddleCenter;
            hexLabel.Size = new Size(containerWidth, labelHeight);
            hexLabel.Location = new Point(0, swatchHeight + 2);

            container.Controls.Add(colorBox);
            container.Controls.Add(hexLabel);

            palettePanel.Controls.Add(container);
        }
    }

    private void SelectColorButton_Click(object sender, EventArgs e)
    {
        if (colorPalette.Count >= MaxPaletteColors) {
            MessageBox.Show($"팔레트에는 최대 {MaxPaletteColors}개의 색상만 저장할 수 있습니다.", "팔레트 가득참", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using (ColorDialog dlg = new ColorDialog()) {
            if (dlg.ShowDialog() == DialogResult.OK) {
                // 선택한 색상을 오버레이에 적용
                ApplyOverlayColor(dlg.Color);

                // 선택한 색상이 팔레트에 없으면 추가
                if (!colorPalette.Contains(dlg.Color)) {
                    if (colorPalette.Count < MaxPaletteColors) {
                        colorPalette.Add(dlg.Color);
                        SettingsManager.SaveColorPalette(colorPalette);
                        UpdatePaletteUI();
                    }
                }
            }
        }
    }

    private void StartOverlay(object sender, EventArgs e)
    {
        if (processList.SelectedItem != null) {
            ProcessItem selectedProcess = (ProcessItem)processList.SelectedItem;
            OverlayForm.ShowOverlay(selectedProcess.ProcessName);
        }
    }

    private void CloseOverlay(object sender, EventArgs e)
    {
        OverlayForm.Instance?.Close();
    }

    private void ApplyOverlayColor(Color color)
    {
        SettingsManager.SaveOverlayColor(color);
        OverlayForm.SelectedOverlayColor = color;
        OverlayForm.Instance?.Invalidate();
    }

    private sealed class LoadedOverlayItem
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string OverlayId { get; set; }

        public override string ToString()
        {
            return $"{Code} | {Title}";
        }
    }
}
