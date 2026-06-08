namespace NMU_CE_App.Services;

public static class FullscreenService
{
    public static bool IsFullscreen { get; private set; }

    public static void Toggle()
    {
        IsFullscreen = !IsFullscreen;
#if WINDOWS
        ToggleWindows();
#elif ANDROID
        ToggleAndroid();
#elif IOS || MACCATALYST
        ToggleApple();
#endif
    }

#if WINDOWS
    private static void ToggleWindows()
    {
        try
        {
            var mauiWindow = App.Current?.Windows[0];
            if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                {
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                    if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
                        op.Restore();
                }
                else
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
            }
        }
        catch { }
    }
#elif ANDROID
    private static void ToggleAndroid()
    {
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null) return;
            var window = activity.Window;
            if (window == null) return;

            if (IsFullscreen)
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                {
                    window.SetDecorFitsSystemWindows(false);
                    window.InsetsController?.Hide(Android.Views.WindowInsets.Type.SystemBars());
                }
                else
                {
                    window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
                    window.DecorView.SystemUiVisibility = (Android.Views.StatusBarVisibility)(
                        Android.Views.SystemUiFlags.Fullscreen |
                        Android.Views.SystemUiFlags.HideNavigation |
                        Android.Views.SystemUiFlags.ImmersiveSticky);
                }
            }
            else
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                {
                    window.SetDecorFitsSystemWindows(true);
                    window.InsetsController?.Show(Android.Views.WindowInsets.Type.SystemBars());
                }
                else
                {
                    window.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
                    window.DecorView.SystemUiVisibility = Android.Views.StatusBarVisibility.Visible;
                }
            }
        }
        catch { }
    }
#elif IOS || MACCATALYST
    private static void ToggleApple()
    {
        try
        {
            var window = UIKit.UIApplication.SharedApplication.KeyWindow;
            if (window == null) return;
            window.WindowScene?.RequestGeometryUpdate(
                new UIKit.UIWindowSceneGeometryPreferencesIOS(
                    IsFullscreen
                        ? UIKit.UIInterfaceOrientationMask.All
                        : UIKit.UIInterfaceOrientationMask.Portrait),
                error => { });
        }
        catch { }
    }
#endif
}
