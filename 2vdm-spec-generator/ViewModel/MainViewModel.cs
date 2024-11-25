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
            FileSystemItems = new ObservableCollection<FileSystemItem>();
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

        private async Task LoadFolder(string folderPath, ObservableCollection<FileSystemItem> items)
        {
            var dirInfo = new DirectoryInfo(folderPath);
            foreach (var dir in dirInfo.GetDirectories())
            {
                var dirItem = new FileSystemItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };
                items.Add(dirItem);
                await LoadFolder(dir.FullName, dirItem.Children);
            }

            foreach (var file in dirInfo.GetFiles("*.md"))
            {
                items.Add(new FileSystemItem
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
            if (!item.IsDirectory)
            {
                SelectedFileContent = item.Content;
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
