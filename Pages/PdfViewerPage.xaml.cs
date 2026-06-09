using System.Security.Cryptography;
using System.Text;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class PdfViewerPage : ContentPage
{
    private string _fileUrl = "";
    private string _fileName = "";
    private bool _isDownloading;

    public PdfViewerPage(string url, string name)
    {
        InitializeComponent();
        _fileUrl = Uri.UnescapeDataString(url ?? "");
        _fileName = Uri.UnescapeDataString(name ?? "");
        PageTitle.Text = _fileName;

        if (!string.IsNullOrEmpty(_fileUrl))
            LoadPdf();
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
            LoadFromCache(cachedPath);
            HideLoadingAfterDelay();
        }
        else
        {
            SyncPdfViewer.DocumentSource = null;
            HideLoadingAfterDelay();
            _ = CachePdfAsync(cachedPath);
        }
    }

    private void LoadFromCache(string cachedPath)
    {
        var stream = File.OpenRead(cachedPath);
        SyncPdfViewer.DocumentSource = stream;
    }

    private async Task CachePdfAsync(string cachedPath)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var data = await http.GetByteArrayAsync(_fileUrl);
            await File.WriteAllBytesAsync(cachedPath, data);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var stream = File.OpenRead(cachedPath);
                SyncPdfViewer.DocumentSource = stream;
            });
        }
        catch { }
    }

    private void HideLoadingAfterDelay()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(1200);
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(500);
                LoadingOverlay.IsVisible = false;
            });
        });
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        SyncPdfViewer.DocumentSource = null;
        NavHelper.Back(this);
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
