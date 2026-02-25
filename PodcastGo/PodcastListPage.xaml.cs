using System.Collections.Generic;
using PodcastGo.Models;
using PodcastGo.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PodcastGo
{
    public sealed partial class PodcastListPage : Page
    {
        public PodcastListPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
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
    }
}
