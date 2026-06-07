namespace NMU_CE_App.Pages;

[QueryProperty(nameof(VideoUrl), "url")]
[QueryProperty(nameof(Title), "title")]
public partial class YouTubePlayerPage : ContentPage
{
    private string _videoUrl = "";
    private string _title = "";

    public string VideoUrl { get => _videoUrl; set => _videoUrl = Uri.UnescapeDataString(value ?? ""); }
    public string Title { get => _title; set { _title = Uri.UnescapeDataString(value ?? ""); PageTitle.Text = _title; } }

    public YouTubePlayerPage()
    {
        InitializeComponent();
        PlayerWebView.Navigating += OnWebViewNavigating;
    }

    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("nmu://back") == true)
        {
            e.Cancel = true;
            _ = HandleBack();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(_videoUrl))
            LoadPlayer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        PlayerWebView.Source = "about:blank";
    }

    private void LoadPlayer()
    {
        var videoId = ExtractYouTubeId(_videoUrl);
        if (string.IsNullOrEmpty(videoId))
        {
            PlayerWebView.Source = new HtmlWebViewSource
            {
                Html = @"<!DOCTYPE html><html><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>
<body style='background:#000;color:#fff;display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif;text-align:center'>
<div><div style='font-size:48px;margin-bottom:16px'>⚠️</div><div style='font-size:16px;font-weight:600'>Invalid Video URL</div></div>
</body></html>"
            };
            return;
        }

        PlayerWebView.Source = $"https://www.youtube.com/watch?v={videoId}";
    }

    private static string? ExtractYouTubeId(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var patterns = new[]
        {
            @"(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/|youtube\.com\/v\/|youtube\.com\/shorts\/)([a-zA-Z0-9_-]{11})",
            @"^([a-zA-Z0-9_-]{11})$"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, pattern);
            if (match.Success) return match.Groups[1].Value;
        }

        return null;
    }

    private async Task HandleBack()
    {
        PlayerWebView.Source = "about:blank";
        await Shell.Current.GoToAsync("..");
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }
}
