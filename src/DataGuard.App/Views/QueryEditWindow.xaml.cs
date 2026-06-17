using System.Windows;
using System.Windows.Controls;
using DataGuard.Core.Models;

namespace DataGuard.App.Views;

/// <summary>체크 쿼리 등록 다이얼로그(스케줄 포함).</summary>
public partial class QueryEditWindow : Window
{
    private readonly IReadOnlyList<DbConnectionInfo> _connections;
    private Guid? _editingId;
    private bool _editingIsEnabled = true;

    public CheckQuery? Query { get; private set; }

    /// <param name="connections">대상 연결로 고를 수 있는, 이미 등록된 연결 목록.</param>
    public QueryEditWindow(IReadOnlyList<DbConnectionInfo> connections)
    {
        InitializeComponent();
        _connections = connections;
        ConnectionBox.ItemsSource = connections;
        if (connections.Count > 0)
        {
            ConnectionBox.SelectedIndex = 0;
        }

        PolicyBox.ItemsSource = Enum.GetValues<NotifyPolicy>();
        PolicyBox.SelectedItem = NotifyPolicy.OnAnomalyOnly;

        ScheduleKindBox.SelectedIndex = 0; // 기본: 수동 실행만
    }

    /// <summary>기존 쿼리를 편집한다. Id·사용여부를 보존한다.</summary>
    public void LoadForEdit(CheckQuery existing)
    {
        _editingId = existing.Id;
        _editingIsEnabled = existing.IsEnabled;
        Title = "체크 쿼리 편집";

        NameBox.Text = existing.Name;
        ConnectionBox.SelectedItem = _connections.FirstOrDefault(c => c.Id == existing.ConnectionId);
        PolicyBox.SelectedItem = existing.NotifyPolicy;
        SqlBox.Text = existing.Sql;

        // 스케줄을 UI 컨트롤로 역매핑(SelectedIndex 설정이 필드 활성화도 갱신).
        switch (existing.Schedule?.Kind)
        {
            case ScheduleKind.EveryMinutes:
                ScheduleKindBox.SelectedIndex = 1;
                IntervalBox.Text = existing.Schedule.IntervalMinutes.ToString();
                break;
            case ScheduleKind.DailyAt:
                ScheduleKindBox.SelectedIndex = 2;
                TimeBox.Text = existing.Schedule.TimeOfDay.ToString(@"hh\:mm");
                break;
            case ScheduleKind.WeekdaysAt:
                ScheduleKindBox.SelectedIndex = 3;
                TimeBox.Text = existing.Schedule.TimeOfDay.ToString(@"hh\:mm");
                break;
            default:
                ScheduleKindBox.SelectedIndex = 0;
                break;
        }
    }

    // 선택한 스케줄 종류에 따라 입력 필드를 활성/비활성화한다.
    private void OnScheduleKindChanged(object sender, SelectionChangedEventArgs e)
    {
        // 생성자에서 InitializeComponent 직후 호출될 수 있어 null 가드.
        if (IntervalBox is null || TimeBox is null)
        {
            return;
        }

        bool isInterval = ScheduleKindBox.SelectedIndex == 1;
        bool isTimeOfDay = ScheduleKindBox.SelectedIndex is 2 or 3;

        IntervalBox.IsEnabled = isInterval;
        IntervalLabel.IsEnabled = isInterval;
        TimeBox.IsEnabled = isTimeOfDay;
        TimeLabel.IsEnabled = isTimeOfDay;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string sql = SqlBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sql))
        {
            Warn("이름과 SQL은 필수입니다.");
            return;
        }

        if (ConnectionBox.SelectedItem is not DbConnectionInfo connection)
        {
            Warn("먼저 연결을 추가한 뒤 대상 연결을 선택하세요.");
            return;
        }

        if (!TryBuildSchedule(out ScheduleSpec? schedule, out string? error))
        {
            Warn(error!);
            return;
        }

        Query = new CheckQuery
        {
            Name = name,
            ConnectionId = connection.Id,
            Sql = sql,
            NotifyPolicy = (NotifyPolicy)PolicyBox.SelectedItem,
            Schedule = schedule,
            IsEnabled = _editingIsEnabled
        };

        // 편집이면 Id를 보존(이력·스케줄 참조 유지).
        if (_editingId.HasValue)
        {
            Query.Id = _editingId.Value;
        }

        DialogResult = true;
    }

    private bool TryBuildSchedule(out ScheduleSpec? schedule, out string? error)
    {
        schedule = null;
        error = null;

        switch (ScheduleKindBox.SelectedIndex)
        {
            case 1: // 매 N분마다
                if (!int.TryParse(IntervalBox.Text.Trim(), out int minutes) || minutes <= 0)
                {
                    error = "간격(분)은 1 이상의 숫자여야 합니다.";
                    return false;
                }
                schedule = new ScheduleSpec { Kind = ScheduleKind.EveryMinutes, IntervalMinutes = minutes };
                return true;

            case 2: // 매일 지정 시각
            case 3: // 평일 지정 시각
                if (!TimeSpan.TryParse(TimeBox.Text.Trim(), out TimeSpan time) ||
                    time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
                {
                    error = "시각은 HH:mm 형식(00:00~23:59)이어야 합니다.";
                    return false;
                }
                schedule = new ScheduleSpec
                {
                    Kind = ScheduleKindBox.SelectedIndex == 2 ? ScheduleKind.DailyAt : ScheduleKind.WeekdaysAt,
                    TimeOfDay = time
                };
                return true;

            default: // 수동 실행만
                return true;
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
}
