# 로컬 SMTP 테스트 (smtp4dev)

실제 메일 계정 없이 DataGuard의 이메일 발송을 시험하기 위한 **가짜 SMTP 서버**입니다.
받은 메일을 실제로 보내지 않고 웹 UI에 보관해 보여줍니다.

## 1. 설치 (.NET 전역 도구)

```powershell
dotnet tool install -g Rnwood.Smtp4dev --version 3.12.0
```

> 최신 버전이 도구 패키지 형식 오류(`DotnetToolSettings.xml` 없음)를 내면 위처럼 버전을 고정하세요.
> 설치 위치: `%USERPROFILE%\.dotnet\tools\smtp4dev.exe`

## 2. 실행

```powershell
& "$env:USERPROFILE\.dotnet\tools\smtp4dev.exe" --smtpport 2525 --urls "http://localhost:5000"
```

| 항목 | 값 |
|------|-----|
| SMTP 포트 | 2525 |
| 웹 UI(받은 메일 확인) | http://localhost:5000 |
| TLS / 인증 | 없음 |

> 재부팅하면 꺼집니다. 테스트할 때마다 위 명령으로 다시 실행하세요.

## 3. DataGuard SMTP 설정

**설정 탭 → SMTP / 수신자 설정...**

| 항목 | 입력 |
|------|------|
| SMTP 호스트 | `localhost` |
| 포트 | `2525` |
| SSL 사용 | **해제** |
| 사용자 / 비밀번호 | 비움 (인증 없음) |
| 발신 주소 | `dataguard@local` |
| 수신자 | `ops@local` (한 줄에 하나) |

## 4. 확인

1. 체크 쿼리를 **지금 실행**(또는 스케줄) → 이상 발견 시 메일 발송
2. 브라우저로 **http://localhost:5000** 열어 수신 메일 확인

빠른 발송 점검(앱 없이):
```powershell
Send-MailMessage -SmtpServer localhost -Port 2525 -From "dataguard@local" -To "ops@local" -Subject "테스트" -Body "본문"
# 수신 확인(API)
Invoke-RestMethod "http://localhost:5000/api/messages" | Select-Object -ExpandProperty results
```

## 5. 종료

```powershell
Get-Process smtp4dev -ErrorAction SilentlyContinue | Stop-Process
```
