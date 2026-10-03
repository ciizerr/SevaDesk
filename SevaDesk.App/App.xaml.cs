using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SevaDesk_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;
    private Window? _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        UnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "crash.txt"),
                    $"UnhandledException: {e.Message}\nException: {e.Exception}\nStackTrace: {e.Exception?.StackTrace}");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "crash_appdomain.txt"),
                    $"AppDomain: {e.ExceptionObject}");
            }
            catch { }
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        _window = MainWindow;
        _window.Activate();

        _ = Services.UpdateService.CheckForUpdatesAsync(silent: true);
    }
}
