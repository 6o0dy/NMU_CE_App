using System.Runtime.InteropServices;
using NMU_CE_App.Pages;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            try { _ = CdnCacheService.PreCacheAllAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[App] PreCache: {ex.Message}"); }

            var window = new Window(new SplashPage());

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

                        ApplyWindowStyles(handle);
                        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
                            op.Restore();
                        nativeWindow.DispatcherQueue.TryEnqueue(
                            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                            () =>
                            {
                                ApplyWindowStyles(handle);
                                TitleBarService.RefreshFullscreenState();
                            });

                        appWindow.Changed += (_, args) =>
                        {
                            if (args.DidPresenterChange)
                                TitleBarService.RefreshFullscreenState();
                        };
                    }
                }
                catch { }
            };
#endif

            window.MinimumWidth = 800;
            window.MinimumHeight = 600;
            window.Title = "";

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

    private static void ApplyWindowStyles(IntPtr handle)
    {
#if WINDOWS
        const int GWL_STYLE = -16;
        const uint WS_CAPTION = 0x00C00000;
        const uint WS_SYSMENU = 0x00080000;
        const uint WS_MINIMIZEBOX = 0x00020000;
        const uint WS_MAXIMIZEBOX = 0x00010000;
        const uint SWP_FRAMECHANGED = 0x0020;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;

        uint style = GetWindowLong(handle, GWL_STYLE);
        style &= ~WS_CAPTION;
        style &= ~WS_SYSMENU;
        style &= ~WS_MINIMIZEBOX;
        style &= ~WS_MAXIMIZEBOX;
        SetWindowLong(handle, GWL_STYLE, style);
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
            SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE);
#endif
    }
}
