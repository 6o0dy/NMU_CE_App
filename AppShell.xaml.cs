using NMU_CE_App.Pages;

namespace NMU_CE_App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("subjectfiles", typeof(SubjectFilesPage));
        Routing.RegisterRoute("viewer", typeof(PdfViewerPage));
        Routing.RegisterRoute("recordedlectures", typeof(RecordedLecturesPage));
        Routing.RegisterRoute("recordedfiles", typeof(RecordedLecturesFilesPage));
        Routing.RegisterRoute("mediaplayer", typeof(MediaPlayerPage));
        Routing.RegisterRoute("youtubechannels", typeof(YouTubeChannelsPage));
        Routing.RegisterRoute("youtubevideos", typeof(YouTubeChannelVideosPage));
        Routing.RegisterRoute("youtubeplayer", typeof(YouTubePlayerPage));
    }
}
