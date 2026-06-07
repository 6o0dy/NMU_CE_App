namespace NMU_CE_App.Helpers;

public enum DeviceForm
{
    Mobile,
    Tablet,
    Desktop
}

public static class ResponsiveHelper
{
    public const double MobileBreakpoint = 600;
    public const double TabletBreakpoint = 1024;

    public const int CardGap = 16;
    public const int MinCardWidth = 140;
    public const int MaxCardWidth = 220;

    public static DeviceForm GetDeviceForm()
    {
        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            return DeviceForm.Mobile;
        if (DeviceInfo.Idiom == DeviceIdiom.Tablet)
            return DeviceForm.Tablet;
        return DeviceForm.Desktop;
    }

    public static bool IsMobile => GetDeviceForm() == DeviceForm.Mobile;

    public static int GetColumns(double availableWidth)
    {
        if (availableWidth <= 0) return 2;
        if (availableWidth < MobileBreakpoint) return 2;
        if (availableWidth < 700) return 3;
        if (availableWidth < TabletBreakpoint) return 4;
        if (availableWidth < 1400) return 5;
        return 6;
    }

    public static (double width, double height) GetCardSize(double availableWidth)
    {
        var cols = GetColumns(availableWidth);
        var totalGaps = CardGap * (cols + 1);
        var cardWidth = (availableWidth - totalGaps) / cols;
        cardWidth = Math.Clamp(cardWidth, MinCardWidth, MaxCardWidth);
        var cardHeight = cardWidth * 1.05;
        return (cardWidth, cardHeight);
    }

    public static double GetCardIconSize(double cardWidth)
    {
        if (cardWidth < 160) return 28;
        if (cardWidth < 190) return 34;
        return 40;
    }

    public static double GetCardFontSize(double cardWidth)
    {
        if (cardWidth < 160) return 11;
        if (cardWidth < 190) return 12;
        return 14;
    }

    public static double GetFileCardWidth(double availableWidth)
    {
        if (availableWidth <= 0) return 450;
        var cols = Math.Clamp((int)((availableWidth + 14) / (450 + 14)), 1, 3);
        var totalGaps = 14 * (cols + 1);
        return (availableWidth - totalGaps) / cols;
    }

    public static double Clamp(double value, double min, double max) =>
        Math.Clamp(value, min, max);

    /// Returns the main content max width based on device form
    public static double GetContentMaxWidth() =>
        IsMobile ? double.MaxValue : 1280;
}
