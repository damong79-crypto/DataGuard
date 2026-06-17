# DataGuard

DB 테이블 정합성을 **등록한 쿼리로 스케줄 자동 체크**하고, 이상 발견 시 **이메일로 알리는** Windows 데스크탑 앱.

자세한 배경·요구사항은 [PRD](PRD_DB정합성체크앱.md) 참고.

## 기술 스택

- .NET 8 / WPF (MVVM, CommunityToolkit.Mvvm)
- SQL Server (Microsoft.Data.SqlClient), PostgreSQL (Npgsql)
- 결과 이력: SQLite (Microsoft.Data.Sqlite)
- 자격 증명: Windows DPAPI (평문 저장 금지)

## 프로젝트 구조

```
DataGuard.sln
└─ src/
   ├─ DataGuard.Core/        # 도메인 모델 + 서비스 (UI 비의존)
   │  ├─ Models/             # DbConnectionInfo, CheckQuery, CheckResult ...
   │  ├─ Abstractions/       # 서비스 인터페이스
   │  └─ Services/           # QueryExecutor, RowCountResultJudge,
   │                         #   DpapiCredentialStore, SmtpEmailNotifier,
   │                         #   SqliteCheckHistoryRepository, CheckRunner ...
   └─ DataGuard.App/         # WPF UI (MVVM)
      ├─ ViewModels/
      └─ MainWindow.xaml
```

핵심 흐름은 `CheckRunner`: **비밀번호 조회 → 쿼리 실행 → 0건 판정 → 이력 저장 → 정책별 이메일 발송**.
판정 규칙은 결과 **0건=정상, 1건 이상=이상**.

## 빌드 / 실행

```bash
dotnet build DataGuard.sln -c Debug
dotnet run --project src/DataGuard.App
dotnet test                              # 단위 테스트 (DataGuard.Core.Tests)
```

> 요구: .NET 8 SDK (Windows). WPF이므로 Windows 전용.

## 배포 (단일 EXE)

.NET 미설치 PC에서도 실행되는 **자체 포함 단일 실행 파일**로 게시:

```bash
dotnet publish src/DataGuard.App/DataGuard.App.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false -o publish
```

→ `publish/DataGuard.App.exe` 하나만 생성(약 72MB, 런타임 포함). 이 파일만 배포하면 됨.
Visual Studio에서는 `Properties/PublishProfiles/win-x64-single.pubxml` 프로필로 게시.

> .NET 8 Desktop Runtime이 이미 깔린 환경이면 `--self-contained false`로 용량을 크게 줄일 수 있음.
> 서명되지 않은 EXE는 SmartScreen/Smart App Control이 실행을 막을 수 있음(사내 배포 시 코드 서명 권장).

## 테스트

`tests/DataGuard.Core.Tests` (xUnit) — 순수 로직 검증:
- `ScheduleSpec.GetNextRun`: 매분/매일/평일 스케줄의 다음 실행 시각, 주말 건너뛰기
- `RowCountResultJudge`: 0건=정상 / 1건+=이상 / 실행실패=오류

## 개발 우선순위 (PRD)

- **MVP**: DB 연결 → 쿼리 등록 → 수동 실행 → 이메일 발송
- **다음**: 스케줄 자동 실행, 결과 이력 화면, 발송 정책 분기
- **추후**: 다중 연결 UI, 이력 대시보드, 쿼리 가져오기/내보내기

## 보안 메모

- DB·SMTP 비밀번호는 코드/설정 파일에 하드코딩하지 않음 → DPAPI 저장.
- 체크 대상 DB는 **읽기 전용 계정** 권장 (쿼리 오류로 인한 부담 방지).
- 이메일 본문엔 건수·요약만 포함 (개인정보·민감값 미포함).
