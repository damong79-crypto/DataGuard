using System.Windows;
using System.Windows.Media;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.App.Views;

/// <summary>
/// DB 연결 등록/편집 다이얼로그. 비밀번호는 보안상 바인딩하지 않고 PasswordBox에서 직접 읽는다.
/// "연결 테스트"로 저장 전에 실제 접속 가능 여부를 확인할 수 있다.
/// </summary>
public partial class ConnectionEditWindow : Window
{
    private readonly IConnectionTester _tester;
    private Guid? _editingId;
    private string? _existingPassword;

    public DbConnectionInfo? Connection { get; private set; }

    public string Password { get; private set; } = string.Empty;

    public bool IsEditMode => _editingId.HasValue;

    public ConnectionEditWindow(IConnectionTester tester)
    {
        InitializeComponent();
        _tester = tester;
        ProviderBox.ItemsSource = Enum.GetValues<DbProvider>();
        ProviderBox.SelectedIndex = 0;
    }

    /// <summary>기존 연결을 편집한다. Id를 보존해 자격증명 키·쿼리 참조가 유지된다.</summary>
    /// <param name="existingPassword">저장돼 있던 비밀번호(연결 테스트 시 빈칸이면 이 값을 사용).</param>
    public void LoadForEdit(DbConnectionInfo existing, string? existingPassword)
    {
        _editingId = existing.Id;
        _existingPassword = existingPassword;
        Title = "연결 편집";
        NameBox.Text = existing.Name;
        ProviderBox.SelectedItem = existing.Provider;
        HostBox.Text = existing.Host;
        PortBox.Text = existing.Port > 0 ? existing.Port.ToString() : string.Empty;
        DatabaseBox.Text = existing.Database;
        UsernameBox.Text = existing.Username;
        HintText.Text = "비밀번호는 빈칸으로 두면 기존 값이 유지됩니다. 변경하려면 입력하세요. 읽기 전용 계정 권장.";
    }

    private async void OnTest(object sender, RoutedEventArgs e)
    {
        // 테스트엔 이름이 필요 없다(접속 정보만 검증).
        if (!TryBuildConnection(requireName: false, out DbConnectionInfo? info, out string? error))
        {
            SetTestResult(success: false, error!);
            return;
        }

        // 편집 중 비밀번호를 비웠다면 기존 저장 값으로 테스트한다.
        string password = PasswordBox.Password.Length > 0
            ? PasswordBox.Password
            : _existingPassword ?? string.Empty;

        TestButton.IsEnabled = false;
        TestResultText.Foreground = SystemColors.GrayTextBrush;
        TestResultText.Text = "테스트 중...";
        TestResultText.ToolTip = null;
        try
        {
            ConnectionTestResult result = await _tester.TestAsync(info!, password);
            if (result.Succeeded)
            {
                SetTestResult(success: true, $"성공 ({(int)result.Duration.TotalMilliseconds}ms)");
            }
            else
            {
                SetTestResult(success: false, result.ErrorMessage ?? "알 수 없는 오류");
            }
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void SetTestResult(bool success, string message)
    {
        TestResultText.Foreground = success ? Brushes.Green : Brushes.Red;
        TestResultText.Text = (success ? "✓ " : "✗ ") + message;
        TestResultText.ToolTip = message; // 전체 메시지는 툴팁으로(길면 본문은 말줄임).
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!TryBuildConnection(requireName: true, out DbConnectionInfo? info, out string? error))
        {
            Warn(error!);
            return;
        }

        // 편집이면 Id를 보존(자격증명 키·쿼리 참조 유지).
        if (_editingId.HasValue)
        {
            info!.Id = _editingId.Value;
        }

        Connection = info;
        Password = PasswordBox.Password;
        DialogResult = true;
    }

    private bool TryBuildConnection(bool requireName, out DbConnectionInfo? info, out string? error)
    {
        info = null;
        error = null;

        string name = NameBox.Text.Trim();
        string host = HostBox.Text.Trim();
        string database = DatabaseBox.Text.Trim();
        string username = UsernameBox.Text.Trim();

        if (requireName && string.IsNullOrWhiteSpace(name))
        {
            error = "이름은 필수입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(username))
        {
            error = "호스트·데이터베이스·사용자는 필수입니다.";
            return false;
        }

        int port = 0; // 0이면 실행 시 기본 포트가 적용된다.
        if (!string.IsNullOrWhiteSpace(PortBox.Text) &&
            (!int.TryParse(PortBox.Text.Trim(), out port) || port is < 1 or > 65535))
        {
            error = "포트는 1~65535 사이의 숫자여야 합니다.";
            return false;
        }

        info = new DbConnectionInfo
        {
            Name = name,
            Provider = (DbProvider)ProviderBox.SelectedItem,
            Host = host,
            Port = port,
            Database = database,
            Username = username
        };
        return true;
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
}
