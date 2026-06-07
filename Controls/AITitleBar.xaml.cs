using System.Runtime.InteropServices;
using NMU_CE_App.Services;

namespace NMU_CE_App.Controls;

public partial class AITitleBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AITitleBar), "",
            propertyChanged: (b, _, n) => ((AITitleBar)b).TitleLabel.Text = (string?)n ?? "");

    public static readonly BindableProperty ShowBackProperty =
        BindableProperty.Create(nameof(ShowBack), typeof(bool), typeof(AITitleBar), false,
            propertyChanged: (b, _, n) => ((AITitleBar)b).BtnBack.IsVisible = (bool)n);

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowBack { get => (bool)GetValue(ShowBackProperty); set => SetValue(ShowBackProperty, value); }

    public event EventHandler? BackClicked;

    public AITitleBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AttachHoverEffects();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdateWindowControlsVisibility();
        RefreshMaximizeIcon();
        TitleBarService.FullscreenChanged += OnPresenterChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        TitleBarService.FullscreenChanged -= OnPresenterChanged;
    }

    private void OnPresenterChanged()
    {
        RefreshMaximizeIcon();
    }

    private void UpdateWindowControlsVisibility()
    {
#if WINDOWS
        BtnMinimize.IsVisible = true;
        BtnMaximize.IsVisible = true;
        BtnClose.IsVisible = true;
#endif
    }

    private void AttachHoverEffects()
    {
        AttachHover(BtnDragHandle, "#15FFFFFF", "#25FFFFFF");
        AttachHover(BtnFullscreen, "#15FFFFFF", "#25FFFFFF");
        AttachHover(BtnBack, "#15AIPrimary", "#25AIPrimary");
        AttachHover(BtnMinimize, "#15FFFFFF", "#25FFFFFF");
        AttachHover(BtnMaximize, "#15FFFFFF", "#25FFFFFF");
        AttachHover(BtnClose, "#20FF4757", "#50FF4757");
    }

    private static void AttachHover(View view, string hoverColor, string pressedColor)
    {
        var hover = Color.FromArgb(hoverColor);
        var defaultBg = view.BackgroundColor;
        var pointerEnter = new PointerGestureRecognizer();
        pointerEnter.PointerEntered += (_, _) => view.BackgroundColor = hover;
        pointerEnter.PointerExited += (_, _) => view.BackgroundColor = defaultBg;
        view.GestureRecognizers.Add(pointerEnter);
    }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private const uint WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;
    private const int HTMINBUTTON = 8;
    private const int HTMAXBUTTON = 9;
    private const int HTCLOSE = 20;

    private static IntPtr GetNativeHandle()
    {
        var mauiWindow = App.Current?.Windows[0];
        if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            return WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        return IntPtr.Zero;
    }

    private static Microsoft.UI.Windowing.AppWindow? GetAppWindow()
    {
        var h = GetNativeHandle();
        if (h == IntPtr.Zero) return null;
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(h);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }
#endif

    private void OnDragTapped(object? sender, TappedEventArgs e)
    {
#if WINDOWS
        var h = GetNativeHandle();
        if (h != IntPtr.Zero)
        {
            ReleaseCapture();
            SendMessage(h, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
#endif
    }

    private void OnMinimizeTapped(object? sender, TappedEventArgs e)
    {
#if WINDOWS
        try
        {
            if (GetAppWindow()?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
                op.Minimize();
        }
        catch { }
#endif
    }

    private void OnMaximizeTapped(object? sender, TappedEventArgs e)
    {
#if WINDOWS
        try
        {
            if (GetAppWindow()?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
            {
                var isMax = op.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
                if (isMax)
                    op.Restore();
                else
                    op.Maximize();
                RefreshMaximizeIcon();
            }
        }
        catch { }
#endif
    }

    public void RefreshMaximizeIcon()
    {
#if WINDOWS
        try
        {
            if (GetAppWindow()?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
            {
                var isMax = op.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
                MaxMaxIcon.IsVisible = !isMax;
                MaxRestoreIcon.IsVisible = isMax;
            }
        }
        catch { }
#endif
    }

    private void OnCloseTapped(object? sender, TappedEventArgs e)
    {
#if WINDOWS
        Application.Current?.Quit();
#endif
    }

    private void OnFullscreenTapped(object? sender, TappedEventArgs e)
    {
        TitleBarService.ToggleFullscreen();
    }

    private void OnBackTapped(object? sender, TappedEventArgs e)
    {
        BackClicked?.Invoke(this, EventArgs.Empty);
    }
}
