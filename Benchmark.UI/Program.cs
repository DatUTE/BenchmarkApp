/**
 * @file Program.cs
 * @brief Application entry point.
 *
 * Configures and launches the Avalonia application with the
 * Win32 desktop back-end and SkiaSharp rendering pipeline.
 */

using Avalonia;
using Benchmark.UI;

/// <summary>
/// Application entry point.
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Application entry point.
    /// Must be STA for COM/Win32 interop compatibility.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Creates and configures the Avalonia application builder.
    /// Extracted as a separate method for Avalonia Designer compatibility.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
