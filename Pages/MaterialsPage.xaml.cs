using System.Text.Json;

namespace NMU_CE_App.Pages;

public partial class MaterialsPage : ContentPage
{
    private const string ArchiveId = "nmu.ce";
    private const string BaseFolder = "NMU";
    private const string CachePrefix = "nmu_materials_";

    private record RawFile(string Name, long? Size, string Source);

    private List<RawFile> _allData = new();
    private double _prevCardWidth;
    private bool _subjectsRendered;
    private DateTime _lastResize = DateTime.MinValue;

    public MaterialsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_allData.Count == 0)
            await LoadDataAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
        if (_subjectsRendered && (DateTime.UtcNow - _lastResize).TotalMilliseconds > 150)
        {
            _lastResize = DateTime.UtcNow;
            RecalcCardSizes();
        }
    }

    private async Task LoadDataAsync()
    {
        ShowBusy("جاري تحميل المواد...");

        var ctx = GetStudentContext();
        var cacheKey = CachePrefix + ctx.level + "_" + ctx.semester;
        var cached = Preferences.Get(cacheKey, "");

        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<RawFile>>(cached);
                if (parsed?.Count > 0)
                {
                    _allData = parsed;
                    RenderSubjects();
                    _ = FetchFromServerAsync(ctx, cacheKey);
                    return;
                }
            }
            catch { Preferences.Remove(cacheKey); }
        }

        await FetchFromServerAsync(ctx, cacheKey);
    }

    private async Task FetchFromServerAsync((string level, string semester) ctx, string cacheKey)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var json = await http.GetStringAsync($"https://archive.org/metadata/{ArchiveId}");
            using var doc = JsonDocument.Parse(json);
            var files = doc.RootElement.GetProperty("files").EnumerateArray();
            var targetPrefix = $"{BaseFolder}/{ctx.level}/{ctx.semester}/";
            var relevant = new List<RawFile>();

            foreach (var f in files)
            {
                var source = f.TryGetProperty("source", out var srcEl) ? srcEl.GetString() : "";
                if (source != "original") continue;

                var name = f.GetProperty("name").GetString();
                if (name != null && name.StartsWith(targetPrefix))
                {
                    long? size = null;
                    if (f.TryGetProperty("size", out var s))
                    {
                        var sizeStr = s.GetString();
                        if (sizeStr != null && long.TryParse(sizeStr, out var sv))
                            size = sv;
                    }
                    relevant.Add(new RawFile(name, size, source));
                }
            }

            if (relevant.Count > 0)
            {
                var newJson = JsonSerializer.Serialize(relevant);
                var oldJson = JsonSerializer.Serialize(_allData);
                if (newJson != oldJson)
                {
                    Preferences.Set(cacheKey, newJson);
                    _allData = relevant;
                    RenderSubjects();
                }
            }
            else if (_allData.Count == 0)
            {
                ShowEmpty();
            }
        }
        catch
        {
            if (_allData.Count == 0)
                ShowError();
        }
    }

    private void ShowBusy(string msg)
    {
        SubjectsGrid.Children.Clear();
        SubjectsGrid.Children.Add(new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 16,
            Margin = new Thickness(0, 40, 0, 0),
            Children =
            {
                new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#7C3AED"), WidthRequest = 40, HeightRequest = 40 },
                new Label { Text = msg, TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center, FontFamily = "Cairo", FontSize = 14 }
            }
        });
    }

    private void ShowError()
    {
        SubjectsGrid.Children.Clear();
        SubjectsGrid.Children.Add(new Label
        {
            Text = "فشل الاتصال بالسيرفر.\nيرجى التحقق من اتصال الإنترنت.",
            TextColor = Color.FromArgb("#ef4444"),
            HorizontalTextAlignment = TextAlignment.Center,
            FontFamily = "Cairo",
            FontSize = 16,
            Margin = new Thickness(0, 40, 0, 0)
        });
    }

    private void ShowEmpty()
    {
        SubjectsGrid.Children.Clear();
        SubjectsGrid.Children.Add(new Label
        {
            Text = "لا توجد مواد دراسية متاحة لمستواك الحالي.",
            TextColor = Color.FromArgb("#94A3B8"),
            HorizontalTextAlignment = TextAlignment.Center,
            FontFamily = "Cairo",
            FontSize = 16,
            Margin = new Thickness(0, 40, 0, 0)
        });
    }

    private static (string level, string semester) GetStudentContext()
    {
        var data = Preferences.Get("nmu_student_v4", "");
        if (!string.IsNullOrEmpty(data))
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var year = doc.RootElement.GetProperty("Year").GetString()?.Replace(" ", "_") ?? "Level_1";
                var term = doc.RootElement.GetProperty("Term").GetString()?.Replace(" ", "_") ?? "Semester_1";
                return (year, term);
            }
            catch { }
        }
        return ("Level_1", "Semester_1");
    }

    private (double width, double height) GetCardSize()
    {
        var available = ContentRoot.Width;
        if (available <= 0) available = SubjectsScrollView.Width - 40;
        if (available <= 0) available = Width - 60;
        if (available <= 0) available = 500;

        int cols;
        if (available < 500) cols = 2;
        else if (available < 700) cols = 3;
        else if (available < 1000) cols = 4;
        else if (available < 1400) cols = 5;
        else cols = 6;

        var gap = 16;
        var totalGaps = gap * (cols + 1);
        var cardWidth = (available - totalGaps) / cols;
        if (cardWidth < 120) cardWidth = 120;
        if (cardWidth > 220) cardWidth = 220;
        var cardHeight = cardWidth * 1.05;

        return (cardWidth, cardHeight);
    }

    private void RecalcCardSizes()
    {
        var (w, h) = GetCardSize();
        if (Math.Abs(w - _prevCardWidth) < 1) return;
        _prevCardWidth = w;

        foreach (var child in SubjectsGrid.Children)
        {
            if (child is Border b)
            {
                b.WidthRequest = w;
                b.HeightRequest = h;
                var iconSize = w < 160 ? 28 : w < 190 ? 34 : 40;
                var fontSize = w < 160 ? 11 : w < 190 ? 12 : 14;
                if (b.Content is VerticalStackLayout vs)
                {
                    foreach (var c in vs.Children)
                    {
                        if (c is Label lbl)
                        {
                            if (lbl.Text != null && lbl.Text.Length <= 2)
                                lbl.FontSize = iconSize;
                            else
                                lbl.FontSize = fontSize;
                        }
                    }
                }
            }
        }
    }

    private void RenderSubjects()
    {
        _subjectsRendered = false;
        var ctx = GetStudentContext();
        var prefix = $"{BaseFolder}/{ctx.level}/{ctx.semester}/PDF/";
        var subjects = new HashSet<string>();

        foreach (var f in _allData)
        {
            if (f.Name.StartsWith(prefix))
            {
                var rel = f.Name[prefix.Length..];
                var parts = rel.Split('/');
                if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                    subjects.Add(parts[0]);
            }
        }

        var list = subjects.OrderBy(s => s).ToList();
        SubjectsGrid.Children.Clear();

        if (list.Count == 0)
        {
            ShowEmpty();
            return;
        }

        _prevCardWidth = 0;
        var (cardW, cardH) = GetCardSize();
        _prevCardWidth = cardW;

        foreach (var sub in list)
        {
            var style = GetSubjectStyle(sub);
            SubjectsGrid.Children.Add(CreateSubjectCard(sub, style, cardW, cardH));
        }

        _subjectsRendered = true;
    }

    private static (string icon, Color color) GetSubjectStyle(string name)
    {
        var n = name.ToLower();
        if (n.Contains("arabic")) return ("🖊", Color.FromArgb("#4DB6AC"));
        if (n.Contains("english") || n.Contains("communication")) return ("🌐", Color.FromArgb("#5C6BC0"));
        if (n.Contains("mat") || n.Contains("math") || n.Contains("calc") || n.Contains("algebra") || n.Contains("discrete") || n.Contains("stat"))
            return ("📐", Color.FromArgb("#EF5350"));
        if (n.Contains("phy") || n.Contains("phys")) return ("⚛", Color.FromArgb("#AB47BC"));
        if (n.Contains("mec") || n.Contains("mech") || n.Contains("static") || n.Contains("dynamic") || n.Contains("control") || n.Contains("material"))
            return ("⚙", Color.FromArgb("#FF7043"));
        if (n.Contains("draw") || n.Contains("graphic")) return ("📏", Color.FromArgb("#FFA726"));
        if (n.Contains("security") || n.Contains("crypto") || n.Contains("cyber"))
            return ("🛡", Color.FromArgb("#78909C"));
        if (n.Contains("robot") || n.Contains("kinematic") || n.Contains("autonomous"))
            return ("🤖", Color.FromArgb("#FF7043"));
        if (n.Contains("ele") || n.Contains("circuit") || n.Contains("electronic") || n.Contains("embedded") || n.Contains("iot"))
            return ("🔌", Color.FromArgb("#FF7043"));
        if (n.Contains("ai") || n.Contains("intelligen") || n.Contains("learning") || n.Contains("neural") || n.Contains("data"))
            return ("🧠", Color.FromArgb("#66BB6A"));
        if (n.Contains("prog") || n.Contains("code") || n.Contains("struct") || n.Contains("oop") || n.Contains("algorithm") || n.Contains("web") || n.Contains("soft") || n.Contains("os") || n.Contains("database"))
            return ("💻", Color.FromArgb("#66BB6A"));
        return ("📖", Color.FromArgb("#7C3AED"));
    }

    private Border CreateSubjectCard(string subject, (string icon, Color color) style, double cardW, double cardH)
    {
        var iconSize = cardW < 160 ? 28 : cardW < 190 ? 34 : 40;
        var fontSize = cardW < 160 ? 11 : cardW < 190 ? 12 : 14;

        var borderColor = style.color;
        var glowColor = Color.FromArgb($"#33{borderColor.ToArgbHex().TrimStart('#')}");

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1A2E"),
            Stroke = borderColor,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            WidthRequest = cardW,
            HeightRequest = cardH,
            Margin = new Thickness(8),
            Padding = new Thickness(12),
            Shadow = new Shadow { Brush = new SolidColorBrush(glowColor), Offset = new Point(0, 0), Radius = 16f, Opacity = 0.35f }
        };

        var originalBg = Color.FromArgb("#1A1A2E");
        var hoverBg = Color.FromArgb("#243447");
        var glowBrush = new SolidColorBrush(glowColor);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            card.ScaleToAsync(1.06, 150, Easing.CubicOut);
            card.FadeToAsync(1, 80);
            card.BackgroundColor = hoverBg;
            card.StrokeThickness = 2;
            card.Shadow = new Shadow { Brush = glowBrush, Offset = new Point(0, 0), Radius = 24f, Opacity = 0.6f };
        };
        pointer.PointerExited += (_, _) =>
        {
            card.ScaleToAsync(1.0, 150, Easing.CubicOut);
            card.FadeToAsync(0.92, 80);
            card.BackgroundColor = originalBg;
            card.StrokeThickness = 1;
            card.Shadow = new Shadow { Brush = glowBrush, Offset = new Point(0, 0), Radius = 16f, Opacity = 0.35f };
        };
        card.GestureRecognizers.Add(pointer);

        var stack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 8
        };

        stack.Children.Add(new Label
        {
            Text = style.icon,
            FontSize = iconSize,
            HorizontalTextAlignment = TextAlignment.Center
        });

        stack.Children.Add(new Label
        {
            Text = subject.Replace("_", " "),
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            FontFamily = "Cairo",
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });

        card.Content = stack;

        var tap = new TapGestureRecognizer();
        var subj = subject;
        tap.Tapped += (_, _) =>
        {
            _ = Shell.Current.GoToAsync($"subjectfiles?subject={Uri.EscapeDataString(subj)}");
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("//home");
    }
}
