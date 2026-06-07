using NMU_CE_App.Services;

namespace NMU_CE_App;

public partial class App : Application
{
    public App()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Init crash: {ex}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            try { _ = CdnCacheService.PreCacheAllAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[App] PreCache: {ex.Message}"); }

            var window = new Window(new AppShell());

#if WINDOWS
            window.HandlerChanged += (s, e) =>
            {
                try
                {
                    var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                    if (nativeWindow != null)
                    {
                        var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                        var titleBar = appWindow.TitleBar;
                        titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
                        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                        titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

                        appWindow.Changed += (_, args) =>
                        {
                            if (args.DidPresenterChange)
                            {
                                TitleBarService.RefreshFullscreenState();
                            }
                        };
                    }
                }
                catch { }
            };
#endif

            window.MinimumWidth = 800;
            window.MinimumHeight = 600;
            window.Title = "NMU-CE & AIE";

            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] CreateWindow: {ex.GetType().Name}: {ex.Message}");
            return new Window(new ContentPage
            {
                BackgroundColor = Colors.Black,
                Content = new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Startup Error",
                            TextColor = Colors.OrangeRed,
                            FontSize = 22,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new Label
                        {
                            Text = ex.GetType().Name + ": " + ex.Message,
                            TextColor = Colors.White,
                            FontSize = 14,
                            HorizontalTextAlignment = TextAlignment.Center,
                            Padding = new Thickness(20)
                        },
                        new Button
                        {
                            Text = "Close App",
                            BackgroundColor = Colors.OrangeRed,
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Center,
                            Margin = new Thickness(0, 20, 0, 0),
                            Command = new Command(() => Application.Current?.Quit())
                        }
                    }
                }
            });
        }
    }
}
