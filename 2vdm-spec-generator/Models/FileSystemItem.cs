using System.Collections.ObjectModel;
using System.ComponentModel;

namespace _2vdm_spec_generator.Models
{
    public abstract class FileSystemItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string Name { get; set; }
        public string FullPath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DirectoryItem : FileSystemItem
    {
        public FileSystemInfo[] Children { get; set; }
        public DirectoryItem()
        {
            Children = [];
        }
    }

    public class FileItem : FileSystemItem
    {
        public string Content { get; set; }
    }
}
