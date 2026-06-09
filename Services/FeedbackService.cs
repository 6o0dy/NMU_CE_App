using System.Text;
using System.Text.Json;
using NMU_CE_App.Models;

namespace NMU_CE_App.Services;

public class FeedbackService
{
    private const string BaseUrl = "https://nmu-ce-default-rtdb.firebaseio.com";
    private readonly HttpClient _http;

    public FeedbackService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<List<FeedbackReview>> GetAllReviewsAsync()
    {
        try
        {
            var json = await _http.GetStringAsync($"{BaseUrl}/reviews.json");
            if (string.IsNullOrEmpty(json) || json == "null")
                return new List<FeedbackReview>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, FeedbackReview>>(json);
            if (dict == null) return new List<FeedbackReview>();

            var list = dict.Values.ToList();
            list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            return list;
        }
        catch
        {
            return new List<FeedbackReview>();
        }
    }

    public async Task<bool> SetReviewAsync(FeedbackReview review)
    {
        try
        {
            var json = JsonSerializer.Serialize(review);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PutAsync($"{BaseUrl}/reviews/{review.Serial}.json", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteReviewAsync(string serial)
    {
        try
        {
            var response = await _http.DeleteAsync($"{BaseUrl}/reviews/{serial}.json");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static string GetDeviceSerial()
    {
        var key = "fb_serial_v3";
        var existing = Preferences.Get(key, "");
        if (!string.IsNullOrEmpty(existing))
            return existing;

        var serial = Guid.NewGuid().ToString("N")[..16];
        Preferences.Set(key, serial);
        return serial;
    }
}
