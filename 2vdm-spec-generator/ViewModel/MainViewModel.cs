using _2vdm_spec_generator.Models;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using _2vdm_spec_generator.Services;

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

        [ObservableProperty]
        string vdmContent;

        [RelayCommand]
        async Task SelectFolder()
        {
            try
            {
                var result = await _folderPicker.PickAsync();
                if (result.IsSuccessful)
                {   
                    ProjectRootPath = result.Folder.Path;
                    LoadedItems.Clear();
                    // 選択フォルダ以下のすべてのディレクトリとフォルダを読み込む
                    await LoadFolder(ProjectRootPath, LoadedItems);

                    // TreeItemsに選択フォルダ(プロジェクトルート)直下のアイテムのみを追加
                    var rootItems = LoadedItems.Where(i => 
                        i.FullPath.StartsWith(ProjectRootPath + Path.DirectorySeparatorChar) &&
                        !i.FullPath.Substring(ProjectRootPath.Length + 1).Contains(Path.DirectorySeparatorChar)
                    );

                    TreeItems.Clear();
                    foreach (var item in rootItems)
                    {
                        TreeItems.Add(item);
                    }
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
                    // 現在のアイテムの直後の要素が、このディレクトリの子要素かどうかを確認
                    var nextIndex = currentIndex + 1;
                    if (nextIndex < TreeItems.Count && 
                        TreeItems[nextIndex].FullPath.StartsWith(item.FullPath + Path.DirectorySeparatorChar))
                    {
                        // 子要素が表示されている場合は、それらを削除
                        var tempItems = new ObservableCollection<FileSystemItem>(TreeItems);
                        while (nextIndex < tempItems.Count && 
                            tempItems[nextIndex].FullPath.StartsWith(item.FullPath + Path.DirectorySeparatorChar))
                        {
                            tempItems.RemoveAt(nextIndex);
                        }
                        TreeItems = tempItems;  // コレクション全体を更新して変更を通知
                    }
                    else
                    {
                        // LoadedItemsから該当するディレクトリの子要素を検索
                        var childItems = LoadedItems.Where(i => 
                            i.FullPath.StartsWith(item.FullPath + Path.DirectorySeparatorChar) &&
                            !i.FullPath.Substring(item.FullPath.Length + 1).Contains(Path.DirectorySeparatorChar)
                        ).ToList();

                        if (childItems.Any())
                        {
                            // 一時的なリストを作成して一括で更新
                            var newItems = new ObservableCollection<FileSystemItem>(TreeItems);
                            foreach (var child in childItems)
                            {
                                currentIndex++;
                                newItems.Insert(currentIndex, child);
                            }
                            TreeItems = newItems;
                        }
                        else
                        {
                            await Shell.Current.DisplayAlert("デバッグ", "子要素が見つかりませんでした", "OK");
                        }
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

        [RelayCommand]
        void ConvertToVdm()
        {
            if (string.IsNullOrEmpty(SelectedFileContent))
            {
                return;
            }

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(SelectedFileContent);
        }
    }
}
