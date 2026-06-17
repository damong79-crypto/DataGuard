using System.Windows;
using DataGuard.Core.Models;

namespace DataGuard.App.Views;

/// <summary>체크 쿼리 등록 다이얼로그.</summary>
public partial class QueryEditWindow : Window
{
    public CheckQuery? Query { get; private set; }

    /// <param name="connections">대상 연결로 고를 수 있는, 이미 등록된 연결 목록.</param>
    public QueryEditWindow(IReadOnlyList<DbConnectionInfo> connections)
    {
        InitializeComponent();
        ConnectionBox.ItemsSource = connections;
        if (connections.Count > 0)
        {
            ConnectionBox.SelectedIndex = 0;
        }

        PolicyBox.ItemsSource = Enum.GetValues<NotifyPolicy>();
        PolicyBox.SelectedItem = NotifyPolicy.OnAnomalyOnly;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string sql = SqlBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sql))
        {
            MessageBox.Show(this, "이름과 SQL은 필수입니다.",
                "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ConnectionBox.SelectedItem is not DbConnectionInfo connection)
        {
            MessageBox.Show(this, "먼저 연결을 추가한 뒤 대상 연결을 선택하세요.",
                "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Query = new CheckQuery
        {
            Name = name,
            ConnectionId = connection.Id,
            Sql = sql,
            NotifyPolicy = (NotifyPolicy)PolicyBox.SelectedItem
        };
        DialogResult = true;
    }
}
