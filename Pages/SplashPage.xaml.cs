using NMU_CE_App.Models;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class SplashPage : ContentPage
{
    private readonly SessionService _session = new();
    private string? _selectedTerm;

    public SplashPage()
    {
        InitializeComponent();
        UpdateFooter();
        YearPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await HandleStartupFlow();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
    }

    private void UpdateFooter()
    {
        FooterLabel.Text = SessionService.GetFooterCredit();
    }

    private async Task HandleStartupFlow()
    {
        if (!_session.HasSeenScan)
        {
            await ShowLoadingScreen();
            _session.MarkScanDone();
        }

        if (!_session.HasData)
        {
            await Task.Delay(200);
            await ShowSetupModal();
        }
        else
        {
            await Task.Delay(300);
            await GoToHome();
        }
    }

    private async Task ShowLoadingScreen()
    {
        LoadingOverlay.IsVisible = true;
        LoadingOverlay.Opacity = 0;
        await LoadingOverlay.FadeToAsync(1, 200);

        GlowLine.WidthRequest = 0;
        GlowLine.AnchorX = 0.5;

        await Task.Delay(300);
        await GlowLine.ScaleXToAsync(1, 1200, Easing.CubicOut);

        await Task.Delay(200);
        await LoadingOverlay.FadeToAsync(0, 300);
        LoadingOverlay.IsVisible = false;
    }

    private async Task ShowSetupModal()
    {
        SetupOverlay.IsVisible = true;
        SetupOverlay.Opacity = 0;
        await SetupOverlay.FadeToAsync(1, 200);
    }

    private async Task GoToHome()
    {
        await Shell.Current.GoToAsync("//home");
    }

    private void OnTerm1Tapped(object? sender, TappedEventArgs e)
    {
        _selectedTerm = "Semester 1";
        Term1Tile.Stroke = Color.FromArgb("#00F2FF");
        Term1Tile.BackgroundColor = Color.FromArgb("#2600F2FF");
        Term1Label.TextColor = Color.FromArgb("#00F2FF");

        Term2Tile.Stroke = Color.FromArgb("#334155");
        Term2Tile.BackgroundColor = Color.FromArgb("#0F172A");
        Term2Label.TextColor = Color.FromArgb("#94A3B8");
    }

    private void OnTerm2Tapped(object? sender, TappedEventArgs e)
    {
        _selectedTerm = "Semester 2";
        Term2Tile.Stroke = Color.FromArgb("#00F2FF");
        Term2Tile.BackgroundColor = Color.FromArgb("#2600F2FF");
        Term2Label.TextColor = Color.FromArgb("#00F2FF");

        Term1Tile.Stroke = Color.FromArgb("#334155");
        Term1Tile.BackgroundColor = Color.FromArgb("#0F172A");
        Term1Label.TextColor = Color.FromArgb("#94A3B8");
    }

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlertAsync("Error", "Please enter your name!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_selectedTerm))
        {
            await DisplayAlertAsync("Error", "Please select a semester!", "OK");
            return;
        }

        var year = YearPicker.SelectedItem as string ?? "Level 1";

        var profile = new StudentProfile
        {
            Name = name,
            Year = year,
            Term = _selectedTerm
        };

        _session.SaveStudentProfile(profile);

        await SetupOverlay.FadeToAsync(0, 200);
        SetupOverlay.IsVisible = false;

        await GoToHome();
    }

    private async void OnFullscreenTapped(object? sender, TappedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
#if WINDOWS
        try
        {
            var mauiWindow = App.Current?.Windows[0];
            if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                else
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
            }
        }
        catch { }
#endif
    }
}
