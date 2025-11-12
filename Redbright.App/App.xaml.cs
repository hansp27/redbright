using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;

namespace Redbright.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static string GetLogsDirectory()
    {
        return AppPaths.GetLogsDirectory();
    }

    private static string WriteCrashLog(string source, Exception ex)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(GetLogsDirectory(), $"crash-{timestamp}.txt");
        try
        {
            using var sw = new StreamWriter(path, false);
            sw.WriteLine($"Source: {source}");
            sw.WriteLine($"Time (UTC): {DateTime.UtcNow:o}");
            sw.WriteLine($"App: Redbright");
            sw.WriteLine($"Version: {typeof(App).Assembly.GetName().Version}");
            sw.WriteLine();
            sw.WriteLine(ex.ToString());
        }
        catch
        {
            // ignore logging failures
        }
        return path;
    }

    private void SetupGlobalExceptionHandlers()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            var path = WriteCrashLog("DispatcherUnhandledException", e.Exception);
            try
            {
                System.Windows.MessageBox.Show($"Redbright encountered an error and may be unstable.\nA crash log was written to:\n{path}", "Redbright", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
            WriteCrashLog("AppDomain.CurrentDomain.UnhandledException", ex);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        SetupGlobalExceptionHandlers();
        base.OnStartup(e);
        try
        {
			var settings = SettingsStorage.Load();
			// Ensure logger state aligns with loaded settings
			AppLogger.SetEnabled(settings.LoggingEnabled);
			var forceShow = Array.Exists(e.Args, a => string.Equals(a, "--force-show", StringComparison.OrdinalIgnoreCase));
			if (AppLogger.IsEnabled)
			{
				AppLogger.EnsureLogFile();
				AppLogger.Log("[lifecycle] App.OnStartup");
				AppLogger.LogConfigSnapshot("Working configuration (post-load, in-memory)", settings);
			}
			var main = new MainWindow(settings);
			MainWindow = main;
			if (forceShow)
			{
				main.Show();
			}
			else if (settings.StartMinimizedToTray)
			{
				// Create HWND (fires SourceInitialized → hotkeys register) without showing the window.
				_ = new WindowInteropHelper(main).EnsureHandle();
				// Keep it in the tray without ever flashing a window.
				main.MinimizeToTrayInitially();
			}
			else
			{
				main.Show();
			}
        }
        catch (Exception ex)
        {
            var path = WriteCrashLog("App.OnStartup", ex);
            try
            {
                System.Windows.MessageBox.Show($"Redbright failed to start.\nA crash log was written to:\n{path}", "Redbright", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            Shutdown(-1);
        }
    }
}

