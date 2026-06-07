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
            UpdateFooter();
            YearPicker.SelectedIndex = 0;
            await HandleStartupFlow();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SplashPage] CRASH: {ex}");
        }
    }

    private void UpdateFooter()
    {
        FooterLabel.Text = SessionService.GetFooterCredit();
    }

    private async Task HandleStartupFlow()
    {
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

        // Pre-cache quiz data for the selected level/term in background
        var level = year.Replace(" ", "_");
        var term = _selectedTerm.Replace(" ", "_");
        _ = CdnCacheService.PreCacheQuizDataAsync(level, term);

        await SetupOverlay.FadeToAsync(0, 200);
        SetupOverlay.IsVisible = false;

        await GoToHome();
    }
}
