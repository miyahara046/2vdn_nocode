using System.Collections.ObjectModel;

namespace _2vdm_spec_generator.Models
{
    public abstract class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
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
