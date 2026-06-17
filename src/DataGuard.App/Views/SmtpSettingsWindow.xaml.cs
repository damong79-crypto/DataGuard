using System.Net.Mail;
using System.Windows;
using DataGuard.Core.Models;

namespace DataGuard.App.Views;

/// <summary>
/// SMTP 발송 설정과 수신자 목록을 편집하는 다이얼로그.
/// 비밀번호는 보안상 기존 값을 다시 보여주지 않는다 — 빈칸이면 변경하지 않는다.
/// </summary>
public partial class SmtpSettingsWindow : Window
{
    public SmtpSettings? Settings { get; private set; }

    public List<string> Recipients { get; private set; } = new();

    /// <summary>새로 입력된 비밀번호. null이면 "변경 없음"을 뜻한다.</summary>
    public string? NewPassword { get; private set; }

    public SmtpSettingsWindow(SmtpSettings current, IReadOnlyList<string> recipients)
    {
        InitializeComponent();

        HostBox.Text = current.Host;
        PortBox.Text = current.Port.ToString();
        SslBox.IsChecked = current.UseSsl;
        UsernameBox.Text = current.Username;
        FromBox.Text = current.FromAddress;
        RecipientsBox.Text = string.Join(Environment.NewLine, recipients);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string host = HostBox.Text.Trim();

        // 호스트가 비면 이메일 발송 비활성 상태로 저장(나머지 검증 생략).
        if (string.IsNullOrWhiteSpace(host))
        {
            Commit(host, port: 0, from: FromBox.Text.Trim(), recipients: ParseRecipients(out _));
            return;
        }

        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            Warn("포트는 1~65535 사이의 숫자여야 합니다.");
            return;
        }

        string from = FromBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(from) || !IsValidEmail(from))
        {
            Warn("발신 주소가 비었거나 형식이 올바르지 않습니다.");
            return;
        }

        List<string> recipients = ParseRecipients(out string? invalid);
        if (invalid is not null)
        {
            Warn($"수신자 주소 형식이 올바르지 않습니다: {invalid}");
            return;
        }

        Commit(host, port, from, recipients);
    }

    private void Commit(string host, int port, string from, List<string> recipients)
    {
        Settings = new SmtpSettings
        {
            Host = host,
            Port = port,
            UseSsl = SslBox.IsChecked == true,
            Username = UsernameBox.Text.Trim(),
            FromAddress = from
        };
        Recipients = recipients;

        // 빈칸이면 기존 비밀번호 유지(null), 입력했으면 새 값으로 교체.
        NewPassword = string.IsNullOrEmpty(PasswordBox.Password) ? null : PasswordBox.Password;

        DialogResult = true;
    }

    // 한 줄에 하나씩. 형식 오류가 있으면 out 파라미터로 첫 오류를 돌려준다.
    private List<string> ParseRecipients(out string? invalid)
    {
        invalid = null;
        var result = new List<string>();
        foreach (string raw in RecipientsBox.Text.Split('\n'))
        {
            string address = raw.Trim();
            if (address.Length == 0)
            {
                continue;
            }

            if (!IsValidEmail(address))
            {
                invalid = address;
                return result;
            }

            result.Add(address);
        }

        return result;
    }

    private static bool IsValidEmail(string value) =>
        MailAddress.TryCreate(value, out _);

    private void Warn(string message) =>
        MessageBox.Show(this, message, "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
}
