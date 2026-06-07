using System.Text.Json;
using NMU_CE_App.Models;

namespace NMU_CE_App.Services;

public class QuizService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "NMU_CE_App/1.0" } }
    };
    private const string BaseMeta = "https://archive.org/metadata/";
    private const string BaseDownload = "https://archive.org/download/";
    private const string ArchiveId = "nmu.ce";
    private const string SubjectsCachePrefix = "nmu_quiz_subjects_";
    private const string QuizDataCachePrefix = "nmu_quiz_data_";
    private const string MetaCacheKey = "nmu_quiz_rawmeta";
    private const string OrderCachePrefix = "nmu_quiz_order_";

    private readonly SessionService _session = new();
    private string? _lastSubjectsJson;
    private string? _lastMetaJson;
    private string? _lastOrderJson;
    private readonly Dictionary<string, string> _lastQuizJsons = new();

    public async Task<List<QuizSubject>> GetSubjectsAsync()
    {
        var profile = _session.GetStudentProfile();
        var level = profile?.Year.Replace(" ", "_") ?? "Level_1";
        var term = profile?.Term.Replace(" ", "_") ?? "Semester_1";
        var cacheKey = $"{SubjectsCachePrefix}{level}_{term}";

        // 1. Try disk cache first (from PreCacheQuizDataAsync)
        var diskSubjects = LoadSubjectsFromDiskCache(level, term);
        if (diskSubjects != null)
        {
            _lastSubjectsJson = JsonSerializer.Serialize(diskSubjects);
            _ = RefreshSubjectsInBackgroundAsync(level, term, cacheKey);
            return diskSubjects;
        }

        // 2. Try Preferences cache
        var cached = Preferences.Get(cacheKey, "");
        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var subjects = JsonSerializer.Deserialize<List<QuizSubject>>(cached);
                if (subjects?.Count > 0)
                {
                    _lastSubjectsJson = cached;
                    _ = RefreshSubjectsInBackgroundAsync(level, term, cacheKey);
                    return subjects;
                }
            }
            catch { Preferences.Remove(cacheKey); }
        }

        // 3. Fetch from server
        return await FetchSubjectsFromServerAsync(level, term, cacheKey);
    }

    private List<QuizSubject>? LoadSubjectsFromDiskCache(string level, string term)
    {
        try
        {
            var quizDir = CdnCacheService.GetQuizDiskCacheDir(level, term);
            if (!Directory.Exists(quizDir)) return null;

            var files = Directory.GetFiles(quizDir, "*.json");
            if (files.Length == 0) return null;

            var quizPath = $"NMU/{level}/{term}/QUIZE/";
            var subjects = new List<QuizSubject>();

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith("order_config.json")) continue;

                var displayName = name.Replace(".json", "").Replace("_", " ");
                subjects.Add(new QuizSubject
                {
                    Name = displayName,
                    Path = $"{quizPath}{name}",
                    Rel = name
                });
            }

            return subjects.Count > 0 ? subjects : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<QuizSubject>> FetchSubjectsFromServerAsync(string level, string term, string cacheKey)
    {
        var quizPath = $"NMU/{level}/{term}/QUIZE/";
        var metaUrl = $"{BaseMeta}{ArchiveId}";
        var metaStr = await _http.GetStringAsync(metaUrl);
        var doc = JsonDocument.Parse(metaStr);
        var files = doc.RootElement.GetProperty("files").EnumerateArray();

        var subjects = new List<QuizSubject>();
        foreach (var f in files)
        {
            var name = f.GetProperty("name").GetString() ?? "";
            if (name.StartsWith(quizPath) && name.EndsWith(".json") && !name.EndsWith("order_config.json"))
            {
                var rel = name.Substring(quizPath.Length);
                var displayName = rel.Split('/')[0].Replace(".json", "").Replace("_", " ");
                subjects.Add(new QuizSubject
                {
                    Name = displayName,
                    Path = name,
                    Rel = rel
                });
            }
        }

        var json = JsonSerializer.Serialize(subjects);
        _lastSubjectsJson = json;
        Preferences.Set(cacheKey, json);
        return subjects;
    }

    private async Task RefreshSubjectsInBackgroundAsync(string level, string term, string cacheKey)
    {
        try
        {
            var quizPath = $"NMU/{level}/{term}/QUIZE/";
            var metaUrl = $"{BaseMeta}{ArchiveId}";
            var metaStr = await _http.GetStringAsync(metaUrl);
            var doc = JsonDocument.Parse(metaStr);
            var files = doc.RootElement.GetProperty("files").EnumerateArray();

            var subjects = new List<QuizSubject>();
            foreach (var f in files)
            {
                var name = f.GetProperty("name").GetString() ?? "";
                if (name.StartsWith(quizPath) && name.EndsWith(".json") && !name.EndsWith("order_config.json"))
                {
                    var rel = name.Substring(quizPath.Length);
                    var displayName = rel.Split('/')[0].Replace(".json", "").Replace("_", " ");
                    subjects.Add(new QuizSubject
                    {
                        Name = displayName,
                        Path = name,
                        Rel = rel
                    });
                }
            }

            var json = JsonSerializer.Serialize(subjects);
            if (json != _lastSubjectsJson)
            {
                _lastSubjectsJson = json;
                Preferences.Set(cacheKey, json);
            }
        }
        catch { }
    }

    private static string GetMetaDiskPath() =>
        Path.Combine(FileSystem.CacheDirectory, "quiz_meta.json");

    private static string GetOrderDiskPath(string level, string term) =>
        Path.Combine(FileSystem.CacheDirectory, $"quiz_order_{level}_{term}.json");

    public async Task<string> GetRawMetaAsync()
    {
        // 1. Try Preferences
        var cached = Preferences.Get(MetaCacheKey, "");
        if (!string.IsNullOrEmpty(cached))
        {
            _lastMetaJson = cached;
            _ = RefreshMetaInBackgroundAsync();
            return cached;
        }

        // 2. Try disk fallback
        var diskPath = GetMetaDiskPath();
        if (File.Exists(diskPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(diskPath);
                _lastMetaJson = json;
                _ = RefreshMetaInBackgroundAsync();
                return json;
            }
            catch { }
        }

        // 3. Fetch from server
        return await FetchMetaFromServerAsync();
    }

    private async Task<string> FetchMetaFromServerAsync()
    {
        try
        {
            var json = await _http.GetStringAsync($"{BaseMeta}{ArchiveId}");
            _lastMetaJson = json;
            Preferences.Set(MetaCacheKey, json);
            await File.WriteAllTextAsync(GetMetaDiskPath(), json);
            return json;
        }
        catch
        {
            return "{}";
        }
    }

    private async Task RefreshMetaInBackgroundAsync()
    {
        try
        {
            var json = await _http.GetStringAsync($"{BaseMeta}{ArchiveId}");
            if (json != _lastMetaJson)
            {
                _lastMetaJson = json;
                Preferences.Set(MetaCacheKey, json);
                await File.WriteAllTextAsync(GetMetaDiskPath(), json);
            }
        }
        catch { }
    }

    public async Task<string> GetOrderConfigAsync()
    {
        var profile = _session.GetStudentProfile();
        var level = profile?.Year.Replace(" ", "_") ?? "Level_1";
        var term = profile?.Term.Replace(" ", "_") ?? "Semester_1";
        var cacheKey = $"{OrderCachePrefix}{level}_{term}";

        // 1. Try Preferences
        var cached = Preferences.Get(cacheKey, "");
        if (!string.IsNullOrEmpty(cached))
        {
            _lastOrderJson = cached;
            _ = RefreshOrderInBackgroundAsync(level, term, cacheKey);
            return cached;
        }

        // 2. Try disk fallback
        var diskPath = GetOrderDiskPath(level, term);
        if (File.Exists(diskPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(diskPath);
                _lastOrderJson = json;
                _ = RefreshOrderInBackgroundAsync(level, term, cacheKey);
                return json;
            }
            catch { }
        }

        // 3. Fetch from server
        return await FetchOrderFromServerAsync(level, term, cacheKey);
    }

    private async Task<string> FetchOrderFromServerAsync(string level, string term, string cacheKey)
    {
        try
        {
            var quizPath = $"NMU/{level}/{term}/QUIZE/";
            var json = await _http.GetStringAsync($"{BaseDownload}{ArchiveId}/{quizPath}order_config.json");
            _lastOrderJson = json;
            Preferences.Set(cacheKey, json);
            await File.WriteAllTextAsync(GetOrderDiskPath(level, term), json);
            return json;
        }
        catch
        {
            return "{}";
        }
    }

    private async Task RefreshOrderInBackgroundAsync(string level, string term, string cacheKey)
    {
        try
        {
            var quizPath = $"NMU/{level}/{term}/QUIZE/";
            var json = await _http.GetStringAsync($"{BaseDownload}{ArchiveId}/{quizPath}order_config.json");
            if (json != _lastOrderJson)
            {
                _lastOrderJson = json;
                Preferences.Set(cacheKey, json);
                await File.WriteAllTextAsync(GetOrderDiskPath(level, term), json);
            }
        }
        catch { }
    }

    public async Task<List<QuizChapter>> GetQuizAsync(string subjectPath)
    {
        var cacheKey = $"{QuizDataCachePrefix}{GetPathHash(subjectPath)}";

        // 1. Try disk cache first (from PreCacheQuizDataAsync)
        var diskChapters = LoadQuizFromDiskCache(subjectPath);
        if (diskChapters != null)
        {
            var json = JsonSerializer.Serialize(diskChapters);
            _lastQuizJsons[cacheKey] = json;
            _ = RefreshQuizInBackgroundAsync(subjectPath, cacheKey);
            return diskChapters;
        }

        // 2. Try Preferences cache
        var cached = Preferences.Get(cacheKey, "");
        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var chapters = JsonSerializer.Deserialize<List<QuizChapter>>(cached);
                if (chapters?.Count > 0)
                {
                    _lastQuizJsons[cacheKey] = cached;
                    _ = RefreshQuizInBackgroundAsync(subjectPath, cacheKey);
                    return chapters;
                }
            }
            catch { Preferences.Remove(cacheKey); }
        }

        // 3. Fetch from server
        return await FetchQuizFromServerAsync(subjectPath, cacheKey);
    }

    private List<QuizChapter>? LoadQuizFromDiskCache(string subjectPath)
    {
        try
        {
            var url = $"{BaseDownload}{ArchiveId}/{subjectPath}";
            var cachedPath = CdnCacheService.GetCachedFile(url);

            // Fallback: try direct path from GetQuizDiskCacheDir
            if (cachedPath == null)
            {
                var segments = subjectPath.Split('/');
                if (segments.Length >= 5)
                {
                    var level = segments[1];
                    var term = segments[2];
                    var quizDir = CdnCacheService.GetQuizDiskCacheDir(level, term);
                    var filename = segments.Last();
                    var directPath = Path.Combine(quizDir, filename);
                    if (File.Exists(directPath))
                        cachedPath = directPath;
                }
            }

            if (cachedPath == null) return null;
            var content = File.ReadAllText(cachedPath);
            return ParseQuizContent(content);
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<QuizChapter>> FetchQuizFromServerAsync(string subjectPath, string cacheKey)
    {
        try
        {
            var segments = subjectPath.Split('/');
            var encoded = string.Join("/", segments.Select(Uri.EscapeDataString));
            var url = $"{BaseDownload}{ArchiveId}/{encoded}";
            using var resp = await _http.GetAsync(url);
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "none";
            if (!resp.IsSuccessStatusCode || contentType != "application/json")
                throw new HttpRequestException($"HTTP {(int)resp.StatusCode}, Type: {contentType}");

            var content = await resp.Content.ReadAsStringAsync();
            var chapters = ParseQuizContent(content);
            var json = JsonSerializer.Serialize(chapters);
            _lastQuizJsons[cacheKey] = json;
            Preferences.Set(cacheKey, json);
            return chapters;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizService] FetchQuiz failed: {ex.Message}");
            return [];
        }
    }

    private async Task RefreshQuizInBackgroundAsync(string subjectPath, string cacheKey)
    {
        try
        {
            var segments = subjectPath.Split('/');
            var encoded = string.Join("/", segments.Select(Uri.EscapeDataString));
            var url = $"{BaseDownload}{ArchiveId}/{encoded}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return;
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType != "application/json") return;
            var content = await resp.Content.ReadAsStringAsync();

            var chapters = ParseQuizContent(content);
            var json = JsonSerializer.Serialize(chapters);

            if (!_lastQuizJsons.TryGetValue(cacheKey, out var last) || json != last)
            {
                _lastQuizJsons[cacheKey] = json;
                Preferences.Set(cacheKey, json);
            }
        }
        catch { }
    }

    private static List<QuizChapter> ParseQuizContent(string content)
    {
        using var doc = JsonDocument.Parse(content);
        var chapters = new List<QuizChapter>();
        foreach (var chapterEl in doc.RootElement.EnumerateArray())
        {
            var chapter = new QuizChapter
            {
                Name = chapterEl.GetProperty("name").GetString() ?? "",
                Questions = new List<QuizQuestion>()
            };
            foreach (var qEl in chapterEl.GetProperty("questions").EnumerateArray())
            {
                var q = new QuizQuestion
                {
                    Id = qEl.GetProperty("id").GetInt32(),
                    Question = qEl.GetProperty("question").GetString() ?? ""
                };
                var opts = new List<string>();
                foreach (var optEl in qEl.GetProperty("options").EnumerateArray())
                {
                    opts.Add(optEl.ValueKind == JsonValueKind.String ? optEl.GetString()! : optEl.GetRawText());
                }
                q.Options = opts;
                q.CorrectAnswer = qEl.GetProperty("correct_answer").GetString() ?? "";
                if (qEl.TryGetProperty("hint", out var hintEl))
                    q.Hint = hintEl.GetString() ?? "";
                if (qEl.TryGetProperty("explanation_ar", out var expArEl))
                    q.ExplanationAr = expArEl.GetString() ?? "";
                if (qEl.TryGetProperty("explanation_en", out var expEnEl))
                    q.ExplanationEn = expEnEl.GetString() ?? "";
                if (qEl.TryGetProperty("code_snippet", out var codeSnEl))
                    q.CodeSnippet = codeSnEl.GetString() ?? "";
                if (qEl.TryGetProperty("code_lang", out var codeLaEl))
                    q.CodeLang = codeLaEl.GetString() ?? "";
                if (qEl.TryGetProperty("graph_type", out var gtEl))
                    q.GraphType = gtEl.GetString() ?? "";
                if (qEl.TryGetProperty("graph_fn", out var gfEl))
                    q.GraphFn = gfEl.GetString() ?? "";
                if (qEl.TryGetProperty("graph_data", out var gdEl))
                    q.GraphData = gdEl.GetString() ?? "";
                chapter.Questions.Add(q);
            }
            chapters.Add(chapter);
        }
        return chapters;
    }

    private static string GetPathHash(string path)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(path);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }
}
