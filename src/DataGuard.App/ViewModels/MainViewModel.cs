using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataGuard.Core.Models;

namespace DataGuard.App.ViewModels;

/// <summary>
/// 메인 화면 ViewModel(MVVM). 현재는 골격 — 등록된 연결·쿼리 목록과
/// "지금 실행" 명령의 자리를 잡아둔다. 실제 실행 배선은 CheckRunner 주입 후 연결한다.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public ObservableCollection<DbConnectionInfo> Connections { get; } = new();

    public ObservableCollection<CheckQuery> Queries { get; } = new();

    public ObservableCollection<CheckResult> RecentResults { get; } = new();

    [ObservableProperty]
    private CheckQuery? _selectedQuery;

    [ObservableProperty]
    private string _statusMessage = "준비됨";

    /// <summary>
    /// PRD MVP ③ "지금 실행". TODO: CheckRunner를 주입받아
    /// 선택된 쿼리를 즉시 실행하고 결과를 RecentResults에 반영한다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunNow))]
    private Task RunNowAsync()
    {
        // 배선 예정: await _checkRunner.RunAsync(SelectedQuery, connection, recipients, smtp);
        StatusMessage = $"'{SelectedQuery?.Name}' 실행 — (구현 예정)";
        return Task.CompletedTask;
    }

    private bool CanRunNow() => SelectedQuery is not null;

    // SelectedQuery 변경 시 RunNow 명령의 실행 가능 여부를 갱신(소스 생성기 패턴).
    partial void OnSelectedQueryChanged(CheckQuery? value) => RunNowCommand.NotifyCanExecuteChanged();
}
