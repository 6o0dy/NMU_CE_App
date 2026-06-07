using NMU_CE_App.Services;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NMU_CE_App;

public static class CachedWebViewHandler
{
    public static void Attach(Microsoft.UI.Xaml.Controls.WebView2 webView)
    {
        webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
        webView.CoreWebView2.AddWebResourceRequestedFilter(
            "https://cdnjs.cloudflare.com/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        webView.CoreWebView2.AddWebResourceRequestedFilter(
            "https://cdn.jsdelivr.net/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        webView.CoreWebView2.AddWebResourceRequestedFilter(
            "https://fonts.googleapis.com/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        webView.CoreWebView2.AddWebResourceRequestedFilter(
            "https://fonts.gstatic.com/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        webView.CoreWebView2.AddWebResourceRequestedFilter(
            "https://archive.org/download/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
    }

    private static void OnWebResourceRequested(Microsoft.Web.WebView2.Core.CoreWebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs e)
    {
        var url = e.Request.Uri;
        if (!CdnCacheService.IsCdnUrl(url)) return;

        var cachedPath = CdnCacheService.GetCachedFile(url);
        if (cachedPath == null) return;

        try
        {
            var deferral = e.GetDeferral();
            try
            {
                var file = StorageFile.GetFileFromPathAsync(cachedPath).AsTask().GetAwaiter().GetResult();
                var stream = file.OpenReadAsync().AsTask().GetAwaiter().GetResult();
                var mime = CdnCacheService.GetMimeType(url);

                var response = sender.Environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: " + mime);
                e.Response = response;
            }
            finally { deferral.Complete(); }
        }
        catch { }
    }
}
