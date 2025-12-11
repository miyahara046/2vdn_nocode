using _2vdm_spec_generator.View;
using _2vdm_spec_generator.ViewModel;
using _2vdm_spec_generator.Services;
using Microsoft.Maui.Controls;
using System.ComponentModel;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Maui.ApplicationModel; // MainThread

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

            if (this.BindingContext is NoCodePageViewModel vm)
            {
                _diagramRenderer.PositionsChanged = elements =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            vm.SaveGuiPositions(elements);

                            var mdPath = vm.SelectedItem?.FullPath;
                            if (!string.IsNullOrWhiteSpace(mdPath) && File.Exists(mdPath))
                            {
                                UiToMarkdownConverter.UpdateMarkdownOrder(mdPath, elements);

                                var newMd = File.ReadAllText(mdPath);
                                vm.MarkdownContent = newMd;

                                var vdmConv = new MarkdownToVdmConverter();
                                var newVdm = vdmConv.ConvertToVdm(newMd);
                                vm.VdmContent = newVdm;
                                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);
                            }

                            vm.GuiElements = new ObservableCollection<GuiElement>(elements.Select(e => e));
                            _diagramRenderer.Render(vm.GuiElements);
                        }
                        catch
                        {
                        }
                    });
                };

                // 既存：ノード選択時は SelectedGuiElement を設定
                _diagramRenderer.NodeClicked = el =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        vm.SelectedGuiElement = el;
                        vm.SelectedBranchIndex = null; // ノードクリックなら分岐選択は解除
                    });
                };

                // 追加：分岐がタップされたときに親イベントと分岐インデックスを受け取る
                _diagramRenderer.BranchClicked = (parent, index) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        vm.SelectedGuiElement = parent;
                        vm.SelectedBranchIndex = index;
                    });
                };

                // 追加：右クリックで Screen ノードを開く
                _diagramRenderer.NodeRightClicked = el =>
                {
                    // 非同期メソッドを fire-and-forget で呼び出す（UI スレッドから起動）
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _ = vm.OpenFileForScreen(el?.Name);
                    });
                };
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is NoCodePageViewModel vm)
            {
                _diagramRenderer.Render(vm.GuiElements);

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
