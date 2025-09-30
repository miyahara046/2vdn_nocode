using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using _2vdm_spec_generator.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace _2vdm_spec_generator.ViewModel
{
    public partial class NoCodePageViewModel : ObservableObject
    {
        [ObservableProperty] private string selectedFolderPath;
        [ObservableProperty] private FolderItem selectedItem;
        [ObservableProperty] private string markdownContent;
        [ObservableProperty] private string vdmContent;
        [ObservableProperty] private bool isClassAddButtonVisible;
        [ObservableProperty] private bool isScreenListAddButtonVisible;
        [ObservableProperty] private bool isFolderSelected = true;

        public ObservableCollection<FolderItem> FolderItems { get; } = new();

        private readonly string mdFileName = "NewClass.md";

        // ===== フォルダ選択 =====
        [RelayCommand]
        private async Task SelectFolderAsync()
        {
#if WINDOWS
            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SelectedFolderPath = folder.Path;
                LoadFolderItems();
            }
            IsFolderSelected = false;
#else
            await Application.Current.MainPage.DisplayAlert("未対応", "このプラットフォームではフォルダ選択は未対応です。", "OK");
#endif
        }

        // ===== フォルダ読み込み =====
        private void LoadFolderItems()
        {
            FolderItems.Clear();
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)) return;

            foreach (var dir in Directory.GetDirectories(SelectedFolderPath))
                AddFolderRecursive(dir, 0);

            foreach (var file in Directory.GetFiles(SelectedFolderPath).Where(f => Path.GetExtension(f) == ".md"))
                FolderItems.Add(new FolderItem { Name = Path.GetFileName(file), FullPath = file, Level = 0 });
        }

        private void AddFolderRecursive(string path, int level)
        {
            // 親フォルダを先に追加
            var folderItem = new FolderItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                Level = level,
                IsExpanded = true
            };
            FolderItems.Add(folderItem);

            // 同階層のファイルを先に追加
            foreach (var file in Directory.GetFiles(path).Where(f => Path.GetExtension(f).ToLower() == ".md"))
            {
                FolderItems.Add(new FolderItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Level = level + 1
                });
            }

            // サブフォルダを最後に再帰追加
            foreach (var dir in Directory.GetDirectories(path))
                AddFolderRecursive(dir, level + 1);
        }


        // ===== 折りたたみ =====
        [RelayCommand]
        private void ToggleExpand(FolderItem folder)
        {
            if (folder == null || !folder.IsFolder) return;

            folder.IsExpanded = !folder.IsExpanded;

            // 子アイテムの表示/非表示
            foreach (var item in FolderItems)
            {
                if (item.FullPath.StartsWith(folder.FullPath) && item.Level > folder.Level)
                    item.IsVisible = folder.IsExpanded;
            }
        }

        // ===== ファイル選択 =====
        [RelayCommand]
        private void SelectItem(FolderItem item)
        {
            if (item == null || !item.IsFile) return;

            SelectedItem = item;
            LoadMarkdownAndVdm(item.FullPath);
        }

        private void LoadMarkdownAndVdm(string path)
        {
            MarkdownContent = File.Exists(path) ? File.ReadAllText(path) : "";

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);

            string firstLine = File.ReadLines(path).FirstOrDefault() ?? "";
            if (firstLine.StartsWith("## ", StringComparison.OrdinalIgnoreCase))
            {
                IsClassAddButtonVisible = false;
                IsScreenListAddButtonVisible = false;
            }
            else if (firstLine.StartsWith("# 画面一覧"))
            {
                IsClassAddButtonVisible = false;
                IsScreenListAddButtonVisible = true;
            }
            else
            {
                IsClassAddButtonVisible = true;
                IsScreenListAddButtonVisible = false;
            }
        }

        // ===== 新規Markdown作成 =====
        [RelayCommand]
        private async Task CreateNewMdFileAsync()
        {
            if (SelectedItem == null)
            {
                await Shell.Current.DisplayAlert("エラー", "ファイルまたはフォルダが選択されていません", "OK");
                return;
            }

            // 追加先のディレクトリを決定
            string targetDir = SelectedItem.IsFile
                ? Path.GetDirectoryName(SelectedItem.FullPath)  // ファイルなら親ディレクトリ
                : SelectedItem.FullPath;                        // フォルダならそのフォルダ直下

            string fileName = await Shell.Current.DisplayPromptAsync(
                "新規ファイル作成",
                "ファイル名を入力してください（拡張子不要）",
                "作成", "キャンセル", placeholder: "NewClass"
            );

            if (string.IsNullOrWhiteSpace(fileName)) return;
            if (!fileName.EndsWith(".md")) fileName += ".md";

            string newPath = Path.Combine(targetDir, fileName);
            if (File.Exists(newPath))
            {
                await Shell.Current.DisplayAlert("エラー", "同名ファイルが存在します", "OK");
                return;
            }

            // とりあえず中身はテンプレートを用意
            File.WriteAllText(newPath, "New Class\n");

            // フォルダアイテムをリロード
            LoadFolderItems();

            // 作ったファイルを選択状態にして読み込み
            SelectedItem = FolderItems.FirstOrDefault(f => f.FullPath == newPath);
            LoadMarkdownAndVdm(newPath);

            IsClassAddButtonVisible = true;
        }


        // ===== Markdown保存 =====
        [RelayCommand]
        private void SaveMarkdown()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            File.WriteAllText(SelectedItem.FullPath, MarkdownContent);

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);
            File.WriteAllText(Path.ChangeExtension(SelectedItem.FullPath, ".vdmpp"), VdmContent);
        }

        // ===== VDM++変換 =====
        [RelayCommand]
        private void ConvertToVdm()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string mdPath = SelectedItem.FullPath;
            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(File.ReadAllText(mdPath));
            File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), VdmContent);
        }

        // ===== クラス追加 / 画面追加 =====
        [RelayCommand]
        private async Task AddClassHeadingAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string classType = await Shell.Current.DisplayActionSheet(
                "追加するクラスの種類を選んでください",
                "キャンセル", null,
                "画面一覧の追加", "クラスの追加"
            );

            if (string.IsNullOrEmpty(classType) || classType == "キャンセル") return;

            string className = null;
            if (classType == "クラスの追加")
            {
                string inputName = await Shell.Current.DisplayPromptAsync(
                    "クラス追加", "クラス名を入力してください", "OK", "キャンセル", placeholder: "MyClass"
                );
                if (string.IsNullOrWhiteSpace(inputName)) return;
                className = $"# {inputName}";
            }

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            string newMarkdown = classType switch
            {
                "画面一覧の追加" => builder.AddClassHeading(currentMarkdown, " 画面一覧"),
                "クラスの追加" => builder.AddClassHeading(currentMarkdown, className),
                _ => currentMarkdown
            };

            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;

            IsClassAddButtonVisible = classType == "クラスの追加";
            IsScreenListAddButtonVisible = classType == "画面一覧の追加";
        }

        [RelayCommand]
        private async Task AddScreenListAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string screenName = await Shell.Current.DisplayPromptAsync(
                "画面追加", "画面名を入力してください", "OK", "キャンセル", placeholder: "MyScreen"
            );
            if (string.IsNullOrWhiteSpace(screenName)) return;

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddScreenList(currentMarkdown, screenName.Trim());
            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
        }

        // ===== スタートページに戻る =====
        [RelayCommand]
        private async Task GoToStartPageAsync()
        {
            await Shell.Current.GoToAsync("//StartPage");
        }
    }
}
