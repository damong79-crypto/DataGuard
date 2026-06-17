using DataGuard.Core.Models;
using DataGuard.Core.Services;

namespace DataGuard.Core.Tests;

/// <summary>
/// SqliteCheckHistoryRepository.QueryAsync 필터 동작 검증.
/// 테스트마다 임시 SQLite 파일을 만들어 격리한다.
/// </summary>
public sealed class HistoryRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteCheckHistoryRepository _repo;
    private readonly Guid _q1 = Guid.NewGuid();
    private readonly Guid _q2 = Guid.NewGuid();

    public HistoryRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dataguard-test-{Guid.NewGuid():N}.db");
        _repo = new SqliteCheckHistoryRepository(_dbPath);
        _repo.InitializeAsync().GetAwaiter().GetResult();

        DateTimeOffset now = DateTimeOffset.Now;
        Seed(_q1, "재고정합성", CheckStatus.Normal, now.AddHours(-1));
        Seed(_q1, "재고정합성", CheckStatus.Anomaly, now.AddDays(-2));
        Seed(_q2, "주문정합성", CheckStatus.Error, now.AddMinutes(-10));
    }

    private void Seed(Guid queryId, string name, CheckStatus status, DateTimeOffset at) =>
        _repo.SaveAsync(new CheckResult
        {
            QueryId = queryId,
            QueryName = name,
            Status = status,
            ExecutedAt = at,
            RowCount = status == CheckStatus.Anomaly ? 3 : 0
        }).GetAwaiter().GetResult();

    [Fact]
    public async Task EmptyFilter_ReturnsAll()
    {
        var results = await _repo.QueryAsync(new HistoryFilter());
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task FilterByQueryId_ReturnsOnlyThatQuery()
    {
        var results = await _repo.QueryAsync(new HistoryFilter { QueryId = _q1 });
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(_q1, r.QueryId));
    }

    [Fact]
    public async Task FilterByStatus_ReturnsOnlyThatStatus()
    {
        var results = await _repo.QueryAsync(new HistoryFilter { Status = CheckStatus.Anomaly });
        Assert.Single(results);
        Assert.Equal(CheckStatus.Anomaly, results[0].Status);
    }

    [Fact]
    public async Task FilterBySince_ExcludesOlderRecords()
    {
        // 최근 24시간: 1시간 전·10분 전은 포함, 2일 전은 제외 → 2건.
        var results = await _repo.QueryAsync(
            new HistoryFilter { Since = DateTimeOffset.Now.AddHours(-24) });
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task CombinedFilters_AreAndedTogether()
    {
        var results = await _repo.QueryAsync(
            new HistoryFilter { QueryId = _q1, Status = CheckStatus.Normal });
        Assert.Single(results);
        Assert.Equal(_q1, results[0].QueryId);
        Assert.Equal(CheckStatus.Normal, results[0].Status);
    }

    [Fact]
    public async Task Results_AreOrderedByExecutedAtDescending()
    {
        var results = await _repo.QueryAsync(new HistoryFilter());
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].ExecutedAt >= results[i].ExecutedAt);
        }
    }

    [Fact]
    public async Task DeleteOlderThan_RemovesOnlyOlderRecords()
    {
        // 시드: now-1h, now-2d, now-10min. 1일 컷오프 → 2일 전 1건만 삭제.
        int deleted = await _repo.DeleteOlderThanAsync(DateTimeOffset.Now.AddDays(-1));

        Assert.Equal(1, deleted);

        var remaining = await _repo.QueryAsync(new HistoryFilter());
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, r => r.Status == CheckStatus.Anomaly); // 삭제된 건이 2일 전 이상 레코드
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* 임시 파일 정리 실패는 무시 */ }
        }
    }
}
