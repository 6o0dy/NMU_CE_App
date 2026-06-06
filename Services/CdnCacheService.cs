using System.Diagnostics;
using System.Text.Json;

namespace NMU_CE_App.Services;

public static class CdnCacheService
{
    private static readonly HttpClient _http = new();
    private static readonly string CacheDir = Path.Combine(FileSystem.CacheDirectory, "cdn");
    private static readonly string VersionFile = Path.Combine(CacheDir, ".version");

    private const string BaseMeta = "https://archive.org/metadata/";
    private const string BaseDownload = "https://archive.org/download/";
    private const string ArchiveId = "nmu.ce";

    public static readonly string[] CdnUrls =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/pako/2.1.0/pako.min.js",
        "https://cdnjs.cloudflare.com/ajax/libs/d3/3.5.17/d3.min.js",
        "https://cdnjs.cloudflare.com/ajax/libs/function-plot/1.22.1/function-plot.min.js",
        "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js",
        "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js",
        "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/line-numbers/prism-line-numbers.min.js",
        "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css",
        "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/line-numbers/prism-line-numbers.min.css",
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css",
        "https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-svg.js",
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/webfonts/fa-solid-900.woff2",
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/webfonts/fa-regular-400.woff2",
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/webfonts/fa-brands-400.woff2",
    ];

    static CdnCacheService()
    {
        Directory.CreateDirectory(CacheDir);
    }

    public static string GetCachePath(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath.TrimStart('/');
        return Path.Combine(CacheDir, path);
    }

    public static bool IsCached(string url)
    {
        return File.Exists(GetCachePath(url));
    }

    public static string? GetCachedFile(string url)
    {
        var path = GetCachePath(url);
        return File.Exists(path) ? path : null;
    }

    public static async Task<string?> DownloadAndCacheAsync(string url)
    {
        try
        {
            var path = GetCachePath(url);
            if (File.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, bytes);
            Debug.WriteLine($"[CdnCache] Cached: {url}");
            return path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CdnCache] Failed: {url} - {ex.Message}");
            return null;
        }
    }

    /// Download all CDN resources in background
    public static async Task PreCacheAllAsync()
    {
        if (File.Exists(VersionFile)) return;

        foreach (var url in CdnUrls)
        {
            if (!IsCached(url))
                await DownloadAndCacheAsync(url);
        }

        try { await File.WriteAllTextAsync(VersionFile, "1"); } catch { }
    }

    /// Download & cache all quiz data from Archive.org for a given level/term
    public static async Task<Dictionary<string, string>> PreCacheQuizDataAsync(string level, string term)
    {
        var result = new Dictionary<string, string>();
        try
        {
            var metaUrl = $"{BaseMeta}{ArchiveId}";
            var metaStr = await _http.GetStringAsync(metaUrl);
            using var doc = JsonDocument.Parse(metaStr);
            var files = doc.RootElement.GetProperty("files").EnumerateArray();
            var quizPathPrefix = $"NMU/{level}/{term}/QUIZE/";

            foreach (var f in files)
            {
                var name = f.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith(quizPathPrefix) || !name.EndsWith(".json"))
                    continue;
                if (name.EndsWith("order_config.json"))
                    continue;

                var url = $"{BaseDownload}{ArchiveId}/{name}";
                var cachePath = GetCachePath(url);
                if (!File.Exists(cachePath))
                {
                    try
                    {
                        var bytes = await _http.GetByteArrayAsync(url);
                        var dir = Path.GetDirectoryName(cachePath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        await File.WriteAllBytesAsync(cachePath, bytes);
                    }
                    catch { }
                }
                result[name] = cachePath;
            }
        }
        catch { }
        return result;
    }

    public static string GetMimeType(string url)
    {
        if (url.EndsWith(".js")) return "application/javascript";
        if (url.EndsWith(".css")) return "text/css";
        if (url.EndsWith(".woff2")) return "font/woff2";
        if (url.EndsWith(".woff")) return "font/woff";
        if (url.EndsWith(".ttf")) return "font/ttf";
        if (url.EndsWith(".svg")) return "image/svg+xml";
        if (url.EndsWith(".json")) return "application/json";
        if (url.EndsWith(".png")) return "image/png";
        if (url.EndsWith(".jpg") || url.EndsWith(".jpeg")) return "image/jpeg";
        return "application/octet-stream";
    }

    public static bool IsCdnUrl(string url)
    {
        return url.StartsWith("https://cdnjs.cloudflare.com/") ||
               url.StartsWith("https://cdn.jsdelivr.net/") ||
               url.StartsWith("https://fonts.gstatic.com/") ||
               url.StartsWith("https://fonts.googleapis.com/") ||
               url.StartsWith(BaseDownload);
    }
}
