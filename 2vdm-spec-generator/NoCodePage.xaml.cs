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
                            _diagramRenderer.Render(vm.GuiElements, vm.ScreenNamesForRenderer);
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

                // 変更：右クリックで Screen ノードを開く → ダブルクリックで Screen ノードを開く
                _diagramRenderer.NodeDoubleClicked = el =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _ = vm.OpenFileForScreen(el?.Name);
                    });
                };

                _diagramRenderer.NodeContextRequested = (el, actionKey) =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            if (this.BindingContext is not NoCodePageViewModel vm) return;
                            if (el == null) return;

                            // 選択状態を設定（ViewModel 側で選択変更時の副作用がある）
                            vm.SelectedGuiElement = el;

                            // actionKey が分岐指定の場合は SelectedBranchIndex をセット
                            int? branchIndex = null;
                            if (!string.IsNullOrWhiteSpace(actionKey) && actionKey.StartsWith("branch:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(actionKey.Substring("branch:".Length), out int idx))
                                    branchIndex = idx;
                            }
                            vm.SelectedBranchIndex = branchIndex;

                            // 表示する選択肢を組み立て
                            string title = branchIndex.HasValue ? "分岐操作" : "ノード操作";
                            string[] options;
                            if (branchIndex.HasValue)
                            {
                                options = new[] { "編集", "削除" };
                            }
                            else
                            {
                                options = new[] { "編集", "削除", "プロパティ" };
                            }

                            var choice = await Shell.Current.DisplayActionSheet(title, "キャンセル", null, options);

                            if (string.IsNullOrWhiteSpace(choice) || choice == "キャンセル") return;

                            if (choice == "削除")
                            {
                                // ViewModel の既存削除処理を利用
                                await vm.DeleteSelectedGuiElementAsync();
                                return;
                            }

                            if (choice == "プロパティ")
                            {
                                // 簡易プロパティ表示
                                var sb = new System.Text.StringBuilder();
                                sb.AppendLine($"種類: {el.Type}");
                                sb.AppendLine($"名前: {el.Name}");
                                if (!string.IsNullOrWhiteSpace(el.Target)) sb.AppendLine($"ターゲット: {el.Target}");
                                if (!string.IsNullOrWhiteSpace(el.Description)) sb.AppendLine($"説明: {el.Description}");
                                await Application.Current.MainPage.DisplayAlert("プロパティ", sb.ToString(), "OK");
                                return;
                            }

                            if (choice == "編集")
                            {
                                // 編集は現状未実装（ここで名前変更などの実装を追加可能）
                                await Application.Current.MainPage.DisplayAlert("編集", "編集機能は未実装です。", "OK");
                                return;
                            }
                        }
                        catch
                        {
                            // 優雅に失敗（必要ならログ追加）
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
                _diagramRenderer.Render(vm.GuiElements, vm.ScreenNamesForRenderer);

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
                    _diagramRenderer.Render(vm.GuiElements, vm.ScreenNamesForRenderer);
                });
            }
        }
    }
}
