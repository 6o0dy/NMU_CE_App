using Android.Graphics;
using Android.Webkit;
using NMU_CE_App.Services;

namespace NMU_CE_App;

public class CachedWebViewClient : WebViewClient
{
    private readonly WebViewClient? _originalClient;

    public CachedWebViewClient(WebViewClient? originalClient)
    {
        _originalClient = originalClient;
    }

    public override WebResourceResponse? ShouldInterceptRequest(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        var url = request?.Url?.ToString();
        if (url != null && CdnCacheService.IsCdnUrl(url))
        {
            var cachedPath = CdnCacheService.GetCachedFile(url);
            if (cachedPath != null)
            {
                var mime = CdnCacheService.GetMimeType(url);
                try
                {
                    var stream = System.IO.File.OpenRead(cachedPath);
                    var headers = new Dictionary<string, string>
                    {
                        { "Access-Control-Allow-Origin", "*" },
                        { "Cache-Control", "public, max-age=31536000, immutable" }
                    };
                    return new WebResourceResponse(mime, null, 200, "OK", headers, stream);
                }
                catch { }
            }
        }
        return base.ShouldInterceptRequest(view, request);
    }

    public override void OnPageStarted(global::Android.Webkit.WebView? view, string? url, Bitmap? favicon)
    {
        _originalClient?.OnPageStarted(view, url, favicon);
        base.OnPageStarted(view, url, favicon);
    }

    public override void OnPageFinished(global::Android.Webkit.WebView? view, string? url)
    {
        _originalClient?.OnPageFinished(view, url);
        base.OnPageFinished(view, url);
    }

    public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        if (_originalClient != null && _originalClient.ShouldOverrideUrlLoading(view, request))
            return true;
        return base.ShouldOverrideUrlLoading(view, request);
    }

    public override void OnReceivedError(global::Android.Webkit.WebView? view, IWebResourceRequest? request, WebResourceError? error)
    {
        _originalClient?.OnReceivedError(view, request, error);
        base.OnReceivedError(view, request, error);
    }

    public override void OnReceivedHttpError(global::Android.Webkit.WebView? view, IWebResourceRequest? request, WebResourceResponse? errorResponse)
    {
        _originalClient?.OnReceivedHttpError(view, request, errorResponse);
        base.OnReceivedHttpError(view, request, errorResponse);
    }
}
