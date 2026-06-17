using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
    private readonly ICheckScheduler _scheduler;
    private readonly IConnectionTester _connectionTester;
    private readonly IStartupRegistration _startup;
    private readonly AppConfig _config;

    /// <summary>체크 1건이 끝날 때마다 발생(수동·자동 공통). 트레이 풍선 알림 등에 사용. UI 스레드에서 발생.</summary>
    public event Action<CheckResult>? CheckCompleted;

    public ObservableCollection<DbConnectionInfo> Connections { get; } = new();
    public ObservableCollection<CheckQuery> Queries { get; } = new();
    public ObservableCollection<CheckResult> RecentResults { get; } = new();

    [ObservableProperty]
    private CheckQuery? _selectedQuery;

    [ObservableProperty]
    private DbConnectionInfo? _selectedConnection;

    [ObservableProperty]
    private string _statusMessage = "준비됨";

    [ObservableProperty]
    private string _smtpSummary = string.Empty;

    // 이력 필터 — null/0이면 해당 조건 미적용(전체).
    [ObservableProperty]
    private CheckQuery? _filterQuery;

    [ObservableProperty]
    private int _filterStatusIndex; // 0=전체, 1=정상, 2=이상, 3=오류

    [ObservableProperty]
    private int _filterPeriodIndex; // 0=전체, 1=최근 24시간, 2=최근 7일, 3=최근 30일

    /// <summary>이력 보관 기간(일). 0이면 무제한.</summary>
    [ObservableProperty]
    private int _retentionDays;

    /// <summary>Windows 시작 시 자동 실행 여부.</summary>
    [ObservableProperty]
    private bool _runAtStartup;

    public MainViewModel(
        CheckRunner checkRunner,
        IAppConfigStore configStore,
        ICredentialStore credentials,
        ICheckHistoryRepository history,
        ICheckScheduler scheduler,
        IConnectionTester connectionTester,
        IStartupRegistration startup)
    {
        _checkRunner = checkRunner;
        _configStore = configStore;
        _credentials = credentials;
        _history = history;
        _scheduler = scheduler;
        _connectionTester = connectionTester;
        _startup = startup;

        _config = _configStore.Load();
        foreach (DbConnectionInfo connection in _config.Connections)
        {
            Connections.Add(connection);
        }
        foreach (CheckQuery query in _config.Queries)
        {
            Queries.Add(query);
            _scheduler.Schedule(query, OnScheduledTriggerAsync); // 스케줄 있는 쿼리만 실제 등록됨
        }

        RetentionDays = _config.HistoryRetentionDays;
        // 현재 등록 상태를 필드에 직접 반영(속성으로 설정하면 OnChanged가 불려 토글이 재실행됨).
        _runAtStartup = _startup.IsEnabled();
        UpdateSmtpSummary();
    }

    // 사용자가 체크박스를 바꿀 때만 호출된다(초기값은 필드로 설정해 제외).
    partial void OnRunAtStartupChanged(bool value)
    {
        try
        {
            if (value)
            {
                _startup.Enable();
            }
            else
            {
                _startup.Disable();
            }
            StatusMessage = value ? "Windows 시작 시 자동 실행 켜짐" : "자동 실행 꺼짐";
        }
        catch (Exception ex)
        {
            StatusMessage = $"자동 실행 설정 실패: {ex.Message}";
        }
    }

    /// <summary>앱 시작 시 호출 — 오래된 이력을 정리한 뒤 현재 필터(기본: 전체)로 로드한다.</summary>
    public async Task InitializeAsync()
    {
        await CleanupOldHistoryAsync();
        await ApplyFilterAsync();
    }

    // 보관 기간이 지난 이력을 삭제한다(0이면 정리하지 않음).
    private async Task CleanupOldHistoryAsync()
    {
        if (RetentionDays <= 0)
        {
            return;
        }

        try
        {
            int deleted = await _history.DeleteOlderThanAsync(DateTimeOffset.Now.AddDays(-RetentionDays));
            if (deleted > 0)
            {
                StatusMessage = $"오래된 이력 {deleted}건 정리됨";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"이력 정리 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveRetentionAsync()
    {
        if (RetentionDays < 0)
        {
            StatusMessage = "보관 기간은 0 이상이어야 합니다.";
            return;
        }

        _config.HistoryRetentionDays = RetentionDays;
        _configStore.Save(_config);
        await CleanupOldHistoryAsync();
        await ApplyFilterAsync();
        StatusMessage = RetentionDays == 0
            ? "이력 보관: 무제한으로 설정됨"
            : $"이력 보관 기간 {RetentionDays}일 저장됨";
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        var filter = new HistoryFilter
        {
            QueryId = FilterQuery?.Id,
            Status = FilterStatusIndex switch
            {
                1 => CheckStatus.Normal,
                2 => CheckStatus.Anomaly,
                3 => CheckStatus.Error,
                _ => null
            },
            Since = FilterPeriodIndex switch
            {
                1 => DateTimeOffset.Now.AddHours(-24),
                2 => DateTimeOffset.Now.AddDays(-7),
                3 => DateTimeOffset.Now.AddDays(-30),
                _ => null
            }
        };

        try
        {
            IReadOnlyList<CheckResult> results = await _history.QueryAsync(filter);
            RecentResults.Clear();
            foreach (CheckResult result in results)
            {
                RecentResults.Add(result);
            }
            StatusMessage = $"이력 {results.Count}건 조회됨";
        }
        catch (Exception ex)
        {
            StatusMessage = $"이력 조회 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (RecentResults.Count == 0)
        {
            StatusMessage = "내보낼 이력이 없습니다.";
            return;
        }

        // WPF 공용 대화상자(WinForms와 모호하지 않도록 정규화).
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "dataguard-history.csv",
            Filter = "CSV 파일 (*.csv)|*.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string csv = CheckResultCsvExporter.ToCsv(RecentResults);
            // Excel이 한글을 올바로 인식하도록 UTF-8 BOM 포함.
            File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            StatusMessage = $"이력 {RecentResults.Count}건을 CSV로 내보냈습니다.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"내보내기 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterQuery = null;
        FilterStatusIndex = 0;
        FilterPeriodIndex = 0;
        await ApplyFilterAsync();
    }

    [RelayCommand]
    private void AddConnection()
    {
        var dialog = new ConnectionEditWindow(_connectionTester) { Owner = Application.Current.MainWindow };
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
        _scheduler.Schedule(dialog.Query, OnScheduledTriggerAsync);

        string scheduleNote = dialog.Query.Schedule is null ? "수동" : dialog.Query.Schedule.ToString();
        StatusMessage = $"쿼리 추가: {dialog.Query.Name} ({scheduleNote})";
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
            CheckResult result = await RunQueryAsync(SelectedQuery);
            RecentResults.Insert(0, result);
            StatusMessage = Describe(result);
            CheckCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            // 자격증명 누락 등 흐름 자체의 실패는 여기서 사용자에게 표시.
            StatusMessage = $"실행 실패: {ex.Message}";
        }
    }

    private bool CanRunNow() => SelectedQuery is not null;

    [RelayCommand]
    private async Task RunAllAsync()
    {
        // 사용 중인 모든 쿼리를 순차 실행(스냅샷으로 순회 — 실행 중 목록 변경 영향 차단).
        List<CheckQuery> targets = Queries.Where(q => q.IsEnabled).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "실행할 쿼리가 없습니다.";
            return;
        }

        int ran = 0, anomalies = 0, errors = 0;
        foreach (CheckQuery query in targets)
        {
            try
            {
                StatusMessage = $"전체 실행 중... ({ran + 1}/{targets.Count}) {query.Name}";
                CheckResult result = await RunQueryAsync(query);
                RecentResults.Insert(0, result);
                CheckCompleted?.Invoke(result);
                if (result.Status == CheckStatus.Anomaly) anomalies++;
                if (result.Status == CheckStatus.Error) errors++;
            }
            catch (Exception ex)
            {
                // 한 건 실패해도 나머지는 계속 진행.
                errors++;
                StatusMessage = $"'{query.Name}' 실행 실패: {ex.Message}";
            }
            ran++;
        }

        StatusMessage = $"전체 실행 완료 — {ran}건 실행, 이상 {anomalies}건, 오류 {errors}건";
    }

    private bool HasSelectedQuery() => SelectedQuery is not null;

    private bool HasSelectedConnection() => SelectedConnection is not null;

    partial void OnSelectedQueryChanged(CheckQuery? value)
    {
        RunNowCommand.NotifyCanExecuteChanged();
        EditQueryCommand.NotifyCanExecuteChanged();
        DeleteQueryCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedConnectionChanged(DbConnectionInfo? value)
    {
        EditConnectionCommand.NotifyCanExecuteChanged();
        DeleteConnectionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedConnection))]
    private void EditConnection()
    {
        if (SelectedConnection is null)
        {
            return;
        }

        var dialog = new ConnectionEditWindow(_connectionTester) { Owner = Application.Current.MainWindow };
        dialog.LoadForEdit(SelectedConnection, _credentials.Retrieve(SelectedConnection.CredentialKey));
        if (dialog.ShowDialog() != true || dialog.Connection is null)
        {
            return;
        }

        DbConnectionInfo updated = dialog.Connection; // 동일 Id 보존
        // 비밀번호는 입력된 경우에만 갱신(빈칸이면 기존 값 유지).
        if (dialog.Password.Length > 0)
        {
            _credentials.Save(updated.CredentialKey, dialog.Password);
        }

        Replace(_config.Connections, updated, c => c.Id == updated.Id);
        Replace(Connections, updated, c => c.Id == updated.Id);
        _configStore.Save(_config);
        SelectedConnection = updated;
        StatusMessage = $"연결 수정: {updated.Name}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedConnection))]
    private void DeleteConnection()
    {
        if (SelectedConnection is null)
        {
            return;
        }

        DbConnectionInfo target = SelectedConnection;

        // 이 연결을 참조하는 쿼리가 있으면 삭제를 막는다(고아 참조 방지).
        if (_config.Queries.Any(q => q.ConnectionId == target.Id))
        {
            MessageBox.Show(Application.Current.MainWindow,
                "이 연결을 사용하는 쿼리가 있어 삭제할 수 없습니다. 먼저 해당 쿼리를 삭제하거나 다른 연결로 변경하세요.",
                "삭제 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Confirm($"연결 '{target.Name}'을(를) 삭제할까요? 저장된 비밀번호도 함께 삭제됩니다."))
        {
            return;
        }

        _credentials.Delete(target.CredentialKey);
        _config.Connections.RemoveAll(c => c.Id == target.Id);
        Connections.Remove(target);
        _configStore.Save(_config);
        StatusMessage = $"연결 삭제: {target.Name}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedQuery))]
    private void EditQuery()
    {
        if (SelectedQuery is null)
        {
            return;
        }

        var dialog = new QueryEditWindow(_config.Connections) { Owner = Application.Current.MainWindow };
        dialog.LoadForEdit(SelectedQuery);
        if (dialog.ShowDialog() != true || dialog.Query is null)
        {
            return;
        }

        CheckQuery updated = dialog.Query; // 동일 Id 보존
        Replace(_config.Queries, updated, q => q.Id == updated.Id);
        Replace(Queries, updated, q => q.Id == updated.Id);
        _configStore.Save(_config);
        _scheduler.Schedule(updated, OnScheduledTriggerAsync); // 기존 스케줄 해제 후 재등록
        SelectedQuery = updated;

        string scheduleNote = updated.Schedule is null ? "수동" : updated.Schedule.ToString();
        StatusMessage = $"쿼리 수정: {updated.Name} ({scheduleNote})";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedQuery))]
    private void DeleteQuery()
    {
        if (SelectedQuery is null)
        {
            return;
        }

        CheckQuery target = SelectedQuery;
        if (!Confirm($"쿼리 '{target.Name}'을(를) 삭제할까요?"))
        {
            return;
        }

        _scheduler.Unschedule(target.Id);
        _config.Queries.RemoveAll(q => q.Id == target.Id);
        Queries.Remove(target);
        _configStore.Save(_config);
        StatusMessage = $"쿼리 삭제: {target.Name}";
    }

    // 리스트에서 동일 키 항목을 새 인스턴스로 교체(ObservableCollection이면 UI도 갱신).
    private static void Replace<T>(IList<T> list, T item, Func<T, bool> match)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (match(list[i]))
            {
                list[i] = item;
                return;
            }
        }
    }

    private static bool Confirm(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "확인",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    // 수동·자동 실행이 공유하는 실제 실행 경로(UI 갱신은 호출자가 담당).
    private Task<CheckResult> RunQueryAsync(CheckQuery query)
    {
        DbConnectionInfo connection =
            _config.Connections.FirstOrDefault(c => c.Id == query.ConnectionId)
            ?? throw new InvalidOperationException("이 쿼리의 대상 연결을 찾을 수 없습니다.");

        SmtpSettings? smtp = _config.IsEmailConfigured ? _config.Smtp : null;
        return _checkRunner.RunAsync(query, connection, _config.Recipients, smtp);
    }

    // 스케줄러 타이머 스레드에서 호출된다 — UI 컬렉션·속성 갱신은 디스패처로 마샬링한다.
    private async Task OnScheduledTriggerAsync(CheckQuery query)
    {
        try
        {
            CheckResult result = await RunQueryAsync(query);
            Application.Current.Dispatcher.Invoke(() =>
            {
                RecentResults.Insert(0, result);
                StatusMessage = "[자동] " + Describe(result);
                CheckCompleted?.Invoke(result);
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
                StatusMessage = $"[자동] {query.Name} 실행 실패: {ex.Message}");
        }
    }

    private static string Describe(CheckResult result) =>
        $"{result.QueryName}: {result.Status} (건수 {result.RowCount}, {result.DurationMs}ms)";
}
