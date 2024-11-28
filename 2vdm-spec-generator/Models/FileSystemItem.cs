using System.Collections.ObjectModel;

namespace _2vdm_spec_generator.Models
{
    public abstract class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; } // 無くしたい
        public int IndentLevel { get; set; } // 無くしたい

    }

    public class DirectoryItem : FileSystemItem
    {
        public ObservableCollection<FileSystemItem> Children { get; set; }
        public DirectoryItem()
        {
            Children = new ObservableCollection<FileSystemItem>();
        }
    }

    public class FileItem : FileSystemItem
    {
        public string Content { get; set; }
    }
}
