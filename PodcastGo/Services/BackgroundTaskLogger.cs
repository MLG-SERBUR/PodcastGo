using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace PodcastGo.Services
{
    public static class BackgroundTaskLogger
    {
        private const string LogFileName = "background_task_log.txt";
        private const int MaxLogLines = 500;

        public static async Task LogAsync(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logMessage = $"[{timestamp}] {message}";

                var folder = ApplicationData.Current.RoamingFolder;
                var file = await folder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);
                
                // Read existing content
                string existingContent = "";
                try
                {
                    existingContent = await FileIO.ReadTextAsync(file);
                }
                catch { }

                // Prepare new content
                var lines = new StringBuilder();
                lines.AppendLine(logMessage);
                lines.Append(existingContent);

                // Limit log size by keeping only last MAX_LOG_LINES
                var allLines = lines.ToString().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var limitedLines = new StringBuilder();
                for (int i = 0; i < Math.Min(allLines.Length, MaxLogLines); i++)
                {
                    if (!string.IsNullOrWhiteSpace(allLines[i]))
                    {
                        limitedLines.AppendLine(allLines[i]);
                    }
                }

                // Write back
                await FileIO.WriteTextAsync(file, limitedLines.ToString());
                
                // Also log to debug output
                System.Diagnostics.Debug.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write background task log: {ex.Message}");
            }
        }

        public static async Task<string> GetLogAsync()
        {
            try
            {
                var folder = ApplicationData.Current.RoamingFolder;
                var file = await folder.GetFileAsync(LogFileName);
                return await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return "No background task log yet. The task will log here when it runs.\n\nMake sure the task has been registered on app startup.";
            }
        }

        public static async Task ClearLogAsync()
        {
            try
            {
                var folder = ApplicationData.Current.RoamingFolder;
                var file = await folder.GetFileAsync(LogFileName);
                await file.DeleteAsync();
            }
            catch { }
        }
    }
}
