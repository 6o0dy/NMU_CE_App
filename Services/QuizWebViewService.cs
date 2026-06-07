namespace NMU_CE_App.Services;

public static class QuizWebViewService
{
    private static readonly HttpClient _http = new();
    private static string? _pakoCached;

    public static async Task<string> BuildHtmlWithData(string metaJson, string orderJson, string? profileJson = null, string? chaptersJson = null, string? quizPath = null)
    {
        var html = await LoadRawAsync("Quizzes/Quizzes.html");

        // Inline pako library for TikZ circuit rendering (CDN scripts don't load from file://)
        html = await InlinePakoAsync(html);

#if IOS || MACCATALYST
        // Replace CDN URLs with custom scheme so WKURLSchemeHandler can intercept them
        html = CdnCacheService.ReplaceCdnUrlsWithCacheScheme(html);
#endif

        var script = $@"
<script>
window.__CACHED_META__ = {metaJson};
window.__CACHED_ORDER__ = {orderJson};
{(profileJson != null ? $@"try {{ localStorage.setItem('nmu_student_v4', '{profileJson.Replace("'", "\\'")}'); }} catch(e) {{}}" : "")}
{(chaptersJson != null ? $@"
window.__CACHED_CHAPTERS__ = {chaptersJson};
(function() {{
    var check = setInterval(function() {{
        if (appState && appState.quizList && appState.quizList.length > 0 && typeof renderChaptersList === 'function') {{
            clearInterval(check);
            appState.currentQuizData = window.__CACHED_CHAPTERS__;
            if (!Array.isArray(appState.currentQuizData)) appState.currentQuizData = [appState.currentQuizData];
            var quiz = appState.quizList.find(function(q) {{ return q.path === '{(quizPath ?? "").Replace("'", "\\'")}'; }});
            renderChaptersList(quiz ? quiz.name : 'Quiz');
        }}
    }}, 50);
}})();
" : "")}
</script>";

        html = html.Replace("</head>", script + "</head>");
        return html;
    }

    private static async Task<string> InlinePakoAsync(string html)
    {
        if (_pakoCached == null)
        {
            var cachePath = Path.Combine(FileSystem.CacheDirectory, "pako.min.js");
            if (File.Exists(cachePath))
            {
                _pakoCached = await File.ReadAllTextAsync(cachePath);
            }
            else
            {
                try
                {
                    var bytes = await _http.GetByteArrayAsync(
                        "https://cdnjs.cloudflare.com/ajax/libs/pako/2.1.0/pako.min.js");
                    _pakoCached = System.Text.Encoding.UTF8.GetString(bytes);
                    await File.WriteAllTextAsync(cachePath, _pakoCached);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[QuizWebViewService] Failed to download pako: {ex.Message}");
                    return html;
                }
            }
        }
        var inline = $"<script>{_pakoCached}</script>";
        var match = System.Text.RegularExpressions.Regex.Match(
            html, @"<script\s+src=""https://cdnjs\.cloudflare\.com/ajax/libs/pako/2\.1\.0/pako\.min\.js""></script>");
        if (match.Success)
            return html.Substring(0, match.Index) + inline + html.Substring(match.Index + match.Length);
        return html;
    }

    private static async Task<string> LoadRawAsync(string filename)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(filename);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
