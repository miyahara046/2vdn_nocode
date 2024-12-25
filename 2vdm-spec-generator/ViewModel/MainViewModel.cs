using _2vdm_spec_generator.Models;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using _2vdm_spec_generator.Services;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Maui.ApplicationModel;

namespace _2vdm_spec_generator.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {

        private readonly IFolderPicker _folderPicker;
        private FileSystemWatcher _watcher;
        private readonly object _fileLock = new object();
        private HashSet<string> _processingFiles = new HashSet<string>();
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
        private string selectedFilePath;

        [ObservableProperty]
        string vdmContent;

        [ObservableProperty]
        private string vdmFilePath;

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

                    // ファイル監視を開始
                    InitializeFileWatcher(ProjectRootPath);
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
            if (fileInfo.Exists)
            {
                var extension = fileInfo.Extension.ToLower();
                if (extension == ".md" || extension == ".vdmpp")
                {
                    // ファイル情報を追加
                    items.Add(new FileItem
                    {
                        Name = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        Content = await File.ReadAllTextAsync(fileInfo.FullName)
                    });
                }
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
                    foreach (var subItem in dirItem.Children)
                    {
                        // 再帰的に子アイテムを処理
                        await LoadFolder(subItem.FullName, items);
                    }
                }
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
                        //var tempItems = new ObservableCollection<FileSystemItem>(TreeItems);
                        while (nextIndex < TreeItems.Count &&
                            TreeItems[nextIndex].FullPath.StartsWith(item.FullPath + Path.DirectorySeparatorChar))
                        {
                            TreeItems.RemoveAt(nextIndex);
                        }
                        //TreeItems = tempItems;  // コレクション全体を更新して変更を通知
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
                            //// ツリー用リストに子要素を追加
                            foreach (var child in childItems)
                            {
                                currentIndex++;
                                TreeItems.Insert(currentIndex, child);
                            }
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
                try
                {
                    SelectedFilePath = fileItem.FullPath;  // パスを保存
                    var newContent = await File.ReadAllTextAsync(fileItem.FullPath);
                    SelectedFileContent = newContent;
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("エラー", $"ファイルの読み込み中にエラーが発生しました: {ex.Message}", "OK");
                }
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

        [RelayCommand]
        void ConvertToVdm()
        {
            if (string.IsNullOrEmpty(SelectedFileContent))
            {
                return;
            }

            // VDM++ファイルのパスを生成
            if (!string.IsNullOrEmpty(SelectedFilePath))
            {
                VdmFilePath = Path.ChangeExtension(SelectedFilePath, ".vdmpp");
            }

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(SelectedFileContent);
        }

        private void InitializeFileWatcher(string path)
        {
            if (_watcher != null)
            {
                _watcher.Dispose();
            }

            _watcher = new FileSystemWatcher
            {
                Path = path,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.*",  // すべてのファイルを監視
                IncludeSubdirectories = true
            };

            _watcher.Changed += async (s, e) => await OnFileSystemChanged(e);
            _watcher.Created += async (s, e) => await OnFileSystemChanged(e);
            _watcher.Deleted += async (s, e) => await OnFileSystemChanged(e);
            _watcher.Renamed += async (s, e) => await OnFileSystemChanged(e);

            _watcher.EnableRaisingEvents = true;
        }

        private async Task OnFileSystemChanged(FileSystemEventArgs e)
        {
            // 既に処理中のファイルは無視
            lock (_fileLock)
            {
                if (_processingFiles.Contains(e.FullPath))
                {
                    return;
                }
                _processingFiles.Add(e.FullPath);
            }

            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        if (e.ChangeType == WatcherChangeTypes.Deleted)
                        {
                            RemoveItemFromCollections(e.FullPath);
                        }
                        else if (e.ChangeType == WatcherChangeTypes.Changed && File.Exists(e.FullPath))
                        {
                            var extension = Path.GetExtension(e.FullPath).ToLower();
                            if (extension == ".md" || extension == ".vdmpp")
                            {
                                // ファイルが完全に書き込まれるまで少し待機
                                await Task.Delay(100);

                                // ファイルの内容を更新
                                var fileInfo = new FileInfo(e.FullPath);
                                var content = await File.ReadAllTextAsync(fileInfo.FullName);

                                // LoadedItemsの該当するファイルを更新
                                var loadedFile = LoadedItems.OfType<FileItem>()
                                    .FirstOrDefault(f => f.FullPath == e.FullPath);
                                if (loadedFile != null)
                                {
                                    loadedFile.Content = content;
                                }

                                // 現在表示中のファイルが変更された場合、内容を更新
                                if (e.FullPath == SelectedFilePath)
                                {
                                    SelectedFileContent = content;
                                }
                            }
                        }
                        else if (e.ChangeType == WatcherChangeTypes.Created)
                        {
                            // 既存の Created の処理
                            if (Directory.Exists(e.FullPath))
                            {
                                var tempItems = new ObservableCollection<FileSystemItem>();
                                await LoadFolder(e.FullPath, tempItems);
                                foreach (var item in tempItems)
                                {
                                    LoadedItems.Add(item);
                                }
                            }
                            else if (File.Exists(e.FullPath) && Path.GetExtension(e.FullPath).ToLower() == ".md")
                            {
                                var fileInfo = new FileInfo(e.FullPath);
                                var fileItem = new FileItem
                                {
                                    Name = fileInfo.Name,
                                    FullPath = fileInfo.FullName,
                                    Content = await File.ReadAllTextAsync(fileInfo.FullName)
                                };
                                LoadedItems.Add(fileItem);
                            }
                            UpdateTreeItems();
                        }
                    }
                    catch (Exception ex)
                    {
                        await Shell.Current.DisplayAlert("エラー", $"ファイル監視中にエラーが発生しました: {ex.Message}", "OK");
                    }
                });
            }
            finally
            {
                // 処理が完了したらファイルをリストから削除
                lock (_fileLock)
                {
                    _processingFiles.Remove(e.FullPath);
                }
            }
        }

        private void RemoveItemFromCollections(string fullPath)
        {
            var loadedItem = LoadedItems.FirstOrDefault(i => i.FullPath == fullPath);
            if (loadedItem != null)
            {
                LoadedItems.Remove(loadedItem);
            }

            var treeItem = TreeItems.FirstOrDefault(i => i.FullPath == fullPath);
            if (treeItem != null)
            {
                TreeItems.Remove(treeItem);
            }
        }

        private void RemoveItemsUnderPath(string basePath)
        {
            // LoadedItemsから削除
            var itemsToRemove = LoadedItems.Where(i =>
                i.FullPath == basePath ||
                i.FullPath.StartsWith(basePath + Path.DirectorySeparatorChar)).ToList();
            foreach (var item in itemsToRemove)
            {
                LoadedItems.Remove(item);
            }

            // TreeItemsから削除
            itemsToRemove = TreeItems.Where(i =>
                i.FullPath == basePath ||
                i.FullPath.StartsWith(basePath + Path.DirectorySeparatorChar)).ToList();
            foreach (var item in itemsToRemove)
            {
                TreeItems.Remove(item);
            }
        }

        private void UpdateTreeItems()
        {
            // 現在表示されているディレクトリパスを取得
            var displayedPaths = TreeItems
                .OfType<DirectoryItem>()
                .Select(d => d.FullPath)
                .ToList();

            // LoadedItemsから、表示すべきアイテムを取得
            var itemsToShow = LoadedItems.Where(i =>
                // ルートレベルのアイテム
                (!i.FullPath.Substring(ProjectRootPath.Length + 1).Contains(Path.DirectorySeparatorChar))
                ||
                // または、表示中のディレクトリの直下のアイテム
                displayedPaths.Any(p =>
                    i.FullPath.StartsWith(p + Path.DirectorySeparatorChar) &&
                    !i.FullPath.Substring(p.Length + 1).Contains(Path.DirectorySeparatorChar))
            ).ToList();

            // 新しいTreeItemsコレクションを作成
            var newTreeItems = new ObservableCollection<FileSystemItem>();

            // 既存のTreeItemsの順序を維持しながら、更新されたアイテムを追加
            foreach (var existingItem in TreeItems)
            {
                var updatedItem = itemsToShow.FirstOrDefault(i => i.FullPath == existingItem.FullPath);
                if (updatedItem != null)
                {
                    newTreeItems.Add(updatedItem);
                    itemsToShow.Remove(updatedItem);
                }
            }

            // 残りの新しいアイテムを適切な位置に追加
            foreach (var newItem in itemsToShow)
            {
                var parentPath = Path.GetDirectoryName(newItem.FullPath);
                var parentIndex = newTreeItems.ToList().FindIndex(i => i.FullPath == parentPath);

                if (parentIndex != -1)
                {
                    // 親の直後に挿入
                    newTreeItems.Insert(parentIndex + 1, newItem);
                }
                else
                {
                    // ルートレベルのアイテムは最後に追加
                    newTreeItems.Add(newItem);
                }
            }

            // TreeItemsを新しいコレクションで更新
            TreeItems = newTreeItems;
        }

        [RelayCommand]
        async Task SaveFile()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedFilePath))
                {
                    await Shell.Current.DisplayAlert("エラー", "保存するファイルが選択されていません。", "OK");
                    return;
                }

                await File.WriteAllTextAsync(SelectedFilePath, SelectedFileContent);
                await Shell.Current.DisplayAlert("成功", "ファイルを保存しました。", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイルの保存中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        async Task SaveVdm()
        {
            try
            {
                if (string.IsNullOrEmpty(VdmContent))
                {
                    await Shell.Current.DisplayAlert("エラー", "保存するVDM++記述がありません。", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(VdmFilePath))
                {
                    await Shell.Current.DisplayAlert("エラー", "VDM++ファイルのパスが設定されていません。", "OK");
                    return;
                }

                // ファイルを保存
                await File.WriteAllTextAsync(VdmFilePath, VdmContent);

                // LoadedItemsに新しいファイルを追加
                var fileInfo = new FileInfo(VdmFilePath);
                var fileItem = new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    Content = VdmContent
                };
                LoadedItems.Add(fileItem);

                // TreeItemsを更新
                UpdateTreeItems();

                await Shell.Current.DisplayAlert("成功", "VDM++記述を保存しました。", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"VDM++記述の保存中にエラーが発生しました: {ex.Message}", "OK");
            }
        }
    }
}
