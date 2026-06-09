using NMU_CE_App.Models;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class FeedbackPage : ContentPage
{
    private readonly FeedbackService _fb = new();
    private readonly SessionService _session = new();

    private static readonly Color StarOn = Color.FromArgb("#FFB800");
    private static readonly Color StarOff = Color.FromArgb("#2A3A5C");

    private double _rating;
    private bool _anon;
    private bool _hasReview;
    private string _serial = "";
    private bool _verified;
    private double _scale = 1;
    private DateTime _lastResize = DateTime.MinValue;

    // 5 visual star Labels
    private readonly Label[] _stars = new Label[5];
    // current reviews (for re-rendering on resize)
    private List<FeedbackReview> _currentReviews = new();
    // polling
    private CancellationTokenSource? _pollCts;

    private double _pageWidth = -1;

    public FeedbackPage()
    {
        InitializeComponent();
        BuildStarUI();
        HoverAll();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
        if (width <= 0) return;
        if (Math.Abs(width - _pageWidth) < 1) return;
        _pageWidth = width;

        var now = DateTime.UtcNow;
        if ((now - _lastResize).TotalMilliseconds < 150) return;
        _lastResize = now;

        var w = Math.Min(width - 30, 600.0);
        if (w <= 50) return;
        var s = Math.Clamp(w / 500.0, 0.6, 1.4);
        if (Math.Abs(s - _scale) < 0.02) return;
        _scale = s;
        Rescale();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrEmpty(_serial))
        {
            _serial = FeedbackService.GetDeviceSerial();
            LoadStudent();
            SendBtnLabel.Text = "📤 إرسال";
            await LoadAll();
            StartPoll();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pollCts?.Cancel();
    }

    // ====================== STARS ======================

    private void BuildStarUI()
    {
        // 10 columns – 2 per star (left half, right half)
        for (int i = 0; i < 10; i++)
            StarsContainer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        StarsContainer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        StarsContainer.HeightRequest = 38;

        // Visual labels – each spans 2 columns
        for (int i = 0; i < 5; i++)
        {
            _stars[i] = new Label
            {
                Text = "☆", FontSize = 28, TextColor = StarOff,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(_stars[i], i * 2);
            Grid.SetColumnSpan(_stars[i], 2);
            StarsContainer.Children.Add(_stars[i]);
        }

        // Tap targets – 10 transparent BoxViews
        for (int i = 0; i < 10; i++)
        {
            var val = (i + 1) * 0.5; // 0.5, 1.0, 1.5 … 5.0
            var box = new BoxView
            {
                Color = Colors.Transparent,
                InputTransparent = false
            };
            var v = val;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { _rating = v; RenderStars(); };
            box.GestureRecognizers.Add(tap);
            Grid.SetColumn(box, i);
            StarsContainer.Children.Add(box);
        }
    }

    private void RenderStars()
    {
        for (int i = 0; i < 5; i++)
        {
            var full = i + 1;
            if (_rating >= full)
            {
                _stars[i].Text = "★";
                _stars[i].TextColor = StarOn;
            }
            else if (_rating >= full - 0.5)
            {
                _stars[i].Text = "★";
                _stars[i].TextColor = StarOn.WithAlpha(0.45f);
            }
            else
            {
                _stars[i].Text = "☆";
                _stars[i].TextColor = StarOff;
            }
        }
    }

    private static string StarStr(double r)
    {
        var s = "";
        for (int i = 1; i <= 5; i++)
        {
            if (r >= i) s += "★";
            else if (r >= i - 0.5) s += "⯨";
            else s += "☆";
        }
        return s;
    }

    // ====================== ANON TOGGLE ======================

    private void OnAnonToggleTapped(object? sender, TappedEventArgs e)
    {
        _anon = !_anon;
        if (_anon)
        {
            AnonToggle.Stroke = Color.FromArgb("#00E5FF");
            AnonToggle.BackgroundColor = Color.FromArgb("#0D00E5FF");
            AnonSwitch.BackgroundColor = Color.FromArgb("#00E5FF");
            AnonKnob.HorizontalOptions = LayoutOptions.Start;
            AnonLabel.TextColor = Color.FromArgb("#00E5FF");
            NameEntry.Text = "";
            NameEntry.IsEnabled = false;
            NameEntry.Placeholder = "مجهول (Anonymous)";
            NameEntry.Opacity = 0.5;
        }
        else
        {
            AnonToggle.Stroke = Color.FromArgb("#334155");
            AnonToggle.BackgroundColor = Color.FromArgb("#1A0A0A1A");
            AnonSwitch.BackgroundColor = Color.FromArgb("#333333");
            AnonKnob.HorizontalOptions = LayoutOptions.End;
            AnonLabel.TextColor = Color.FromArgb("#94A3B8");
            NameEntry.IsEnabled = true;
            NameEntry.Opacity = 1;
            NameEntry.Placeholder = "ادخل اسمك هنا...";
            LoadStudent();
        }
    }

    // ====================== SEND ======================

    private async void OnSendTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrEmpty(_serial)) return;

        var comment = CommentEditor.Text?.Trim() ?? "";
        var name = NameEntry.Text?.Trim() ?? "";

        if (!_anon && name.Length < 3) { ShowAlert("يرجى إدخال اسم صحيح.", "error"); return; }
        if (_rating == 0) { ShowAlert("الرجاء تحديد التقييم بالنجوم أولاً.", "info"); return; }

        SendBtnLabel.Text = "⏳ جاري الإرسال...";
        SendBtn.BackgroundColor = Color.FromArgb("#334155");

        var ok = await _fb.SetReviewAsync(new FeedbackReview
        {
            Serial = _serial,
            Review = _rating.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            Comment = comment,
            Name = _anon ? "مجهول" : name,
            IsVerified = _verified && !_anon,
            Level = _verified && !_anon ? "Verified" : "",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        if (ok)
        {
            SendBtnLabel.Text = "✏️ تحديث التقييم";
            SendBtn.BackgroundColor = Color.FromArgb("#7C3AED");
            DeleteBtn.IsVisible = true;
            _hasReview = true;
            ShowAlert("تم حفظ تقييمك بنجاح! شكراً لك.", "success");
            _ = LoadAll(); // refresh immediately
        }
        else
        {
            SendBtnLabel.Text = "📤 إرسال";
            SendBtn.BackgroundColor = Color.FromArgb("#7C3AED");
            ShowAlert("حدث خطأ في الاتصال بالخادم.", "error");
        }
    }

    // ====================== DELETE ======================

    private void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrEmpty(_serial) || !_hasReview) return;
        ConfirmOverlay.IsVisible = true;
        ConfirmOverlay.Opacity = 0;
        _ = ConfirmOverlay.FadeToAsync(1, 200);
    }

    private async void OnConfirmDeleteTapped(object? sender, TappedEventArgs e)
    {
        _ = ConfirmOverlay.FadeToAsync(0, 150);
        ConfirmOverlay.IsVisible = false;

        SendBtnLabel.Text = "⏳ جاري الحذف...";
        var ok = await _fb.DeleteReviewAsync(_serial);

        if (ok)
        {
            _rating = 0; _hasReview = false;
            RenderStars();
            CommentEditor.Text = "";
            DeleteBtn.IsVisible = false;
            SendBtnLabel.Text = "📤 إرسال";
            if (!_anon) { NameEntry.Text = ""; LoadStudent(); }
            ShowAlert("تم حذف التقييم نهائياً.", "success");
            _ = LoadAll(); // refresh immediately
        }
        else
        {
            SendBtnLabel.Text = "📤 إرسال";
            ShowAlert("فشل الحذف، يرجى المحاولة لاحقاً.", "error");
        }
    }

    private void OnConfirmCancelTapped(object? sender, TappedEventArgs e)
    {
        _ = ConfirmOverlay.FadeToAsync(0, 150);
        ConfirmOverlay.IsVisible = false;
    }

    // ====================== LOAD & POLL ======================

    private async Task LoadAll()
    {
        LoaderLabel.IsVisible = true;
        var list = await _fb.GetAllReviewsAsync();
        LoaderLabel.IsVisible = false;
        _currentReviews = list;
        ApplyData(list);
    }

    private void StartPoll()
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        var ct = _pollCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(10_000, ct); } catch { break; }
                var list = await _fb.GetAllReviewsAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _currentReviews = list;
                    ApplyData(list);
                });
            }
        });
    }

    private void ApplyData(List<FeedbackReview> list)
    {
        var found = false;
        foreach (var r in list)
        {
            if (r.Serial == _serial) { found = true; FillMy(r); break; }
        }
        if (!found && _hasReview) { _rating = 0; _hasReview = false; RenderStars(); CommentEditor.Text = ""; DeleteBtn.IsVisible = false; SendBtnLabel.Text = "📤 إرسال"; }
        CountLabel.Text = $"{list.Count} تقييم";
        RenderCards();
    }

    private void FillMy(FeedbackReview data)
    {
        _hasReview = true;
        _rating = double.TryParse(data.Review, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
        RenderStars();
        if (!CommentEditor.IsFocused) CommentEditor.Text = data.Comment ?? "";

        if (!NameEntry.IsFocused)
        {
            if (data.Name == "مجهول" || string.IsNullOrEmpty(data.Name))
            {
                _anon = true;
                AnonToggle.Stroke = Color.FromArgb("#00E5FF"); AnonToggle.BackgroundColor = Color.FromArgb("#0D00E5FF");
                AnonSwitch.BackgroundColor = Color.FromArgb("#00E5FF"); AnonKnob.HorizontalOptions = LayoutOptions.Start;
                AnonLabel.TextColor = Color.FromArgb("#00E5FF");
                NameEntry.Text = ""; NameEntry.IsEnabled = false; NameEntry.Placeholder = "مجهول (Anonymous)"; NameEntry.Opacity = 0.5;
            }
            else
            {
                _anon = false;
                AnonToggle.Stroke = Color.FromArgb("#334155"); AnonToggle.BackgroundColor = Color.FromArgb("#1A0A0A1A");
                AnonSwitch.BackgroundColor = Color.FromArgb("#333333"); AnonKnob.HorizontalOptions = LayoutOptions.End;
                AnonLabel.TextColor = Color.FromArgb("#94A3B8");
                NameEntry.IsEnabled = true; NameEntry.Opacity = 1; NameEntry.Text = data.Name;
            }
        }
        SendBtnLabel.Text = "✏️ تحديث التقييم";
        DeleteBtn.IsVisible = true;
    }

    // ====================== RENDER CARDS ======================

    private void RenderCards()
    {
        ReviewsList.Children.Clear();
        var s = _scale;
        var fontSize = (double size) => Math.Clamp(size * s, 9, 18);

        foreach (var rev in _currentReviews)
        {
            var rating = double.TryParse(rev.Review, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rv) ? rv : 0;
            var mine = rev.Serial == _serial;
            var name = rev.Name == "مجهول" ? "طالب مجهول" : rev.Name;
            var comment = rev.Comment ?? "";
            var avatar = (rev.Name == "مجهول" || string.IsNullOrEmpty(rev.Name))
                ? "https://ui-avatars.com/api/?name=A&background=1e293b&color=94a3b8&size=45"
                : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(name)}&background=00f2ff&color=000&font-weight=bold&size=45";
            var date = rev.Timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(rev.Timestamp).ToString("d/M/yyyy") : "";

            var card = new Border
            {
                BackgroundColor = mine ? Color.FromArgb("#0500E5FF") : Color.FromArgb("#1A1E293B"),
                Stroke = mine ? Color.FromArgb("#00E5FF") : Color.FromArgb("#1A334155"),
                StrokeThickness = mine ? 1 : 0.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(16 * s) },
                Padding = new Thickness(14 * s, 12 * s)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(40 * s)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12 * s
            };

            grid.Add(new Border
            {
                WidthRequest = 40 * s, HeightRequest = 40 * s,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(20 * s) },
                Stroke = Color.FromArgb("#1AFFFFFF"), StrokeThickness = 1, Padding = 0,
                Content = new Image { Source = avatar, WidthRequest = 40 * s, HeightRequest = 40 * s, Aspect = Aspect.AspectFill },
                VerticalOptions = LayoutOptions.Start
            }, 0);

            var body = new VerticalStackLayout { Spacing = 4 * s };

            // top row: name + verified badge + date
            var top = new Grid
            {
                ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }
            };
            var nameRow = new HorizontalStackLayout { Spacing = 4 * s };
            nameRow.Children.Add(new Label
            {
                Text = name, FontSize = fontSize(13), FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White, FontFamily = "Cairo", VerticalTextAlignment = TextAlignment.Center
            });
            if (rev.IsVerified)
            {
                nameRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#00E5FF"), StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(7 * s) },
                    WidthRequest = 16 * s, HeightRequest = 16 * s, VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = "✓", FontSize = fontSize(9), FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#0A0A1A"),
                        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
                    }
                });
            }
            top.Add(nameRow, 0);
            top.Add(new Label
            {
                Text = date, FontSize = fontSize(10), TextColor = Color.FromArgb("#64748B"),
                FontFamily = "Fira Code", VerticalTextAlignment = TextAlignment.Center
            }, 1);
            body.Children.Add(top);

            body.Children.Add(new Label { Text = StarStr(rating), FontSize = fontSize(11), TextColor = StarOn });

            if (!string.IsNullOrEmpty(comment))
                body.Children.Add(new Label
                {
                    Text = comment, FontSize = fontSize(12), TextColor = Color.FromArgb("#94A3B8"),
                    FontFamily = "Cairo", LineBreakMode = LineBreakMode.WordWrap
                });

            grid.Add(body, 1);
            card.Content = grid;
            ReviewsList.Children.Add(card);
        }
    }

    // ====================== RESIZE ======================

    private void Rescale()
    {
        var s = _scale;

        WidgetCard.Padding = new Thickness(20 * s);
        WidgetHeader.ColumnSpacing = 12 * s;

        var avSize = 50 * s;
        foreach (var av in new[] { LeftAvatar, RightAvatar })
        {
            av.WidthRequest = avSize; av.HeightRequest = avSize;
            av.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(avSize / 2) };
            if (av.Content is Label l) l.FontSize = 24 * s;
        }

        ProfileNameLabel.FontSize = Math.Clamp(13 * s, 10, 18);
        ProfileDescLabel.FontSize = Math.Clamp(11 * s, 9, 15);

        var starSz = Math.Clamp(28 * s, 18, 40);
        StarsContainer.HeightRequest = starSz + 8;
        foreach (var lbl in _stars) lbl.FontSize = starSz;

        NameEntry.FontSize = Math.Clamp(14 * s, 11, 18);
        CommentEditor.FontSize = Math.Clamp(14 * s, 11, 18);
        CommentEditor.HeightRequest = 90 * s;

        SendBtn.HeightRequest = 44 * s;
        SendBtnLabel.FontSize = Math.Clamp(13 * s, 10, 17);
        DeleteBtn.WidthRequest = 50 * s;
        DeleteBtn.HeightRequest = 44 * s;

        AnonToggle.Padding = new Thickness(12 * s, 6 * s);
        AnonLabel.FontSize = Math.Clamp(12 * s, 9, 15);

        CountLabel.FontSize = Math.Clamp(11 * s, 9, 14);

        RenderCards(); // re-render with new scale
    }

    // ====================== STUDENT DATA ======================

    private void LoadStudent()
    {
        var p = _session.GetStudentProfile();
        if (p != null && !string.IsNullOrEmpty(p.Name))
        {
            _verified = true;
            if (string.IsNullOrEmpty(NameEntry.Text)) NameEntry.Text = p.Name;
        }
    }

    // ====================== ALERT ======================

    private void ShowAlert(string msg, string type)
    {
        AlertIcon.Text = type == "error" ? "❌" : type == "success" ? "✅" : "⭐";
        AlertMessage.Text = msg;
        AlertOverlay.IsVisible = true;
        AlertOverlay.Opacity = 0;
        _ = AlertOverlay.FadeToAsync(1, 200);
    }

    private void OnAlertDismissTapped(object? sender, TappedEventArgs e)
    {
        _ = AlertOverlay.FadeToAsync(0, 150);
        AlertOverlay.IsVisible = false;
    }

    // ====================== WHATSAPP ======================

    private async void OnWhatsAppTapped(object? sender, TappedEventArgs e)
    {
        try { await Launcher.OpenAsync(new Uri("https://wa.me/201029691937")); }
        catch { ShowAlert("تعذر فتح واتساب.", "error"); }
    }

    // ====================== BACK ======================

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        _pollCts?.Cancel();
        NavHelper.Back(this);
    }

    // ====================== HOVER ======================

    private void HoverAll()
    {
        Hover(SendBtn, 1.05);
        Hover(DeleteBtn, 1.08);
        Hover(WhatsAppBtn, 1.04);
        Hover(AnonToggle, 1.04);
        Hover(AlertDismissBtn, 1.05);
        Hover(ConfirmCancelBtn, 1.04);
        Hover(ConfirmDeleteBtn, 1.04);
    }

    private static void Hover(View v, double sc)
    {
        var p = new PointerGestureRecognizer();
        p.PointerEntered += (_, _) => v.ScaleToAsync(sc, 120, Easing.CubicOut);
        p.PointerExited += (_, _) => v.ScaleToAsync(1.0, 120, Easing.CubicOut);
        v.GestureRecognizers.Add(p);
    }
}
