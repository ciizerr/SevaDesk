using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SevaDesk_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    [DllImport("gdi32.dll", EntryPoint = "AddFontResourceExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string lpszFileName, uint fl, IntPtr pdv);

    private const uint FR_PRIVATE = 0x10;

    public static Window MainWindow { get; private set; } = null!;
    private Window? _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        RegisterBundledFonts();
        InitializeComponent();

        try
        {
            Resources["SymbolThemeFontFamily"] = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        }
        catch { }

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

    private static void RegisterBundledFonts()
    {
        try
        {
            var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "SegoeIcons.ttf");
            if (File.Exists(fontPath))
            {
                int added = AddFontResourceEx(fontPath, FR_PRIVATE, IntPtr.Zero);
                System.Diagnostics.Debug.WriteLine($"[FontLoader] Registered {added} font(s) from {fontPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontLoader] Failed to register font: {ex.Message}");
        }
    }
}
