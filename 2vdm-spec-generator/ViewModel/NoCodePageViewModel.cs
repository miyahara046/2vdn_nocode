using _2vdm_spec_generator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
#if WINDOWS
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Storage.Pickers;
#endif

namespace _2vdm_spec_generator.ViewModel
{
    public partial class NoCodePageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string selectedFolderPath;

        [ObservableProperty]
        private string classNameToAdd;

        [ObservableProperty]
        private string selectedFileName;

        [ObservableProperty]
        private string markdownContent;

        [ObservableProperty]
        private string vdmContent;

        [ObservableProperty]
        private string newFileName;

        [ObservableProperty]
        private bool isMakeNewFile;

        [ObservableProperty]
        private bool isFolderSelected = true;

        [ObservableProperty]
        private bool isClassAddButtonVisible = false;

        [ObservableProperty]
        private bool isScreenListAddButtonVisible = false;

        public ObservableCollection<string> FolderItems { get; } = new ObservableCollection<string>();

        private readonly string mdFileName = "NewClass.md";

        public NoCodePageViewModel() { }

        // ===== スタートページへ戻る =====
        [RelayCommand]
        private async Task GoToStartPageAsync()
        {
            await Shell.Current.GoToAsync("//StartPage");
        }

        // ===== フォルダ選択 =====
        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            {
#if WINDOWS
            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;

            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SelectedFolderPath = folder.Path;
                LoadFolderItems();
            }
#else
                await Application.Current.MainPage.DisplayAlert("未対応", "このプラットフォームではフォルダ選択は未対応です。", "OK");
#endif
            }
            IsFolderSelected = false;
        }

        [RelayCommand]
        private async Task CreateNewMdFileAsync()
        {
            string newFilePath = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(selectedFolderPath))
                {
                    await Shell.Current.DisplayAlert("エラー", "フォルダが選択されていません。", "OK");
                    return;
                }

                // 入力ダイアログを表示
                string fileName = await Shell.Current.DisplayPromptAsync(
                    "新規ファイル作成",
                    "ファイル名を入力してください（拡張子は付けないでください）",
                    "作成",
                    "キャンセル",
                    placeholder: "example"
                );

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return; // キャンセル or 未入力なら処理終了
                }

                // .md 拡張子を付与
                if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".md";
                }

                newFilePath = Path.Combine(selectedFolderPath, fileName);

                if (File.Exists(newFilePath))
                {
                    await Shell.Current.DisplayAlert("エラー", "同名のファイルが既に存在します。", "OK");
                    return;
                }

                // Markdown ファイルを作成（初期内容あり）
                await File.WriteAllTextAsync(newFilePath, "New Class\n");

                // FolderItems を更新
                LoadFolderItems();

                // === 新規作成したファイルを選択して内容を表示 ===
                SelectedFileName = Path.GetFileName(newFilePath);

                await Shell.Current.DisplayAlert("成功", "新しい Markdown ファイルを作成しました。", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイル作成中にエラーが発生しました: {ex.Message}", "OK");
            }

            IsClassAddButtonVisible = true;
        }




        [RelayCommand]
        private async Task AddClassHeadingAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)
                || string.IsNullOrWhiteSpace(SelectedFileName))
            {
                await Shell.Current.DisplayAlert("エラー", "ファイルが選択されていません。", "OK");
                return;
            }

            // ① 種類を選択
            string classType = await Shell.Current.DisplayActionSheet(
                "追加するクラスの種類を選んでください",
                "キャンセル",
                null,
                "画面一覧の追加",
                "クラスの追加"
            );

            if (classType == null || classType == "キャンセル")
            {
                return; // キャンセル時は終了
            }

            string className = null;

            // ② 「画面の追加」の場合だけクラス名を入力させる
            if (classType == "クラスの追加")
            {
                string inputName = await Shell.Current.DisplayPromptAsync(
                    "クラス追加",
                    "クラス名を入力してください",
                    accept: "OK",
                    cancel: "キャンセル",
                    placeholder: "MyClass"
                );

                if (string.IsNullOrWhiteSpace(inputName))
                {
                    return; // 入力キャンセルまたは未入力なら終了
                }

                className = $"# {inputName}";

            }

            // ファイルパス
            string path = Path.Combine(SelectedFolderPath, SelectedFileName);
            string currentMarkdown = File.Exists(path) ? File.ReadAllText(path) : "";

            // 種類によって処理分岐
            var builder = new UiToMarkdownConverter();
            string newMarkdown = classType switch
            {
                "画面一覧の追加" => builder.AddClassHeading(currentMarkdown," 画面一覧"),
                "クラスの追加" => builder.AddClassHeading(currentMarkdown, className),
                _ => currentMarkdown
            };


            // ファイルに保存
            File.WriteAllText(path, newMarkdown);

            // VDM++ に変換（既存のコンバーターを使う）
            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            string vdmPath = Path.ChangeExtension(path, ".vdmpp");
            File.WriteAllText(vdmPath, vdmContent);

            // UI に反映
            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
            if (classType == "クラスの追加")
            {
                IsClassAddButtonVisible = false;
                IsScreenListAddButtonVisible = false;
            }
            else if (classType == "画面一覧の追加")
            {
                IsClassAddButtonVisible = false;
                IsScreenListAddButtonVisible = true;
            }
        }

        [RelayCommand]
        private async Task AddScreenListAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)
                || string.IsNullOrWhiteSpace(SelectedFileName))
            {
                await Shell.Current.DisplayAlert("エラー", "ファイルが選択されていません。", "OK");
                return;
            }
            string screenName = await Shell.Current.DisplayPromptAsync(
                "画面追加",
                "画面名を入力してください",
                accept: "OK",
                cancel: "キャンセル",
                placeholder: "MyScreen"
            );
            if (string.IsNullOrWhiteSpace(screenName))
            {
                return; // 入力キャンセルまたは未入力なら終了
            }
            screenName = screenName.Trim();
            // ファイルパス
            string path = Path.Combine(SelectedFolderPath, SelectedFileName);
            string currentMarkdown = File.Exists(path) ? File.ReadAllText(path) : "";

            // 画面追加
            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddScreenList(currentMarkdown,screenName);
            // ファイルに保存
            File.WriteAllText(path, newMarkdown);
            // VDM++ に変換（既存のコンバーターを使う）
            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            string vdmPath = Path.ChangeExtension(path, ".vdmpp");
            File.WriteAllText(vdmPath, vdmContent);
            // UI に反映
            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
        }



        // ===== VDM++ に変換 =====
        [RelayCommand]
        private void ConvertToVdm()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath) || string.IsNullOrWhiteSpace(SelectedFileName)) return;

            string mdPath = Path.Combine(SelectedFolderPath, SelectedFileName);
            if (!File.Exists(mdPath)) return;

            string markdownContent = File.ReadAllText(mdPath);
            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(markdownContent);

            string vdmPath = Path.ChangeExtension(mdPath, ".vdmpp");
            File.WriteAllText(vdmPath, vdmContent);

            // 右側エディタにも反映
            VdmContent = vdmContent;
        }

        // ===== Markdown 保存 =====
        [RelayCommand]
        private void SaveMarkdown()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath) || string.IsNullOrWhiteSpace(SelectedFileName)) return;

            string path = Path.Combine(SelectedFolderPath, SelectedFileName);
            File.WriteAllText(path, MarkdownContent);
        }

        // ===== フォルダ内ファイル一覧を更新 =====
        private void LoadFolderItems()
        {
            FolderItems.Clear();
            if (!string.IsNullOrWhiteSpace(SelectedFolderPath) && Directory.Exists(SelectedFolderPath))
            {
                foreach (var file in Directory.GetFiles(SelectedFolderPath))
                {
                    if(Path.GetExtension(file).ToLower() == ".md")
                        FolderItems.Add(Path.GetFileName(file));
                }
            }
        }

partial void OnSelectedFileNameChanged(string oldValue, string newValue)
{
    if (string.IsNullOrWhiteSpace(newValue) || string.IsNullOrWhiteSpace(SelectedFolderPath))
        return;

    string mdPath = Path.Combine(SelectedFolderPath, newValue);
    if (File.Exists(mdPath))
    {
        // Markdown読み込み
        MarkdownContent = File.ReadAllText(mdPath);

        // 先頭行だけ読む
        string firstLine = File.ReadLines(mdPath).FirstOrDefault() ?? string.Empty;

        // 分岐処理
        if (firstLine.StartsWith("New Class", StringComparison.OrdinalIgnoreCase))
        {
            IsClassAddButtonVisible = true;
            IsScreenListAddButtonVisible = false;
        }
        else if (firstLine.StartsWith("# 画面一覧"))
        {
            IsClassAddButtonVisible = false;
            IsScreenListAddButtonVisible = true;
        }
        else
        {
            IsClassAddButtonVisible = false;
            IsScreenListAddButtonVisible = false;
        }
    }
    else
    {
        MarkdownContent = string.Empty;
    }

    string vdmPath = Path.ChangeExtension(mdPath, ".vdmpp");
    if (File.Exists(vdmPath))
    {
        VdmContent = File.ReadAllText(vdmPath);
    }
    else
    {
        VdmContent = string.Empty;
    }
}



    }
}
