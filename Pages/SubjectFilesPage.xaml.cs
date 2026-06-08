using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class SubjectFilesPage : ContentPage
{
    private const string ArchiveId = "nmu.ce";
    private const string BaseFolder = "NMU";
    private const string BaseDownload = "https://archive.org/download/";
    private const string CachePrefix = "nmu_materials_";

    private record RawFile(string Name, long? Size, string Source);
    private record MaterialFile(string Name, string Path, string Folder, long? Size);

    private List<RawFile> _allData = new();
    private string _subjectName = "";
    private string _currentFolder = "";
    private List<MaterialFile> _currentFiles = new();
    private List<string> _currentFolders = new();
    private Dictionary<string, List<string>> _orderCache = new();
    private const int CardGap = 14;
    private const int MinCardWidth = 450;
    private int _lastCols;
    private string _lastFolder = "";
    private List<MaterialFile> _currentDisplayFiles = new();
    private double _pageWidth;

    public SubjectFilesPage(string subject)
    {
        InitializeComponent();
        _subjectName = Uri.UnescapeDataString(subject ?? "");
        PageTitle.Text = _subjectName.Replace("_", " ");
        _ = LoadSubjectFilesAsync();
        try
        {
            var cached = Preferences.Get("nmu_materials_orders", "{}");
            _orderCache = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(cached) ?? new();
        }
        catch { _orderCache = new(); }
    }

    private async Task LoadSubjectFilesAsync()
    {
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
                    ProcessFiles();
                    _ = FetchFromServerAsync(ctx, cacheKey);
                    return;
                }
            }
            catch { Preferences.Remove(cacheKey); }
        }

        ShowBusy();
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
                    ProcessFiles();
                }
            }
        }
        catch { }

        if (_allData.Count == 0)
            ShowEmpty();
    }

    private void ProcessFiles()
    {
        var ctx = GetStudentContext();
        var matPrefix = $"{BaseFolder}/{ctx.level}/{ctx.semester}/PDF/{_subjectName}/";

        _currentFiles = _allData
            .Where(f => f.Name.StartsWith(matPrefix))
            .Select(f =>
            {
                var rel = f.Name[matPrefix.Length..];
                var parts = rel.Split('/');
                return new MaterialFile(parts[^1], f.Name, parts.Length > 1 ? parts[0] : "ROOT", f.Size);
            })
            .ToList();

        _currentFolders = _currentFiles
            .Select(f => f.Folder)
            .Where(f => f != "ROOT")
            .Distinct()
            .ToList();

        BuildTabs();
    }

    private void BuildTabs()
    {
        FolderTabs.Children.Clear();

        var sortOrder = new[] { "lec", "tut", "lab", "quiz", "other" };
        _currentFolders.Sort((a, b) =>
        {
            var aName = a.ToLower();
            var bName = b.ToLower();
            var idxA = Array.FindIndex(sortOrder, key => aName.Contains(key));
            var idxB = Array.FindIndex(sortOrder, key => bName.Contains(key));
            if (idxA == -1) idxA = 99;
            if (idxB == -1) idxB = 99;
            return idxA == idxB ? string.Compare(aName, bName, StringComparison.Ordinal) : idxA - idxB;
        });

        if (_currentFolders.Count > 1)
        {
            FolderTabs.IsVisible = true;
            foreach (var folder in _currentFolders)
                FolderTabs.Children.Add(CreateFolderTab(folder));
        }
        else
        {
            FolderTabs.IsVisible = false;
        }

        _currentFolder = _currentFolders.Count > 0 ? _currentFolders[0] : "ROOT";
        _ = RenderFilesAsync(_currentFolder);
        UpdateTabSelection();
    }

    private Border CreateFolderTab(string folder)
    {
        var isActive = folder == _currentFolder;
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
        _currentFolder = folder;
        _ = RenderFilesAsync(folder);
        UpdateTabSelection();
    }

    private void UpdateTabSelection()
    {
        foreach (var child in FolderTabs.Children)
        {
            if (child is Border border && border.Content is HorizontalStackLayout hsl && hsl.Children[1] is Label lbl)
            {
                var isActive = lbl.Text == _currentFolder.ToUpper();
                border.BackgroundColor = isActive ? Color.FromArgb("#2600F2FF") : Colors.Transparent;
                border.Stroke = isActive ? Color.FromArgb("#00F2FF") : Colors.Transparent;
                lbl.TextColor = isActive ? Color.FromArgb("#00F2FF") : Color.FromArgb("#94A3B8");
            }
        }
    }

    private async Task RenderFilesAsync(string folderFilter)
    {
        var files = _currentFiles
            .Where(f => f.Folder == folderFilter && f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !f.Name.EndsWith("_text.pdf", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            Dispatcher.Dispatch(() =>
            {
                FilesList.Children.Clear();
                FilesList.Children.Add(new Label
                {
                    Text = "No PDF files found.",
                    TextColor = Color.FromArgb("#94A3B8"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    FontFamily = "Cairo",
                    FontSize = 14,
                    Margin = new Thickness(0, 20, 0, 0)
                });
            });
            return;
        }

        _lastFolder = folderFilter;

        var ctx = GetStudentContext();
        var dirPath = $"{BaseFolder}/{ctx.level}/{ctx.semester}/PDF/{_subjectName}/";
        if (folderFilter != "ROOT") dirPath += $"{folderFilter}/";

        var orderList = _orderCache.GetValueOrDefault(dirPath);
        _ = TryUpdateOrderCacheAsync(dirPath, folderFilter);

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
                var card = CreateFileCard(_currentDisplayFiles[idx]);
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

    private static bool IsFileCached(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        var cachedPath = Path.Combine(FileSystem.CacheDirectory, $"pdf_{hash}.pdf");
        return File.Exists(cachedPath);
    }

    private static bool IsOnline()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private Border CreateFileCard(MaterialFile file)
    {
        var displayName = file.Name.Replace(".pdf", "", StringComparison.OrdinalIgnoreCase);
        var sizeMb = file.Size.HasValue ? $"{file.Size.Value / 1024.0 / 1024.0:F1} MB" : "—";
        var fullUrl = $"{BaseDownload}{ArchiveId}/{file.Path}";
        var online = IsOnline();
        var cached = IsFileCached(fullUrl);
        var available = online || cached;
        var accent = GetFileColor(displayName);
        var accentHex = accent.ToArgbHex().TrimStart('#');

        var statusIcon = available ? "🟢" : "🔴";
        var statusColor = available ? Color.FromArgb("#00FF88") : Color.FromArgb("#FF0055");
        var statusLabel = available
            ? (cached ? "متاح أوفلاين" : "محتاج نت")
            : "محتاج نت";

        var iconBorder = new Border
        {
            WidthRequest = 56, HeightRequest = 56,
            BackgroundColor = Color.FromArgb($"#0D{accentHex}"),
            Stroke = accent,
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 28 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = available ? "📄" : "🔒", FontSize = 26,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var nameLabel = new Label
        {
            Text = displayName,
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "Cairo",
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var sizeLabel = new Label
        {
            Text = sizeMb,
            TextColor = accent,
            FontSize = 13,
            FontFamily = "Fira Code",
            FontAttributes = FontAttributes.Bold
        };

        var statusBadge = new Label
        {
            Text = $"{statusIcon} {statusLabel}",
            TextColor = statusColor,
            FontSize = 11,
            FontFamily = "Cairo",
            FontAttributes = FontAttributes.Bold
        };

        var textStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { nameLabel, sizeLabel, statusBadge }
        };

        var innerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(56)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14,
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
            AutomationId = displayName.ToLower(),
            HeightRequest = 120
        };

        if (!available)
        {
            card.BackgroundColor = Color.FromArgb("#1A3B0015");
            card.Stroke = Color.FromArgb("#44FF0055");
        }

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            if (!available)
            {
                card.BackgroundColor = Color.FromArgb("#33FF0055");
                card.Stroke = Color.FromArgb("#FF0055");
            }
            else
            {
                card.BackgroundColor = Color.FromArgb($"#1A{accentHex}");
                card.Stroke = accent;
            }
        };
        pointer.PointerExited += (_, _) =>
        {
            if (!available)
            {
                card.BackgroundColor = Color.FromArgb("#1A3B0015");
                card.Stroke = Color.FromArgb("#44FF0055");
            }
            else
            {
                card.BackgroundColor = Color.FromArgb("#1A1E293B");
                card.Stroke = Color.FromArgb("#334155");
            }
        };
        card.GestureRecognizers.Add(pointer);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (!available)
            {
                await DisplayAlertAsync("متاح فقط أوفلاين",
                    $"الملف \"{displayName}\" لسه ما اتفتحش قبل كدا.\n\nعشان تفتحه، لازم يكون فيه اتصال بالنت أول مرة.",
                    "OK");
                return;
            }
            NavHelper.Go(this, new PdfViewerPage(fullUrl, file.Name));
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private async Task TryUpdateOrderCacheAsync(string fullFolderPath, string folderFilter)
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
                Preferences.Set("nmu_materials_orders", JsonSerializer.Serialize(_orderCache));
                if (_lastFolder == folderFilter)
                    _ = RenderFilesAsync(_lastFolder);
            }
        }
        catch { }
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
                new Label { Text = "Loading files...", TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center, FontFamily = "Cairo", FontSize = 14 }
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
                Text = "No files available for this subject.",
                TextColor = Color.FromArgb("#94A3B8"),
                HorizontalTextAlignment = TextAlignment.Center,
                FontFamily = "Cairo",
                FontSize = 16,
                Margin = new Thickness(0, 40, 0, 0)
            });
        });
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        NavHelper.Back(this);
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
}
