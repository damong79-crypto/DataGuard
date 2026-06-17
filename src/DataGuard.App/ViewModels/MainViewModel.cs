using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataGuard.App.Views;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;
using DataGuard.Core.Services;

namespace DataGuard.App.ViewModels;

/// <summary>
/// 메인 화면 ViewModel. 설정(연결·쿼리)을 로드하고, 등록 다이얼로그를 띄우며,
/// "지금 실행"으로 CheckRunner를 호출해 MVP 흐름을 완성한다.
/// (소규모 앱이라 다이얼로그를 VM에서 직접 연다 — 규모가 커지면 다이얼로그 서비스로 분리 권장.)
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly CheckRunner _checkRunner;
    private readonly IAppConfigStore _configStore;
    private readonly ICredentialStore _credentials;
    private readonly ICheckHistoryRepository _history;
    private readonly AppConfig _config;

    public ObservableCollection<DbConnectionInfo> Connections { get; } = new();
    public ObservableCollection<CheckQuery> Queries { get; } = new();
    public ObservableCollection<CheckResult> RecentResults { get; } = new();

    [ObservableProperty]
    private CheckQuery? _selectedQuery;

    [ObservableProperty]
    private string _statusMessage = "준비됨";

    [ObservableProperty]
    private string _smtpSummary = string.Empty;

    public MainViewModel(
        CheckRunner checkRunner,
        IAppConfigStore configStore,
        ICredentialStore credentials,
        ICheckHistoryRepository history)
    {
        _checkRunner = checkRunner;
        _configStore = configStore;
        _credentials = credentials;
        _history = history;

        _config = _configStore.Load();
        foreach (DbConnectionInfo connection in _config.Connections)
        {
            Connections.Add(connection);
        }
        foreach (CheckQuery query in _config.Queries)
        {
            Queries.Add(query);
        }

        UpdateSmtpSummary();
    }

    /// <summary>앱 시작 시 호출 — 저장된 과거 이력을 이력 탭에 로드한다.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            IReadOnlyList<CheckResult> recent = await _history.GetRecentAcrossAllAsync();
            RecentResults.Clear();
            foreach (CheckResult result in recent)
            {
                RecentResults.Add(result);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"이력 로드 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddConnection()
    {
        var dialog = new ConnectionEditWindow { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Connection is null)
        {
            return;
        }

        // 비밀번호는 자격 증명 저장소에만, 연결 정보는 설정에만 저장(평문 분리).
        _credentials.Save(dialog.Connection.CredentialKey, dialog.Password);
        _config.Connections.Add(dialog.Connection);
        Connections.Add(dialog.Connection);
        _configStore.Save(_config);
        StatusMessage = $"연결 추가: {dialog.Connection.Name}";
    }

    [RelayCommand]
    private void AddQuery()
    {
        if (Connections.Count == 0)
        {
            MessageBox.Show(Application.Current.MainWindow,
                "먼저 DB 연결을 추가하세요.", "안내",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new QueryEditWindow(_config.Connections) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Query is null)
        {
            return;
        }

        _config.Queries.Add(dialog.Query);
        Queries.Add(dialog.Query);
        _configStore.Save(_config);
        StatusMessage = $"쿼리 추가: {dialog.Query.Name}";
    }

    [RelayCommand]
    private void OpenSmtpSettings()
    {
        var dialog = new SmtpSettingsWindow(_config.Smtp, _config.Recipients)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.Settings is null)
        {
            return;
        }

        _config.Smtp = dialog.Settings;
        _config.Recipients = dialog.Recipients;

        // 비밀번호는 입력된 경우에만 갱신(빈칸이면 기존 값 유지).
        if (dialog.NewPassword is not null)
        {
            _credentials.Save(SmtpSettings.CredentialKey, dialog.NewPassword);
        }

        _configStore.Save(_config);
        UpdateSmtpSummary();
        StatusMessage = "SMTP 설정이 저장되었습니다.";
    }

    private void UpdateSmtpSummary() =>
        SmtpSummary = _config.IsEmailConfigured
            ? $"발송 활성 — {_config.Smtp.Host}:{_config.Smtp.Port}, 수신자 {_config.Recipients.Count}명"
            : "이메일 발송 비활성 — SMTP 호스트 또는 수신자가 설정되지 않았습니다.";

    [RelayCommand(CanExecute = nameof(CanRunNow))]
    private async Task RunNowAsync()
    {
        if (SelectedQuery is null)
        {
            return;
        }

        DbConnectionInfo? connection =
            _config.Connections.FirstOrDefault(c => c.Id == SelectedQuery.ConnectionId);
        if (connection is null)
        {
            StatusMessage = "이 쿼리의 대상 연결을 찾을 수 없습니다.";
            return;
        }

        try
        {
            StatusMessage = $"'{SelectedQuery.Name}' 실행 중...";
            SmtpSettings? smtp = _config.IsEmailConfigured ? _config.Smtp : null;

            CheckResult result = await _checkRunner.RunAsync(
                SelectedQuery, connection, _config.Recipients, smtp);

            RecentResults.Insert(0, result);
            StatusMessage =
                $"{result.QueryName}: {result.Status} (건수 {result.RowCount}, {result.DurationMs}ms)";
        }
        catch (Exception ex)
        {
            // 자격증명 누락 등 흐름 자체의 실패는 여기서 사용자에게 표시.
            StatusMessage = $"실행 실패: {ex.Message}";
        }
    }

    private bool CanRunNow() => SelectedQuery is not null;

    partial void OnSelectedQueryChanged(CheckQuery? value) => RunNowCommand.NotifyCanExecuteChanged();
}
