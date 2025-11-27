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

            // PositionsChanged を購読して ViewModel に位置情報と Markdown の再書き込みを行う
            if (this.BindingContext is NoCodePageViewModel vm)
            {
                _diagramRenderer.PositionsChanged = elements =>
                {
                    // UI スレッドで処理
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            // 1) ViewModel に位置を保存（JSON）
                            vm.SaveGuiPositions(elements);

                            // 2) 現在選択中の Markdown ファイルがあれば、そのファイル内の
                            //    有効ボタン一覧 / イベント一覧の順序を elements の Y 順に合わせて上書きする
                            var mdPath = vm.SelectedItem?.FullPath;
                            if (!string.IsNullOrWhiteSpace(mdPath) && File.Exists(mdPath))
                            {
                                UiToMarkdownConverter.UpdateMarkdownOrder(mdPath, elements);

                                // Markdown と VDM++ を再読み込み / 再生成して ViewModel に反映
                                var newMd = File.ReadAllText(mdPath);
                                vm.MarkdownContent = newMd;

                                var vdmConv = new MarkdownToVdmConverter();
                                var newVdm = vdmConv.ConvertToVdm(newMd);
                                vm.VdmContent = newVdm;
                                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);
                            }

                            // 3) ViewModel の GuiElements を最新の順序に合わせて更新（UI 再描画用）
                            vm.GuiElements = new ObservableCollection<GuiElement>(elements.Select(e => e));

                            // 4) Renderer を再描画（要素参照は同じなので位置が反映される）
                            _diagramRenderer.Render(vm.GuiElements);
                        }
                        catch
                        {
                            // 失敗しても UI を壊したくないため無視（必要ならログ出力を追加）
                        }
                    });
                };
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is NoCodePageViewModel vm)
            {
                // 初回描画
                _diagramRenderer.Render(vm.GuiElements);

                // 一度だけ登録する（重複登録防止）
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
