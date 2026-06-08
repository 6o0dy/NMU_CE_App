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

        var dragPointer = new PointerGestureRecognizer();
        dragPointer.PointerPressed += OnDragPointerPressed;
        RootGrid.GestureRecognizers.Add(dragPointer);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AttachHoverEffects();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdateWindowControlsVisibility();
        TitleBarService.FullscreenChanged += OnPresenterChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        TitleBarService.FullscreenChanged -= OnPresenterChanged;
    }

    private void OnPresenterChanged()
    {
        UpdateWindowControlsVisibility();
    }

    private void UpdateWindowControlsVisibility()
    {
#if WINDOWS
        var fs = TitleBarService.IsFullscreen;
        BtnMinimize.IsVisible = !fs;
        BtnClose.IsVisible = !fs;
        if (BtnFullscreen.Content is Label fullLabel)
            fullLabel.Text = fs ? "\uE73F" : "\uE740";
#endif
    }

    private void AttachHoverEffects()
    {
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
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const uint WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;
    private const int SW_MINIMIZE = 6;

    private static IntPtr GetNativeHandle()
    {
        var mauiWindow = App.Current?.Windows[0];
        if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            return WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        return IntPtr.Zero;
    }
#endif

    private static bool IsOverButton(Point pos, View? btn, View root)
    {
        if (btn == null || !btn.IsVisible) return false;
        double x = btn.X, y = btn.Y;
        var p = btn.Parent;
        while (p != null && p != root)
        {
            if (p is View v) { x += v.X; y += v.Y; }
            p = p.Parent;
        }
        return pos.X >= x && pos.X <= x + btn.Width &&
               pos.Y >= y && pos.Y <= y + btn.Height;
    }

    private bool IsOverAnyButton(Point pos)
    {
        return IsOverButton(pos, BtnBack, RootGrid) ||
               IsOverButton(pos, BtnFullscreen, RootGrid) ||
               IsOverButton(pos, BtnMinimize, RootGrid) ||
               IsOverButton(pos, BtnClose, RootGrid);
    }

    private void OnDragPointerPressed(object? sender, PointerEventArgs e)
    {
#if WINDOWS
        if (TitleBarService.IsFullscreen) return;
        var p = e.GetPosition(RootGrid);
        if (p == null) return;
        if (IsOverAnyButton(p.Value))
            return;

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
        var h = GetNativeHandle();
        if (h != IntPtr.Zero)
            ShowWindow(h, SW_MINIMIZE);
#endif
    }

    private void OnMaximizeTapped(object? sender, TappedEventArgs e) { }

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
