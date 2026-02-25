using System.Collections.Generic;
using PodcastGo.Models;
using PodcastGo.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace PodcastGo
{
    public sealed partial class PodcastListPage : Page
    {
        public PodcastListPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ReloadPodcasts();
        }

        private async void ReloadPodcasts()
        {
            var podcasts = await StorageService.LoadPodcastsAsync();
            PodcastGridView.ItemsSource = podcasts;
        }

        private void PodcastGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var podcast = e.ClickedItem as Podcast;
            if (podcast != null)
            {
                Frame.Navigate(typeof(EpisodeListPage), podcast);
            }
        }

        private void PodcastItem_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            var panel = sender as StackPanel;
            if (panel != null)
            {
                FlyoutBase.ShowAttachedFlyout(panel);
            }
        }

        private async void DeletePodcast_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;
            if (menuFlyoutItem != null)
            {
                // The Tag is now bound to the whole Podcast object
                var podcast = menuFlyoutItem.Tag as Podcast;
                
                // Fallback to DataContext if Tag is somehow null
                if (podcast == null)
                {
                    podcast = menuFlyoutItem.DataContext as Podcast;
                }

                if (podcast != null)
                {
                    // Use Id if available, fallback to RssUrl
                    string identifier = podcast.Id ?? podcast.RssUrl;
                    if (!string.IsNullOrEmpty(identifier))
                    {
                        await StorageService.DeletePodcastAsync(identifier);
                        ReloadPodcasts();
                    }
                }
            }
        }
    }
}
