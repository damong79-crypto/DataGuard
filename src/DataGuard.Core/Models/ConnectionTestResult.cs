namespace DataGuard.Core.Models;

/// <summary>연결 테스트 결과(접속 성공 여부 + 실패 원인).</summary>
public sealed record ConnectionTestResult
{
    public required bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }

    public static ConnectionTestResult Success(TimeSpan duration) =>
        new() { Succeeded = true, Duration = duration };

    public static ConnectionTestResult Failure(string error, TimeSpan duration) =>
        new() { Succeeded = false, ErrorMessage = error, Duration = duration };
}
