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
        private const string LogPrefix = "[BACKGROUND-TASK]";

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            await LogAsync($"{LogPrefix} Started at {DateTime.Now:g}");

            try
            {
                await DoDownloadWorkAsync();
                await LogAsync($"{LogPrefix} Completed successfully");
            }
            catch (Exception ex)
            {
                await LogAsync($"{LogPrefix} Error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _deferral.Complete();
                await LogAsync($"{LogPrefix} Deferral completed");
            }
        }

        private async Task LogAsync(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            try
            {
                var folder = ApplicationData.Current.RoamingFolder;
                var file = await folder.CreateFileAsync("background_task_log.txt", CreationCollisionOption.OpenIfExists);
                string existing = "";
                try
                {
                    existing = await FileIO.ReadTextAsync(file);
                }
                catch { }
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var newContent = $"[{timestamp}] {message}\n" + existing;
                await FileIO.WriteTextAsync(file, newContent);
            }
            catch { }
        }

        private async Task DoDownloadWorkAsync()
        {
            var folder = ApplicationData.Current.RoamingFolder;
            string content = null;
            try
            {
                var file = await folder.GetFileAsync("podcasts.json");
                content = await FileIO.ReadTextAsync(file);
                await LogAsync($"{LogPrefix} Loaded podcasts.json");
            }
            catch (Exception ex)
            {
                await LogAsync($"{LogPrefix} Could not load podcasts.json: {ex.Message}");
                return;
            }

            if (string.IsNullOrEmpty(content))
            {
                await LogAsync($"{LogPrefix} podcasts.json is empty");
                return;
            }

            dynamic podcasts = JsonConvert.DeserializeObject(content);
            if (podcasts == null)
            {
                await LogAsync($"{LogPrefix} Failed to deserialize podcasts");
                return;
            }

            await LogAsync($"{LogPrefix} Processing {podcasts.Count} podcasts");

            // 1. Clean up listened episodes, erroneous extra downloads, and orphaned mp3s
            await CleanUpEpisodesAsync(podcasts, folder);

            // 2. Manage existing queued downloads (cancel erroneous ones, attach to valid ones, start new)
            await ManageDownloadsAsync(podcasts, folder);
        }

        private async Task CleanUpEpisodesAsync(dynamic podcasts, StorageFolder folder)
        {
            bool changed = false;
            var validFileNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Protect actively downloading/queued files so we don't treat them as orphans
            var activeDownloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            foreach (var download in activeDownloads)
            {
                if (download.ResultFile != null)
                {
                    validFileNames.Add(download.ResultFile.Name);
                }
            }

            foreach (var podcast in podcasts)
            {
                bool foundOldestUnlistened = false;
                int count = podcast.Episodes.Count;

                // Episodes are stored newest first. Iterate backwards to start with the oldest.
                for (int i = count - 1; i >= 0; i--)
                {
                    var episode = podcast.Episodes[i];
                    bool isListened = (bool?)episode.IsListened ?? false;
                    string localPath = (string)episode.LocalFilePath;
                    string title = (string)episode.Title ?? "Unknown Title";

                    if (isListened)
                    {
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            await DeleteFileAsync(localPath, $"Listened episode - '{title}'");
                            episode.LocalFilePath = null;
                            changed = true;
                        }
                    }
                    else
                    {
                        // Unlistened episodes
                        if (!foundOldestUnlistened)
                        {
                            // This is the absolute oldest unlistened episode. It's allowed to be downloaded.
                            foundOldestUnlistened = true;
                            if (!string.IsNullOrEmpty(localPath))
                            {
                                validFileNames.Add(System.IO.Path.GetFileName(localPath));
                            }
                        }
                        else
                        {
                            // This is a newer unlistened episode. Due to the previous bug, it shouldn't be downloaded!
                            if (!string.IsNullOrEmpty(localPath))
                            {
                                await DeleteFileAsync(localPath, $"Erroneously downloaded newer episode - '{title}'");
                                episode.LocalFilePath = null;
                                changed = true;
                            }
                        }
                    }
                }
            }

            if (changed)
            {
                await SavePodcastsAsync(podcasts, folder);
                await LogAsync($"{LogPrefix} Saved podcasts.json after JSON cleanup");
            }
            else
            {
                await LogAsync($"{LogPrefix} No JSON-tracked episodes needed cleanup");
            }

            // Clean up orphaned .mp3 files not attached to our allowed list or active queue
            await CleanUpOrphanedFilesAsync(folder, validFileNames);
        }

        private async Task CleanUpOrphanedFilesAsync(StorageFolder folder, System.Collections.Generic.HashSet<string> validFileNames)
        {
            try
            {
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    if (file.FileType.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!validFileNames.Contains(file.Name))
                        {
                            try
                            {
                                await file.DeleteAsync();
                                await LogAsync($"{LogPrefix} Cleaned up orphaned/ghost file: {file.Name}");
                            }
                            catch (Exception ex)
                            {
                                await LogAsync($"{LogPrefix} Failed to delete orphaned file {file.Name}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogAsync($"{LogPrefix} Error cleaning up orphaned files: {ex.Message}");
            }
        }

        private async Task DeleteFileAsync(string localPath, string reason)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(localPath);
                await file.DeleteAsync();
                await LogAsync($"{LogPrefix} Cleaned up [{reason}] at {localPath}");
            }
            catch (FileNotFoundException)
            {
                await LogAsync($"{LogPrefix} Cleaned up[{reason}] (file already missing) at {localPath}");
            }
            catch (Exception ex)
            {
                await LogAsync($"{LogPrefix} Failed to delete file for [{reason}] at {localPath}: {ex.Message}");
            }
        }

        private async Task ManageDownloadsAsync(dynamic podcasts, StorageFolder folder)
        {
            // Build a list of allowed audio URLs (only the oldest unlistened episode per podcast)
            var allowedUrls = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var podcast in podcasts)
            {
                int count = podcast.Episodes.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    var episode = podcast.Episodes[i];
                    bool isListened = (bool?)episode.IsListened ?? false;
                    if (!isListened)
                    {
                        string audioUrl = (string)episode.AudioUrl;
                        if (!string.IsNullOrEmpty(audioUrl)) allowedUrls.Add(audioUrl);
                        break;
                    }
                }
            }

            var activeDownloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            bool isActivelyDownloading = false;

            foreach (var download in activeDownloads)
            {
                string url = download.RequestedUri.ToString();

                // If the previous bug queued up newer episodes or duplicates, explicitly terminate them
                if (!allowedUrls.Contains(url))
                {
                    await LogAsync($"{LogPrefix} Cancelling erroneously queued background download: {url}");
                    try
                    {
                        var operation = download.AttachAsync();
                        operation.Cancel();
                    }
                    catch { }
                    continue;
                }

                isActivelyDownloading = true;
                try
                {
                    await LogAsync($"{LogPrefix} Attaching to existing permitted download: {url}");
                    await download.AttachAsync();

                    // If we reach here, the download has completed. Match it back to JSON.
                    await HandleCompletedDownloadAsync(download, podcasts, folder);
                }
                catch (Exception ex)
                {
                    // The download failed (404, cancelled, etc.) 
                    await LogAsync($"{LogPrefix} Existing download failed or was cancelled: {ex.Message}");
                }
            }

            if (isActivelyDownloading)
            {
                await LogAsync($"{LogPrefix} Handled existing downloads. Exiting to avoid queueing multiple.");
                return;
            }

            // Start new download for the oldest unlistened if no active downloads
            await StartNewDownloadAsync(podcasts, folder);
        }

        private async Task HandleCompletedDownloadAsync(DownloadOperation download, dynamic podcasts, StorageFolder folder)
        {
            string audioUrl = download.RequestedUri.ToString();
            bool matched = false;

            foreach (var podcast in podcasts)
            {
                foreach (var episode in podcast.Episodes)
                {
                    if ((string)episode.AudioUrl == audioUrl)
                    {
                        episode.LocalFilePath = download.ResultFile.Path;
                        matched = true;
                        string title = (string)episode.Title ?? "Unknown Title";
                        await LogAsync($"{LogPrefix} Recovered completed download: '{title}'");
                        UpdateLiveTile($"Downloaded: {title}");
                        break;
                    }
                }
                if (matched) break;
            }

            if (matched)
            {
                await SavePodcastsAsync(podcasts, folder);
            }
            else
            {
                try
                {
                    await download.ResultFile.DeleteAsync();
                    await LogAsync($"{LogPrefix} Deleted unmatched completed download: {download.ResultFile.Name}");
                }
                catch { }
            }
        }

        private async Task StartNewDownloadAsync(dynamic podcasts, StorageFolder folder)
        {
            foreach (var podcast in podcasts)
            {
                int count = podcast.Episodes.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    var episode = podcast.Episodes[i];
                    bool isListened = (bool?)episode.IsListened ?? false;
                    string localPath = (string)episode.LocalFilePath;
                    string audioUrl = (string)episode.AudioUrl;
                    string title = (string)episode.Title ?? "Unknown Title";

                    if (isListened || !string.IsNullOrEmpty(localPath))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(audioUrl))
                    {
                        try
                        {
                            await LogAsync($"{LogPrefix} Starting download of oldest unlistened episode: '{title}'");
                            string fileName = Guid.NewGuid().ToString() + ".mp3";
                            var destinationFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                            BackgroundDownloader downloader = new BackgroundDownloader();
                            DownloadOperation download = downloader.CreateDownload(new Uri(audioUrl), destinationFile);

                            await download.StartAsync();

                            // Update JSON only after successfully completing
                            episode.LocalFilePath = destinationFile.Path;
                            await SavePodcastsAsync(podcasts, folder);

                            UpdateLiveTile($"Downloaded: {title}");
                            await LogAsync($"{LogPrefix} Successfully downloaded episode: '{title}'");

                            return; // Enforce only ONE unlistened episode downloaded globally at a time
                        }
                        catch (Exception ex)
                        {
                            await LogAsync($"{LogPrefix} Failed to download episode '{title}': {ex.Message}");
                            return;
                        }
                    }
                }
            }

            await LogAsync($"{LogPrefix} No unlistened episodes to download");
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