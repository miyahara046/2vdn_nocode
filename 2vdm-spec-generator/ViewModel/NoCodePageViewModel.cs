using _2vdm_spec_generator.Services; // MarkdownToVdmConverter 用
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

        public ObservableCollection<string> FolderItems { get; } = new ObservableCollection<string>();

        private readonly string mdFileName = "NewClass.md";

        public NoCodePageViewModel()
        {
        }

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
                 // 選択フォルダのファイル一覧を更新
            FolderItems.Clear();
            foreach (var file in Directory.GetFiles(SelectedFolderPath))
            {
                FolderItems.Add(Path.GetFileName(file));
            }
            }
#else
            await Application.Current.MainPage.DisplayAlert("未対応", "このプラットフォームではフォルダ選択は未対応です。", "OK");
#endif
        }

        // ===== 新規 Markdown ファイル作成 =====
        [RelayCommand]
        private void CreateNewMdFile()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)) return;

            string path = Path.Combine(SelectedFolderPath, mdFileName);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "# Markdown VDM Document\n");
                FolderItems.Add(mdFileName);
            }
        }

        // ===== クラスを追加 =====
        [RelayCommand]
        private void AddClassHeading()
        {
            if (string.IsNullOrWhiteSpace(ClassNameToAdd) || string.IsNullOrWhiteSpace(SelectedFolderPath)) return;

            string path = Path.Combine(SelectedFolderPath, mdFileName);
            File.AppendAllText(path, $"\n### {ClassNameToAdd}\n");
            LoadFolderItems();
        }

        // ===== VDM++ に変換 =====
        [RelayCommand]
        private void ConvertToVdm()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)) return;

            string mdPath = Path.Combine(SelectedFolderPath, mdFileName);
            if (!File.Exists(mdPath)) return;

            string markdownContent = File.ReadAllText(mdPath);
            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(markdownContent);

            string vdmPath = Path.Combine(SelectedFolderPath, Path.ChangeExtension(mdFileName, ".vdmpp"));
            File.WriteAllText(vdmPath, vdmContent);
            LoadFolderItems();
        }

        // ===== フォルダ内ファイル一覧を更新 =====
        private void LoadFolderItems()
        {
            FolderItems.Clear();
            if (!string.IsNullOrWhiteSpace(SelectedFolderPath) && Directory.Exists(SelectedFolderPath))
            {
                foreach (var file in Directory.GetFiles(SelectedFolderPath))
                {
                    FolderItems.Add(Path.GetFileName(file));
                }
            }
        }
    }
}
