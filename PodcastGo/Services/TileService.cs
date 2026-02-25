using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using PodcastGo.Models;

namespace PodcastGo.Services
{
    public static class TileService
    {
        public static void UpdateLiveTile(string title, string subtitle = "")
        {
            try
            {
                string tileXmlString = $@"
<tile>
  <visual version='2'>
    <binding template='TileWide310x150Text03' fallback='TileWideText03'>
      <text id='1'>{title}</text>
      <text id='2'>{subtitle}</text>
    </binding>
    <binding template='TileSquare150x150Text04' fallback='TileSquareText04'>
      <text id='1'>{title}</text>
      <text id='2'>{subtitle}</text>
    </binding>
  </visual>
</tile>";

                XmlDocument tileXml = new XmlDocument();
                tileXml.LoadXml(tileXmlString);

                TileNotification notification = new TileNotification(tileXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(notification);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update live tile: {ex.Message}");
            }
        }

        public static void UpdateSecondaryLiveTile(string tileId, string title, string subtitle)
        {
            try
            {
                string tileXmlString = $@"
<tile>
  <visual version='2'>
    <binding template='TileWide310x150Text03' fallback='TileWideText03'>
      <text id='1'>{title}</text>
      <text id='2'>{subtitle}</text>
    </binding>
    <binding template='TileSquare150x150Text04' fallback='TileSquareText04'>
      <text id='1'>{title}</text>
      <text id='2'>{subtitle}</text>
    </binding>
  </visual>
</tile>";

                XmlDocument tileXml = new XmlDocument();
                tileXml.LoadXml(tileXmlString);

                TileNotification notification = new TileNotification(tileXml);
                TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileId).Update(notification);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update secondary live tile: {ex.Message}");
            }
        }

        public static async Task UpdatePodcastTileAsync(Podcast podcast, Episode playingEpisode = null)
        {
            if (podcast == null) return;
            string safeId = new string((podcast.Id ?? podcast.RssUrl ?? "").Where(c => char.IsLetterOrDigit(c)).ToArray());
            string tileId = $"Podcast_{safeId}";

            bool isSecondaryPinned = Windows.UI.StartScreen.SecondaryTile.Exists(tileId);

            string titleToDisplay = "PodcastGo";
            string subtitleToDisplay = "Ready to play";

            if (playingEpisode != null)
            {
                titleToDisplay = "Now Playing:";
                subtitleToDisplay = playingEpisode.Title;
            }
            else
            {
                var downloaded = podcast.Episodes?.Where(e => e.IsDownloaded).OrderByDescending(e => e.PublishDate).FirstOrDefault();
                if (downloaded != null)
                {
                    titleToDisplay = "Downloaded: " + downloaded.Title;
                    string timeStamp = "Recently";
                    try
                    {
                        if (!string.IsNullOrEmpty(downloaded.LocalFilePath))
                        {
                            Windows.Storage.StorageFile file = null;
                            if (System.IO.Path.IsPathRooted(downloaded.LocalFilePath))
                            {
                                file = await Windows.Storage.StorageFile.GetFileFromPathAsync(downloaded.LocalFilePath);
                            }
                            else
                            {
                                file = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFileAsync(downloaded.LocalFilePath);
                            }
                            var basicProperties = await file.GetBasicPropertiesAsync();
                            timeStamp = basicProperties.DateModified.ToString("g"); // Gives Short Date & Time String
                        }
                    }
                    catch { }
                    subtitleToDisplay = timeStamp;
                }
                else
                {
                    titleToDisplay = podcast.Title;
                    subtitleToDisplay = "Ready to play";
                }
            }

            // Update App's Main Tile
            UpdateLiveTile(titleToDisplay, subtitleToDisplay);

            // Update Secondary Pinned Tile uniquely if the user pinned it
            if (isSecondaryPinned)
            {
                UpdateSecondaryLiveTile(tileId, titleToDisplay, subtitleToDisplay);
            }
        }
    }
}