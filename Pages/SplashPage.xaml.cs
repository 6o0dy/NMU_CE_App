using NMU_CE_App.Models;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class SplashPage : ContentPage
{
    private readonly SessionService _session = new();
    private string? _selectedTerm;
    private bool _hasAnimated;

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
        if (!_session.HasData)
        {
            SetupForm.IsVisible = true;
            SetupForm.Opacity = 0;
            await Task.Delay(150);
            await AnimateEntrance();
        }
        else
        {
            LoadingSection.IsVisible = true;
            SetupForm.IsVisible = false;
            LoadingLabel.Text = "Welcome back! Loading your workspace...";
            await Task.Delay(400);
            await GoToHome();
        }
    }

    private async Task AnimateEntrance()
    {
        var heroElements = new View[] { MainTitle, Term1Tile, Term2Tile, SaveBtn };
        foreach (var el in heroElements)
            el.Opacity = 0;

        await Task.WhenAll(
            HeroSection.FadeToAsync(1, 600, Easing.CubicOut),
            HeroSection.TranslateTo(0, 0, 500, Easing.CubicOut)
        );

        var formElements = new (View element, int delay)[]
        {
            (SetupForm, 100),
        };

        foreach (var (element, delay) in formElements)
        {
            element.Opacity = 0;
            element.TranslationY = 30;
        }

        for (int i = 0; i < formElements.Length; i++)
        {
            var (element, delay) = formElements[i];
            await Task.Delay(delay);
            await Task.WhenAll(
                element.FadeToAsync(1, 400, Easing.CubicOut),
                element.TranslateTo(0, 0, 400, Easing.CubicOut)
            );
        }

        var staggerItems = new (View element, int delay)[]
        {
            (NameEntry.Parent is Border nameBorder ? nameBorder.Parent as VerticalStackLayout ?? SetupForm : SetupForm, 80),
        };

        var children = new View[]
        {
            MainTitle, Term1Tile, Term2Tile, SaveBtn
        };

        foreach (var child in children)
        {
            if (child == MainTitle) continue;
            child.Opacity = 0;
            child.TranslationY = 20;
        }

        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child == MainTitle) continue;
            await Task.Delay(80);
            await Task.WhenAll(
                child.FadeToAsync(1, 350, Easing.CubicOut),
                child.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }

        _hasAnimated = true;
    }

    private async Task GoToHome()
    {
        await Shell.Current.GoToAsync("//home");
    }

    private void OnTerm1Tapped(object? sender, TappedEventArgs e)
    {
        _selectedTerm = "Semester 1";
        Term1Tile.Stroke = Color.FromArgb("#7C3AED");
        Term1Tile.BackgroundColor = Color.FromArgb("#267C3AED");
        Term1Label.TextColor = Color.FromArgb("#7C3AED");

        Term2Tile.Stroke = Color.FromArgb("#334155");
        Term2Tile.BackgroundColor = Color.FromArgb("#0F172A");
        Term2Label.TextColor = Color.FromArgb("#94A3B8");
    }

    private void OnTerm2Tapped(object? sender, TappedEventArgs e)
    {
        _selectedTerm = "Semester 2";
        Term2Tile.Stroke = Color.FromArgb("#7C3AED");
        Term2Tile.BackgroundColor = Color.FromArgb("#267C3AED");
        Term2Label.TextColor = Color.FromArgb("#7C3AED");

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

        var level = year.Replace(" ", "_");
        var term = _selectedTerm.Replace(" ", "_");
        _ = CdnCacheService.PreCacheQuizDataAsync(level, term);

        SaveLabel.Text = "✓ Starting...";
        SaveBtn.BackgroundColor = Color.FromArgb("#06D6A0");
        SaveBtn.IsEnabled = false;

        await Task.Delay(300);
        await GoToHome();
    }
}
