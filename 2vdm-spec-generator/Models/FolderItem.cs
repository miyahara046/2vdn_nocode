using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;

namespace _2vdm_spec_generator.ViewModel
{
    public class FolderItem : ObservableObject
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public int Level { get; set; } = 0;

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                    UpdateChildrenVisibility();
            }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public bool IsFolder => Directory.Exists(FullPath);
        public bool IsFile => File.Exists(FullPath);

        public ObservableCollection<FolderItem> Children { get; } = new();

        private void UpdateChildrenVisibility()
        {
            foreach (var child in Children)
            {
                child.IsVisible = this.IsExpanded;
                if (child.IsFolder)
                    child.UpdateChildrenVisibility();
            }
        }
    }
}
