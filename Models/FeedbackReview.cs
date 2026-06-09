using System.Text.Json.Serialization;

namespace NMU_CE_App.Models;

public class FeedbackReview
{
    [JsonPropertyName("serial")]
    public string Serial { get; set; } = "";

    [JsonPropertyName("review")]
    public string Review { get; set; } = "";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("level")]
    public string Level { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}
