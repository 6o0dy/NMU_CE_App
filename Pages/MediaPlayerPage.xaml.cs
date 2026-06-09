using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class MediaPlayerPage : ContentPage
{
    private string _fileUrl = "";
    private string _fileName = "";
    private bool _isAudio;
    private string _title = "";

    public MediaPlayerPage(string url, string name, bool isAudio, string title)
    {
        InitializeComponent();
        _fileUrl = Uri.UnescapeDataString(url ?? "");
        _fileName = Uri.UnescapeDataString(name ?? "");
        _isAudio = isAudio;
        _title = Uri.UnescapeDataString(title ?? "");
        PageTitle.Text = _title;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(_fileUrl))
            LoadPlayer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SavePosition();
        Player.Handler?.DisconnectHandler();
    }

    private void LoadPlayer()
    {
        var savedPos = GetPlaybackPosition(_fileUrl);
        Player.Source = MediaSource.FromUri(_fileUrl);

        if (savedPos > 5)
        {
            _ = ResumeToPosition(savedPos);
        }
    }

    private async Task ResumeToPosition(double seconds)
    {
        await Task.Delay(500);
        if (Player.Duration.TotalSeconds > 0 && Player.Duration.TotalSeconds > seconds)
        {
            Player.SeekTo(TimeSpan.FromSeconds(seconds));
        }
        else
        {
            Player.MediaOpened += (s, e) =>
            {
                if (Player.Duration.TotalSeconds > seconds)
                    Player.SeekTo(TimeSpan.FromSeconds(seconds));
            };
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
    }

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
    }

    private void SavePosition()
    {
        try
        {
            var pos = Player.Position.TotalSeconds;
            if (pos > 1)
                SavePlaybackPosition(_fileUrl, pos);
        }
        catch { }
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        SavePosition();
        Player.Stop();
        NavHelper.Back(this);
    }

    private static string GetPosKey(string url)
    {
        return $"vidpos_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16]}";
    }

    private static void SavePlaybackPosition(string url, double seconds)
    {
        Preferences.Set(GetPosKey(url), seconds);
    }

    private static double GetPlaybackPosition(string url)
    {
        return Preferences.Get(GetPosKey(url), 0.0);
    }
}
