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
            loadedItems = new ObservableCollection<FileSystemItem>();
            treeItems = new ObservableCollection<FileSystemItem>();
        }
        
        // 開いているプロジェクトルートのパス
        [ObservableProperty]
        string projectRootPath;

        // 保持しておくデータ
        [ObservableProperty]
        ObservableCollection<FileSystemItem> loadedItems;
        
        // ツリー表示に使用するデータ
        [ObservableProperty]
        ObservableCollection<FileSystemItem> treeItems;

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
                    LoadedItems.Clear();
                    await LoadFolder(result.Folder.Path, LoadedItems);
                    // TreeItemsを新しいコレクションとして作成
                    TreeItems = new ObservableCollection<FileSystemItem>(LoadedItems);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"フォルダ選択中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        private async Task LoadFolder(string path, ObservableCollection<FileSystemItem> items)
        {   
            var fileInfo = new FileInfo(path);
            var dirInfo = new DirectoryInfo(path);
            
            // ファイルの場合
            // 基底部
            if (fileInfo.Exists && fileInfo.Extension.ToLower() == ".md")
            {   
                // ファイル情報を追加
                items.Add(new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    Content = await File.ReadAllTextAsync(fileInfo.FullName)
                });
            }
            
            // ディレクトリの場合
            if (dirInfo.Exists)
            {
                var dirItem = new DirectoryItem
                {
                    Name = dirInfo.Name,
                    FullPath = dirInfo.FullName,
                    Children = dirInfo.GetFileSystemInfos()
                };
                // ディレクトリ情報を追加
                items.Add(dirItem);

                if (dirItem.Children.Count() > 0)
                {
                    foreach(var subItem in dirItem.Children)
                    {
                        // 再帰的に子アイテムを処理
                        await LoadFolder(subItem.FullName, items);
                    }
                }
                // else if (fsInfo is FileInfo file)
                // {
                //     // 再帰的にファイルを処理
                //     await LoadFolder(file.FullName, items);
                // }
            }
        }

        [RelayCommand]
        async Task SelectItem(FileSystemItem item)
        {
            if (item is DirectoryItem dirItem)
            {
        // 現在のアイテムのインデックスを取得
        var currentIndex = TreeItems.IndexOf(item);
        if (currentIndex != -1)
        {
            // LoadedItemsから該当するディレクトリの子要素を検索
            var childItems = LoadedItems.Where(i => 
                i.FullPath.StartsWith(item.FullPath + Path.DirectorySeparatorChar) &&
                !i.FullPath.Substring(item.FullPath.Length + 1).Contains(Path.DirectorySeparatorChar)
            ).ToList();

            // 子要素を現在のアイテムの直後に挿入
            foreach (var child in childItems)
            {
                currentIndex++;
                TreeItems.Insert(currentIndex, child);
            }
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

        // [RelayCommand]
        // void PrintTreeItems()
        // {
        //     foreach (var item in treeItems)
        //     {
        //         Console.WriteLine($"アイテム名: {item.Name}");
                
        //         if (item is DirectoryItem dirItem)
        //         {
        //             PrintChildren(dirItem.Children, 1);
        //         }
        //     }
        // }

        // private void PrintChildren(ObservableCollection<FileSystemItem> items, int level)
        // {
        //     string indent = new string(' ', level * 2);
            
        //     foreach (var item in items)
        //     {
        //         Console.WriteLine($"{indent}アイテム名: {item.Name}");
                
        //         if (item is DirectoryItem dirItem)
        //         {
        //             PrintChildren(dirItem.Children, level + 1);
        //         }
        //     }
        // }
    }
}
