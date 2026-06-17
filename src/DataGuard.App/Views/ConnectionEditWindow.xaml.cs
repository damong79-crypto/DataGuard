using System.Windows;
using DataGuard.Core.Models;

namespace DataGuard.App.Views;

/// <summary>
/// DB 연결 등록 다이얼로그. 비밀번호는 보안상 바인딩하지 않고 PasswordBox에서 직접 읽는다.
/// 저장 결과는 <see cref="Connection"/>·<see cref="Password"/>로 노출하고, 호출자가
/// 비밀번호를 자격 증명 저장소에 넣고 연결 정보를 설정에 저장한다.
/// </summary>
public partial class ConnectionEditWindow : Window
{
    private Guid? _editingId;

    public DbConnectionInfo? Connection { get; private set; }

    public string Password { get; private set; } = string.Empty;

    public bool IsEditMode => _editingId.HasValue;

    public ConnectionEditWindow()
    {
        InitializeComponent();
        ProviderBox.ItemsSource = Enum.GetValues<DbProvider>();
        ProviderBox.SelectedIndex = 0;
    }

    /// <summary>기존 연결을 편집한다. Id를 보존해 자격증명 키·쿼리 참조가 유지된다.</summary>
    public void LoadForEdit(DbConnectionInfo existing)
    {
        _editingId = existing.Id;
        Title = "연결 편집";
        NameBox.Text = existing.Name;
        ProviderBox.SelectedItem = existing.Provider;
        HostBox.Text = existing.Host;
        PortBox.Text = existing.Port > 0 ? existing.Port.ToString() : string.Empty;
        DatabaseBox.Text = existing.Database;
        UsernameBox.Text = existing.Username;
        // 비밀번호는 보안상 다시 보여주지 않는다 — 빈칸이면 기존 값을 유지한다.
        HintText.Text = "비밀번호는 빈칸으로 두면 기존 값이 유지됩니다. 변경하려면 입력하세요. 읽기 전용 계정 권장.";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string host = HostBox.Text.Trim();
        string database = DatabaseBox.Text.Trim();
        string username = UsernameBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show(this, "이름·호스트·데이터베이스·사용자는 필수입니다.",
                "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int port = 0; // 0이면 실행 시 기본 포트가 적용된다.
        if (!string.IsNullOrWhiteSpace(PortBox.Text) &&
            (!int.TryParse(PortBox.Text.Trim(), out port) || port is < 1 or > 65535))
        {
            MessageBox.Show(this, "포트는 1~65535 사이의 숫자여야 합니다.",
                "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Connection = new DbConnectionInfo
        {
            Name = name,
            Provider = (DbProvider)ProviderBox.SelectedItem,
            Host = host,
            Port = port,
            Database = database,
            Username = username
        };

        // 편집이면 Id를 보존(자격증명 키·쿼리 참조 유지).
        if (_editingId.HasValue)
        {
            Connection.Id = _editingId.Value;
        }

        Password = password;
        DialogResult = true;
    }
}
