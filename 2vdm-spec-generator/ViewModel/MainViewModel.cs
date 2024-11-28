using _2vdm_spec_generator.Models;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace _2vdm_spec_generator.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {

        private readonly IFolderPicker _folderPicker;
        public MainViewModel(IFolderPicker folderPicker)
        {
            _folderPicker = folderPicker;
            fileSystemItems = new ObservableCollection<FileSystemItem>();
        }

        [ObservableProperty]
        ObservableCollection<FileSystemItem> fileSystemItems;

        [ObservableProperty]
        string selectedFileContent;

        [RelayCommand]
        async Task SelectFolder()
        {
            try
            {
                var result = await _folderPicker.PickAsync();
                if (result.IsSuccessful)
                {
                    FileSystemItems.Clear();
                    await LoadFolder(result.Folder.Path, FileSystemItems);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"フォルダ選択中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        private async Task LoadFolder(string path, ObservableCollection<FileSystemItem> items)
        {
            var itemInfo = new DirectoryInfo(path);
            foreach (var dir in itemInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                var dirItem = new DirectoryItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };
                items.Add(dirItem);
            }

            foreach (var file in itemInfo.GetFiles("*.md"))
            {
                items.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Content = await File.ReadAllTextAsync(file.FullName)
                });
            }
        }

        [RelayCommand]
        async Task SelectFile(FileSystemItem item)
        {
            if (item is DirectoryItem dirItem)
            {
                dirItem.Children.Clear();
                var itemInfo = new DirectoryInfo(item.FullPath);

                foreach (var dir in itemInfo.GetDirectories())
                {
                    var newDirItem = new DirectoryItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName
                    };
                    dirItem.Children.Add(newDirItem);
                }

                foreach (var file in itemInfo.GetFiles("*.md"))
                {
                    dirItem.Children.Add(new FileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Content = await File.ReadAllTextAsync(file.FullName)
                    });
                }
            }
            else if (item is FileItem fileItem)
            {
                SelectedFileContent = fileItem.Content;
            }
        }

        //[RelayCommand]
        //void Delete(MarkdownFile file)
        //{
        //    if (Items.Contains(file))
        //    {
        //        Items.Remove(file);
        //    }
        //}
    }
}
