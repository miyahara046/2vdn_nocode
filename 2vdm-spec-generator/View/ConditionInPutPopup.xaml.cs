using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;

namespace _2vdm_spec_generator.View
{
    public partial class ConditionInputPopup : Popup
    {
        enum TabType { Transition, Add, Delete, Other }
        private TabType _selected = TabType.Add;

        // フラグ: 条件入力を必須にするか
        public bool RequireCondition { get; set; } = true;

        // Windows ネイティブハンドラを二重登録しないように追跡
        private readonly HashSet<Microsoft.Maui.Controls.Entry> _nativeHandlerAttached = new();

        public ConditionInputPopup()
        {
            InitializeComponent();
            SetSelectedTab(TabType.Add);

            // RequireCondition に応じた UI 表示を初期化
            UpdateRequireCondition();

            // Enter キーで確定／フォーカス移動: Completed を接続
            try
            {
                ConditionEntry.Completed += ConditionEntry_Completed;
                TargetEntry.Completed += TargetEntry_Completed;
            }
            catch
            {
                // 無視
            }

            // Windows では Esc を捕まえてキャンセルするためネイティブキーイベントを接続
            ConditionEntry.HandlerChanged += (s, e) => AttachWinEscapeHandlerIfNeeded(ConditionEntry);
            TargetEntry.HandlerChanged += (s, e) => AttachWinEscapeHandlerIfNeeded(TargetEntry);
        }

        private void ConditionEntry_Completed(object sender, EventArgs e)
        {
            // 条件が不要な場合はそのまま確定（Delete などもある）
            if (!RequireCondition)
            {
                OnOkClicked(this, EventArgs.Empty);
                return;
            }

            // ターゲット欄が表示されていればフォーカス移動、なければ確定
            if (TargetEntry.IsVisible)
            {
                TargetEntry?.Focus();
            }
            else
            {
                OnOkClicked(this, EventArgs.Empty);
            }
        }

        private void TargetEntry_Completed(object sender, EventArgs e)
        {
            // 最終入力欄なので確定処理を呼ぶ
            OnOkClicked(this, EventArgs.Empty);
        }

#if WINDOWS
        // WinUI 用
        private void AttachWinEscapeHandlerIfNeeded(Entry entry)
        {
            if (entry == null) return;
            if (_nativeHandlerAttached.Contains(entry)) return;

            try
            {
                if (entry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox tb)
                {
                    tb.KeyDown += TextBox_KeyDown;
                    _nativeHandlerAttached.Add(entry);
                }
            }
            catch
            {
                // 接続できなくても継続
            }
        }

        private void TextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Esc キーで閉じる
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Close(null);
                e.Handled = true;
            }
        }
#endif

        /// <summary>
        /// RequireCondition の状態に応じて UI を更新する。
        /// (AddEventAsync などで popup.RequireCondition = false; popup.UpdateRequireCondition(); とする想定)
        /// </summary>
        public void UpdateRequireCondition()
        {
            try
            {
                ConditionEntry.IsVisible = RequireCondition;
                if (!RequireCondition)
                {
                    // 条件が不要な場合の補助文
                    HintLabel.Text = "条件は不要です。ターゲットのみ入力してください。";
                }
                else
                {
                    // 条件が必要な場合は現在タブに応じた表示を行う
                    SetSelectedTab(_selected);
                }
            }
            catch
            {
                // 無視
            }
        }

        private void SetSelectedTab(TabType t)
        {
            _selected = t;

            // タブのビジュアルリセット
            ResetTabVisuals();
            Color selectedBg = Color.FromArgb("#007AFF");
            Color selectedText = Colors.White;

            // TargetEntry の表示制御（タブによっては非表示にする）
            TargetEntry.IsVisible = true;

            switch (t)
            {
                case TabType.Transition:
                    BtnTransition.BackgroundColor = selectedBg;
                    BtnTransition.TextColor = selectedText;
                    TargetEntry.Placeholder = "例: 画面Cへ";
                    HintLabel.Text = "画面遷移の指定です。表示例: 画面Cへ";
                    break;
                case TabType.Add:
                    BtnAdd.BackgroundColor = selectedBg;
                    BtnAdd.TextColor = selectedText;
                    TargetEntry.Placeholder = "例: 1";
                    HintLabel.Text = "表示部への追加操作などを指定します。";
                    break;
                case TabType.Delete:
                    BtnDelete.BackgroundColor = selectedBg;
                    BtnDelete.TextColor = selectedText;
                    // 削除はターゲット入力不要
                    TargetEntry.IsVisible = false;
                    HintLabel.Text = "削除操作を選択しています。OKで削除指定として扱います。";
                    break;
                case TabType.Other:
                    BtnOther.BackgroundColor = selectedBg;
                    BtnOther.TextColor = selectedText;
                    TargetEntry.Placeholder = "その他の操作内容を入力してください";
                    HintLabel.Text = "その他の操作を指定します。";
                    break;
            }

            // 条件入力の可視性を再設定
            ConditionEntry.IsVisible = RequireCondition;
        }

        private void ResetTabVisuals()
        {
            Color defaultBg = Colors.Transparent;
            var defaultTextLight = Color.FromArgb("#222222");

            try
            {
                if (App.Current?.Resources != null)
                {
                    if (App.Current.Resources.TryGetValue("Transparent", out var obj) && obj is Color c)
                    {
                        defaultBg = c;
                    }
                }
            }
            catch
            {
                // 無視
            }

            BtnTransition.BackgroundColor = defaultBg;
            BtnTransition.TextColor = defaultTextLight;
            BtnAdd.BackgroundColor = defaultBg;
            BtnAdd.TextColor = defaultTextLight;
            BtnDelete.BackgroundColor = defaultBg;
            BtnDelete.TextColor = defaultTextLight;
            BtnOther.BackgroundColor = defaultBg;
            BtnOther.TextColor = defaultTextLight;
        }

        private void OnTabClicked(object sender, EventArgs e)
        {
            if (sender == BtnTransition) SetSelectedTab(TabType.Transition);
            else if (sender == BtnAdd) SetSelectedTab(TabType.Add);
            else if (sender == BtnDelete) SetSelectedTab(TabType.Delete);
            else if (sender == BtnOther) SetSelectedTab(TabType.Other);

            // 条件入力の可視性を反映
            ConditionEntry.IsVisible = RequireCondition;
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        // OK ボタンおよび Entry.Completed から呼ばれる共通ハンドラ
        private void OnOkClicked(object sender, EventArgs e)
        {
            var condition = ConditionEntry.Text?.Trim();

            // 条件必須のときはチェック
            if (RequireCondition && string.IsNullOrEmpty(condition))
            {
                Application.Current?.MainPage?.DisplayAlert("入力エラー", "条件を入力してください。", "OK");
                return;
            }

            string target;

            if (_selected == TabType.Delete)
            {
                // 削除タブの場合は固定のターゲット表現
                target = "削除";
            }
            else
            {
                target = TargetEntry.Text?.Trim();

                if (string.IsNullOrEmpty(target))
                {
                    Application.Current?.MainPage?.DisplayAlert("入力エラー", "ターゲットを入力してください。", "OK");
                    return;
                }

                // 各タブに対応した整形
                if (_selected == TabType.Transition)
                {
                    if (!target.EndsWith("へ", StringComparison.Ordinal))
                        target += "へ";
                }
                else if (_selected == TabType.Add)
                {
                    if (!target.StartsWith("表示部に", StringComparison.Ordinal))
                        target = "表示部に" + target;
                    if (!target.EndsWith("を追加", StringComparison.Ordinal))
                        target = target + "を追加";
                }
                // Other はそのまま
            }

            if (!RequireCondition) condition = null;

            Close((condition, target));
        }
    }
}
