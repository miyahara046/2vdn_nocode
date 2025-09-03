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

        // ===== 新規 Markdown ファイル作成 =====
        [RelayCommand]
        private void CreateNewMdFile()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath) || string.IsNullOrWhiteSpace(NewFileName))
                return;

            string fileName = Path.HasExtension(NewFileName) ? NewFileName : $"{NewFileName}.md";
            string path = Path.Combine(SelectedFolderPath, fileName);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "## New Class\n");
                LoadFolderItems();
                NewFileName = "";             // 入力をクリア
                IsMakeNewFile = false; // 作成後は非表示に戻す
            }
        }

        // ===== クラスを追加 =====
        [RelayCommand]
        private void AddClassHeading()
        {
            if (string.IsNullOrWhiteSpace(ClassNameToAdd)
                || string.IsNullOrWhiteSpace(SelectedFolderPath)
                || string.IsNullOrWhiteSpace(SelectedFileName))
                return;

            string path = Path.Combine(SelectedFolderPath, SelectedFileName);
            string currentMarkdown = File.Exists(path) ? File.ReadAllText(path) : "";

            // サービスで Markdown を構築
            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddClassHeading(currentMarkdown, ClassNameToAdd);

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

            // 入力をクリア
            ClassNameToAdd = "";
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
        [RelayCommand]
        private void ShowNewFileEntry()
        {
            IsMakeNewFile = true;
        }

        partial void OnSelectedFileNameChanged(string oldValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue) || string.IsNullOrWhiteSpace(SelectedFolderPath))
                return;

            string mdPath = Path.Combine(SelectedFolderPath, newValue);
            if (File.Exists(mdPath))
            {
                MarkdownContent = File.ReadAllText(mdPath);
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
