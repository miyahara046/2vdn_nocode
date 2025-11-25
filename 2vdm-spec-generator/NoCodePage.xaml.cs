using _2vdm_spec_generator.Services;
using _2vdm_spec_generator.View;
using _2vdm_spec_generator.ViewModel;
using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace _2vdm_spec_generator
{
    public partial class NoCodePage : ContentPage
    {
        private GuiDiagramRenderer _diagramRenderer;

        public NoCodePage()
        {
            InitializeComponent();
            this.BindingContext = new NoCodePageViewModel();

            _diagramRenderer = new GuiDiagramRenderer();
            DiagramContainer.Content = _diagramRenderer;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is NoCodePageViewModel vm)
            {
                // ‰‰ñ•`‰æ
                _diagramRenderer.Render(vm.GuiElements);

                // ƒhƒ‰ƒbƒO‚É‚æ‚éˆÊ’u•Ï‰»‚ð VM ‚É’Ê’m‚µ‚Ä•Û‘¶‚·‚é
                _diagramRenderer.PositionsChanged = (elements) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        vm.SaveGuiPositions(elements); // Šù‘¶Fpositions.json •Û‘¶
                                                       // + Markdown ‚Ì•À‚Ñ‘Ö‚¦‚às‚¤
                        try
                        {
                            UiToMarkdownConverter.UpdateMarkdownOrder(vm.SelectedItem.FullPath, elements);
                        }
                        catch { /* ƒGƒ‰[‚ÍUI‚ð‰ó‚³‚È‚¢‚æ‚¤‚É–³Ž‹ */ }
                    });
                };


                // ˆê“x‚¾‚¯“o˜^‚·‚éid•¡“o˜^–hŽ~j
                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is NoCodePageViewModel vm &&
                e.PropertyName == nameof(vm.GuiElements))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _diagramRenderer.Render(vm.GuiElements);
                });
            }
        }
    }
}
