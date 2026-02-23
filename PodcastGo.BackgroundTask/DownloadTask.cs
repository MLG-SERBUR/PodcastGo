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
            var folder = ApplicationData.Current.LocalFolder;
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

            foreach (var podcast in podcasts)
            {
                foreach (var episode in podcast.Episodes)
                {
                    bool isListened = episode.IsListened ?? false;
                    string localPath = episode.LocalFilePath;
                    
                    if (!isListened && string.IsNullOrEmpty(localPath))
                    {
                        string audioUrl = episode.AudioUrl;
                        if (!string.IsNullOrEmpty(audioUrl))
                        {
                            string fileName = Guid.NewGuid().ToString() + ".mp3";
                            var destinationFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                            
                            BackgroundDownloader downloader = new BackgroundDownloader();
                            DownloadOperation download = downloader.CreateDownload(new Uri(audioUrl), destinationFile);
                            
                            await download.StartAsync();

                            // Update JSON with new local path
                            episode.LocalFilePath = destinationFile.Path;
                            
                            // Save updated JSON
                            var newContent = JsonConvert.SerializeObject(podcasts, Formatting.Indented);
                            var fileToSave = await folder.CreateFileAsync("podcasts.json", CreationCollisionOption.ReplaceExisting);
                            await FileIO.WriteTextAsync(fileToSave, newContent);

                            // Update Live Tile
                            UpdateLiveTile(episode.Title?.ToString() ?? "New Episode Downloaded");
                            
                            // Only download one at a time
                            return;
                        }
                    }
                }
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
