using System.Text.Json;

namespace NMU_CE_App.Pages;

[QueryProperty(nameof(ChannelKey), "channel")]
public partial class YouTubeChannelVideosPage : ContentPage
{
    private const string FirebaseDbUrl = "https://nmu-ce-default-rtdb.firebaseio.com";
    private const string CachePrefix = "yt_channels_";
    private const int CardGap = 14;
    private const int MinCardWidth = 450;

    private string _channelKey = "";
    private string _channelName = "";
    private List<YouTubeChannelsPage.VideoItem> _allVideos = new();
    private List<YouTubeChannelsPage.VideoItem> _displayVideos = new();
    private int _lastCols;
    private double _pageWidth;

    public string ChannelKey
    {
        get => _channelKey;
        set
        {
            _channelKey = Uri.UnescapeDataString(value ?? "");
            _ = LoadChannelVideos();
        }
    }

    public YouTubeChannelVideosPage()
    {
        InitializeComponent();
    }

    private async Task LoadChannelVideos()
    {
        var parts = _channelKey.Split("||");
        if (parts.Length != 2) { ShowEmpty(); return; }

        var subjectKey = parts[0];
        var channelKey = parts[1];

        var ctx = GetStudentContext();
        var cacheKey = CachePrefix + ctx.level + "_" + ctx.semester;
        var cached = Preferences.Get(cacheKey, "");

        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cached);
                if (data != null)
                {
                    ExtractVideos(data, subjectKey, channelKey);
                    if (_allVideos.Count > 0)
                    {
                        PageTitle.Text = _channelName;
                        RenderVideos();
                        _ = FetchAndUpdateAsync(cacheKey, subjectKey, channelKey);
                        return;
                    }
                }
            }
            catch { }
        }

        ShowBusy();
        await FetchAndUpdateAsync(cacheKey, subjectKey, channelKey);
    }

    private async Task FetchAndUpdateAsync(string cacheKey, string subjectKey, string channelKey)
    {
        try
        {
            var path = $"NMU/{GetStudentContext().level}/{GetStudentContext().semester}/Channels";
            var url = $"{FirebaseDbUrl}/{Uri.EscapeDataString(path)}.json";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var json = await http.GetStringAsync(url);

            if (string.IsNullOrEmpty(json) || json == "null")
            {
                if (_allVideos.Count == 0) ShowEmpty();
                return;
            }

            var rawData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (rawData != null)
            {
                Preferences.Set(cacheKey, JsonSerializer.Serialize(rawData));
                ExtractVideos(rawData, subjectKey, channelKey);
            }

            if (_allVideos.Count > 0)
            {
                PageTitle.Text = _channelName;
                RenderVideos();
            }
            else
            {
                ShowEmpty();
            }
        }
        catch
        {
            if (_allVideos.Count == 0) ShowError();
        }
    }

    private void ExtractVideos(Dictionary<string, JsonElement> data, string subjectKey, string channelKey)
    {
        _allVideos.Clear();
        _channelName = channelKey;

        if (!data.TryGetValue(subjectKey, out var subjectObj) || subjectObj.ValueKind != JsonValueKind.Object)
            return;

        if (!subjectObj.TryGetProperty(channelKey, out var channelObj) || channelObj.ValueKind != JsonValueKind.Object)
            return;

        _channelName = channelObj.TryGetProperty("channelName", out var nameEl)
            ? nameEl.GetString() ?? channelKey : channelKey;

        foreach (var prop in channelObj.EnumerateObject())
        {
            if (prop.Name == "channelName") continue;
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;

            var vTitle = prop.Value.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var vUrl = prop.Value.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            var vImg = prop.Value.TryGetProperty("img", out var im) ? im.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(vUrl))
                _allVideos.Add(new YouTubeChannelsPage.VideoItem(vTitle, vUrl, vImg));
        }

        _allVideos.Reverse();
        _displayVideos = new List<YouTubeChannelsPage.VideoItem>(_allVideos);
    }

    // ===== RENDER =====

    private int CalcCols()
    {
        if (_pageWidth <= 0) return 1;
        return Math.Clamp((int)((_pageWidth + CardGap) / (MinCardWidth + CardGap)), 1, 3);
    }

    private void RenderVideos()
    {
        Dispatcher.Dispatch(ReflowCards);
    }

    private void ReflowCards()
    {
        FilesList.Children.Clear();
        if (_displayVideos.Count == 0) { ShowEmpty(); return; }

        var cols = CalcCols();
        _lastCols = cols;

        for (int i = 0; i < _displayVideos.Count; i += cols)
        {
            var row = new Grid { ColumnSpacing = CardGap, HorizontalOptions = LayoutOptions.Center };
            for (int c = 0; c < cols; c++)
                row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(450)));

            for (int j = 0; j < cols; j++)
            {
                var idx = i + j;
                if (idx >= _displayVideos.Count) break;
                var card = CreateVideoCard(_displayVideos[idx]);
                card.Margin = new Thickness(0);
                row.Add(card, j, 0);
            }

            FilesList.Children.Add(row);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
        if (width <= 0) return;
        _pageWidth = width;
        var cols = CalcCols();
        if (cols != _lastCols)
            Dispatcher.Dispatch(ReflowCards);
    }

    private Border CreateVideoCard(YouTubeChannelsPage.VideoItem video)
    {
        var cleanTitle = video.Title.Length > 80 ? video.Title[..80] + "..." : video.Title;
        var accent = Color.FromArgb("#FF0055");
        var accentHex = accent.ToArgbHex().TrimStart('#');

        View iconContent;
        if (!string.IsNullOrEmpty(video.Img))
        {
            iconContent = new Image
            {
                Source = video.Img,
                Aspect = Aspect.AspectFill,
                WidthRequest = 110,
                HeightRequest = 80
            };
        }
        else
        {
            iconContent = new Label
            {
                Text = "▶",
                FontSize = 26,
                TextColor = accent,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
        }

        var iconBorder = new Border
        {
            Stroke = accent,
            StrokeThickness = 2,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            BackgroundColor = Color.FromArgb($"#0D{accentHex}"),
            VerticalOptions = LayoutOptions.Center,
            Content = new Border
            {
                Stroke = Colors.Transparent,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Content = iconContent
            }
        };

        var titleLabel = new Label
        {
            Text = cleanTitle,
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "Cairo",
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };

        var typeBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#1AFF0055"),
            Stroke = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Padding = new Thickness(6, 2),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "YOUTUBE",
                TextColor = accent,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                FontFamily = "Cairo"
            }
        };

        var metaRow = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { typeBadge }
        };

        var textStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { titleLabel, metaRow }
        };

        var innerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(110)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14,
            Padding = new Thickness(0, 0, 4, 0),
            Children = { iconBorder, textStack }
        };
        Grid.SetColumn(textStack, 1);

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1E293B"),
            Stroke = Color.FromArgb("#334155"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(16, 0),
            Content = innerGrid,
            AutomationId = cleanTitle.ToLower(),
            HeightRequest = 100
        };

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            card.BackgroundColor = Color.FromArgb($"#1A{accentHex}");
            card.Stroke = accent;
        };
        pointer.PointerExited += (_, _) =>
        {
            card.BackgroundColor = Color.FromArgb("#1A1E293B");
            card.Stroke = Color.FromArgb("#334155");
        };
        card.GestureRecognizers.Add(pointer);

        var tap = new TapGestureRecognizer();
        var vUrl = video.Url;
        var vTitle = video.Title;
        tap.Tapped += async (_, _) =>
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await DisplayAlertAsync("No Internet", "You need internet to play videos.", "OK");
                return;
            }
            _ = Shell.Current.GoToAsync($"youtubeplayer?url={Uri.EscapeDataString(vUrl)}&title={Uri.EscapeDataString(vTitle)}");
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    // ===== STATES =====

    private void ShowBusy()
    {
        FilesList.Children.Clear();
        FilesList.Children.Add(new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 16,
            Margin = new Thickness(0, 40, 0, 0),
            Children =
            {
                new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#00F2FF"), WidthRequest = 40, HeightRequest = 40 },
                new Label { Text = "Loading videos...", TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center, FontFamily = "Cairo", FontSize = 14 }
            }
        });
    }

    private void ShowError()
    {
        FilesList.Children.Clear();
        FilesList.Children.Add(new Label
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
        Dispatcher.Dispatch(() =>
        {
            FilesList.Children.Clear();
            FilesList.Children.Add(new Label
            {
                Text = "No videos found.",
                TextColor = Color.FromArgb("#94A3B8"),
                HorizontalTextAlignment = TextAlignment.Center,
                FontFamily = "Cairo",
                FontSize = 14,
                Margin = new Thickness(0, 40, 0, 0)
            });
        });
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.ToLower() ?? "";
        _displayVideos = string.IsNullOrEmpty(query)
            ? new List<YouTubeChannelsPage.VideoItem>(_allVideos)
            : _allVideos.Where(v => v.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        RenderVideos();
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

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync(_channelName,
            "تصفح فيديوهات القناة.\nيتم جلب البيانات من Firebase.", "OK");
    }
}
