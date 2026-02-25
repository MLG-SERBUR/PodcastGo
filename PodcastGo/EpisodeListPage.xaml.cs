using System;
using System.Collections.ObjectModel;
using System.Linq;
using PodcastGo.Models;
using PodcastGo.Services;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PodcastGo
{
    public sealed partial class EpisodeListPage : Page
    {
        private Podcast _podcast;
        public ObservableCollection<Episode> Episodes { get; } = new ObservableCollection<Episode>();
        private Episode _selectedEpisode;
        private bool _showAll = false;

        public EpisodeListPage()
        {
            this.InitializeComponent();
            EpisodeListView.ItemsSource = Episodes;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _podcast = e.Parameter as Podcast;
            if (_podcast != null)
            {
                PodcastTitleTextBlock.Text = _podcast.Title;
                RefreshEpisodeList();
            }
        }

        private void RefreshEpisodeList(string searchQuery = "")
        {
            Episodes.Clear();
            if (_podcast == null) return;

            var allSorted = _podcast.Episodes.OrderByDescending(ep => ep.PublishDate).ToList();
            Episode nextUnlistened = allSorted.Where(ep => !ep.IsListened).OrderBy(ep => ep.PublishDate).FirstOrDefault();

            foreach (var ep in allSorted)
            {
                bool matchesFilter = _showAll || ep.IsListened || ep == nextUnlistened;
                bool matchesSearch = string.IsNullOrWhiteSpace(searchQuery) ||
                                     (!string.IsNullOrEmpty(ep.Notes) && ep.Notes.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matchesFilter && matchesSearch)
                {
                    Episodes.Add(ep);
                }
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
                    TileService.UpdateLiveTile(_selectedEpisode.Title);
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
                await SaveChangesAsync();
                RefreshEpisodeList(SearchNotesBox.Text);
            }
        }

        private async void MarkPreviousListened_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEpisode != null && _podcast != null)
            {
                var allEpisodes = _podcast.Episodes.OrderByDescending(ep => ep.PublishDate).ToList();
                int index = allEpisodes.IndexOf(_selectedEpisode);
                if (index >= 0)
                {
                    for (int i = index + 1; i < allEpisodes.Count; i++)
                    {
                        allEpisodes[i].IsListened = true;
                    }
                    await SaveChangesAsync();
                    RefreshEpisodeList(SearchNotesBox.Text);
                }
            }
        }

        private async void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedEpisode != null && _selectedEpisode.Notes != NotesTextBox.Text)
            {
                _selectedEpisode.Notes = NotesTextBox.Text;
                await SaveChangesAsync();
            }
        }

        private void SearchNotesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshEpisodeList(SearchNotesBox.Text);
        }

        private void ShowAll_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                _showAll = cb.IsChecked ?? false;
                RefreshEpisodeList(SearchNotesBox.Text);
            }
        }

        private async System.Threading.Tasks.Task SaveChangesAsync()
        {
            var podcasts = await StorageService.LoadPodcastsAsync();
            var existing = podcasts.FirstOrDefault(p => p.RssUrl == _podcast.RssUrl);
            if (existing != null)
            {
                int idx = podcasts.IndexOf(existing);
                podcasts[idx] = _podcast;
                await StorageService.SavePodcastsAsync(podcasts);
            }
        }
    }
}
