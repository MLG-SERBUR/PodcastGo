using System;
using System.Collections.Generic;
using System.Linq;
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

        private async void PinPodcast_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;
            var podcast = menuFlyoutItem?.Tag as Podcast ?? menuFlyoutItem?.DataContext as Podcast;

            if (podcast != null)
            {
                // Tiles IDs must be alphanumeric
                string safeId = new string((podcast.Id ?? podcast.RssUrl ?? "").Where(c => char.IsLetterOrDigit(c)).ToArray());
                string tileId = $"Podcast_{safeId}";

                if (!Windows.UI.StartScreen.SecondaryTile.Exists(tileId))
                {
                    var secondaryTile = new Windows.UI.StartScreen.SecondaryTile(
                        tileId,
                        podcast.Title,
                        $"podcastId={podcast.Id}",
                        new Uri("ms-appx:///Assets/Square150x150Logo.png"),
                        Windows.UI.StartScreen.TileSize.Default);

                    secondaryTile.VisualElements.ShowNameOnSquare150x150Logo = true;

                    try
                    {
                        // This line attempts to show a system dialog. 
                        // If the Shell (Start Menu) is modified/broken (ExplorerPatcher), this throws 0x80070490.
                        bool isPinned = await secondaryTile.RequestCreateAsync();

                        if (isPinned)
                        {
                            // Only try to update the tile if the pin was actually successful
                            await Services.TileService.UpdatePodcastTileAsync(podcast, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error for debugging
                        System.Diagnostics.Debug.WriteLine($"Pinning failed: {ex.Message}");

                        // Optional: Inform the user via a dialog that pinning isn't supported in their current environment
                        var dialog = new ContentDialog
                        {
                            Title = "Pinning Failed",
                            Content = "Could not pin the tile to Start. This often happens if the Start Menu is modified (e.g., ExplorerPatcher - blame MS for removing live tiles) or if the system shell is unresponsive.",
                            CloseButtonText = "OK"
                        };
                        await dialog.ShowAsync();
                    }
                }
            }
        }

        private async void DeletePodcast_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;
            var podcast = menuFlyoutItem?.Tag as Podcast ?? menuFlyoutItem?.DataContext as Podcast;

            if (podcast != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Podcast",
                    Content = $"Are you sure you want to delete '{podcast.Title}'? This will remove all downloaded episodes.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel"
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
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