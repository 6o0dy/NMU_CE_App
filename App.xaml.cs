using NMU_CE_App.Services;

namespace NMU_CE_App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ = CdnCacheService.PreCacheAllAsync();

        var window = new Window(new AppShell());

#if WINDOWS
        window.Created += (s, e) =>
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
}
