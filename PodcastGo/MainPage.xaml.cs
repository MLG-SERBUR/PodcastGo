using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Core;
using Windows.UI.Xaml;
using PodcastGo.Models;
using PodcastGo.Services;

namespace PodcastGo
{
    public sealed partial class MainPage : Page
    {
        public static MainPage Current { get; private set; }

        private DispatcherTimer _saveTimer;
        private bool _isSaving;

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
            ContentFrame.Navigated += ContentFrame_Navigated;

            // Set up an actual periodic timer to execute the saves every 15 seconds
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _saveTimer.Tick += (s, e) => SaveEpisodePosition();
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateBackButtonVisibility();
        }

        private void UpdateBackButtonVisibility()
        {
            var visibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;
            if (ContentFrame.CanGoBack)
            {
                visibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;
            }
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = visibility;
        }

        private void MainPage_BackRequested(object sender, Windows.UI.Core.BackRequestedEventArgs e)
        {
            if (e.Handled) return;

            if (ContentFrame.Content is EpisodeListPage episodeListPage && episodeListPage.IsShowingDetails)
            {
                episodeListPage.ShowMasterList();
                e.Handled = true;
            }
            else if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
                e.Handled = true;
            }
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

            // Resume logic
            if (GlobalPlayer.Source is Windows.Media.Playback.MediaPlaybackItem oldItem && 
                oldItem.Source is MediaSource oldSource && oldSource.Uri == uri)
            {
                if (GlobalPlayer.MediaPlayer.PlaybackSession.PlaybackState != Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    GlobalPlayer.MediaPlayer.Play();
                }
                return;
            }

            NowPlayingTitle.Text = episode.Title;
            NowPlayingBar.Visibility = Windows.UI.Xaml.Visibility.Visible;

            var mediaSource = MediaSource.CreateFromUri(uri);

            mediaSource.OpenOperationCompleted += (sender, args) =>
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        if (episode.Position.TotalSeconds > 3)
                        {
                            GlobalPlayer.MediaPlayer.PlaybackSession.Position = episode.Position.Subtract(TimeSpan.FromSeconds(2));
                        }
                        else if (episode.Position.TotalSeconds > 0)
                        {
                            GlobalPlayer.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                        }
                    }
                    catch { }
                });
            };

            var playbackItem = new Windows.Media.Playback.MediaPlaybackItem(mediaSource);
            var props = playbackItem.GetDisplayProperties();
            props.Type = Windows.Media.MediaPlaybackType.Music;
            props.MusicProperties.Title = episode.Title;
            props.MusicProperties.Artist = podcast.Title;
            props.MusicProperties.AlbumArtist = podcast.Title;
            playbackItem.ApplyDisplayProperties(props);

            GlobalPlayer.Source = playbackItem;
            GlobalPlayer.MediaPlayer.Play();
            episode.LastPlayedTime = DateTimeOffset.Now;

            GlobalPlayer.MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            GlobalPlayer.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            _saveTimer.Start();

            // Update both main live tile and specific secondary tile
            _ = Services.TileService.UpdatePodcastTileAsync(podcast, episode);
        }

        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            SaveEpisodePosition();

            // Manage the periodic save timer based on whether it is actively playing or paused
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    _saveTimer.Start();
                }
                else
                {
                    _saveTimer.Stop();
                }
            });
        }

        private void SaveEpisodePosition()
        {
            // Must dispatch to UI thread since this can be called from MediaPlayer events
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                // Simple thread guard to prevent concurrent file locking if timer and pause events trigger near simultaneously
                if (_isSaving) return;

                if (_currentEpisode != null && GlobalPlayer.MediaPlayer.PlaybackSession.NaturalDuration > TimeSpan.Zero)
                {
                    _isSaving = true;
                    try
                    {
                        _currentEpisode.Position = GlobalPlayer.MediaPlayer.PlaybackSession.Position;

                        // Save to disk
                        var podcasts = await StorageService.LoadPodcastsAsync();
                        var podcast = podcasts.FirstOrDefault(p => p.Id == _currentPodcast?.Id);

                        if (podcast != null)
                        {
                            // Map the modified position to the episode inside the newly deserialized data list
                            var episodeToUpdate = podcast.Episodes?.FirstOrDefault(e => e.AudioUrl == _currentEpisode.AudioUrl || e.Title == _currentEpisode.Title);

                            if (episodeToUpdate != null)
                            {
                                episodeToUpdate.Position = _currentEpisode.Position;
                                episodeToUpdate.LastPlayedTime = _currentEpisode.LastPlayedTime;
                            }

                            await StorageService.SavePodcastsAsync(podcasts);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save episode position: {ex.Message}");
                    }
                    finally
                    {
                        _isSaving = false;
                    }
                }
            });
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

            // Only navigate to PodcastListPage if the frame is completely empty
            if (ContentFrame.Content == null)
            {
                NavListView.SelectedIndex = 0;
                ContentFrame.Navigate(typeof(PodcastListPage));
            }

            // Catch parameters if launched from Start Menu secondary tile
            if (e.Parameter is string args && !string.IsNullOrEmpty(args))
            {
                HandleLaunchArguments(args);
            }
        }

        public async void HandleLaunchArguments(string args)
        {
            if (args.StartsWith("podcastId="))
            {
                string podcastId = args.Substring(10);
                var podcasts = await StorageService.LoadPodcastsAsync();
                var podcast = podcasts.FirstOrDefault(p => p.Id == podcastId);

                if (podcast != null)
                {
                    // Find the oldest unlistened episode
                    var oldestUnlistened = podcast.Episodes
                        .Where(ep => !ep.IsListened)
                        .OrderBy(ep => ep.PublishDate)
                        .FirstOrDefault();

                    if (oldestUnlistened != null)
                    {
                        PlayEpisode(podcast, oldestUnlistened);
                    }
                    else
                    {
                        // Fallback: just play the newest episode if user is entirely caught up
                        var newest = podcast.Episodes.OrderByDescending(ep => ep.PublishDate).FirstOrDefault();
                        if (newest != null)
                        {
                            PlayEpisode(podcast, newest);
                        }
                    }
                }
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SaveEpisodePosition();
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
                    case "debug":
                        ContentFrame.Navigate(typeof(DebugPage));
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