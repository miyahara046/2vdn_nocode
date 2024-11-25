using _2vdm_spec_generator.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace _2vdm_spec_generator.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {

        public MainViewModel()
        {
            Items = new ObservableCollection<MarkdownFile>();
        }

        [ObservableProperty]
        ObservableCollection<MarkdownFile> items;

        [ObservableProperty]
        MarkdownFile selectedFile;

        [RelayCommand]
        async Task SelectFile() 
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "MDファイルを選択してください",
                    FileTypes = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                        { DevicePlatform.WinUI, new[] { ".md" } },
                        { DevicePlatform.macOS, new[] { "md" } }
                        })
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    var content = await File.ReadAllTextAsync(result.FullPath);
                    // ここでファイルの内容（fileContent）を処理
                    var mdFile = new MarkdownFile
                    {
                        FileName = result.FileName,
                        FilePath = result.FullPath,
                        Content = content
                    };
                    Items.Add(mdFile);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("エラー", $"ファイル選択中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        void Delete(MarkdownFile file)
        {
            if (Items.Contains(file))
            {
                Items.Remove(file);
            }
        }

        [RelayCommand]
        async Task ShowContent(MarkdownFile file)
        {
            await Shell.Current.DisplayAlert(file.FileName, file.Content, "閉じる");
        }
    }
}
