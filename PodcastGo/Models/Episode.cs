using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PodcastGo.Models
{
    public class Episode : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string AudioUrl { get; set; }
        public string LocalFilePath { get; set; }
        
        private bool _isListened;
        public bool IsListened 
        { 
            get => _isListened; 
            set 
            {
                if (_isListened != value)
                {
                    _isListened = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsListenedVisibility));
                }
            }
        }
        
        private string _notes;
        public string Notes 
        { 
            get => _notes; 
            set 
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public DateTimeOffset PublishDate { get; set; }
        public int DurationSeconds { get; set; }
        public TimeSpan Position { get; set; }
        public DateTimeOffset? LastPlayedTime { get; set; }

        public string IsListenedVisibility => IsListened ? "Visible" : "Collapsed";

        public bool IsDownloaded => !string.IsNullOrEmpty(LocalFilePath);
        public string IsDownloadedVisibility => IsDownloaded ? "Visible" : "Collapsed";


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
