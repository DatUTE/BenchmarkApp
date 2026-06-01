/**
 * @file App.axaml.cs
 * @brief Application bootstrap — configures DI container and creates the main window.
 *
 * Dependency Injection is wired here using Microsoft.Extensions.DependencyInjection.
 * All services and ViewModels are registered as singletons so that the same
 * BenchmarkService instance is shared between the Dashboard and the Export VMs.
 */

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Benchmark.UI.Services;
using Benchmark.UI.ViewModels;
using Benchmark.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmark.UI;

/// <summary>
/// Application entry point and DI composition root.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? serviceProvider_;

    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        serviceProvider_ = BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = serviceProvider_.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.MainWindow = mainWindow;

            // Dispose the DI container when the app exits
            desktop.Exit += (_, _) => serviceProvider_?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── DI composition root ───────────────────────────────────────────────────

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // ── Infrastructure services ─────────────────────────────────────────
        services.AddSingleton<IBenchmarkService,        BenchmarkService>();
        services.AddSingleton<IProcessDiscoveryService, ProcessDiscoveryService>();
        services.AddSingleton<IExportService,           ExportService>();
        services.AddSingleton<ITemperatureService,      HardwareTemperatureService>();

        // ── ViewModels (singletons so they share the same service instances) ─
        services.AddSingleton<ProcessSelectionViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ExportViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
