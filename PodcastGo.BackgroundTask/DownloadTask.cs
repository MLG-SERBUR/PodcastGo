using System;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Networking.BackgroundTransfer;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace PodcastGo.BackgroundTask
{
    public sealed class DownloadTask : IBackgroundTask
    {
        BackgroundTaskDeferral _deferral;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            try
            {
                await DoDownloadWorkAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Background task error: {ex.Message}");
            }
            finally
            {
                _deferral.Complete();
            }
        }

        private async Task DoDownloadWorkAsync()
        {
            var folder = ApplicationData.Current.RoamingFolder;
            string content = null;
            try
            {
                var file = await folder.GetFileAsync("podcasts.json");
                content = await FileIO.ReadTextAsync(file);
            }
            catch { return; }

            if (string.IsNullOrEmpty(content)) return;

            // Using dynamics to avoid needing the full model class in this project
            dynamic podcasts = JsonConvert.DeserializeObject(content);
            if (podcasts == null) return;

            // First, clean up listened episodes that have been downloaded
            await DeleteListenedDownloadedEpisodesAsync(podcasts, folder);

            // Then, download the next unlistened episode
            await DownloadNextUnlistenedEpisodeAsync(podcasts, folder);
        }

        private async Task DeleteListenedDownloadedEpisodesAsync(dynamic podcasts, StorageFolder folder)
        {
            bool changed = false;
            foreach (var podcast in podcasts)
            {
                foreach (var episode in podcast.Episodes)
                {
                    bool isListened = episode.IsListened ?? false;
                    string localPath = episode.LocalFilePath;

                    if (isListened && !string.IsNullOrEmpty(localPath))
                    {
                        try
                        {
                            var file = await StorageFile.GetFileFromPathAsync(localPath);
                            await file.DeleteAsync();
                            episode.LocalFilePath = null;
                            changed = true;
                            System.Diagnostics.Debug.WriteLine($"Deleted listened episode: {episode.Title}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to delete file {localPath}: {ex.Message}");
                        }
                    }
                }
            }

            if (changed)
            {
                await SavePodcastsAsync(podcasts, folder);
            }
        }

        private async Task DownloadNextUnlistenedEpisodeAsync(dynamic podcasts, StorageFolder folder)
        {
            // Find the first unlistened episode that hasn't been downloaded yet
            foreach (var podcast in podcasts)
            {
                foreach (var episode in podcast.Episodes)
                {
                    bool isListened = episode.IsListened ?? false;
                    string localPath = episode.LocalFilePath;
                    string audioUrl = episode.AudioUrl;

                    // Skip if already listened or already downloaded
                    if (isListened || !string.IsNullOrEmpty(localPath))
                    {
                        continue;
                    }

                    // Found unlistened, not-yet-downloaded episode
                    if (!string.IsNullOrEmpty(audioUrl))
                    {
                        try
                        {
                            string fileName = Guid.NewGuid().ToString() + ".mp3";
                            var destinationFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                            BackgroundDownloader downloader = new BackgroundDownloader();
                            DownloadOperation download = downloader.CreateDownload(new Uri(audioUrl), destinationFile);

                            await download.StartAsync();

                            // Update JSON with new local path
                            episode.LocalFilePath = destinationFile.Path;

                            // Save updated JSON
                            await SavePodcastsAsync(podcasts, folder);

                            // Update Live Tile
                            UpdateLiveTile($"Downloaded: {episode.Title}");
                            System.Diagnostics.Debug.WriteLine($"Downloaded episode: {episode.Title}");

                            // Only download one at a time
                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to download episode {episode.Title}: {ex.Message}");
                            return;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("No unlistened episodes to download");
        }

        private async Task SavePodcastsAsync(dynamic podcasts, StorageFolder folder)
        {
            try
            {
                var newContent = JsonConvert.SerializeObject(podcasts, Formatting.Indented);
                var fileToSave = await folder.CreateFileAsync("podcasts.json", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(fileToSave, newContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save podcasts: {ex.Message}");
            }
        }
        
        private void UpdateLiveTile(string title)
        {
            try
            {
                string tileXmlString = $@"
<tile>
  <visual version='2'>
    <binding template='TileWide310x150Text03' fallback='TileWideText03'>
      <text id='1'>{title}</text>
    </binding>
    <binding template='TileSquare150x150Text04' fallback='TileSquareText04'>
      <text id='1'>{title}</text>
    </binding>
  </visual>
</tile>";

                XmlDocument tileXml = new XmlDocument();
                tileXml.LoadXml(tileXmlString);
                var notification = new TileNotification(tileXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(notification);
            }
            catch { }
        }
    }
}
