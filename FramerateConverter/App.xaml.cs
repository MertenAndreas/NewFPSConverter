using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommandLine;

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

        var args = e.Args;
        var isNoGui = false;

        var parserResult = new Parser(settings =>
        {
            settings.IgnoreUnknownArguments = true;
            settings.HelpWriter = null;
        }).ParseArguments<Options>(args);

        parserResult.WithParsed(options =>
        {
            isNoGui = options.NoGui;
        });

        var mainWindow = new MainWindow();

        if (isNoGui)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.ShowInTaskbar = false;
            mainWindow.Visibility = Visibility.Hidden;
        }
        else
        {
            mainWindow.Show();
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.Handled = true;

        if (Environment.GetCommandLineArgs().Length <= 1)
        {
            MessageBox.Show($"Unhandled UI exception: {e.Exception.Message}", "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            Console.Error.WriteLine($"Unhandled exception: {e.Exception.Message}");
        }

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
