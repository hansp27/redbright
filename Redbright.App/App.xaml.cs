using System.Configuration;
using System.Data;
using System.Windows;

namespace Redbright.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
			var settings = SettingsStorage.Load();
			var main = new MainWindow(settings);
        MainWindow = main;
			main.Show();
			if (settings.StartMinimizedToTray)
			{
				main.MinimizeToTrayInitially();
			}
    }
}

