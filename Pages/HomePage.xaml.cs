using NMU_CE_App.Models;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class HomePage : ContentPage
{
    private readonly SessionService _session = new();
    private bool _isFabOpen;
    private bool _isEditMode;
    private string? _editTerm;
    private IDispatcherTimer? _pulseTimer;
    private bool _pulseDirection;
    private bool _hasAnimated;

    public HomePage()
    {
        InitializeComponent();
        AttachHoverEffects();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUserData();
        if (!_hasAnimated)
            AnimateEntry();
        StartPulse();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pulseTimer?.Stop();
    }

    private async void AnimateEntry()
    {
        _hasAnimated = true;
        var cards = new View[] { CardMaterials, CardYoutube, CardRecorded, CardTests, CardLab };
        for (int i = 0; i < cards.Length; i++)
            cards[i].Opacity = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            _ = cards[i].FadeToAsync(1, 350, Easing.CubicOut);
            await Task.Delay(80);
        }
    }

    private void StartPulse()
    {
        _pulseTimer = Dispatcher.CreateTimer();
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(1200);
        _pulseTimer.Tick += (_, _) =>
        {
            _pulseDirection = !_pulseDirection;
            var target = _pulseDirection ? 0.65 : 0.35;
            var shadows = new[] { MatShadow, YtShadow, RecShadow, TestShadow, LabShadow };
            foreach (var s in shadows)
                s.Opacity = (float)target;
        };
        _pulseTimer.Start();
    }

    private void AttachHoverEffects()
    {
        AddHover(CardMaterials, Color.FromArgb("#0A7C3AED"), Color.FromArgb("#1A1A2E"));
        AddHover(CardYoutube, Color.FromArgb("#152A1B1B"), Color.FromArgb("#1A1A2E"));
        AddHover(CardRecorded, Color.FromArgb("#151E1B2A"), Color.FromArgb("#1A1A2E"));
        AddHover(CardTests, Color.FromArgb("#152A2015"), Color.FromArgb("#1A1A2E"));
        AddHover(CardLab, Color.FromArgb("#15152A1E"), Color.FromArgb("#1A1A2E"));
    }

    private static void AddHover(Border card, Color hoverBg, Color originalBg)
    {
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            card.BackgroundColor = hoverBg;
        };
        pointer.PointerExited += (_, _) =>
        {
            card.BackgroundColor = originalBg;
        };
        card.GestureRecognizers.Add(pointer);
    }

    private double _lastGoodScale = 1.2;
    private DateTime _lastResize = DateTime.MinValue;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
        if ((DateTime.UtcNow - _lastResize).TotalMilliseconds > 200)
        {
            _lastResize = DateTime.UtcNow;
            ResizeCards();
        }
    }

    private void ResizeCards()
    {
        if (CardsGrid == null) return;
        var available = CardsGrid.Width;
        if (available <= 0) available = Width - 40;
        if (available < 300) return;

        var scale = Math.Clamp(available / 680.0, 1.0, 2.4);
        _lastGoodScale = scale;

        CardMaterials.Padding = new Thickness(28 * scale, 24 * scale);
        CardMaterials.MinimumHeightRequest = 90;

        var matIconSize = 62 * scale;
        MatIconContainer.WidthRequest = matIconSize;
        MatIconContainer.HeightRequest = matIconSize;
        MatIconContainer.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(matIconSize / 2) };
        if (MatIconContainer.Content is Label matIcon)
            matIcon.FontSize = 30 * scale;

        var cardPad = 20 * scale;

        foreach (var card in new[] { CardYoutube, CardRecorded, CardTests, CardLab })
        {
            card.Padding = new Thickness(cardPad);

            if (card.Content is Grid grid)
            {
                for (int i = 0; i < grid.Children.Count; i++)
                {
                    if (i == 0 && grid.Children[i] is Border iconBorder)
                    {
                        var iconSize = 58 * scale;
                        iconBorder.WidthRequest = iconSize;
                        iconBorder.HeightRequest = iconSize;
                        iconBorder.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(iconSize / 2) };
                        if (iconBorder.Content is Label iconLabel)
                            iconLabel.FontSize = 28 * scale;
                    }
                    else if (grid.Children[i] is VerticalStackLayout vs)
                    {
                        vs.Spacing = (int)(3 * scale);
                        foreach (var child in vs.Children)
                        {
                            if (child is Label l)
                            {
                                if (l.FontAttributes == FontAttributes.Bold)
                                    l.FontSize = Math.Clamp(15 * scale, 12, 22);
                                else
                                    l.FontSize = Math.Clamp(11 * scale, 9, 15);
                            }
                        }
                    }
                }
            }
        }
    }

    private void LoadUserData()
    {
        var profile = _session.GetStudentProfile();
        if (profile != null)
        {
            UserNameLabel.Text = profile.Name;
            EditName.Text = profile.Name;
            EditYear.SelectedItem = profile.Year;
            _editTerm = profile.Term;

            if (profile.Term == "Semester 1")
                SelectEditTerm1();
            else
                SelectEditTerm2();
        }
        else
        {
            UserNameLabel.Text = "Future Engineer";
        }
    }

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string page)
        {
            if (page == "materials")
                await Shell.Current.GoToAsync("//materials");
            else if (page == "youtube")
                await Shell.Current.GoToAsync("youtubechannels");
            else if (page == "recorded")
                await Shell.Current.GoToAsync("recordedlectures");
            else if (page == "tests")
                await Shell.Current.GoToAsync("//quizweb");
            else if (page == "laboratory")
                await DisplayAlertAsync("Coming Soon", "Laboratory page will be available soon.", "OK");
        }
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("NMU-CE & AIE",
            "Version 2.0\nDeveloped by ABDELRHMAN ELSAYED\nYour AI-Powered Engineering Platform.",
            "OK");
    }

    private async void OnFabTapped(object? sender, TappedEventArgs e)
    {
        _isFabOpen = !_isFabOpen;
        await AnimateFab(_isFabOpen);
    }

    private async void OnFabBackdropTapped(object? sender, TappedEventArgs e)
    {
        if (_isFabOpen)
        {
            _isFabOpen = false;
            await AnimateFab(false);
        }
    }

    private async Task AnimateFab(bool open)
    {
        if (open)
        {
            _ = FabMain.ScaleToAsync(1.08, 120, Easing.CubicOut).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() => _ = FabMain.ScaleToAsync(1.0, 100, Easing.CubicIn)));

            _ = FabIcon.RotateToAsync(135, 300, Easing.CubicOut);

            FabBackdrop.IsVisible = true;
            FabBackdrop.Opacity = 0;
            _ = FabBackdrop.FadeToAsync(1, 200);

            FabItemWhatsApp.IsVisible = true;
            FabItemEval.IsVisible = true;
            FabItemData.IsVisible = true;

            FabItemWhatsApp.TranslationX = 0;
            FabItemWhatsApp.TranslationY = 0;
            FabItemEval.TranslationX = 0;
            FabItemEval.TranslationY = 0;
            FabItemData.TranslationX = 0;
            FabItemData.TranslationY = 0;

            await Task.WhenAll(
                FabItemWhatsApp.FadeToAsync(1, 150),
                FabItemEval.FadeToAsync(1, 200),
                FabItemData.FadeToAsync(1, 260)
            );

            await Task.WhenAll(
                FabItemWhatsApp.TranslateToAsync(0, -80, 280, Easing.CubicOut),
                FabItemEval.TranslateToAsync(0, -145, 320, Easing.CubicOut),
                FabItemData.TranslateToAsync(0, -210, 400, Easing.CubicOut)
            );
        }
        else
        {
            await Task.WhenAll(
                FabItemData.TranslateToAsync(0, 0, 220, Easing.CubicIn),
                FabItemEval.TranslateToAsync(0, 0, 170, Easing.CubicIn),
                FabItemWhatsApp.TranslateToAsync(0, 0, 140, Easing.CubicIn)
            );

            await Task.WhenAll(
                FabItemData.FadeToAsync(0, 100),
                FabItemEval.FadeToAsync(0, 100),
                FabItemWhatsApp.FadeToAsync(0, 100)
            );

            FabItemWhatsApp.IsVisible = false;
            FabItemEval.IsVisible = false;
            FabItemData.IsVisible = false;

            _ = FabIcon.RotateToAsync(0, 250, Easing.CubicIn);

            await FabBackdrop.FadeToAsync(0, 150);
            FabBackdrop.IsVisible = false;
        }
    }

    private async void OnWhatsAppTapped(object? sender, TappedEventArgs e)
    {
        await CloseFab();
        try
        {
            await Launcher.OpenAsync(new Uri("https://wa.me/201029691937"));
        }
        catch
        {
            await DisplayAlertAsync("Error", "Could not open WhatsApp.", "OK");
        }
    }

    private async void OnEvalTapped(object? sender, TappedEventArgs e)
    {
        await CloseFab();
        await DisplayAlertAsync("Coming Soon", "Evaluation page will be available soon.", "OK");
    }

    private async void OnManageDataTapped(object? sender, TappedEventArgs e)
    {
        await CloseFab();
        ShowManageModal();
    }

    private async Task CloseFab()
    {
        if (_isFabOpen)
        {
            _isFabOpen = false;
            await AnimateFab(false);
        }
    }

    private void ShowManageModal()
    {
        var profile = _session.GetStudentProfile();
        if (profile == null)
        {
            DisplayAlertAsync("Error", "No data available.", "OK");
            return;
        }

        EditName.Text = profile.Name;
        EditYear.SelectedItem = profile.Year;
        _editTerm = profile.Term;

        if (profile.Term == "Semester 1")
            SelectEditTerm1();
        else
            SelectEditTerm2();

        ManageOverlay.IsVisible = true;
        ManageOverlay.Opacity = 0;
        ManageOverlay.FadeToAsync(1, 200);
    }

    private async void OnCloseModalTapped(object? sender, TappedEventArgs e)
    {
        await HideManageModal();
    }

    private async Task HideManageModal()
    {
        await ManageOverlay.FadeToAsync(0, 200);
        ManageOverlay.IsVisible = false;
        ResetEditState();
    }

    private void SelectEditTerm1()
    {
        _editTerm = "Semester 1";
        EditTerm1.Stroke = Color.FromArgb("#7C3AED");
        EditTerm1.BackgroundColor = Color.FromArgb("#267C3AED");
        EditTerm1Label.TextColor = Color.FromArgb("#7C3AED");

        EditTerm2.Stroke = Color.FromArgb("#334155");
        EditTerm2.BackgroundColor = Color.FromArgb("#0F172A");
        EditTerm2Label.TextColor = Color.FromArgb("#94A3B8");
    }

    private void SelectEditTerm2()
    {
        _editTerm = "Semester 2";
        EditTerm2.Stroke = Color.FromArgb("#7C3AED");
        EditTerm2.BackgroundColor = Color.FromArgb("#267C3AED");
        EditTerm2Label.TextColor = Color.FromArgb("#7C3AED");

        EditTerm1.Stroke = Color.FromArgb("#334155");
        EditTerm1.BackgroundColor = Color.FromArgb("#0F172A");
        EditTerm1Label.TextColor = Color.FromArgb("#94A3B8");
    }

    private void OnEditTerm1Tapped(object? sender, TappedEventArgs e)
    {
        if (!_isEditMode) return;
        SelectEditTerm1();
    }

    private void OnEditTerm2Tapped(object? sender, TappedEventArgs e)
    {
        if (!_isEditMode) return;
        SelectEditTerm2();
    }

    private async void OnEditSaveTapped(object? sender, TappedEventArgs e)
    {
        var btn = EditSaveLabel;

        if (!_isEditMode)
        {
            _isEditMode = true;
            EditName.IsEnabled = true;
            EditYear.IsEnabled = true;

            btn.Text = "Save Changes";
            EditSaveBtn.BackgroundColor = Color.FromArgb("#06D6A0");
        }
        else
        {
            var name = EditName.Text?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                await DisplayAlertAsync("Error", "Please enter a valid name!", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_editTerm))
            {
                await DisplayAlertAsync("Error", "Please select a semester!", "OK");
                return;
            }

            var year = EditYear.SelectedItem as string ?? "Level 1";

            var profile = new StudentProfile
            {
                Name = name,
                Year = year,
                Term = _editTerm
            };

            _session.SaveStudentProfile(profile);
            UserNameLabel.Text = name;

            await DisplayAlertAsync("Success", "Your data has been updated!", "OK");
            await HideManageModal();
        }
    }

    private void ResetEditState()
    {
        _isEditMode = false;
        EditName.IsEnabled = false;
        EditYear.IsEnabled = false;

        EditSaveLabel.Text = "Change";
        EditSaveBtn.BackgroundColor = Color.FromArgb("#7C3AED");
    }

    private async void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        var confirm = await DisplayAlertAsync("Delete Data",
            "Are you sure you want to delete all your data?", "Yes, Delete", "Cancel");

        if (confirm)
        {
            Preferences.Remove("nmu_student_v4");
            await HideManageModal();
            UserNameLabel.Text = "Future Engineer";
            await DisplayAlertAsync("Deleted", "Your data has been removed.", "OK");
            await Shell.Current.GoToAsync("//splash");
        }
    }
}