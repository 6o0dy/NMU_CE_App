namespace NMU_CE_App.Services;

public static class TitleBarService
{
    public static event Action? FullscreenChanged;

    private static bool _isFullscreen;
    public static bool IsFullscreen
    {
        get => _isFullscreen;
        private set
        {
            if (_isFullscreen != value)
            {
                _isFullscreen = value;
                FullscreenChanged?.Invoke();
            }
        }
    }

    public static bool IsMobile =>
        DeviceInfo.Idiom == DeviceIdiom.Phone ||
        DeviceInfo.Idiom == DeviceIdiom.Tablet;

    public static void ToggleFullscreen()
    {
        FullscreenService.Toggle();
    }

    public static void RefreshFullscreenState()
    {
#if WINDOWS
        try
        {
            var mauiWindow = App.Current?.Windows[0];
            if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                IsFullscreen = appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
            }
        }
        catch
        {
            IsFullscreen = false;
        }
#else
        IsFullscreen = FullscreenService.IsFullscreen;
#endif
    }
}
