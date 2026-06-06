namespace NMU_CE_App.Services;

public static class FileDownloader
{
    public static async Task<string> DownloadFileAsync(string url, string fileName, IProgress<double>? progress = null)
    {
        var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        var totalRead = 0L;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;
            if (totalBytes > 0 && progress != null)
                progress.Report((double)totalRead / totalBytes);
        }

        return filePath;
    }

    public static async Task OpenFileAsync(string filePath)
    {
        try
        {
            await Launcher.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(filePath)
            });
        }
        catch
        {
            try { await Launcher.OpenAsync(new Uri(filePath)); }
            catch { }
        }
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
