namespace NMU_CE_App.Pages;

public partial class DebugErrorPage : ContentPage
{
    public DebugErrorPage(string errorMessage, string? stackTrace = null)
    {
        InitializeComponent();
        ErrorDetailsLabel.Text = errorMessage;
        StackTraceLabel.Text = stackTrace;
    }

    private async void OnGoBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
