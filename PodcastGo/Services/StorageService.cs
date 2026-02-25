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
                var roamingFolder = ApplicationData.Current.RoamingFolder;
                
                // Migration: Check if file exists in RoamingFolder. If not, check LocalFolder.
                var roamingFile = await roamingFolder.TryGetItemAsync(PodcastsFileName) as StorageFile;
                if (roamingFile == null)
                {
                    var localFolder = ApplicationData.Current.LocalFolder;
                    var localFile = await localFolder.TryGetItemAsync(PodcastsFileName) as StorageFile;
                    if (localFile != null)
                    {
                        // Move the file from Local to Roaming
                        await localFile.MoveAsync(roamingFolder, PodcastsFileName, NameCollisionOption.ReplaceExisting);
                        roamingFile = await roamingFolder.GetFileAsync(PodcastsFileName);
                    }
                }

                if (roamingFile == null)
                {
                    return new List<Podcast>();
                }

                var content = await FileIO.ReadTextAsync(roamingFile);
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
                var folder = ApplicationData.Current.RoamingFolder;
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

        public static async Task<string> GetBackgroundTaskLogAsync()
        {
            try
            {
                var folder = ApplicationData.Current.RoamingFolder;
                var file = await folder.GetFileAsync("background_task_log.txt");
                return await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return "No background task log yet. The background task will log here when it runs.\n\nMake sure:\n- The app was launched at least once (to register the task)\n- Device is plugged in\n- Device has internet connection\n- ~24 hours have passed since last run (or you can force a test in the Debug page)";
            }
        }

        public static async Task ClearBackgroundTaskLogAsync()
        {
            try
            {
                var folder = ApplicationData.Current.RoamingFolder;
                var file = await folder.GetFileAsync("background_task_log.txt");
                await file.DeleteAsync();
            }
            catch { }
        }
    }
}
