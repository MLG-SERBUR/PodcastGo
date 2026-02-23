using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PodcastGo.Models;
using PodcastGo.Services;
using Windows.Media.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace PodcastGo
{
    public sealed partial class MainPage : Page
    {
        private List<Podcast> _podcasts = new List<Podcast>();
        public ObservableCollection<Episode> Episodes { get; } = new ObservableCollection<Episode>();
        private Episode _selectedEpisode;

        public MainPage()
        {
            this.InitializeComponent();
            
            // Manual field mapping because the XAML compiler in this environment is not generating partial fields
            EpisodeListView = (ListView)this.FindName("EpisodeListView");
            DetailTitleTextBlock = (TextBlock)this.FindName("DetailTitleTextBlock");
            NotesTextBox = (TextBox)this.FindName("NotesTextBox");
            PlayerElement = (MediaPlayerElement)this.FindName("PlayerElement");
            SearchNotesBox = (TextBox)this.FindName("SearchNotesBox");

            EpisodeListView.ItemsSource = Episodes;
        }

        private ListView EpisodeListView;
        private TextBlock DetailTitleTextBlock;
        private TextBox NotesTextBox;
        private MediaPlayerElement PlayerElement;
        private TextBox SearchNotesBox;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _podcasts = await StorageService.LoadPodcastsAsync();
            RefreshEpisodeList();
        }

        private void RefreshEpisodeList(string searchQuery = "")
        {
            Episodes.Clear();
            var allEpisodes = _podcasts.SelectMany(p => p.Episodes).OrderByDescending(ep => ep.PublishDate);
            
            foreach (var ep in allEpisodes)
            {
                if (string.IsNullOrWhiteSpace(searchQuery) ||
                    (!string.IsNullOrEmpty(ep.Notes) && ep.Notes.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    Episodes.Add(ep);
                }
            }
        }

        private async void SyncFeed_Click(object sender, RoutedEventArgs e)
        {
            // Using a reliable sample feed for testing
            string sampleFeed = "https://feeds.npr.org/500005/podcast.xml"; // NPR News Now
            var podcast = await PodcastService.FetchPodcastAsync(sampleFeed);
            if (podcast != null)
            {
                var existing = _podcasts.FirstOrDefault(p => p.RssUrl == sampleFeed);
                if (existing == null)
                {
                    _podcasts.Add(podcast);
                }
                else
                {
                    foreach (var ep in podcast.Episodes)
                    {
                        if (!existing.Episodes.Any(x => x.Id == ep.Id))
                        {
                            existing.Episodes.Add(ep);
                        }
                    }
                }
                await StorageService.SavePodcastsAsync(_podcasts);
                RefreshEpisodeList(SearchNotesBox.Text);
            }
        }

        private void EpisodeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEpisode = EpisodeListView.SelectedItem as Episode;
            if (_selectedEpisode != null)
            {
                DetailTitleTextBlock.Text = _selectedEpisode.Title;
                NotesTextBox.Text = _selectedEpisode.Notes ?? "";

                if (!string.IsNullOrEmpty(_selectedEpisode.LocalFilePath))
                {
                    PlayerElement.Source = MediaSource.CreateFromUri(new Uri(_selectedEpisode.LocalFilePath));
                }
                else if (!string.IsNullOrEmpty(_selectedEpisode.AudioUrl))
                {
                    PlayerElement.Source = MediaSource.CreateFromUri(new Uri(_selectedEpisode.AudioUrl));
                }

                if (PlayerElement.MediaPlayer != null)
                {
                    PlayerElement.MediaPlayer.Play();
                    TileService.UpdateLiveTile(_selectedEpisode.Title); // Update Live Tile
                }
            }
            else
            {
                DetailTitleTextBlock.Text = "Select an episode";
                NotesTextBox.Text = "";
                PlayerElement.Source = null;
            }
        }

        private async void MarkListened_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEpisode != null)
            {
                _selectedEpisode.IsListened = !_selectedEpisode.IsListened;
                await StorageService.SavePodcastsAsync(_podcasts);
            }
        }

        private async void MarkPreviousListened_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEpisode != null)
            {
                var allEpisodes = _podcasts.SelectMany(p => p.Episodes).OrderByDescending(ep => ep.PublishDate).ToList();
                int index = allEpisodes.IndexOf(_selectedEpisode);
                if (index >= 0)
                {
                    // Everything after the selected episode is older (since ordered desc)
                    for (int i = index + 1; i < allEpisodes.Count; i++)
                    {
                        allEpisodes[i].IsListened = true;
                    }
                    await StorageService.SavePodcastsAsync(_podcasts);
                }
            }
        }

        private async void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedEpisode != null && _selectedEpisode.Notes != NotesTextBox.Text)
            {
                _selectedEpisode.Notes = NotesTextBox.Text;
                await StorageService.SavePodcastsAsync(_podcasts);
            }
        }

        private void SearchNotesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshEpisodeList(SearchNotesBox.Text);
        }
    }
}
