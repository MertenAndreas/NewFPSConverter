using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NewFPSConverter;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.Handled = true;
        MessageBox.Show($"Unhandled UI exception: {e.Exception.Message}", "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
        Current.Shutdown(1);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
        else
        {
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt"), "Unhandled domain exception without Exception object." + Environment.NewLine);
        }
    }

    private static void LogException(Exception ex)
    {
        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt"), ex + Environment.NewLine + new string('-', 80) + Environment.NewLine);
    }
}
