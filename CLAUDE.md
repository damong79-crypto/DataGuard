# CLAUDE.md — DataGuard 작업 가이드

> 이 파일은 Claude Code가 새 세션에서 자동으로 읽는 프로젝트 컨텍스트입니다.
> 작업을 이어서 진행할 때 먼저 이 문서를 따르세요.

## 프로젝트 개요

**DataGuard** — DB 테이블 정합성을 등록한 쿼리로 점검하고, 이상 발견 시 이메일로 알리는 Windows 데스크탑 앱.
- 판정 규칙: 쿼리 결과 **0건 = 정상, 1건 이상 = 이상** (쿼리는 "틀어진 행만 반환"하게 작성)
- 배경·요구사항: [PRD](PRD_DB정합성체크앱.md) / 사용법: [docs/사용자_매뉴얼.md](docs/사용자_매뉴얼.md)
- 원격 저장소: https://github.com/damong79-crypto/DataGuard (public), 기본 브랜치 `main`, remote `origin`

## 아키텍처

- **DataGuard.Core** (`net8.0`, UI 비의존)
  - `Models/` 도메인 모델 · `Abstractions/` 인터페이스 · `Services/` 구현
  - 핵심: `CheckRunner`(오케스트레이터: 비밀번호 조회→실행→판정→이력 저장→정책별 메일),
    `QueryExecutor`, `DbConnectionFactory`, `RowCountResultJudge`, `DpapiCredentialStore`,
    `SmtpEmailNotifier`, `SqliteCheckHistoryRepository`, `InAppCheckScheduler`,
    `ConnectionTester`, `JsonAppConfigStore`, `StartupRegistration`, `CheckResultCsvExporter`
- **DataGuard.App** (`net8.0-windows`, WPF + WinForms 상호운용)
  - `App.xaml.cs` = DI 합성 루트(`ConfigureServices`), `MainWindow`(트레이 상주), `ViewModels/MainViewModel`, `Views/`(다이얼로그들)
  - `GlobalUsings.cs`: `UseWindowsForms` 때문에 `Application/MessageBox/Brushes/SystemColors`를 **WPF 타입으로 별칭** (충돌 방지) — 새 파일에서 이 타입 쓸 때 주의
- **tests/DataGuard.Core.Tests** (xUnit) — 현재 23개, 순수 로직 검증

런타임 데이터: `%LOCALAPPDATA%\DataGuard\` (config.json, history.db, credentials/)

## 개발 규약

- MVVM: CommunityToolkit.Mvvm `[ObservableProperty]` / `[RelayCommand]`
- 비밀(DB·SMTP 비밀번호)은 코드/설정에 하드코딩 금지 → DPAPI 저장. 점검 DB는 읽기 전용 계정 권장
- 주석은 한국어, "무엇"보다 "왜". 새 외부 의존성 추가 시 이유·대안 한 줄
- 새 서비스는 인터페이스(`Abstractions`) + 구현(`Services`) + `App.xaml.cs` DI 등록 패턴

## 명령어 (Windows PowerShell)

`dotnet`이 PATH에 없으면 전체 경로 사용: `& "$env:ProgramFiles\dotnet\dotnet.exe"`

```powershell
# 빌드 / 테스트
dotnet build DataGuard.sln -c Debug
dotnet test tests/DataGuard.Core.Tests/DataGuard.Core.Tests.csproj

# 디버그 실행 파일
src/DataGuard.App/bin/Debug/net8.0-windows/DataGuard.App.exe

# 배포용 단일 exe (publish/DataGuard.App.exe, 약 72MB)
dotnet publish src/DataGuard.App/DataGuard.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o publish
```

## ⚠️ 환경 특이사항 (중요 — 시간 낭비 방지)

1. **Smart App Control 켜짐**: 새로 빌드한 **서명되지 않은 exe 실행을 간헐적으로 차단**
   (`0x800711C7` / 종료코드 `0xE0434352`). **빌드·테스트는 정상**, 실행만 막힘.
   → 실행 검증은 Visual Studio/VS Code 디버그로 하거나, 막히면 "코드 이슈 아님"으로 처리. 단정적으로 "정상 실행"이라 보고하지 말 것.
2. **PowerShell 5.1**: git 커밋 메시지에 here-string(`@'...'@`)·heredoc(`<<EOF`) 쓰면 자주 파싱 깨짐.
   → 커밋 메시지는 파일에 쓰고 **`git commit -F <파일>`** 사용. 파싱 오류 나면 `git add`도 실행 안 된 것이니 다시 add.
3. **줄바꿈 경고**(LF→CRLF)는 무해. git push의 stderr 출력도 정상(에러 아님).
4. 커밋 메시지 끝에 트레일러: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## 로컬 테스트 환경 (이미 구축됨)

- **PostgreSQL 16** 설치됨 (서비스 `postgresql-x64-16`, 포트 5432, 슈퍼유저 `postgres`/`postgres` — 테스트 전용)
  - 샘플 DB: `tools/sample-db/postgres_seed.sql` 재실행하면 `dataguard_test` 재생성
  - 읽기 전용 로그인 `dataguard_ro` / `dataguard_ro`, 정합성 이상 3건·정상 2건 데이터
- **smtp4dev** (.NET 전역 도구) — 가짜 SMTP. 실행:
  `& "$env:USERPROFILE\.dotnet\tools\smtp4dev.exe" --smtpport 2525 --urls "http://localhost:5000"`
  - DataGuard SMTP 설정값: 호스트 `localhost` / 포트 `2525` / SSL 해제 / 사용자·비밀번호 비움 / 발신 `dataguard@local` / 수신자 1명 이상
  - 받은 메일 확인: http://localhost:5000 (가이드: `tools/local-smtp/README.md`)

## 현재 상태 (구현 완료)

연결/쿼리 CRUD + 연결 테스트 · 수동 실행(지금 실행=선택 1건 / 전체 실행=모든 쿼리) · 스케줄 자동 실행(매분/매일/평일) ·
0건 판정 · 이력 영속·필터(쿼리/상태/기간, "(전체)" 포함)·CSV 내보내기·보관기간 자동 정리 ·
이메일 알림(무인증 SMTP 허용) · 트레이 상주 · Windows 자동 실행 · 앱 아이콘 · 단일 exe 패키징 · 단위 테스트 23개

## 남은 작업 후보

- **코드 서명** (SAC/SmartScreen 차단 해소 — 사내 인증서 필요)
- 실제 SQL Server·PostgreSQL 대상 통합 테스트
- 다국어/추가 DB 지원, 이력 대시보드 등

## 작업 마무리 체크리스트

1. `dotnet build` 0 오류 · `dotnet test` 통과 확인
2. 사용자 향 기능 추가 시 README "주요 기능" / `docs/사용자_매뉴얼.md` 갱신
3. 커밋(-F 파일 방식) → `git push origin main`
4. 배포 exe 갱신이 필요하면 publish 재실행
