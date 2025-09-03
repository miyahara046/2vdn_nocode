using Microsoft.Maui.Controls;
using System.IO;

namespace _2vdm_spec_generator
{
    public partial class NoCodePage : ContentPage
    {
        public NoCodePage()
        {
            InitializeComponent();
            BindingContext = new ViewModel.NoCodePageViewModel();
        }

        private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BindingContext is ViewModel.NoCodePageViewModel vm &&
                e.CurrentSelection.FirstOrDefault() is string selectedFile)
            {
                vm.SelectedFileName = selectedFile;

                // Markdown内容を読み込む
                string path = Path.Combine(vm.SelectedFolderPath, selectedFile);
                vm.MarkdownContent = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

                // VDM++内容も更新
                string vdmPath = Path.ChangeExtension(path, ".vdmpp");
                vm.VdmContent = File.Exists(vdmPath) ? File.ReadAllText(vdmPath) : string.Empty;
            }
        }
    }
}
