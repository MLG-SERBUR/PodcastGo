using System;
using PodcastGo.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PodcastGo
{
    public sealed partial class AddPodcastPage : Page
    {
        public AddPodcastPage()
        {
            this.InitializeComponent();
        }

        private async void AddPodcast_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            StatusTextBlock.Visibility = Visibility.Collapsed;
            var podcast = await PodcastService.FetchPodcastAsync(url);
            if (podcast != null)
            {
                var podcasts = await StorageService.LoadPodcastsAsync();
                if (!podcasts.Exists(p => p.RssUrl == url))
                {
                    podcasts.Add(podcast);
                    await StorageService.SavePodcastsAsync(podcasts);
                }
                Frame.Navigate(typeof(PodcastListPage));
            }
            else
            {
                StatusTextBlock.Text = "Failed to load podcast from this URL.";
                StatusTextBlock.Visibility = Visibility.Visible;
            }
        }
    }
}
