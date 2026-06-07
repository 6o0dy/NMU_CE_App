using System.Text.Json;

namespace NMU_CE_App.Pages;

public partial class YouTubeChannelsPage : ContentPage
{
    private const string FirebaseDbUrl = "https://nmu-ce-default-rtdb.firebaseio.com";
    private const string CachePrefix = "yt_channels_";

    private string _level = "Level_1";
    private string _semester = "Semester_1";

    private List<ChannelGroup> _allGroups = new();
    private List<ChannelGroup> _displayGroups = new();
    private double _prevCardWidth;
    private DateTime _lastResize = DateTime.MinValue;

    public record VideoItem(string Title, string Url, string Img);
    public record ChannelGroup(string Key, string DisplayName, string Subject, string AvatarUrl, List<VideoItem> Videos);

    public YouTubeChannelsPage()
    {
        InitializeComponent();
        LoadStudentContext();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_allGroups.Count == 0)
            await LoadChannelsAsync();
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

    private void LoadStudentContext()
    {
        try
        {
            var data = Preferences.Get("nmu_student_v4", "");
            if (!string.IsNullOrEmpty(data))
            {
                using var doc = JsonDocument.Parse(data);
                _level = doc.RootElement.GetProperty("Year").GetString()?.Replace(" ", "_") ?? "Level_1";
                _semester = doc.RootElement.GetProperty("Term").GetString()?.Replace(" ", "_") ?? "Semester_1";
            }
        }
        catch { }
    }

    private async Task LoadChannelsAsync()
    {
        var cacheKey = CachePrefix + _level + "_" + _semester;
        var cached = Preferences.Get(cacheKey, "");

        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cached);
                if (data != null && data.Count > 0)
                {
                    ProcessFirebaseData(data);
                    RenderChannels();
                    _ = FetchFromFirebaseAsync(cacheKey);
                    return;
                }
            }
            catch { Preferences.Remove(cacheKey); }
        }

        ShowBusy("Loading channels...");
        await FetchFromFirebaseAsync(cacheKey);
    }

    private async Task FetchFromFirebaseAsync(string cacheKey)
    {
        try
        {
            var path = $"NMU/{_level}/{_semester}/Channels";
            var url = $"{FirebaseDbUrl}/{Uri.EscapeDataString(path)}.json";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var json = await http.GetStringAsync(url);

            if (string.IsNullOrEmpty(json) || json == "null")
            {
                if (_allGroups.Count == 0) ShowEmpty();
                return;
            }

            var rawData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (rawData == null || rawData.Count == 0)
            {
                if (_allGroups.Count == 0) ShowEmpty();
                return;
            }

            Preferences.Set(cacheKey, JsonSerializer.Serialize(rawData));
            ProcessFirebaseData(rawData);
            RenderChannels();
        }
        catch
        {
            if (_allGroups.Count == 0) ShowError();
        }
    }

    private void ProcessFirebaseData(Dictionary<string, JsonElement> data)
    {
        _allGroups.Clear();

        foreach (var subjectKvp in data)
        {
            var subjectKey = subjectKvp.Key;
            var channelsObj = subjectKvp.Value;
            if (channelsObj.ValueKind != JsonValueKind.Object) continue;

            foreach (var channelKvp in channelsObj.EnumerateObject())
            {
                var channelKey = channelKvp.Name;
                var channelData = channelKvp.Value;
                if (channelData.ValueKind != JsonValueKind.Object) continue;

                var displayName = channelData.TryGetProperty("channelName", out var nameEl)
                    ? nameEl.GetString() ?? channelKey : channelKey;

                var avatarUrl = $"https://ui-avatars.com/api/?name=YT&background=141414&color=00f2ff";
                if (channelKey.StartsWith("@"))
                {
                    var cleanHandle = channelKey[1..].Replace("-dot-", ".");
                    avatarUrl = $"https://unavatar.io/youtube/{cleanHandle}?fallback={Uri.EscapeDataString(avatarUrl)}";
                }

                var videos = new List<VideoItem>();
                foreach (var prop in channelData.EnumerateObject())
                {
                    if (prop.Name == "channelName") continue;
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;

                    var vTitle = prop.Value.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var vUrl = prop.Value.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    var vImg = prop.Value.TryGetProperty("img", out var im) ? im.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(vUrl))
                        videos.Add(new VideoItem(vTitle, vUrl, vImg));
                }

                if (videos.Count > 0)
                {
                    videos.Reverse();
                    var groupKey = $"{subjectKey}||{channelKey}";
                    _allGroups.Add(new ChannelGroup(groupKey, displayName, subjectKey.Replace("_", " "), avatarUrl, videos));
                }
            }
        }

        _displayGroups = new List<ChannelGroup>(_allGroups);
    }

    // ===== CARD SIZING =====

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

        foreach (var child in ChannelsGrid.Children)
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
                        if (c is Border imgBorder)
                        {
                            imgBorder.WidthRequest = iconSize + 28;
                            imgBorder.HeightRequest = iconSize + 28;
                            imgBorder.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)((iconSize + 28) / 2) };
                        }
                    }
                }
            }
        }
    }

    // ===== RENDER =====

    private void RenderChannels()
    {
        var (cardW, cardH) = GetCardSize();
        _prevCardWidth = cardW;
        ChannelsGrid.Children.Clear();

        if (_displayGroups.Count == 0)
        {
            ShowEmpty();
            return;
        }

        foreach (var group in _displayGroups)
        {
            ChannelsGrid.Children.Add(CreateChannelCard(group, cardW, cardH));
        }
    }

    private Border CreateChannelCard(ChannelGroup group, double cardW, double cardH)
    {
        var iconSize = cardW < 160 ? 28 : cardW < 190 ? 34 : 40;
        var fontSize = cardW < 160 ? 11 : cardW < 190 ? 12 : 14;
        var borderColor = Color.FromArgb("#FF0055");
        var glowColor = Color.FromArgb($"#33FF0055");

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

        var imgSize = iconSize + 28;
        var img = new Image
        {
            Source = group.AvatarUrl,
            WidthRequest = imgSize,
            HeightRequest = imgSize,
            Aspect = Aspect.AspectFill
        };

        var imgBorder = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = imgSize / 2 },
            Stroke = Color.FromArgb("#33FFFFFF"),
            StrokeThickness = 2,
            WidthRequest = imgSize,
            HeightRequest = imgSize,
            BackgroundColor = Color.FromArgb("#111111"),
            Content = img,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 4,
            Children =
            {
                imgBorder,
                new Label
                {
                    Text = group.DisplayName,
                    FontSize = fontSize,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    FontFamily = "Cairo",
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap,
                    MaxLines = 2
                },
                new Label
                {
                    Text = $"{group.Videos.Count} videos",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#94A3B8"),
                    FontFamily = "Cairo",
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        card.Content = stack;

        var tap = new TapGestureRecognizer();
        var g = group.Key;
        tap.Tapped += (_, _) =>
        {
            _ = Shell.Current.GoToAsync($"youtubevideos?channel={Uri.EscapeDataString(g)}");
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    // ===== STATES =====

    private void ShowBusy(string msg)
    {
        ChannelsGrid.Children.Clear();
        ChannelsGrid.Children.Add(new VerticalStackLayout
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
        ChannelsGrid.Children.Clear();
        ChannelsGrid.Children.Add(new Label
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
        ChannelsGrid.Children.Clear();
        ChannelsGrid.Children.Add(new Label
        {
            Text = "No YouTube channels found.",
            TextColor = Color.FromArgb("#94A3B8"),
            HorizontalTextAlignment = TextAlignment.Center,
            FontFamily = "Cairo",
            FontSize = 16,
            Margin = new Thickness(0, 40, 0, 0)
        });
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("//home");
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("YouTube Channels",
            "تصفح قنوات YouTube المسجلة.\nيتم جلب البيانات من Firebase.", "OK");
    }
}
