using System;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace PodcastGo.Services
{
    public static class TileService
    {
        public static void UpdateLiveTile(string title)
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

                TileNotification notification = new TileNotification(tileXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(notification);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update live tile: {ex.Message}");
            }
        }
    }
}
