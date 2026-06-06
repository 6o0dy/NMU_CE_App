using NMU_CE_App.Models;

namespace NMU_CE_App.Services;

public class SessionService
{
    private const string ScanDoneKey = "nmu_scan_done";
    private const string StudentDataKey = "nmu_student_v4";

    public const string DeveloperName = "ABDELRHMAN ELSAYED";

    public bool HasSeenScan
    {
        get => Preferences.Get(ScanDoneKey, false);
    }

    public bool HasData
    {
        get => Preferences.ContainsKey(StudentDataKey);
    }

    public void MarkScanDone()
    {
        Preferences.Set(ScanDoneKey, true);
    }

    public StudentProfile? GetStudentProfile()
    {
        var json = Preferences.Get(StudentDataKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return null;
        return StudentProfile.FromJson(json);
    }

    public void SaveStudentProfile(StudentProfile profile)
    {
        Preferences.Set(StudentDataKey, profile.ToJson());
    }

    public static string GetFooterCredit()
    {
        return $"Powered By {DeveloperName} © {DateTime.Now.Year}";
    }
}
