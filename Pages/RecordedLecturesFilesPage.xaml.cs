using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class RecordedLecturesFilesPage : ContentPage
{
    private const string ArchiveId = "nmu.ce";
    private const string BaseDownload = "https://archive.org/download/";
    private const string CachePrefix = "nmu_recorded_";
    private const string ThumbCachePrefix = "nmu_recorded_thumbs_";
    private const int CardGap = 14;
    private const int MinCardWidth = 450;

    private record RawFile(string Name, long? Size, string Source);
    private record MediaFile(string Name, string Path, string Group, string SubFolder, bool IsAudio, string ThumbUrl, long? Size);

    private List<RawFile> _allData = new();
    private List<RawFile> _thumbData = new();
    private List<MediaFile> _currentFiles = new();
    private List<MediaFile> _currentDisplayFiles = new();
    private Dictionary<string, List<string>> _orderCache = new();
    private string _groupName = "";
    private string _currentSubFolder = "";
    private string _lastSubFolder = "";
    private int _lastCols;
    private double _pageWidth;

    public RecordedLecturesFilesPage(string group)
    {
        InitializeComponent();
        _groupName = Uri.UnescapeDataString(group ?? "");
        PageTitle.Text = _groupName.Replace("_", " ");
        try
        {
            var cached = Preferences.Get("nmu_recorded_orders", "{}");
            _orderCache = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(cached) ?? new();
        }
        catch { _orderCache = new(); }
        _ = LoadFilesAsync();
        try
        {
            var cached = Preferences.Get("nmu_recorded_orders", "{}");
            _orderCache = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(cached) ?? new();
        }
        catch { _orderCache = new(); }
    }

    private async Task LoadFilesAsync()
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
                    ProcessAndRender();
                    _ = FetchFromServerAsync(ctx, cacheKey, thumbKey);
                    return;
                }
            }
            catch { }
        }

        ShowBusy();
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
                var name = f.GetProperty("name").GetString();
                if (name == null) continue;

                if ((name.StartsWith(thumbsPrefix1) || name.StartsWith(thumbsPrefix2)) && name.EndsWith(".jpg"))
                {
                    thumbs.Add(new RawFile(name, null, "original"));
                    continue;
                }

                var source = f.TryGetProperty("source", out var srcEl) ? srcEl.GetString() : "";
                if (source != "original") continue;

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
                    ProcessAndRender();
                }
            }
            else if (_currentFiles.Count == 0)
            {
                ShowEmpty();
            }
        }
        catch
        {
            if (_currentFiles.Count == 0)
                ShowEmpty();
        }
    }

    private void ProcessAndRender()
    {
        var ctx = GetStudentContext();
        var targetPath1 = $"NMU/{ctx.level}/{ctx.semester}/RECORDED_LECTURER/";
        var targetPath2 = $"NMU/{ctx.level}/{ctx.semester}/RECORDED LECTURER/";

        _currentFiles.Clear();
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
            if (parts.Length < 2 || parts[0] != _groupName) continue;

            var isAudio = System.Text.RegularExpressions.Regex.IsMatch(name,
                @"\.(mp3|wav|m4a)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var subFolder = parts.Length > 2 ? parts[1] : "ROOT";
            var fileName = parts[^1];
            var fileNoExt = Path.GetFileNameWithoutExtension(fileName);
            var thumbMatch = _thumbData.FirstOrDefault(t => t.Name.Contains(fileNoExt));
            var thumbUrl = thumbMatch != null ? $"{BaseDownload}{ArchiveId}/{thumbMatch.Name}" : "";

            _currentFiles.Add(new MediaFile(fileName, name, _groupName, subFolder, isAudio, thumbUrl, f.Size));
        }

        _ = CacheThumbsInBackgroundAsync(_currentFiles.Where(f => !string.IsNullOrEmpty(f.ThumbUrl)).Select(f => f.ThumbUrl).Distinct().ToList());
        Dispatcher.Dispatch(BuildTabs);
    }

    private void BuildTabs()
    {
        FolderTabs.Children.Clear();

        var subFolders = _currentFiles
            .Select(f => f.SubFolder)
            .Where(f => f != "ROOT")
            .Distinct()
            .ToList();

        if (subFolders.Count > 1)
        {
            FolderTabs.IsVisible = true;
            foreach (var folder in subFolders)
                FolderTabs.Children.Add(CreateFolderTab(folder));
        }
        else
        {
            FolderTabs.IsVisible = false;
        }

        _currentSubFolder = subFolders.Count > 0 ? subFolders[0] : "ROOT";
        _ = RenderFilesAsync(_currentSubFolder);
        UpdateTabSelection();
    }

    private Border CreateFolderTab(string folder)
    {
        var isActive = folder == _currentSubFolder;
        var tab = new Border
        {
            BackgroundColor = isActive ? Color.FromArgb("#2600F2FF") : Colors.Transparent,
            Stroke = isActive ? Color.FromArgb("#00F2FF") : Colors.Transparent,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(16, 8),
            Margin = new Thickness(4)
        };

        var icon = "📁";
        var f = folder.ToLower();
        if (f.Contains("lec")) icon = "👨‍🏫";
        else if (f.Contains("tut")) icon = "✏️";
        else if (f.Contains("lab")) icon = "🔬";
        else if (f.Contains("quiz")) icon = "❓";
        else if (f.Contains("other")) icon = "📦";

        var stack = new HorizontalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label { Text = icon, FontSize = 14, VerticalTextAlignment = TextAlignment.Center });
        stack.Children.Add(new Label
        {
            Text = folder.ToUpper(),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#00F2FF") : Color.FromArgb("#94A3B8"),
            FontFamily = "Cairo",
            VerticalTextAlignment = TextAlignment.Center
        });

        tab.Content = stack;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectFolder(folder);
        tab.GestureRecognizers.Add(tap);

        return tab;
    }

    private void SelectFolder(string folder)
    {
        _currentSubFolder = folder;
        _ = RenderFilesAsync(folder);
        UpdateTabSelection();
    }

    private void UpdateTabSelection()
    {
        foreach (var child in FolderTabs.Children)
        {
            if (child is Border border && border.Content is HorizontalStackLayout hsl && hsl.Children[1] is Label lbl)
            {
                var isActive = lbl.Text == _currentSubFolder.ToUpper();
                border.BackgroundColor = isActive ? Color.FromArgb("#2600F2FF") : Colors.Transparent;
                border.Stroke = isActive ? Color.FromArgb("#00F2FF") : Colors.Transparent;
                lbl.TextColor = isActive ? Color.FromArgb("#00F2FF") : Color.FromArgb("#94A3B8");
            }
        }
    }

    private async Task RenderFilesAsync(string subFolder)
    {
        var files = subFolder == "ROOT"
            ? _currentFiles.Where(f => f.SubFolder == "ROOT").ToList()
            : _currentFiles.Where(f => f.SubFolder == subFolder).ToList();

        if (files.Count == 0)
        {
            Dispatcher.Dispatch(() =>
            {
                FilesList.Children.Clear();
                FilesList.Children.Add(new Label
                {
                    Text = "No media files found.",
                    TextColor = Color.FromArgb("#94A3B8"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    FontFamily = "Cairo",
                    FontSize = 14,
                    Margin = new Thickness(0, 20, 0, 0)
                });
            });
            return;
        }

        _lastSubFolder = subFolder;

        var dirPath = "";
        if (files.Count > 0)
        {
            var fullPath = files[0].Path;
            dirPath = fullPath.Substring(0, fullPath.LastIndexOf('/'));
        }

        var orderList = _orderCache.GetValueOrDefault(dirPath);
        _ = TryUpdateOrderCacheAsync(dirPath, subFolder);

        if (orderList?.Count > 0)
        {
            files.Sort((a, b) =>
            {
                var idxA = orderList.FindIndex(item => a.Path.Contains(item, StringComparison.OrdinalIgnoreCase) || item.Contains(a.Path, StringComparison.OrdinalIgnoreCase));
                var idxB = orderList.FindIndex(item => b.Path.Contains(item, StringComparison.OrdinalIgnoreCase) || item.Contains(b.Path, StringComparison.OrdinalIgnoreCase));
                if (idxA == -1 && idxB == -1) return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (idxA == -1) return 1;
                if (idxB == -1) return -1;
                return idxA - idxB;
            });
        }
        else
        {
            files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        _currentDisplayFiles = files;
        Dispatcher.Dispatch(ReflowCards);
    }

    private int CalcCols()
    {
        if (_pageWidth <= 0) return 1;
        return Math.Clamp((int)((_pageWidth + CardGap) / (MinCardWidth + CardGap)), 1, 3);
    }

    private void ReflowCards()
    {
        FilesList.Children.Clear();
        if (_currentDisplayFiles.Count == 0) return;

        var cols = CalcCols();
        _lastCols = cols;

        for (int i = 0; i < _currentDisplayFiles.Count; i += cols)
        {
            var row = new Grid { ColumnSpacing = CardGap, HorizontalOptions = LayoutOptions.Center };
            for (int c = 0; c < cols; c++)
                row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(450)));

            for (int j = 0; j < cols; j++)
            {
                var idx = i + j;
                if (idx >= _currentDisplayFiles.Count) break;
                var card = CreateMediaCard(_currentDisplayFiles[idx]);
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

    private async Task TryUpdateOrderCacheAsync(string fullFolderPath, string subFolder)
    {
        try
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"{BaseDownload}{ArchiveId}/{string.Join("/", fullFolderPath.Split('/').Select(Uri.EscapeDataString))}/order_config.json?t={ts}";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var order = doc.RootElement.GetProperty("order").EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();

            if (order.Count > 0 && (!_orderCache.TryGetValue(fullFolderPath, out var existing) || !existing.SequenceEqual(order)))
            {
                _orderCache[fullFolderPath] = order;
                Preferences.Set("nmu_recorded_orders", JsonSerializer.Serialize(_orderCache));
                if (_lastSubFolder == subFolder)
                    _ = RenderFilesAsync(_lastSubFolder);
            }
        }
        catch { }
    }

    private static readonly Color[] FileColors =
    {
        Color.FromArgb("#00F2FF"), Color.FromArgb("#FF0055"), Color.FromArgb("#BD00FF"),
        Color.FromArgb("#00FF88"), Color.FromArgb("#FF6600"), Color.FromArgb("#FFDD00"),
        Color.FromArgb("#FF00AA")
    };

    private static Color GetFileColor(string name)
    {
        var hash = name.GetHashCode();
        return FileColors[Math.Abs(hash) % FileColors.Length];
    }

    private Border CreateMediaCard(MediaFile file)
    {
        var cleanName = Path.GetFileNameWithoutExtension(file.Name).Replace('_', ' ');
        var typeColor = file.IsAudio ? "#BD00FF" : "#00F2FF";
        var typeText = file.IsAudio ? "AUDIO" : "VIDEO";
        var typeBg = file.IsAudio ? "#1ABD00FF" : "#1A00F2FF";
        var fullUrl = $"{BaseDownload}{ArchiveId}/{file.Path}";
        var accent = GetFileColor(cleanName);
        var accentHex = accent.ToArgbHex().TrimStart('#');

        View iconContent;
        if (!string.IsNullOrEmpty(file.ThumbUrl))
        {
            var cachedPath = GetThumbCachedPath(file.ThumbUrl);
            var source = File.Exists(cachedPath)
                ? ImageSource.FromFile(cachedPath)
                : (ImageSource)file.ThumbUrl;
            iconContent = new Image
            {
                Source = source,
                Aspect = Aspect.AspectFill,
                WidthRequest = 110,
                HeightRequest = 80
            };
        }
        else
        {
            iconContent = new Label
            {
                Text = file.IsAudio ? "🎵" : "🎬",
                FontSize = 26,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
        }

        Border iconBorder;
        if (!string.IsNullOrEmpty(file.ThumbUrl))
        {
            iconBorder = new Border
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
        }
        else
        {
            iconBorder = new Border
            {
                WidthRequest = 80,
                HeightRequest = 80,
                BackgroundColor = Color.FromArgb($"#0D{accentHex}"),
                Stroke = accent,
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 28 },
                VerticalOptions = LayoutOptions.Center,
                Content = iconContent
            };
        }

        var nameLabel = new Label
        {
            Text = cleanName,
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "Cairo",
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var typeBadge = new Border
        {
            BackgroundColor = Color.FromArgb(typeBg),
            Stroke = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Padding = new Thickness(6, 2),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = typeText,
                TextColor = accent,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                FontFamily = "Fira Code"
            }
        };

        var subLabel = new Label
        {
            Text = file.SubFolder == "ROOT" ? "" : file.SubFolder.Replace('_', ' '),
            FontSize = 11,
            TextColor = Color.FromArgb("#94A3B8"),
            FontFamily = "Cairo"
        };

        var metaRow = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { typeBadge, subLabel }
        };

        var textStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { nameLabel, metaRow }
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
            AutomationId = cleanName.ToLower(),
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

        TapGestureRecognizer CreatePlayTap()
        {
            var t = new TapGestureRecognizer();
            t.Tapped += async (_, _) =>
            {
                if (!IsOnline())
                {
                    await DisplayAlertAsync("No Internet", "You need internet to stream media.", "OK");
                    return;
                }
                NavHelper.Go(this, new MediaPlayerPage(fullUrl, file.Name, file.IsAudio, cleanName));
            };
            return t;
        }

        iconBorder.GestureRecognizers.Add(CreatePlayTap());
        textStack.GestureRecognizers.Add(CreatePlayTap());

        return card;
    }

    private static bool IsOnline() =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    private static string GetThumbCacheKey(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        return $"thumb_{hash}.jpg";
    }

    private static string GetThumbCachedPath(string url)
    {
        return Path.Combine(FileSystem.CacheDirectory, GetThumbCacheKey(url));
    }

    private static bool IsThumbCached(string url)
    {
        return File.Exists(GetThumbCachedPath(url));
    }

    private static async Task CacheThumbAsync(string url)
    {
        try
        {
            var cachedPath = GetThumbCachedPath(url);
            if (File.Exists(cachedPath)) return;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var data = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(cachedPath, data);
        }
        catch { }
    }

    private static async Task CacheThumbsInBackgroundAsync(List<string> urls)
    {
        foreach (var url in urls)
        {
            if (!IsThumbCached(url))
                await CacheThumbAsync(url);
        }
    }

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
                new Label { Text = "Loading media...", TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center, FontFamily = "Cairo", FontSize = 14 }
            }
        });
    }

    private void ShowEmpty()
    {
        Dispatcher.Dispatch(() =>
        {
            FilesList.Children.Clear();
            FilesList.Children.Add(new Label
            {
                Text = "No media files found.",
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
        foreach (var child in FilesList.Children)
        {
            if (child is Grid row)
            {
                bool anyVisible = false;
                foreach (var element in row.Children)
                {
                    if (element is Border b)
                    {
                        var visible = string.IsNullOrEmpty(query) || (b.AutomationId ?? "").Contains(query);
                        b.IsVisible = visible;
                        if (visible) anyVisible = true;
                    }
                }
                row.IsVisible = string.IsNullOrEmpty(query) || anyVisible;
            }
        }
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

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        NavHelper.Back(this);
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync(_groupName.Replace("_", " "),
            "تصفح ملفات المحاضرات المسجلة.\nيتم جلب البيانات من Archive.org.", "OK");
    }
}
