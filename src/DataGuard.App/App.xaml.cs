using System.Windows;
using DataGuard.App.ViewModels;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataGuard.App;

/// <summary>
/// 앱 진입점 겸 합성 루트(composition root).
/// 모든 서비스를 여기서 등록·주입한다.
/// (DI는 Microsoft.Extensions.DependencyInjection 사용 — 표준 방식. 대안: 수동 배선.)
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        _services = collection.BuildServiceProvider();

        // 이력 저장소 스키마를 최초 1회 생성한다.
        await _services.GetRequiredService<ICheckHistoryRepository>()
            .InitializeAsync().ConfigureAwait(true);

        // ViewModel 생성 후 과거 이력을 로드한다(스키마 생성 이후여야 함).
        var viewModel = _services.GetRequiredService<MainViewModel>();
        await viewModel.InitializeAsync().ConfigureAwait(true);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core 서비스 (인터페이스 → 구현)
        services.AddSingleton<IQueryExecutor, QueryExecutor>();
        services.AddSingleton<IResultJudge, RowCountResultJudge>();
        services.AddSingleton<ICredentialStore>(_ => new DpapiCredentialStore());
        services.AddSingleton<IEmailNotifier, SmtpEmailNotifier>();
        services.AddSingleton<ICheckHistoryRepository>(_ => new SqliteCheckHistoryRepository());
        services.AddSingleton<IAppConfigStore>(_ => new JsonAppConfigStore());
        services.AddSingleton<ICheckScheduler, InAppCheckScheduler>();

        // 오케스트레이터 + ViewModel
        services.AddSingleton<CheckRunner>();
        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<ICheckScheduler>()?.StopAll();
        _services?.Dispose();
        base.OnExit(e);
    }
}
