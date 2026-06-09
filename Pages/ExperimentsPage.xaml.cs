using NMU_CE_App.Models;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class ExperimentsPage : ContentPage
{
    private static readonly LabExperiment[] AllExperiments =
    {
        new(1, "Determination of high resistance by leakage Method", "https://www.tinkercad.com/embed/fK5Xmq5zgDi?editbtn=1"),
        new(2, "Characteristic curve of diode", "https://www.tinkercad.com/embed/dxCRXwAYzNS?editbtn=1"),
        new(3, "Zener Diode Characteristics", "https://www.tinkercad.com/embed/d7aB801Fiy1?editbtn=1"),
        new(4, "Filters", "https://www.tinkercad.com/embed/8EEZtOFwYss?editbtn=1"),
        new(5, "Determination of a coil inductance by vectors method", "https://www.tinkercad.com/embed/03TS2nAmyAW?editbtn=1"),
        new(6, "Determination of a condenser capacitor by vectors method", "https://www.tinkercad.com/embed/f7t5x9cE3HX?editbtn=1"),
        new(7, "RLC Resonance in Series Circuits", "https://dcaclab.com/ar/experiments/84033/iframe"),
        new(8, "Clipper Circuits", "https://dcaclab.com/ar/experiments/81582/iframe"),
        new(9, "Ohm's law", "https://www.tinkercad.com/embed/5AchqeXHK96?editbtn=1"),
        new(10, "Kirchhoff's Rules", "https://www.tinkercad.com/embed/jwPvWI0Sz99?editbtn=1"),
        new(11, "Clipper Circuits", "https://dcaclab.com/ar/experiments/80970/iframe"),
        new(12, "RL Circuit", "https://dcaclab.com/ar/experiments/77352/iframe"),
        new(13, "RC Circuit", "https://dcaclab.com/ar/experiments/77351/iframe")
    };

    private static readonly string EmptyLabUrl = "https://dcaclab.com/ar/experiments/82098/iframe";

    private LabExperiment[] _filtered = AllExperiments;
    private double _prevCardWidth;

    public ExperimentsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!WarningOverlay.IsVisible)
        {
            _ = ShowWarningAfterDelay();
        }
    }

    private async Task ShowWarningAfterDelay()
    {
        await Task.Delay(500);
        WarningOverlay.IsVisible = true;
        WarningOverlay.Opacity = 0;
        await WarningOverlay.FadeToAsync(1, 300);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        GridCanvas?.Invalidate();
        var cardW = CalcCardWidth();
        if (Math.Abs(cardW - _prevCardWidth) > 1)
        {
            _prevCardWidth = cardW;
            RenderExperiments(SearchEntry.Text);
        }
    }

    private double CalcCardWidth()
    {
        var available = ContentRoot.Width;
        if (available <= 0) available = ContentScroll.Width - 40;
        if (available <= 0) available = Width - 60;
        if (available <= 0) available = 500;

        int cols;
        if (available < 400) cols = 2;
        else if (available < 600) cols = 3;
        else if (available < 900) cols = 4;
        else cols = 5;

        var gap = 16;
        var totalGaps = gap * (cols + 1);
        var cardWidth = (available - totalGaps) / cols;
        if (cardWidth < 140) cardWidth = 140;
        if (cardWidth > 220) cardWidth = 220;
        return cardWidth;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim().ToLower() ?? "";
        _filtered = string.IsNullOrEmpty(query)
            ? AllExperiments
            : AllExperiments.Where(exp => exp.Title.ToLower().Contains(query)).ToArray();
        RenderExperiments(query);
    }

    private void RenderExperiments(string filter)
    {
        ExperimentsGrid.Children.Clear();

        if (_filtered.Length == 0)
        {
            ExperimentsGrid.Children.Add(new Label
            {
                Text = "No experiments found.",
                TextColor = Color.FromArgb("#94A3B8"),
                HorizontalTextAlignment = TextAlignment.Center,
                FontFamily = "Cairo",
                FontSize = 14,
                Margin = new Thickness(0, 20, 0, 0)
            });
            return;
        }

        var cardW = _prevCardWidth > 0 ? _prevCardWidth : CalcCardWidth();
        _prevCardWidth = cardW;

        foreach (var exp in _filtered)
            ExperimentsGrid.Children.Add(CreateExperimentCard(exp, cardW));
    }

    private Border CreateExperimentCard(LabExperiment exp, double cardW)
    {
        var cardH = cardW * 0.9;
        var iconSize = cardW < 160 ? 28 : 34;
        var fontSize = cardW < 160 ? 11 : 13;

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1E293B"),
            Stroke = Color.FromArgb("#334155"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            WidthRequest = cardW,
            HeightRequest = Math.Max(cardH, 110),
            Margin = new Thickness(8),
            Padding = new Thickness(10, 12)
        };

        var originalBg = Color.FromArgb("#1A1E293B");
        var hoverBg = Color.FromArgb("#1A1A2E");
        var borderColor = Color.FromArgb("#00F2FF");

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            card.BackgroundColor = Color.FromArgb("#242943");
            card.Stroke = Color.FromArgb("#00F2FF");
            card.StrokeThickness = 1.5;
            _ = card.ScaleToAsync(1.04, 120, Easing.CubicOut);
        };
        pointer.PointerExited += (_, _) =>
        {
            card.BackgroundColor = originalBg;
            card.Stroke = Color.FromArgb("#334155");
            card.StrokeThickness = 1;
            _ = card.ScaleToAsync(1.0, 120, Easing.CubicOut);
        };
        card.GestureRecognizers.Add(pointer);

        var stack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 10
        };

        stack.Children.Add(new Label
        {
            Text = "🔬",
            FontSize = iconSize,
            HorizontalTextAlignment = TextAlignment.Center,
            Opacity = 0.9
        });

        var url = exp.IframeUrl;
        stack.Children.Add(new Label
        {
            Text = exp.Title,
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            FontFamily = "Fira Code",
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3,
            HeightRequest = fontSize * 3.6
        });

        card.Content = stack;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OpenExperiment(exp.IframeUrl);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void OpenExperiment(string url)
    {
        ExperimentWebView.Source = new UrlWebViewSource { Url = url };
        NormalView.IsVisible = false;
        ViewerContainer.IsVisible = true;
    }

    private void CloseViewer()
    {
        ExperimentWebView.Source = "about:blank";
        ViewerContainer.IsVisible = false;
        NormalView.IsVisible = true;
    }

    private void OnViewerBackTapped(object? sender, TappedEventArgs e)
    {
        CloseViewer();
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        if (ViewerContainer.IsVisible)
        {
            CloseViewer();
            return;
        }
        NavHelper.Back(this);
    }

    private void OnNewLabTapped(object? sender, TappedEventArgs e)
    {
        OpenExperiment(EmptyLabUrl);
    }

    private void OnWarningDismissTapped(object? sender, TappedEventArgs e)
    {
        _ = WarningOverlay.FadeToAsync(0, 250);
        WarningOverlay.IsVisible = false;
    }

    private void OnWarningOverlayTapped(object? sender, TappedEventArgs e)
    {
        // prevent click-through
    }
}
