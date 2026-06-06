using System.Text.Json;

namespace NMU_CE_App.Models;

public class StudentProfile
{
    public string Name { get; set; } = string.Empty;
    public string Year { get; set; } = "Level 1";
    public string Term { get; set; } = string.Empty;

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static StudentProfile? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<StudentProfile>(json); }
        catch { return null; }
    }
}
