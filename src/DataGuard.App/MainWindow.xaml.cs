using System.ComponentModel;
using System.Windows;
using DataGuard.App.ViewModels;
using DataGuard.Core.Models;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DataGuard.App;

/// <summary>
/// 메인 창 + 트레이 상주 동작(PRD 상주형 A).
/// 닫기/최소화 시 종료하지 않고 트레이로 숨고, 실제 종료는 트레이 메뉴에서만.
/// (트레이 아이콘은 WPF에 없어 WinForms NotifyIcon을 사용 — 타입은 전부 정규화해 모호성 방지.)
/// </summary>
public partial class MainWindow : Window
{
    private readonly WinForms.NotifyIcon _trayIcon;
    private bool _exitRequested;

    /// <summary>true면 시작하자마자 트레이로 숨긴다(Windows 자동 실행 시).</summary>
    public bool StartMinimizedToTray { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Visible = true,
            Text = "DataGuard — DB 정합성 자동 체크"
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("열기", null, (_, _) => ShowFromTray());
        menu.Items.Add("종료", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CheckCompleted += OnCheckCompleted;
        }

        if (StartMinimizedToTray)
        {
            HideToTray();
        }
    }

    // 최소화하면 작업표시줄 대신 트레이로 숨긴다.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    // 창의 X 버튼은 종료가 아니라 트레이로 보낸다(실제 종료는 트레이 메뉴 '종료').
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon.BalloonTipTitle = "DataGuard";
        _trayIcon.BalloonTipText = "백그라운드에서 계속 실행됩니다. 트레이 아이콘을 더블클릭하면 다시 열립니다.";
        _trayIcon.ShowBalloonTip(2000);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _exitRequested = true;
        Close();
    }

    // 자동/수동 체크 결과 중 이상·오류만 트레이 풍선으로 알린다(정상은 조용히).
    private void OnCheckCompleted(CheckResult result)
    {
        if (result.Status == CheckStatus.Normal)
        {
            return;
        }

        _trayIcon.BalloonTipTitle = "DataGuard 정합성 경고";
        _trayIcon.BalloonTipText = result.Status == CheckStatus.Anomaly
            ? $"{result.QueryName}: 이상 {result.RowCount}건 발견"
            : $"{result.QueryName}: 실행 오류";
        _trayIcon.ShowBalloonTip(3000);
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosed(e);
    }
}
