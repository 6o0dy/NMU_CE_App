using Android.Webkit;
using NMU_CE_App.Services;

namespace NMU_CE_App;

public class CachedWebViewClient : WebViewClient
{
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
}
