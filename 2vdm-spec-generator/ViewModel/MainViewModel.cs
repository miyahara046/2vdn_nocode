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
using System.Linq;
using System.Runtime.CompilerServices;
using System;
using System.Windows.Input;
using _2vdm_spec_generator.Converters;

namespace _2vdm_spec_generator.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IFolderPicker _folderPicker;
        private FileSystemWatcher _watcher;
        private readonly object _fileLock = new object();
        private HashSet<string> _processingFiles = new HashSet<string>();
        private double _fontSize = 14; // デフォルトサイズ

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand IncreaseFontSizeCommand { get; }
        public ICommand DecreaseFontSizeCommand { get; }
        public ICommand ResetFontSizeCommand { get; }

        public MainViewModel(IFolderPicker folderPicker)
        {
            _folderPicker = folderPicker;
            loadedItems = new ObservableCollection<FileSystemItem>();
            treeItems = new ObservableCollection<FileSystemItem>();

            IncreaseFontSizeCommand = new Command(() => FontSize = Math.Min(20, FontSize + 2));
            DecreaseFontSizeCommand = new Command(() => FontSize = Math.Max(10, FontSize - 2));
            ResetFontSizeCommand = new Command(() => FontSize = 14);
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

        [ObservableProperty]
        private string vdmSourceFilePath;  // VDM++変換元のMarkdownファイルパス

        [ObservableProperty]
        private string selectedItemPath;

        [ObservableProperty]
        private string newFileName;

        [ObservableProperty]
        private bool isFirstLaunch = true;

        private ICommand _selectFolderCommand;
        public ICommand SelectFolderCommand =>
            _selectFolderCommand ??= new Command(async () =>
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

                        // ツリーを辞書順にソート
                        SortFileSystemItems(LoadedItems);

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
                        IsFirstLaunch = false;
                    }
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("エラー", $"ファルダ選択中にエラーが発生しました: {ex.Message}", "OK");
                }
            });

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
            // MDファイルとVDMファイルの不整合をなくすためにアイテム選択時にvdmContentを空にしておく
            // こうすることで、SaveVDM実行時にvdmFilePathとselectedItemPathが対応したものになるはず
            if (!string.IsNullOrEmpty(vdmContent))
            {
                VdmContent = string.Empty;
            }

            // 以前の選択アイテムの選択状態を解除
            var previousSelectedItem = TreeItems.FirstOrDefault(i => i.FullPath == SelectedItemPath);
            if (previousSelectedItem != null)
            {
                previousSelectedItem.IsSelected = false;
            }

            // 新しい選択アイテムを設定
            SelectedItemPath = item.FullPath;

            // 新しい選択アイテムを選択状態に設定
            var currentSelectedItem = TreeItems.FirstOrDefault(i => i.FullPath == SelectedItemPath);
            if (currentSelectedItem != null)
            {
                currentSelectedItem.IsSelected = true;
            }

            if (item is DirectoryItem dirItem)
            {
                selectedItemPath = dirItem.FullPath;  // 選択アイテムのパスを保存
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
                            // ツリー用リストに子要素を追加
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
                    selectedItemPath = fileItem.FullPath;  // 選択アイテムのパスを保存
                    SelectedFilePath = fileItem.FullPath;  // 表示ファイルのパスを保存
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

            // VDM++変換元のファイルパスを保存
            VdmSourceFilePath = SelectedFilePath;

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
                        string extension = Path.GetExtension(e.FullPath).ToLower();
                        bool isDirectory = Directory.Exists(e.FullPath);

                        if (!isDirectory && extension != ".md" && extension != ".vdmpp")
                        {
                            // サポートされていないファイルタイプは無視
                            System.Diagnostics.Debug.WriteLine($"サポート外のファイルタイプ: {e.FullPath}");
                            return;
                        }

                        if (e.ChangeType == WatcherChangeTypes.Deleted)
                        {
                            System.Diagnostics.Debug.WriteLine($"削除イベント: {e.FullPath}");
                            RemoveItemFromCollections(e.FullPath);
                        }
                        else if (e.ChangeType == WatcherChangeTypes.Changed && File.Exists(e.FullPath))
                        {
                            if (extension == ".md" || extension == ".vdmpp")
                            {
                                // ファイルが完全に書き込まれるまで待機
                                await Task.Delay(500);

                                // ファイルの内容を更新
                                string content;
                                try
                                {
                                    content = await File.ReadAllTextAsync(e.FullPath);
                                }
                                catch (IOException ioEx)
                                {
                                    // 読み取り失敗、後で再試行するか無視
                                    System.Diagnostics.Debug.WriteLine($"ファイル読み込みエラー: {ioEx.Message}");
                                    return;
                                }

                                // LoadedItemsの該当するファイルを更新
                                var loadedFile = LoadedItems.OfType<FileItem>()
                                    .FirstOrDefault(f => f.FullPath == e.FullPath);
                                if (loadedFile != null)
                                {
                                    loadedFile.Content = content;
                                    System.Diagnostics.Debug.WriteLine($"ファイル内容更新: {e.FullPath}");
                                }

                                // 現在表示中のファイルが変更された場合、内容を更新
                                if (e.FullPath == SelectedFilePath)
                                {
                                    SelectedFileContent = content;
                                    System.Diagnostics.Debug.WriteLine($"選択中ファイルの内容更新: {e.FullPath}");
                                }
                            }
                        }
                        else if (e.ChangeType == WatcherChangeTypes.Created)
                        {
                            if (isDirectory)
                            {
                                System.Diagnostics.Debug.WriteLine($"ディレクトリ作成イベント: {e.FullPath}");
                                // 新しいディレクトリが作成された場合
                                var tempItems = new ObservableCollection<FileSystemItem>();
                                await LoadFolder(e.FullPath, tempItems);
                                foreach (var item in tempItems)
                                {
                                    LoadedItems.Add(item);
                                }
                                AddItemFromCollections(e.FullPath);
                            }
                            else if (extension == ".md" || extension == ".vdmpp")
                            {
                                System.Diagnostics.Debug.WriteLine($"ファイル作成イベント: {e.FullPath}");
                                try
                                {
                                    var fileInfo = new FileInfo(e.FullPath);
                                    var fileItem = new FileItem
                                    {
                                        Name = fileInfo.Name,
                                        FullPath = fileInfo.FullName,
                                        Content = await File.ReadAllTextAsync(fileInfo.FullName)
                                    };
                                    LoadedItems.Add(fileItem);
                                    AddItemFromCollections(e.FullPath);
                                    System.Diagnostics.Debug.WriteLine($"ファイル追加: {e.FullPath}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"ファイル追加エラー: {ex.Message}");
                                    await Shell.Current.DisplayAlert("エラー", $"ファイル監視中にエラーが発生しました: {ex.Message}", "OK");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ファイル監視処理エラー: {ex.Message}");
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

        private void AddItemFromCollections(string fullPath)
        {
            if (string.IsNullOrEmpty(ProjectRootPath))
            {
                return;  // プロジェクトルートパスが設定されていない場合は処理を中断
            }

            // LoadedItemsから追加するアイテムを検索
            var itemToAdd = LoadedItems.FirstOrDefault(i => i.FullPath == fullPath);
            if (itemToAdd == null) return;

            // アイテムの親ディレクトリパスを取得
            var parentPath = Path.GetDirectoryName(fullPath);

            // アイテムがルートディレクトリ直下の場合
            if (parentPath == ProjectRootPath)
            {
                if (!TreeItems.Any(i => i.FullPath == fullPath))
                {
                    TreeItems.Add(itemToAdd);
                }
                return;
            }

            // 親ディレクトリが TreeItems に存在し、展開されているかチェック
            var parentIndex = TreeItems.ToList().FindIndex(i => i.FullPath == parentPath);
            if (parentIndex != -1)
            {
                // 親の次のアイテムが親のパスで始まる場合、ディレクトリは展開されている
                var nextIndex = parentIndex + 1;
                if (nextIndex < TreeItems.Count &&
                    TreeItems[nextIndex].FullPath.StartsWith(parentPath + Path.DirectorySeparatorChar))
                {
                    // 既に存在しない場合のみ追加
                    if (!TreeItems.Any(i => i.FullPath == fullPath))
                    {
                        // 適切な位置に挿入
                        var insertIndex = TreeItems
                            .Skip(parentIndex + 1)
                            .TakeWhile(i => i.FullPath.StartsWith(parentPath + Path.DirectorySeparatorChar))
                            .ToList()
                            .FindIndex(i => string.Compare(i.Name, itemToAdd.Name, StringComparison.OrdinalIgnoreCase) > 0);

                        if (insertIndex == -1)
                        {
                            // 最後に追加
                            var lastSiblingIndex = TreeItems
                                .Skip(parentIndex + 1)
                                .TakeWhile(i => i.FullPath.StartsWith(parentPath + Path.DirectorySeparatorChar))
                                .Count();
                            TreeItems.Insert(parentIndex + 1 + lastSiblingIndex, itemToAdd);
                        }
                        else
                        {
                            // 見つかった位置に挿入
                            TreeItems.Insert(parentIndex + 1 + insertIndex, itemToAdd);
                        }
                    }
                }
            }
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

                // ファイル処理中としてマーク
                _processingFiles.Add(SelectedFilePath);

                // ファイルを書き込む
                await File.WriteAllTextAsync(SelectedFilePath, SelectedFileContent);

                await Shell.Current.DisplayAlert("成功", "ファイルを保存しました。", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイルの保存中にエラーが発生しました: {ex.Message}", "OK");
            }
            finally
            {
                // ファイル処理中のマークを解除
                _processingFiles.Remove(SelectedFilePath);
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

                _processingFiles.Add(VdmFilePath); // ファイルパスを追加
                // 既存のファイルをLoadedItemsとTreeItemsから削除
                var existingLoadedItem = LoadedItems.OfType<FileItem>()
                    .FirstOrDefault(f => f.FullPath == VdmFilePath);
                if (existingLoadedItem != null)
                {
                    LoadedItems.Remove(existingLoadedItem);
                }

                var existingTreeItem = TreeItems.OfType<FileItem>()
                    .FirstOrDefault(f => f.FullPath == VdmFilePath);
                if (existingTreeItem != null)
                {
                    TreeItems.Remove(existingTreeItem);
                }

                // ファイルを保存
                await File.WriteAllTextAsync(VdmFilePath, VdmContent);

                // 新しいファイルアイテムを作成
                var fileInfo = new FileInfo(VdmFilePath);
                var fileItem = new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    Content = VdmContent
                };

                // LoadedItemsとTreeItemsに追加
                if (!string.IsNullOrEmpty(VdmSourceFilePath))
                {
                    var mdIndex = LoadedItems.ToList().FindIndex(i => i.FullPath == VdmSourceFilePath);
                    if (mdIndex != -1)
                    {
                        InsertInOrder(LoadedItems, fileItem);

                        var treeIndex = TreeItems.ToList().FindIndex(i => i.FullPath == VdmSourceFilePath);
                        if (treeIndex != -1)
                        {
                            InsertInOrder(TreeItems, fileItem);
                        }
                    }
                    else
                    {
                        InsertInOrder(LoadedItems, fileItem);
                        InsertInOrder(TreeItems, fileItem);
                    }
                }
                else
                {
                    InsertInOrder(LoadedItems, fileItem);
                    InsertInOrder(TreeItems, fileItem);
                }

                await Shell.Current.DisplayAlert("成功", "VDM++記述を保存しました。", "OK");
                // VDM保存時に開いているMDファイルも一緒に保存する。
                // SelectItem内でアイテム選択の毎に、
                // vdmContent = null;
                // としているので、VDM保存時のVDMファイルとMDファイルは、対応するものであるはずだが、
                // VDMへ変換後に、MDファイル側を編集&保存されたら、内容はズレてしまう。
                await SaveFile();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"VDM++記述の保存中にエラーが発生しました: {ex.Message}", "OK");
            }
            finally
            {
                _processingFiles.Remove(VdmFilePath); // ファイルパスを削除
            }
        }

        private void SortFileSystemItems(ObservableCollection<FileSystemItem> items)
        {
            var sortedItems = items.OrderBy(item => item.Name).ToList();
            items.Clear();
            foreach (var item in sortedItems)
            {
                items.Add(item);
            }
        }

        [RelayCommand]
        void SortItems()
        {
            SortFileSystemItems(LoadedItems);
            SortFileSystemItems(TreeItems);
        }

        [RelayCommand]
        async Task DeleteSelectedFile()
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                await Shell.Current.DisplayAlert("エラー", "削除するファイルが選択されていません。", "OK");
                return;
            }

            try
            {
                // ユーザーに確認
                bool answer = await Shell.Current.DisplayAlert(
                    "確認",
                    $"ファイル '{Path.GetFileName(SelectedFilePath)}' を削除してもよろしいですか？",
                    "はい",
                    "いいえ");

                if (!answer) return;

                // ファイルをローカル環境から削除
                File.Delete(SelectedFilePath);

                // LoadedItemsから削除
                var loadedItem = LoadedItems.FirstOrDefault(i => i.FullPath == SelectedFilePath);
                if (loadedItem != null)
                {
                    LoadedItems.Remove(loadedItem);
                }

                // TreeItemsから削除
                var treeItem = TreeItems.FirstOrDefault(i => i.FullPath == SelectedFilePath);
                if (treeItem != null)
                {
                    TreeItems.Remove(treeItem);
                }

                // 選択状態をクリア
                SelectedFilePath = null;
                SelectedFileContent = string.Empty;

                await Shell.Current.DisplayAlert("成功", "ファイルを削除しました。", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイルの削除中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        async Task CreateNewFile()
        {
            string newFilePath = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(ProjectRootPath))
                {
                    await Shell.Current.DisplayAlert("エラー", "プロジェクトフォルダが選択されていません。", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(NewFileName))
                {
                    await Shell.Current.DisplayAlert("エラー", "ファイル名を入力してください。", "OK");
                    return;
                }

                // 作成先のディレクトリパスを決定
                string targetDirectory = ProjectRootPath;

                // 選択されているアイテムを取得
                var selectedItem = LoadedItems.FirstOrDefault(i => i.FullPath == selectedItemPath);

                // // デバッグ用
                // if (selectedItem != null)
                // {
                //     await Shell.Current.DisplayAlert("デバッグ情報", 
                //         $"選択アイテムの型: {selectedItem.GetType().Name}\n" +
                //         $"パス: {selectedItem.FullPath}\n" +
                //         $"名前: {selectedItem.Name}", 
                //         "OK");
                // }
                // else
                // {
                //     await Shell.Current.DisplayAlert("デバッグ情報", 
                //         $"selectedItem is null\n" +
                //         $"selectedItemPath: {selectedItemPath}", 
                //         "OK");
                // }

                // 選択されているアイテムがFileItemの場合は警告を表示して終了
                if (selectedItem is FileItem)
                {
                    await Shell.Current.DisplayAlert("エラー", "フォルダを選択してください。", "OK");
                    return;
                }
                // 選択されているアイテムがDirectoryItemの場合は確認ダイアログを表示
                else if (selectedItem is DirectoryItem)
                {
                    var dirName = Path.GetFileName(selectedItemPath);
                    bool createInSelectedDir = await Shell.Current.DisplayAlert(
                        "確認",
                        $"{dirName}フォルダの中にmdファイルを作成しますか?",
                        "はい",
                        "いいえ");

                    if (createInSelectedDir)
                    {
                        targetDirectory = selectedItemPath;
                    }
                }

                // 拡張子が.mdでない場合は追加
                string fileName = NewFileName;
                if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".md";
                }

                // 新規ファイルのパスを事前に宣言
                newFilePath = Path.Combine(targetDirectory, fileName);

                // ファイル処理中としてマーク
                _processingFiles.Add(newFilePath);

                // 既存のファイルをチェック
                if (File.Exists(newFilePath))
                {
                    await Shell.Current.DisplayAlert("エラー", "同名のファイルが既に存在します。", "OK");
                    return;
                }

                // 空のファイルを作成
                await File.WriteAllTextAsync(newFilePath, string.Empty);

                // 新しいFileItemを作成
                var fileItem = new FileItem
                {
                    Name = fileName,
                    FullPath = newFilePath,
                    Content = string.Empty
                };

                // LoadedItemsに追加
                InsertInOrder(LoadedItems, fileItem);

                // TreeItemsに辞書順を維持しながら追加（選択されているディレクトリが展開されている場合のみ）
                if (targetDirectory == ProjectRootPath)
                {
                    // ルートディレクトリの場合は辞書順に挿入
                    InsertInOrder(TreeItems, fileItem);
                }
                else
                {
                    // 選択されたディレクトリの子アイテムが表示されている場合のみ追加
                    var parentDirIndex = TreeItems.ToList().FindIndex(i => i.FullPath == targetDirectory);
                    if (parentDirIndex != -1 &&
                        parentDirIndex + 1 < TreeItems.Count &&
                        TreeItems[parentDirIndex + 1].FullPath.StartsWith(targetDirectory))
                    {
                        // 子アイテムの範囲を取得
                        int insertIndex = parentDirIndex + 1;
                        while (insertIndex < TreeItems.Count &&
                               TreeItems[insertIndex].FullPath.StartsWith(targetDirectory + Path.DirectorySeparatorChar))
                        {
                            if (string.Compare(TreeItems[insertIndex].Name, fileItem.Name, StringComparison.OrdinalIgnoreCase) > 0)
                            {
                                break;
                            }
                            insertIndex++;
                        }
                        TreeItems.Insert(insertIndex, fileItem);
                    }
                }

                // ファイル名をクリア
                NewFileName = string.Empty;

                await Shell.Current.DisplayAlert("成功", "新しいファイルを作成しました。", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイルの作成中にエラーが発生しました: {ex.Message}", "OK");
            }
            finally
            {
                // 処理が完了したらファイルパスを解除
                _processingFiles.Remove(newFilePath);
            }
        }

        [RelayCommand]
        async Task SetNewFileName(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    await Shell.Current.DisplayAlert("エラー", "ファイル名を入力してください。", "OK");
                    return;
                }

                // 使用できない文字が含まれていないかチェック
                var invalidChars = Path.GetInvalidFileNameChars();
                if (fileName.Any(c => invalidChars.Contains(c)))
                {
                    await Shell.Current.DisplayAlert("エラー", "ファイル名に使用できない文字が含まれています。", "OK");
                    return;
                }

                NewFileName = fileName;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイル名の設定中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        async Task ShowNewFileDialog()
        {
            if (string.IsNullOrEmpty(ProjectRootPath))
            {
                await Shell.Current.DisplayAlert("エラー", "プロジェクトフォルダが選択されていません。", "OK");
                return;
            }

            string result = await Shell.Current.DisplayPromptAsync(
                "新規ファイル作成",
                "ファイル名を入力してください",
                "作成",
                "キャンセル",
                "例: specification");

            if (!string.IsNullOrEmpty(result))
            {
                await SetNewFileName(result);
                if (!string.IsNullOrEmpty(NewFileName))  // SetNewFileNameでのバリデーションが通った場合のみ
                {
                    await CreateNewFile();
                }
            }
        }

        /// ObservableCollectionに辞書順でアイテムを挿入するメソッド
        private void InsertInOrder(ObservableCollection<FileSystemItem> collection, FileSystemItem newItem)
        {
            if (collection.Count == 0)
            {
                collection.Add(newItem);
                return;
            }

            for (int i = 0; i < collection.Count; i++)
            {
                if (string.Compare(newItem.Name, collection[i].Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    collection.Insert(i, newItem);
                    return;
                }
            }

            // すべてのアイテムよりも後の場合は末尾に追加
            collection.Add(newItem);
        }
    }
}
