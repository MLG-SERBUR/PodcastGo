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
        public Podcast CurrentPodcast => _podcast;
        public ObservableCollection<Episode> Episodes { get; } = new ObservableCollection<Episode>();
        private Episode _selectedEpisode;
        private bool _showAll = false;

        private bool IsNarrow => RootGrid.ActualWidth < 800;

        public bool IsShowingDetails => IsNarrow
            && DetailGrid.Visibility == Visibility.Visible
            && MasterPane.Visibility == Visibility.Collapsed;

        public EpisodeListPage()
        {
            this.InitializeComponent();
            EpisodeListView.ItemsSource = Episodes;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _podcast = e.Parameter as Podcast;
            if (_podcast == null) return;

            PodcastTitleTextBlock.Text = _podcast.Title;
            RefreshEpisodeList();

            // Auto-select playing episode details (without scrolling the list)
            if (MainPage.Current.PlayingPodcast == _podcast && MainPage.Current.PlayingEpisode != null)
            {
                PopulateDetail(MainPage.Current.PlayingEpisode);
                
                RoutedEventHandler loadedHandler = null;
                loadedHandler = (s, args) =>
                {
                    this.Loaded -= loadedHandler;
                    ShowDetailPane();
                };
                this.Loaded += loadedHandler;
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
                                     (!string.IsNullOrEmpty(ep.Notes) && ep.Notes.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                     (!string.IsNullOrEmpty(ep.Title) && ep.Title.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matchesFilter && matchesSearch)
                {
                    Episodes.Add(ep);
                }
            }
        }

        /// <summary>
        /// Fill detail pane fields for an episode. Does NOT scroll or switch panes.
        /// </summary>
        private void PopulateDetail(Episode episode)
        {
            _selectedEpisode = episode;
            DetailTitleTextBlock.Text = episode.Title;
            NotesTextBox.Text = episode.Notes ?? "";
            UpdateMarkListenedButtonText();
            EpisodeListView.SelectedItem = episode;
        }

        /// <summary>
        /// Show the detail pane (narrow: hide master; wide: just make detail visible).
        /// </summary>
        private void ShowDetailPane()
        {
            if (IsNarrow)
            {
                // Collapse master — does NOT reflow ListView to 0 width, preserving scroll
                MasterPane.Visibility = Visibility.Collapsed;
                // Let detail fill the entire width
                Grid.SetColumn(DetailGrid, 0);
                Grid.SetColumnSpan(DetailGrid, 2);
            }
            DetailGrid.Visibility = Visibility.Visible;
        }

        private void EpisodeListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var episode = e.ClickedItem as Episode;
            if (episode == null) return;

            PopulateDetail(episode);
            ShowDetailPane();
            MainPage.Current.PlayEpisode(_podcast, episode);
        }

        /// <summary>
        /// Called from MainPage when now-playing bar tapped for this podcast.
        /// </summary>
        public void ShowEpisodeDetails(Episode episode)
        {
            PopulateDetail(episode);
            EpisodeListView.ScrollIntoView(episode);
            ShowDetailPane();
        }

        public void ShowMasterList()
        {
            if (!IsNarrow) return;

            // Restore detail to its normal column
            Grid.SetColumn(DetailGrid, 1);
            Grid.SetColumnSpan(DetailGrid, 1);
            DetailGrid.Visibility = Visibility.Collapsed;

            // Restore master — scroll position preserved since it was never reflowed
            MasterPane.Visibility = Visibility.Visible;
        }

        private void UpdateMarkListenedButtonText()
        {
            MarkListenedButton.Content = _selectedEpisode?.IsListened == true
                ? "Unmark as listened"
                : "Mark as listened";
        }

        private async void MarkListened_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEpisode != null)
            {
                _selectedEpisode.IsListened = !_selectedEpisode.IsListened;
                UpdateMarkListenedButtonText();
                await SaveChangesAsync();
                RefreshEpisodeList(SearchBox.Text);
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
                    RefreshEpisodeList(SearchBox.Text);
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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshEpisodeList(SearchBox.Text);
        }

        private void ShowAll_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                _showAll = cb.IsChecked ?? false;
                RefreshEpisodeList(SearchBox.Text);
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
