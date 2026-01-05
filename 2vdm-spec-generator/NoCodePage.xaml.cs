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

                            // 表示する選択肢を組み立て（ノード種別ごと）
                            string title = branchIndex.HasValue ? "分岐操作" : "ノード操作";
                            string[] options;

                            if (branchIndex.HasValue)
                            {
                                // 分岐は基本これだけでOK（必要なら「分岐コピー」「分岐追加」なども足せる）
                                options = new[] { "分岐編集", "分岐削除" };
                            }
                            else
                            {
                                options = el.Type switch
                                {
                                    GuiElementType.Button => new[] { "イベント追加","コピー", "貼り付け", "ボタン名変更", "削除" },
                                    GuiElementType.Screen => new[] { "開く", "コピー", "貼り付け", "画面名変更", "削除"},
                                    GuiElementType.Event => el.IsConditional
                                                                ? new[] { "分岐編集", "削除" }
                                                                : new[] { "イベント変更", "削除" },
                                    GuiElementType.Timeout => new[] { "タイムアウト編集", "削除" },
                                    _ => new[] { "編集", "削除" }
                                };
                            }

                            var choice = await Shell.Current.DisplayActionSheet(title, "キャンセル", null, options);

                            if (string.IsNullOrWhiteSpace(choice) || choice == "キャンセル") return;
                            if(choice == "開く")
{
                                _ = vm.OpenFileForScreen(el?.Name);
                                return;
                            }

                            if (choice == "コピー")
                            {
                                await vm.CopySelectedNodeAsync();
                                return;
                            }

                            if (choice == "貼り付け")
                            {
                                await vm.PasteCopiedNodeAsync();
                                return;
                            }

                            if (choice == "ボタン名変更")
                            {
                                await vm.RenameSelectedButtonAsync();
                                return;
                            }

                            if (choice == "画面名変更")
                            {
                                await vm.RenameSelectedScreenAsync();
                                return;
                            }

                            if (choice == "イベント変更")
                            {
                                await vm.EditSelectedEventAsync();
                                return;
                            }

                            if (choice == "タイムアウト編集")
                            {
                                await vm.EditSelectedNodeAsync();
                                return;
                            }

                            if (choice == "分岐編集")
                            {
                                // 分岐が選択されていない場合でも、分岐イベントなら「どの分岐を編集するか」UIにするなど拡張可能
                                await vm.EditSelectedBranchAsync();
                                return;
                            }

                            if (choice == "分岐削除" || choice == "削除")
                            {
                                // 既存削除（分岐選択時は分岐削除、通常はノード削除にしてもよい）
                                // いまの DeleteSelectedGuiElementAsync が分岐対応済みならそのまま使える
                                vm.DeleteSelectedGuiElementCommand?.Execute(null);
                                return;
                            }

                            if (choice == "編集")
                            {
                                // 汎用編集（EditSelectedNodeAsync がある場合）
                                await vm.EditSelectedNodeAsync();
                                return;
                            }
                            if (choice == "イベント追加")
                            {
                                vm.AddEventCommand.Execute(null);
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
