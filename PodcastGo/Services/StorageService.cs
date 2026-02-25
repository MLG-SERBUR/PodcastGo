using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PodcastGo.Models;
using Windows.Storage;

namespace PodcastGo.Services
{
    public static class StorageService
    {
        private const string PodcastsFileName = "podcasts.json";

        public static async Task<List<Podcast>> LoadPodcastsAsync()
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.GetFileAsync(PodcastsFileName);
                var content = await FileIO.ReadTextAsync(file);
                var podcasts = JsonConvert.DeserializeObject<List<Podcast>>(content);
                return podcasts ?? new List<Podcast>();
            }
            catch (FileNotFoundException)
            {
                return new List<Podcast>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load podcasts: {ex.Message}");
                return new List<Podcast>();
            }
        }

        public static async Task SavePodcastsAsync(List<Podcast> podcasts)
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(PodcastsFileName, CreationCollisionOption.ReplaceExisting);
                var content = JsonConvert.SerializeObject(podcasts, Formatting.Indented);
                await FileIO.WriteTextAsync(file, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save podcasts: {ex.Message}");
            }
        }

        public static async Task DeletePodcastAsync(string podcastId)
        {
            var podcasts = await LoadPodcastsAsync();
            var podcastToRemove = podcasts.Find(p => p.Id == podcastId);
            
            // Fallback: search by ID or title/url if ID is missing (for older data)
            if (podcastToRemove == null && !string.IsNullOrEmpty(podcastId))
            {
                podcastToRemove = podcasts.Find(p => p.RssUrl == podcastId || p.Title == podcastId);
            }

            if (podcastToRemove != null)
            {
                podcasts.Remove(podcastToRemove);
                await SavePodcastsAsync(podcasts);
            }
        }
    }
}
