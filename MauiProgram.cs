using Microsoft.Extensions.Logging;
using NMU_CE_App.Services;
using NMU_CE_App.Converters;
using Microsoft.Maui.Handlers;
using Syncfusion.Maui.Core.Hosting;
using CommunityToolkit.Maui;

namespace NMU_CE_App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        try
        {
            return BuildMauiApp();
        }
        catch (Exception ex)
        {
            // Log to all available targets before crashing
            System.Diagnostics.Debug.WriteLine($"[FATAL] CreateMauiApp: {ex}");
#if ANDROID
            try { Android.Util.Log.Error("NMU_CE", $"FATAL CreateMauiApp: {ex}"); } catch { }
#endif
            try
            {
                var crashPath = Path.Combine(FileSystem.CacheDirectory, "crash.log");
                File.WriteAllText(crashPath, $"CreateMauiApp: {ex}");
            }
            catch { }
            throw;
        }
    }

    private static MauiApp BuildMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Cairo.ttf", "Cairo");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                WebViewHandler.Mapper.AppendToMapping("WebViewSettings", (handler, view) =>
                {
                    if (handler.PlatformView is Android.Webkit.WebView wv)
                    {
                        wv.Settings.JavaScriptEnabled = true;
                        wv.Settings.DomStorageEnabled = true;
                        wv.Settings.AllowFileAccess = true;
                        wv.Settings.AllowFileAccessFromFileURLs = true;
                        wv.Settings.AllowUniversalAccessFromFileURLs = true;
                        wv.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
                    }
                });
                WebViewHandler.Mapper.PrependToMapping(nameof(WebView.Source), (handler, view) =>
                {
                    if (handler.PlatformView is Android.Webkit.WebView wv)
                    {
                        wv.Settings.JavaScriptEnabled = true;
                        wv.Settings.DomStorageEnabled = true;
                        wv.Settings.AllowFileAccess = true;
                        wv.Settings.AllowFileAccessFromFileURLs = true;
                        wv.Settings.AllowUniversalAccessFromFileURLs = true;
                        wv.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
                    }
                });

                WebViewHandler.Mapper.AppendToMapping("CachedClient", (handler, view) =>
                {
                    if (handler.PlatformView is Android.Webkit.WebView wv)
                    {
                        var original = wv.WebViewClient;
                        if (!(original is CachedWebViewClient))
                            wv.SetWebViewClient(new CachedWebViewClient(original));
                    }
                });
#elif IOS
                WebViewHandler.Mapper.AppendToMapping("CachedScheme", (handler, view) =>
                {
                    if (handler.PlatformView is WebKit.WKWebView wv)
                    {
                        wv.Configuration.SetUrlSchemeHandler(new CachedSchemeHandler(), "nmu-cache");
                    }
                });
#elif MACCATALYST
                WebViewHandler.Mapper.AppendToMapping("CachedScheme", (handler, view) =>
                {
                    if (handler.PlatformView is WebKit.WKWebView wv)
                    {
                        wv.Configuration.SetUrlSchemeHandler(new CachedSchemeHandler(), "nmu-cache");
                    }
                });
#elif WINDOWS
                WebViewHandler.Mapper.AppendToMapping("CachedWebView", (handler, view) =>
                {
                    if (handler.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 wv)
                    {
                        wv.CoreWebView2Initialized += (s, e) =>
                        {
                            try { CachedWebViewHandler.Attach(wv); }
                            catch { }
                        };
                    }
                });
#endif
            });

        // Converters
        builder.Services.AddTransient<InverseBoolConverter>();
        builder.Services.AddTransient<BoolToBorderColorConverter>();
        builder.Services.AddTransient<BoolToBgColorConverter>();
        builder.Services.AddTransient<BoolToCheckBorderColorConverter>();
        builder.Services.AddTransient<BoolToVisibleConverter>();
        builder.Services.AddTransient<BoolToPrimaryBgConverter>();
        builder.Services.AddTransient<BoolToEndStartConverter>();
        builder.Services.AddTransient<MultiplyByTwoConverter>();
        builder.Services.AddTransient<ColorFromStringConverter>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXleeXVXQmJcUUVxXEdWYEo=");

        return builder.Build();
    }
}
