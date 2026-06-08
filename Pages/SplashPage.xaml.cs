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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            FooterLabel.Text = SessionService.GetFooterCredit();
            ResetForm();
            await HandleStartupFlow();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SplashPage] CRASH: {ex}");
        }
    }

    private void ResetForm()
    {
        NameEntry.Text = string.Empty;

        YearPicker.SelectedIndex = 0;

        _selectedTerm = null;

        Term1Btn.Stroke = Color.FromArgb("#374151");
        Term1Btn.BackgroundColor = Color.FromArgb("#1F2937");
        Term1Label.TextColor = Color.FromArgb("#9CA3AF");

        Term2Btn.Stroke = Color.FromArgb("#374151");
        Term2Btn.BackgroundColor = Color.FromArgb("#1F2937");
        Term2Label.TextColor = Color.FromArgb("#9CA3AF");

        SaveLabel.Text = "Save Data";
        SaveBtn.IsEnabled = true;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
    }

    private async Task HandleStartupFlow()
    {
        if (!_session.HasData)
        {
            SetupForm.IsVisible = true;
            LoadingSection.IsVisible = false;

            DialogCard.Opacity = 0;
            DialogCard.Scale = 0.95;

            await Task.WhenAll(
                DialogCard.FadeTo(1, 450, Easing.CubicOut),
                DialogCard.ScaleTo(1, 450, Easing.CubicOut)
            );
        }
        else
        {
            SetupForm.IsVisible = false;
            LoadingSection.IsVisible = true;
            LoadingLabel.Text = "Welcome back, loading...";

            await DialogCard.FadeTo(1, 300, Easing.CubicOut);
            DialogCard.Scale = 1;

            await Task.Delay(400);
            await GoToHome();
        }
    }

    private async Task GoToHome()
    {
        await Shell.Current.GoToAsync("//home");
    }

    private void OnTerm1Tapped(object? sender, TappedEventArgs e)
    {
        _selectedTerm = "Semester 1";
        Term1Btn.Stroke = Color.FromArgb("#00E5FF");
        Term1Btn.BackgroundColor = Color.FromArgb("#003344");
        Term1Label.TextColor = Color.FromArgb("#00E5FF");

        Term2Btn.Stroke = Color.FromArgb("#374151");
        Term2Btn.BackgroundColor = Color.FromArgb("#1F2937");
        Term2Label.TextColor = Color.FromArgb("#9CA3AF");
    }

    private void OnTerm2Tapped(object? sender, TappedEventArgs e)
    {
        _selectedTerm = "Semester 2";
        Term2Btn.Stroke = Color.FromArgb("#00E5FF");
        Term2Btn.BackgroundColor = Color.FromArgb("#003344");
        Term2Label.TextColor = Color.FromArgb("#00E5FF");

        Term1Btn.Stroke = Color.FromArgb("#374151");
        Term1Btn.BackgroundColor = Color.FromArgb("#1F2937");
        Term1Label.TextColor = Color.FromArgb("#9CA3AF");
    }

    private async void OnSaveBtnPointerEntered(object? sender, PointerEventArgs e)
    {
        await SaveBtn.ScaleTo(1.03, 150, Easing.CubicOut);
    }

    private async void OnSaveBtnPointerExited(object? sender, PointerEventArgs e)
    {
        await SaveBtn.ScaleTo(1.0, 150, Easing.CubicIn);
    }

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        await SaveBtn.ScaleTo(0.92, 100, Easing.CubicOut);
        await SaveBtn.ScaleTo(1, 100, Easing.CubicIn);

        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Error", "Please enter your name.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_selectedTerm))
        {
            await DisplayAlert("Error", "Please select a semester.", "OK");
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

        var level = year.Replace(" ", "_");
        var term = _selectedTerm.Replace(" ", "_");
        _ = CdnCacheService.PreCacheQuizDataAsync(level, term);

        SaveLabel.Text = "Loading...";
        SaveBtn.IsEnabled = false;

        await Task.Delay(200);
        await GoToHome();
    }
}