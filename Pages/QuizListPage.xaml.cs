using NMU_CE_App.Models;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class QuizListPage : ContentPage
{
    private readonly QuizService _quizService = new();

    public QuizListPage()
    {
        InitializeComponent();
        LoadSubjects();
    }

    private async void LoadSubjects()
    {
        try
        {
            StatusLabel.Text = "Synchronizing Data...";
            var subjects = await _quizService.GetSubjectsAsync();

            if (subjects.Count == 0)
            {
                StatusLabel.Text = "No quizzes available for your level.";
                return;
            }

            StatusLabel.IsVisible = false;
            SubjectsGrid.IsVisible = true;

            foreach (var subject in subjects)
            {
                SubjectsGrid.Children.Add(CreateSubjectCard(subject));
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Failed to load: {ex.Message}";
        }
    }

    private Border CreateSubjectCard(QuizSubject subject)
    {
        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1E293B"),
            Stroke = Color.FromArgb("#3300F2FF"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16),
            WidthRequest = 160,
            HeightRequest = 160,
            Margin = new Thickness(6),
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#0D00F2FF")),
                Offset = new Point(0, 4),
                Radius = 12,
                Opacity = 0.3f
            }
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnSubjectTapped(subject))
        });

        var stack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 12
        };

        var icon = new Label
        {
            Text = "📝",
            FontSize = 36,
            HorizontalTextAlignment = TextAlignment.Center
        };
        stack.Children.Add(icon);

        var nameLabel = new Label
        {
            Text = subject.Name,
            FontSize = 14,
            FontFamily = "Cairo",
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3
        };
        stack.Children.Add(nameLabel);

        card.Content = stack;
        return card;
    }

    private async Task OnSubjectTapped(QuizSubject subject)
    {
        await Shell.Current.GoToAsync("//quizweb");
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//home");
    }
}
