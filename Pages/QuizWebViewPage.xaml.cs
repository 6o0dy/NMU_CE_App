using NMU_CE_App.Services;
using System.Text.Json;

namespace NMU_CE_App.Pages;

public partial class QuizWebViewPage : ContentPage
{
    private readonly QuizService _quizService = new();
    private bool _initialized;

    public QuizWebViewPage()
    {
        InitializeComponent();
        QuizWebView.Navigating += OnNavigating;
        QuizWebView.Navigated += OnNavigated;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialized)
        {
            _initialized = true;
            await LoadHtml();
        }
    }

    private readonly SessionService _session = new();

    private async Task LoadHtml()
    {
        try
        {
            LoadingOverlay.IsVisible = true;
            LoadingLabel.Text = "Fetching data...";

            var metaJson = await _quizService.GetRawMetaAsync();
            var orderJson = await _quizService.GetOrderConfigAsync();

            var profile = _session.GetStudentProfile();
            var profileJson = profile != null ? JsonSerializer.Serialize(new { year = profile.Year, term = profile.Term }) : null;

            LoadingLabel.Text = "Building page...";
            var html = await QuizWebViewService.BuildHtmlWithData(metaJson, orderJson, profileJson);

            // Save to file for valid file:// origin (localStorage works)
            var filePath = Path.Combine(FileSystem.CacheDirectory, "quiz.html");
            await File.WriteAllTextAsync(filePath, html);
            QuizWebView.Source = new UrlWebViewSource { Url = filePath };
        }
        catch (Exception ex)
        {
            LoadingLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingOverlay.IsVisible = false;
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url == null)
            return;
        if (!e.Url.StartsWith("app://") && !e.Url.StartsWith("https://nmu.app/"))
            return;

        e.Cancel = true;

        if (e.Url.StartsWith("https://nmu.app/fetch?path="))
        {
            LoadingOverlay.IsVisible = true;
            LoadingLabel.Text = "Fetching quiz...";
            var path = Uri.UnescapeDataString(e.Url.Substring("https://nmu.app/fetch?path=".Length));
            await LoadAndInjectQuiz(path);
            LoadingOverlay.IsVisible = false;
        }
        else if (e.Url.StartsWith("app://quiz/load?"))
        {
            var qs = e.Url.Substring(e.Url.IndexOf('?') + 1);
            var parts = qs.Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                          .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
            var path = parts.GetValueOrDefault("path");
            if (!string.IsNullOrEmpty(path))
                await LoadAndInjectQuiz(path);
        }
    }

    private async Task LoadAndInjectQuiz(string path)
    {
        try
        {
            var chapters = await _quizService.GetQuizAsync(path);
            var chaptersJson = JsonSerializer.Serialize(chapters);
            // Build a new HTML page with quiz data embedded, then navigate to it
            var metaJson = await _quizService.GetRawMetaAsync();
            var orderJson = await _quizService.GetOrderConfigAsync();
            var profile = _session.GetStudentProfile();
            var profileJson = profile != null ? JsonSerializer.Serialize(new { year = profile.Year, term = profile.Term }) : null;
            var html = await QuizWebViewService.BuildHtmlWithData(metaJson, orderJson, profileJson, chaptersJson, path);
            var filePath = Path.Combine(FileSystem.CacheDirectory, $"quiz_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(filePath, html);
            QuizWebView.Source = new UrlWebViewSource { Url = filePath };
        }
        catch (Exception ex)
        {
            LoadingLabel.Text = $"Error: {ex.Message}";
        }
    }
}
