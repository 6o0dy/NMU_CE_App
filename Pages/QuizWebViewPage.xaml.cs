using NMU_CE_App.Services;
using System.Text.Json;

namespace NMU_CE_App.Pages;

public partial class QuizWebViewPage : ContentPage
{
    private readonly QuizService _quizService = new();
    private readonly SessionService _session = new();
    private bool _initialized;
    private CancellationTokenSource? _loadCts;

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
            await LoadHtml();
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        NavHelper.Back(this);
    }

    private async Task LoadHtml()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = _loadCts.Token;

        LoadingOverlay.IsVisible = true;
        CacheSummaryLabel.IsVisible = false;
        var summary = new List<string>();

        try
        {
            ct.ThrowIfCancellationRequested();

            LoadingLabel.Text = "Fetching metadata...";
            var metaJson = await FetchWithTimeout(() => _quizService.GetRawMetaAsync(), "meta", summary, ct);

            ct.ThrowIfCancellationRequested();
            LoadingLabel.Text = "Fetching order config...";
            var orderJson = await FetchWithTimeout(() => _quizService.GetOrderConfigAsync(), "order", summary, ct);

            ct.ThrowIfCancellationRequested();
            LoadingLabel.Text = "Loading profile...";
            var profile = _session.GetStudentProfile();
            var profileJson = profile != null
                ? JsonSerializer.Serialize(new { year = profile.Year, term = profile.Term })
                : null;
            summary.Add(profile != null ? "Profile: loaded" : "Profile: none");

            ct.ThrowIfCancellationRequested();
            LoadingLabel.Text = "Building page...";
            var html = await FetchWithTimeout(
                () => QuizWebViewService.BuildHtmlWithData(metaJson, orderJson, profileJson),
                "page builder", summary, ct);

            ct.ThrowIfCancellationRequested();
            var filePath = Path.Combine(FileSystem.CacheDirectory, $"quiz_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(filePath, html);
            QuizWebView.Source = new UrlWebViewSource { Url = filePath };
            _initialized = true;

            LoadingLabel.Text = "Ready!";
            CacheSummaryLabel.Text = string.Join("  ·  ", summary);
            CacheSummaryLabel.IsVisible = true;

            // Hide overlay after showing summary for 2 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000, CancellationToken.None);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!ct.IsCancellationRequested)
                        LoadingOverlay.IsVisible = false;
                });
            });
        }
        catch (OperationCanceledException)
        {
            LoadingLabel.Text = "Timed out";
            CacheSummaryLabel.Text = string.Join("  ·  ", summary) + "  ·  Timed out!";
            CacheSummaryLabel.IsVisible = true;
            _initialized = true;
        }
        catch (Exception ex)
        {
            _initialized = true;
            LoadingLabel.Text = "Error loading quiz";
            CacheSummaryLabel.Text = string.Join("  ·  ", summary) + $"  ·  Error: {ex.Message}";
            CacheSummaryLabel.TextColor = Color.FromArgb("#FF6666");
            CacheSummaryLabel.IsVisible = true;

            System.Diagnostics.Debug.WriteLine($"[QuizWebViewPage] {ex}");

            NavHelper.Go(this, new DebugErrorPage(ex.Message, ex.StackTrace));
        }
    }

    private static async Task<T> FetchWithTimeout<T>(Func<Task<T>> fetch, string label, List<string> summary, CancellationToken ct)
    {
        try
        {
            var result = await fetch();
            summary.Add($"{label}: ok");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            summary.Add($"{label}: failed");
            throw;
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingOverlay.IsVisible = false;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url == null)
            return;
        if (!e.Url.StartsWith("app://") && !e.Url.StartsWith("https://nmu.app/"))
            return;

        e.Cancel = true;
        _ = HandleNavigationUrl(e.Url);
    }

    private async Task HandleNavigationUrl(string url)
    {
        try
        {
            if (url.StartsWith("https://nmu.app/fetch?path="))
            {
                LoadingOverlay.IsVisible = true;
                LoadingLabel.Text = "Fetching quiz...";
                var path = Uri.UnescapeDataString(url.Substring("https://nmu.app/fetch?path=".Length));
                await LoadAndInjectQuiz(path);
                LoadingOverlay.IsVisible = false;
            }
            else if (url.StartsWith("https://nmu.app/quiz/load?") || url.StartsWith("app://quiz/load?"))
            {
                var qs = url.Substring(url.IndexOf('?') + 1);
                var parts = qs.Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                              .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
                var path = parts.GetValueOrDefault("path");
                if (!string.IsNullOrEmpty(path))
                    await LoadAndInjectQuiz(path);
            }
        }
        catch
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private async Task LoadAndInjectQuiz(string path)
    {
        try
        {
            var chapters = await _quizService.GetQuizAsync(path);
            if (chapters.Count == 0)
            {
                LoadingOverlay.IsVisible = true;
                LoadingLabel.Text = "No data available offline";
                CacheSummaryLabel.Text = "Quiz data wasn't pre-cached. Connect to the internet and open this subject once, then it'll work offline.";
                CacheSummaryLabel.TextColor = Color.FromArgb("#FFAA44");
                CacheSummaryLabel.IsVisible = true;
                return;
            }
            var chaptersJson = JsonSerializer.Serialize(chapters);
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
            LoadingOverlay.IsVisible = false;
            System.Diagnostics.Debug.WriteLine($"[QuizWebViewPage] LoadAndInjectQuiz: {ex.Message}");

            NavHelper.Go(this, new DebugErrorPage(ex.Message, ex.StackTrace));
        }
    }
}
