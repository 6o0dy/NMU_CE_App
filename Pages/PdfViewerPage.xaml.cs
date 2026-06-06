using System.Security.Cryptography;
using System.Text;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

[QueryProperty(nameof(FileUrl), "url")]
[QueryProperty(nameof(FileName), "name")]
public partial class PdfViewerPage : ContentPage
{
    private string _fileUrl = "";
    private string _fileName = "";
    private bool _isDownloading;

    public string FileUrl
    {
        get => _fileUrl;
        set
        {
            _fileUrl = Uri.UnescapeDataString(value ?? "");
            if (!string.IsNullOrEmpty(_fileUrl))
                LoadPdf();
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = Uri.UnescapeDataString(value ?? "");
            PageTitle.Text = _fileName;
        }
    }

    public PdfViewerPage()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
    }

    private static string GetCacheKey(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        return $"pdf_{hash}";
    }

    private string GetCachedPath()
    {
        var ext = Path.GetExtension(_fileName);
        if (string.IsNullOrEmpty(ext)) ext = ".pdf";
        return Path.Combine(FileSystem.CacheDirectory, GetCacheKey(_fileUrl) + ext);
    }

    private void LoadPdf()
    {
        LoadingOverlay.IsVisible = true;
        var cachedPath = GetCachedPath();

        if (File.Exists(cachedPath))
        {
            var localUrl = new Uri(cachedPath).AbsoluteUri;
            PdfViewer.Source = new UrlWebViewSource { Url = localUrl };
            HideLoadingAfterDelay();
        }
        else
        {
            PdfViewer.Source = new UrlWebViewSource { Url = _fileUrl };
            HideLoadingAfterDelay();
            _ = CachePdfAsync(cachedPath);
        }
    }

    private async Task CachePdfAsync(string cachedPath)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var data = await http.GetByteArrayAsync(_fileUrl);
            await File.WriteAllBytesAsync(cachedPath, data);
        }
        catch { }
    }

    private void HideLoadingAfterDelay()
    {
        EventHandler<WebNavigatedEventArgs>? handler = null;
        handler = (_, _) =>
        {
            PdfViewer.Navigated -= handler;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(800);
                LoadingOverlay.IsVisible = false;
            });
        };
        PdfViewer.Navigated += handler;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        PdfViewer.Source = new UrlWebViewSource { Url = "about:blank" };
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDownloadTapped(object? sender, TappedEventArgs e)
    {
        if (_isDownloading || string.IsNullOrEmpty(_fileUrl)) return;
        _isDownloading = true;

        try
        {
            var dlBtn = DownloadBtn;
            dlBtn.IsEnabled = false;
            dlBtn.Opacity = 0.5;

            var fileName = FileDownloader.SanitizeFileName(_fileName);
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains('.'))
                fileName = "document.pdf";

            var savedPath = await FileDownloader.DownloadFileAsync(_fileUrl, fileName);
            await FileDownloader.OpenFileAsync(savedPath);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Download Error", $"Could not download file.\n{ex.Message}", "OK");
        }
        finally
        {
            var dlBtn = DownloadBtn;
            dlBtn.IsEnabled = true;
            dlBtn.Opacity = 1;
            _isDownloading = false;
        }
    }
}
