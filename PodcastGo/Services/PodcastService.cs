using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PodcastGo.Models;
using Windows.Web.Syndication;

namespace PodcastGo.Services
{
    public static class PodcastService
    {
        public static async Task<Podcast> FetchPodcastAsync(string rssUrl)
        {
            try
            {
                SyndicationClient client = new SyndicationClient();
                SyndicationFeed feed = await client.RetrieveFeedAsync(new Uri(rssUrl));

                var podcast = new Podcast
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = feed.Title?.Text ?? "Unknown Podcast",
                    RssUrl = rssUrl,
                    ImageUrl = feed.ImageUri?.ToString()
                };

                foreach (var item in feed.Items)
                {
                    var enclosure = item.Links.FirstOrDefault(l => l.Relationship == "enclosure");
                    if (enclosure != null)
                    {
                        var episode = new Episode
                        {
                            Id = item.Id ?? Guid.NewGuid().ToString(),
                            Title = item.Title?.Text ?? "Untitled",
                            AudioUrl = enclosure.Uri?.ToString(),
                            PublishDate = item.PublishedDate,
                            IsListened = false
                        };
                        podcast.Episodes.Add(episode);
                    }
                }

                return podcast;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching podcast: {ex.Message}");
                return null;
            }
        }
    }
}
