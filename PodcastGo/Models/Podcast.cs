using System.Collections.Generic;

namespace PodcastGo.Models
{
    public class Podcast
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string RssUrl { get; set; }
        public string ImageUrl { get; set; }
        public List<Episode> Episodes { get; set; } = new List<Episode>();
    }
}
