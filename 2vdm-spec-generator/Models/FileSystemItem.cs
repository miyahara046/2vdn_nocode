using System.Collections.ObjectModel;

namespace _2vdm_spec_generator.Models
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public ObservableCollection<FileSystemItem> Children { get; set; }
        public string Content { get; set; }
        public int IndentLevel { get; set; }

        public FileSystemItem()
        {
            Children = new ObservableCollection<FileSystemItem>();
        }
    }
}
