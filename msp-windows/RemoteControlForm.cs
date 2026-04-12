using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

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
    private Button selectColorButton;
    private FlowLayoutPanel palettePanel; // 저장된 색상 스와치를 보여줄 영역
    private TextBox errorLogTextBox;

    private List<Color> colorPalette = new List<Color>();
    private const int MaxPaletteColors = 10;

    public RemoteControlForm()
    {
        Text = "Oculo Remote Control";
        Size = new Size(370, 520); // 5개에 맞게 창 크기
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

        selectColorButton = new Button() { Text = "색상 선택", Top = 140, Left = 20, Width = 320 };
        selectColorButton.Click += SelectColorButton_Click;

        palettePanel = new FlowLayoutPanel();
        palettePanel.Location = new Point(20, 180);
        palettePanel.Size = new Size(320, 260); // 5개 x 64px(스와치+라벨+마진) = 320px, 2줄(128~260px)
        palettePanel.BorderStyle = BorderStyle.FixedSingle;
        palettePanel.FlowDirection = FlowDirection.LeftToRight;
        palettePanel.WrapContents = true;

        errorLogTextBox = new TextBox() {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Location = new Point(20, 450),
            Size = new Size(320, 50)
        };

        startOverlayButton.Click += StartOverlay;
        closeOverlayButton.Click += CloseOverlay;

        Controls.Add(processLabel);
        Controls.Add(processList);
        Controls.Add(startOverlayButton);
        Controls.Add(closeOverlayButton);
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
