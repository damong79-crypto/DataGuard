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
    public DbConnectionInfo? Connection { get; private set; }

    public string Password { get; private set; } = string.Empty;

    public ConnectionEditWindow()
    {
        InitializeComponent();
        ProviderBox.ItemsSource = Enum.GetValues<DbProvider>();
        ProviderBox.SelectedIndex = 0;
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
        Password = password;
        DialogResult = true;
    }
}
