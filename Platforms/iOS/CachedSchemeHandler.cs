using Foundation;
using WebKit;
using NMU_CE_App.Services;

namespace NMU_CE_App;

public class CachedSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        var url = urlSchemeTask.Request.Url?.ToString();
        if (url == null)
        {
            urlSchemeTask.DidFailWithError(new NSError(NSError.CocoaErrorDomain, -1));
            return;
        }

        var originalUrl = CdnCacheService.OriginalUrlFromScheme(url);
        var cachedPath = CdnCacheService.GetCachedFile(originalUrl);

        if (cachedPath == null)
        {
            // On-demand download: if PreCache didn't finish yet, fetch now
            var evt = new System.Threading.ManualResetEvent(false);
            Task.Run(async () =>
            {
                cachedPath = await CdnCacheService.DownloadAndCacheAsync(originalUrl);
                evt.Set();
            });
            evt.WaitOne(TimeSpan.FromSeconds(15));
        }

        if (cachedPath != null)
        {
            try
            {
                var data = NSData.FromFile(cachedPath);
                var mime = CdnCacheService.GetMimeType(originalUrl);
                var nsUrl = new NSUrl(url);
                var response = new NSHttpUrlResponse(nsUrl, 200, "OK", new NSDictionary());

                urlSchemeTask.DidReceiveResponse(response);
                urlSchemeTask.DidReceiveData(data);
                urlSchemeTask.DidFinish();
                return;
            }
            catch { }
        }

        urlSchemeTask.DidFailWithError(new NSError(NSError.CocoaErrorDomain, -1001, new NSDictionary()));
    }

    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
    }
}
