using System;
using System.Linq;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Core;
using Windows.UI.Xaml;
using PodcastGo.Models;

namespace PodcastGo
{
    public sealed partial class MainPage : Page
    {
        public static MainPage Current { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;
        }

        private Podcast _currentPodcast;
        private Episode _currentEpisode;

        public Podcast PlayingPodcast => _currentPodcast;
        public Episode PlayingEpisode => _currentEpisode;

        public void PlayEpisode(Podcast podcast, Episode episode)
        {
            _currentPodcast = podcast;
            _currentEpisode = episode;

            string url = !string.IsNullOrEmpty(episode.LocalFilePath) ? episode.LocalFilePath : episode.AudioUrl;
            if (string.IsNullOrEmpty(url)) return;

            Uri uri = new Uri(url);

            // Resume logic: check if same URI is already loaded
            if (GlobalPlayer.Source is MediaSource oldSource && oldSource.Uri == uri)
            {
                if (GlobalPlayer.MediaPlayer.PlaybackSession.PlaybackState != Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    GlobalPlayer.MediaPlayer.Play();
                }
                return;
            }

            NowPlayingTitle.Text = episode.Title;
            NowPlayingBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
            GlobalPlayer.Source = MediaSource.CreateFromUri(uri);
            GlobalPlayer.MediaPlayer.Play();
            Services.TileService.UpdateLiveTile(episode.Title);
        }

        private void NowPlayingBar_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (_currentPodcast != null)
            {
                // Check if we are already on this episode list page
                if (ContentFrame.Content is EpisodeListPage page && page.CurrentPodcast == _currentPodcast)
                {
                    // If we are already here, ensure the detail view is shown for the playing episode
                    page.ShowEpisodeDetails(_currentEpisode);
                }
                else
                {
                    ContentFrame.Navigate(typeof(EpisodeListPage), _currentPodcast);
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            NavListView.SelectedIndex = 0;
            ContentFrame.Navigate(typeof(PodcastListPage));
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
        }

        private void NavListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavListView.SelectedItem is ListViewItem item)
            {
                string tag = item.Tag?.ToString();
                switch (tag)
                {
                    case "podcasts":
                        ContentFrame.Navigate(typeof(PodcastListPage));
                        break;
                    case "add":
                        ContentFrame.Navigate(typeof(AddPodcastPage));
                        break;
                }
                
                // Close pane if in Overlay mode (Narrow view)
                if (RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
                {
                    RootSplitView.IsPaneOpen = false;
                }
            }
        }
    }
}
