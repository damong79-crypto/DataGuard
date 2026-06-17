namespace DataGuard.Core.Models;

/// <summary>지원하는 DB 종류. PRD 4단계: SQL Server + PostgreSQL.</summary>
public enum DbProvider
{
    SqlServer,
    PostgreSql
}

/// <summary>쿼리별 이메일 발송 정책. PRD 3단계: 쿼리마다 선택(C).</summary>
public enum NotifyPolicy
{
    /// <summary>이상(결과 1건 이상)일 때만 발송.</summary>
    OnAnomalyOnly,

    /// <summary>정상이어도 "이상 없음" 리포트까지 항상 발송.</summary>
    Always
}

/// <summary>체크 실행 판정 결과.</summary>
public enum CheckStatus
{
    /// <summary>결과 0건 — 정합성 정상.</summary>
    Normal,

    /// <summary>결과 1건 이상 — 정합성 이상 발견.</summary>
    Anomaly,

    /// <summary>쿼리 실행 실패(연결 오류·문법 오류·타임아웃 등).</summary>
    Error
}
