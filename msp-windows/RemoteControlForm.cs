using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using msp_windows.Api;
using msp_windows.Settings;
using msp_windows.Overlay;

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

    private Button selectColorButton;
    private FlowLayoutPanel palettePanel; // 저장된 색상 스와치를 보여줄 영역
    private TextBox errorLogTextBox;

    private List<Color> colorPalette = new List<Color>();
    private const int MaxPaletteColors = 10;

    private static readonly Regex OverlayCodeRegex = new Regex("^[A-Z0-9]{6}$", RegexOptions.Compiled);

    private readonly AppSettingsService appSettingsService = new AppSettingsService();
    private readonly OverlayJsonParser overlayJsonParser = new OverlayJsonParser();
    private readonly OverlayApplyService overlayApplyService = new OverlayApplyService();
    private readonly OverlayCacheService overlayCacheService = new OverlayCacheService();
    private string lastLoadedOverlayJson;

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

        selectColorButton = new Button() { Text = "색상 선택", Top = 200, Left = 20, Width = 320 };
        selectColorButton.Click += SelectColorButton_Click;

        palettePanel = new FlowLayoutPanel();
        palettePanel.Location = new Point(20, 240);
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

            lastLoadedOverlayJson = resp.Data.OverlayJson;

            if (string.IsNullOrWhiteSpace(lastLoadedOverlayJson)) {
                string msg = "overlayJson is missing in server response.";
                codeLoadStatusLabel.Text = msg;
                ErrorLogger.LogError("E211", msg);
                return;
            }

            try {
                var document = overlayJsonParser.Parse(lastLoadedOverlayJson);
                overlayApplyService.Apply(document);
                overlayCacheService.SaveOverlayJson(document.OverlayId, lastLoadedOverlayJson);
                appSettingsService.UpdateLastSelectedOverlayId(document.OverlayId);
            }
            catch (Exception ex) {
                codeLoadStatusLabel.Text = ex.Message;
                ErrorLogger.LogError("E212", ex.ToString());
                return;
            }

            if (!string.IsNullOrWhiteSpace(resp.Data.OverlayId)) {
                appSettingsService.UpdateLastSelectedOverlayId(resp.Data.OverlayId);
            }

            codeLoadStatusLabel.Text = $"Overlay loaded: {resp.Data.Name}";
        }
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
}
