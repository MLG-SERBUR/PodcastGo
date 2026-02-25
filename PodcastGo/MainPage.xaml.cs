using System;
using System.Linq;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PodcastGo
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            MainNavView.SelectedItem = MainNavView.MenuItems[0];
            ContentFrame.Navigate(typeof(PodcastListPage));
        }

        private void MainNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                string tag = args.SelectedItemContainer.Tag.ToString();
                switch (tag)
                {
                    case "podcasts":
                        ContentFrame.Navigate(typeof(PodcastListPage));
                        break;
                    case "add":
                        ContentFrame.Navigate(typeof(AddPodcastPage));
                        break;
                }
            }
        }
    }
}
