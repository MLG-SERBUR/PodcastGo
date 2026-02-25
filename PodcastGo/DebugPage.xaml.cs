using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PodcastGo.Services;

namespace PodcastGo
{
    public sealed partial class DebugPage : Page
    {
        private List<string> _debugLog = new List<string>();
        private const int MAX_LOG_LINES = 100;

        public DebugPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshAllDebugInfo();
        }

        private async void RefreshAllDebugInfo()
        {
            RefreshBackgroundTaskInfo();
            await RefreshBackgroundTaskLog();
            RefreshPlaybackInfo();
            RefreshCurrentEpisodeInfo();
        }

        private void RefreshBackgroundTaskInfo()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== BACKGROUND TASK REGISTRATION INFO ===\n");

                var allTasks = BackgroundTaskRegistration.AllTasks;
                sb.AppendLine($"Total registered tasks: {allTasks.Count}\n");

                foreach (var task in allTasks.Values)
                {
                    sb.AppendLine($"Task Name: {task.Name}");
                    sb.AppendLine($"Task ID: {task.TaskId}");
                    sb.AppendLine();
                }

                if (allTasks.Count == 0)
                {
                    sb.AppendLine("No background tasks registered.");
                    sb.AppendLine("\nIf you expect a task to be registered, the app may need to be restarted.");
                }
                else
                {
                    sb.AppendLine("Tasks are registered and will run based on their configured triggers.");
                    sb.AppendLine("Monitor the Debug Output window in Visual Studio for [BACKGROUND-TASK] logs.");
                }

                BackgroundTaskStatus.Text = sb.ToString();
                AddLog("Background task info refreshed");
            }
            catch (Exception ex)
            {
                BackgroundTaskStatus.Text = $"Error: {ex.Message}\n{ex.StackTrace}";
                AddLog($"Error reading background tasks: {ex.Message}");
            }
        }

        private async void RefreshPlaybackInfo()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== PLAYBACK POSITION INFO ===\n");

                var podcasts = await StorageService.LoadPodcastsAsync();
                int totalEpisodes = podcasts.Sum(p => p.Episodes.Count);
                int episodesWithSavedPosition = podcasts.Sum(p => p.Episodes.Count(ep => ep.Position.TotalSeconds > 0));

                sb.AppendLine($"Total podcasts: {podcasts.Count}");
                sb.AppendLine($"Total episodes: {totalEpisodes}");
                sb.AppendLine($"Episodes with saved position: {episodesWithSavedPosition}\n");

                sb.AppendLine("Episodes with positions:\n");
                foreach (var podcast in podcasts)
                {
                    var episodesWithPos = podcast.Episodes.Where(ep => ep.Position.TotalSeconds > 0).ToList();
                    if (episodesWithPos.Any())
                    {
                        sb.AppendLine($"Podcast: {podcast.Title}");
                        foreach (var episode in episodesWithPos)
                        {
                            sb.AppendLine($"  - {episode.Title}");
                            sb.AppendLine($"    Position: {episode.Position.TotalSeconds:F1}s / {episode.DurationSeconds}s");
                            sb.AppendLine($"    Downloaded: {!string.IsNullOrEmpty(episode.LocalFilePath)}");
                            if (episode.LastPlayedTime.HasValue)
                            {
                                sb.AppendLine($"    Last Played: {episode.LastPlayedTime:g}");
                            }
                        }
                        sb.AppendLine();
                    }
                }

                PlaybackPositionStatus.Text = sb.ToString();
                AddLog("Playback info refreshed");
            }
            catch (Exception ex)
            {
                PlaybackPositionStatus.Text = $"Error: {ex.Message}\n{ex.StackTrace}";
                AddLog($"Error reading playback info: {ex.Message}");
            }
        }

        private void RefreshCurrentEpisodeInfo()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== CURRENTLY PLAYING EPISODE ===\n");

                var current = MainPage.Current;
                if (current?.PlayingEpisode != null)
                {
                    var episode = current.PlayingEpisode;
                    var podcast = current.PlayingPodcast;

                    sb.AppendLine($"Podcast: {podcast?.Title ?? "Unknown"}");
                    sb.AppendLine($"Episode: {episode.Title}");
                    sb.AppendLine($"Saved Position: {episode.Position.TotalSeconds:F1}s");
                    sb.AppendLine($"Duration: {episode.DurationSeconds}s");
                    sb.AppendLine($"Is Listened: {episode.IsListened}");
                    sb.AppendLine($"Is Downloaded: {episode.IsDownloaded}");
                    if (episode.LastPlayedTime.HasValue)
                    {
                        sb.AppendLine($"Last Played: {episode.LastPlayedTime:g}");
                    }
                }
                else
                {
                    sb.AppendLine("No episode currently playing");
                }

                CurrentEpisodeStatus.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                CurrentEpisodeStatus.Text = $"Error: {ex.Message}";
                AddLog($"Error reading current episode: {ex.Message}");
            }
        }

        private void RefreshBackgroundTaskInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshBackgroundTaskInfo();
        }

        private async void RefreshBackgroundTaskLog_Click(object sender, RoutedEventArgs e)
        {
            await RefreshBackgroundTaskLog();
        }

        private async void ClearBackgroundTaskLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await StorageService.ClearBackgroundTaskLogAsync();
                BackgroundTaskLog.Text = "Log cleared!";
                AddLog("Background task log cleared");
            }
            catch (Exception ex)
            {
                AddLog($"Error clearing log: {ex.Message}");
            }
        }

        private async Task RefreshBackgroundTaskLog()
        {
            try
            {
                var log = await StorageService.GetBackgroundTaskLogAsync();
                BackgroundTaskLog.Text = log;
                AddLog("Background task log refreshed");
            }
            catch (Exception ex)
            {
                BackgroundTaskLog.Text = $"Error: {ex.Message}";
                AddLog($"Error loading background task log: {ex.Message}");
            }
        }

        private void RefreshPlaybackInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshPlaybackInfo();
        }

        private async void ClearPositions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var podcasts = await StorageService.LoadPodcastsAsync();
                foreach (var podcast in podcasts)
                {
                    foreach (var episode in podcast.Episodes)
                    {
                        episode.Position = TimeSpan.Zero;
                    }
                }
                await StorageService.SavePodcastsAsync(podcasts);
                AddLog("All saved positions cleared!");
                RefreshPlaybackInfo();
                var dialog = new ContentDialog
                {
                    Title = "Success",
                    Content = "All saved positions have been cleared.",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                AddLog($"Error clearing positions: {ex.Message}");
            }
        }

        private async void TriggerBackgroundTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find and trigger the background task
                var task = BackgroundTaskRegistration.AllTasks.Values.FirstOrDefault(t => t.Name == "PodcastDownloadTask");
                if (task != null)
                {
                    AddLog("Triggering background download task...");
                    // Note: We can't directly trigger it from code, but we can show the status
                    var dialog = new ContentDialog
                    {
                        Title = "Background Task",
                        Content = "Background task is scheduled. It will run when conditions are met (device plugged in, internet available, ~60 minutes from last run).\n\nIn Debug mode, check Application Output for debug messages.",
                        CloseButtonText = "OK"
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Not Found",
                        Content = "PodcastDownloadTask is not registered!",
                        CloseButtonText = "OK"
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error triggering task: {ex.Message}");
            }
        }

        private async void CopyDebugLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(DebugLog.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                AddLog("Debug log copied to clipboard!");
                var dialog = new ContentDialog
                {
                    Title = "Success",
                    Content = "Debug log copied to clipboard!",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                AddLog($"Error copying: {ex.Message}");
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _debugLog.Insert(0, $"[{timestamp}] {message}");

            // Keep only last MAX_LOG_LINES
            while (_debugLog.Count > MAX_LOG_LINES)
            {
                _debugLog.RemoveAt(_debugLog.Count - 1);
            }

            DebugLog.Text = string.Join("\n", _debugLog);
        }
    }
}