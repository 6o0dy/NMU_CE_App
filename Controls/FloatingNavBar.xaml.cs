namespace NMU_CE_App.Controls;

public partial class FloatingNavBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FloatingNavBar), "",
            propertyChanged: (b, _, n) => ((FloatingNavBar)b).NavTitle.Text = (string?)n ?? "");

    public static readonly BindableProperty ShowBackProperty =
        BindableProperty.Create(nameof(ShowBack), typeof(bool), typeof(FloatingNavBar), false,
            propertyChanged: (b, _, n) => ((FloatingNavBar)b).BackBtn.IsVisible = (bool)n);

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowBack { get => (bool)GetValue(ShowBackProperty); set => SetValue(ShowBackProperty, value); }

    public event EventHandler? BackClicked;
    public event EventHandler? FullscreenClicked;

    public FloatingNavBar()
    {
        InitializeComponent();
    }

    private void OnBackTapped(object? sender, TappedEventArgs e) => BackClicked?.Invoke(this, EventArgs.Empty);
    private void OnFullscreenTapped(object? sender, TappedEventArgs e) => FullscreenClicked?.Invoke(this, EventArgs.Empty);
}
