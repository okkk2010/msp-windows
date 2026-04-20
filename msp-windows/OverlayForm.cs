using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using msp_windows.Overlay.Models;

public class OverlayForm : Form
{
    private static OverlayForm instance;
    public static OverlayForm Instance => instance;

    public static OverlayDocument CurrentOverlayDocument { get; set; }

    // 전역 색상 프로퍼티 (기본 빨간색)
    public static Color SelectedOverlayColor { get; set; } = Color.FromArgb( 255, 192, 0, 0 );


    private string targetProcessName;
    private System.Windows.Forms.Timer windowTrackerTimer;
    private HotkeyManager hotkeyManager;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x00000008;

    [DllImport( "user32.dll" )]
    private static extern int SetWindowLong ( IntPtr hWnd, int nIndex, int dwNewLong );

    [DllImport( "user32.dll" )]
    private static extern int GetWindowLong ( IntPtr hWnd, int nIndex );

    public static void ShowOverlay ( string processName )
    {
        instance?.Close(); // 기존 오버레이가 있으면 종료
        instance = new OverlayForm( processName );
        instance.Show();
    }

    private OverlayForm ( string processName )
    {
        targetProcessName = processName;
        instance = this;

        // 투명 배경 설정
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;

        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.BackColor = Color.Lime; // 또는 다른 색
        this.TransparencyKey = this.BackColor;


        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;

        // 더블 버퍼링 (깜빡임 방지)
        this.SetStyle( ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true );

        windowTrackerTimer = new System.Windows.Forms.Timer { Interval = 30 };
        windowTrackerTimer.Tick += TrackGameWindow;
        windowTrackerTimer.Start();
    }

    protected override CreateParams CreateParams {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: 클릭 무시
            cp.ExStyle |= 0x80000; // WS_EX_LAYERED: 레이어드 창 (투명 지원)
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
        SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST);

        hotkeyManager = new HotkeyManager(this.Handle, () => this.Close());
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // 배경 그리기 생략 (TransparencyKey 사용)
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(this.BackColor);
        if (CurrentOverlayDocument != null) {
            Renderer.DrawOverlayDocument(e.Graphics, this.ClientRectangle, CurrentOverlayDocument);
        }
        else {
            // 선택한 색상(SelectedOverlayColor)을 사용하여 UI를 그림
            Renderer.DrawOverlayUI(e.Graphics, this.ClientRectangle);
        }
    }

    private void TrackGameWindow(object sender, EventArgs e)
    {
        Rectangle gameBounds = WindowTracker.GetGameWindowBounds(targetProcessName);

        if (gameBounds.IsEmpty) {
            this.Hide();
        } else {
            this.Show();
            this.Bounds = gameBounds;
            this.Invalidate();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        windowTrackerTimer?.Stop();
        hotkeyManager?.Dispose();
        instance = null;
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (hotkeyManager != null && hotkeyManager.HandleHotkeyMessage(m)) {
            return;
        }
        base.WndProc(ref m);
    }
}
