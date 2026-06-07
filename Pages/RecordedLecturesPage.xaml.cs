using System.Text.Json;

namespace NMU_CE_App.Pages;

public partial class RecordedLecturesPage : ContentPage
{
    private const string ArchiveId = "nmu.ce";
    private const string CachePrefix = "nmu_recorded_";
    private const string ThumbCachePrefix = "nmu_recorded_thumbs_";

    private record RawFile(string Name, long? Size, string Source);

    private List<RawFile> _allData = new();
    private List<RawFile> _thumbData = new();
    private double _prevCardWidth;
    private DateTime _lastResize = DateTime.MinValue;

    public RecordedLecturesPage()
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
        if (_prevCardWidth > 0 && (DateTime.UtcNow - _lastResize).TotalMilliseconds > 150)
        {
            _lastResize = DateTime.UtcNow;
            RecalcCardSizes();
        }
    }

    private async Task LoadDataAsync()
    {
        var ctx = GetStudentContext();
        var cacheKey = CachePrefix + ctx.level + "_" + ctx.semester;
        var thumbKey = ThumbCachePrefix + ctx.level + "_" + ctx.semester;
        var cached = Preferences.Get(cacheKey, "");
        var cachedThumbs = Preferences.Get(thumbKey, "");

        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<RawFile>>(cached);
                if (parsed?.Count > 0)
                {
                    _allData = parsed;
                    _thumbData = string.IsNullOrEmpty(cachedThumbs)
                        ? new()
                        : JsonSerializer.Deserialize<List<RawFile>>(cachedThumbs) ?? new();
                    RenderFolders();
                    _ = FetchFromServerAsync(ctx, cacheKey, thumbKey);
                    return;
                }
            }
            catch { Preferences.Remove(cacheKey); }
        }

        ShowBusy("Loading lectures...");
        await FetchFromServerAsync(ctx, cacheKey, thumbKey);
    }

    private async Task FetchFromServerAsync((string level, string semester) ctx, string cacheKey, string thumbKey)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var json = await http.GetStringAsync($"https://archive.org/metadata/{ArchiveId}");
            using var doc = JsonDocument.Parse(json);
            var files = doc.RootElement.GetProperty("files").EnumerateArray();

            var targetPath1 = $"NMU/{ctx.level}/{ctx.semester}/RECORDED_LECTURER/";
            var targetPath2 = $"NMU/{ctx.level}/{ctx.semester}/RECORDED LECTURER/";
            var thumbsPrefix1 = $"nmu.ce.thumbs/{targetPath1}";
            var thumbsPrefix2 = $"nmu.ce.thumbs/{targetPath2}";

            var relevant = new List<RawFile>();
            var thumbs = new List<RawFile>();

            foreach (var f in files)
            {
                var source = f.TryGetProperty("source", out var srcEl) ? srcEl.GetString() : "";
                if (source != "original") continue;
                var name = f.GetProperty("name").GetString();
                if (name == null) continue;

                if (name.StartsWith(targetPath1) || name.StartsWith(targetPath2))
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

                if ((name.StartsWith(thumbsPrefix1) || name.StartsWith(thumbsPrefix2)) && name.EndsWith(".jpg"))
                    thumbs.Add(new RawFile(name, null, source));
            }

            if (relevant.Count > 0)
            {
                var newJson = JsonSerializer.Serialize(relevant);
                var oldJson = JsonSerializer.Serialize(_allData);
                var newThumbJson = JsonSerializer.Serialize(thumbs);
                var oldThumbJson = JsonSerializer.Serialize(_thumbData);

                if (newJson != oldJson || newThumbJson != oldThumbJson)
                {
                    Preferences.Set(cacheKey, newJson);
                    Preferences.Set(thumbKey, newThumbJson);
                    _allData = relevant;
                    _thumbData = thumbs;
                    RenderFolders();
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
        FoldersGrid.Children.Clear();
        FoldersGrid.Children.Add(new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 16,
            Margin = new Thickness(0, 40, 0, 0),
            Children =
            {
                new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#00F2FF"), WidthRequest = 40, HeightRequest = 40 },
                new Label { Text = msg, TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center, FontFamily = "Cairo", FontSize = 14 }
            }
        });
    }

    private void ShowError()
    {
        FoldersGrid.Children.Clear();
        FoldersGrid.Children.Add(new Label
        {
            Text = "Connection failed. Please check your internet.",
            TextColor = Color.FromArgb("#ef4444"),
            HorizontalTextAlignment = TextAlignment.Center,
            FontFamily = "Cairo",
            FontSize = 16,
            Margin = new Thickness(0, 40, 0, 0)
        });
    }

    private void ShowEmpty()
    {
        FoldersGrid.Children.Clear();
        FoldersGrid.Children.Add(new Label
        {
            Text = "No recorded lectures available.",
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

    // ===== FOLDERS GRID =====

    private (double width, double height) GetCardSize()
    {
        var available = ContentRoot.Width;
        if (available <= 0) available = FoldersScrollView.Width - 40;
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

        foreach (var child in FoldersGrid.Children)
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

    private void RenderFolders()
    {
        var ctx = GetStudentContext();
        var targetPath1 = $"NMU/{ctx.level}/{ctx.semester}/RECORDED_LECTURER/";
        var targetPath2 = $"NMU/{ctx.level}/{ctx.semester}/RECORDED LECTURER/";

        var groups = new Dictionary<string, int>();
        foreach (var f in _allData)
        {
            var name = f.Name;
            if (!System.Text.RegularExpressions.Regex.IsMatch(name,
                @"\.(mp4|mkv|webm|mp3|wav|m4a)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;

            string relPath;
            if (name.StartsWith(targetPath1))
                relPath = name[targetPath1.Length..];
            else if (name.StartsWith(targetPath2))
                relPath = name[targetPath2.Length..];
            else continue;

            var parts = relPath.Split('/');
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                var group = parts[0];
                groups.TryGetValue(group, out var count);
                groups[group] = count + 1;
            }
        }

        var sorted = groups.Keys.OrderBy(k => k).ToList();
        FoldersGrid.Children.Clear();

        if (sorted.Count == 0)
        {
            ShowEmpty();
            return;
        }

        var (cardW, cardH) = GetCardSize();
        _prevCardWidth = cardW;

        foreach (var group in sorted)
        {
            var count = groups[group];
            var style = GetFolderStyle(group);
            FoldersGrid.Children.Add(CreateFolderCard(group, count, style, cardW, cardH));
        }
    }

    private static (string icon, Color color) GetFolderStyle(string name)
    {
        var n = name.ToLower();
        if (n.Contains("arabic")) return ("📖", Color.FromArgb("#4DB6AC"));
        if (n.Contains("english") || n.Contains("communication")) return ("🗣", Color.FromArgb("#5C6BC0"));
        if (n.Contains("math") || n.Contains("calc") || n.Contains("algebra") || n.Contains("stat") || n.Contains("probabilit")) return ("📐", Color.FromArgb("#EF5350"));
        if (n.Contains("phy") || n.Contains("magnetic") || n.Contains("optic")) return ("⚛", Color.FromArgb("#AB47BC"));
        if (n.Contains("chem")) return ("🧪", Color.FromArgb("#FFA726"));
        if (n.Contains("ele") || n.Contains("electric") || n.Contains("circuit") || n.Contains("signal")) return ("🔌", Color.FromArgb("#FF7043"));
        if (n.Contains("robot") || n.Contains("kinematic")) return ("🤖", Color.FromArgb("#78909C"));
        if (n.Contains("mec") || n.Contains("mech") || n.Contains("static") || n.Contains("dynamic") || n.Contains("control")) return ("⚙", Color.FromArgb("#FF7043"));
        if (n.Contains("network")) return ("🌐", Color.FromArgb("#5C6BC0"));
        if (n.Contains("database") || n.Contains("sql")) return ("💾", Color.FromArgb("#66BB6A"));
        if (n.Contains("ai") || n.Contains("intelligen") || n.Contains("learning") || n.Contains("neural") || n.Contains("fuzzy")) return ("🧠", Color.FromArgb("#66BB6A"));
        if (n.Contains("security") || n.Contains("crypto")) return ("🛡", Color.FromArgb("#78909C"));
        if (n.Contains("prog") || n.Contains("code") || n.Contains("computer") || n.Contains("algorithm")) return ("💻", Color.FromArgb("#66BB6A"));
        if (n.Contains("project") || n.Contains("training") || n.Contains("grad")) return ("🎓", Color.FromArgb("#AB47BC"));
        if (n.Contains("history") || n.Contains("psychology") || n.Contains("social")) return ("🏛", Color.FromArgb("#78909C"));
        if (n.Contains("draw") || n.Contains("graphic")) return ("📏", Color.FromArgb("#FFA726"));
        return ("🎬", Color.FromArgb("#00F2FF"));
    }

    private Border CreateFolderCard(string group, int count, (string icon, Color color) style, double cardW, double cardH)
    {
        var iconSize = cardW < 160 ? 28 : cardW < 190 ? 34 : 40;
        var fontSize = cardW < 160 ? 11 : cardW < 190 ? 12 : 14;
        var borderColor = style.color;
        var glowColor = Color.FromArgb($"#33{borderColor.ToArgbHex().TrimStart('#')}");

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1E293B"),
            Stroke = borderColor,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            WidthRequest = cardW,
            HeightRequest = cardH,
            Margin = new Thickness(8),
            Padding = new Thickness(12),
            Shadow = new Shadow { Brush = new SolidColorBrush(glowColor), Offset = new Point(0, 0), Radius = 16f, Opacity = 0.35f }
        };

        var originalBg = Color.FromArgb("#1E293B");
        var hoverBg = Color.FromArgb("#243447");
        var glowBrush = new SolidColorBrush(glowColor);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            card.ScaleToAsync(1.06, 150, Easing.CubicOut);
            card.BackgroundColor = hoverBg;
            card.StrokeThickness = 2;
            card.Shadow = new Shadow { Brush = glowBrush, Offset = new Point(0, 0), Radius = 24f, Opacity = 0.6f };
        };
        pointer.PointerExited += (_, _) =>
        {
            card.ScaleToAsync(1.0, 150, Easing.CubicOut);
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
            Text = group.Replace("_", " "),
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            FontFamily = "Cairo",
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });

        stack.Children.Add(new Label
        {
            Text = $"{count} media",
            FontSize = 10,
            TextColor = Color.FromArgb("#94A3B8"),
            FontFamily = "Fira Code",
            HorizontalTextAlignment = TextAlignment.Center
        });

        card.Content = stack;

        var tap = new TapGestureRecognizer();
        var g = group;
        tap.Tapped += (_, _) =>
        {
            _ = Shell.Current.GoToAsync($"recordedfiles?group={Uri.EscapeDataString(g)}");
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("//home");
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("Recorded Lectures",
            "تصفح المحاضرات المسجلة.\nيتم جلب البيانات من Archive.org.", "OK");
    }
}
