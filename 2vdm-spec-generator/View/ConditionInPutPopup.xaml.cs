using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System;

namespace _2vdm_spec_generator.View
{
    public partial class ConditionInputPopup : Popup
    {
        enum TabType { Transition, Add, Delete, Other }
        private TabType _selected = TabType.Add;

        // 追加: 条件入力が必須かどうか（外部から設定可能）
        public bool RequireCondition { get; set; } = true;

        public ConditionInputPopup()
        {
            InitializeComponent();
            SetSelectedTab(TabType.Add);

            // 初期表示は RequireCondition に従う
            UpdateRequireCondition();
        }

        /// <summary>
        /// RequireCondition を外部で設定したあとに呼んで UI に反映させるためのメソッド。
        /// (AddEventAsync から popup.RequireCondition = false; popup.UpdateRequireCondition(); のように使う)
        /// </summary>
        public void UpdateRequireCondition()
        {
            try
            {
                ConditionEntry.IsVisible = RequireCondition;
                if (!RequireCondition)
                {
                    // 条件不要時の案内
                    HintLabel.Text = "条件は不要です。遷移/操作内容を入力してください。";
                }
                else
                {
                    // 条件必須時は既存のタブ説明に委ねる
                    SetSelectedTab(_selected);
                }
            }
            catch
            {
                // 安全に無視
            }
        }

        private void SetSelectedTab(TabType t)
        {
            _selected = t;

            // 視覚的に選択中タブを強調（背景色・文字色を切り替え）
            ResetTabVisuals();
            Color selectedBg = Color.FromArgb("#007AFF");
            Color selectedText = Colors.White;

            // デフォルト：TargetEntry を表示
            TargetEntry.IsVisible = true;

            switch (t)
            {
                case TabType.Transition:
                    BtnTransition.BackgroundColor = selectedBg;
                    BtnTransition.TextColor = selectedText;
                    TargetEntry.Placeholder = "例: 画面Cへ";
                    HintLabel.Text = "画面遷移先を入力してください（例: 画面A）。";
                    break;
                case TabType.Add:
                    BtnAdd.BackgroundColor = selectedBg;
                    BtnAdd.TextColor = selectedText;
                    TargetEntry.Placeholder = "例: 1";
                    HintLabel.Text = "表示部へ追加するものを記入してください";
                    break;
                case TabType.Delete:
                    BtnDelete.BackgroundColor = selectedBg;
                    BtnDelete.TextColor = selectedText;
                    // 削除は入力欄を消して固定文字列扱いにする
                    TargetEntry.IsVisible = false;
                    HintLabel.Text = "OK を押すと削除処理が設定されます。";
                    break;
                case TabType.Other:
                    BtnOther.BackgroundColor = selectedBg;
                    BtnOther.TextColor = selectedText;
                    TargetEntry.Placeholder = "任意の操作や説明を入力してください";
                    HintLabel.Text = "その他の操作や注記を入力してください。";
                    break;
            }

            // 条件欄の表示は RequireCondition に従う（外部設定を優先）
            ConditionEntry.IsVisible = RequireCondition;
        }

        private void ResetTabVisuals()
        {
            // タブの初期見た目（リソースから Transparent を取得する場合は TryGetValue の out 引数を使う）
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
                // リソース参照失敗は無視してデフォルトを使用
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

            // タブ切替後に RequireCondition の状態を再反映
            ConditionEntry.IsVisible = RequireCondition;
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        private void OnOkClicked(object sender, EventArgs e)
        {
            var condition = ConditionEntry.Text?.Trim();

            // 条件が必須の場合のみ検証
            if (RequireCondition && string.IsNullOrEmpty(condition))
            {
                Application.Current?.MainPage?.DisplayAlert("入力エラー", "条件を入力してください。", "OK");
                return;
            }

            string target;

            if (_selected == TabType.Delete)
            {
                // 削除タブ：固定文字列を設定
                target = "表示部の文字削除";
            }
            else
            {
                target = TargetEntry.Text?.Trim();

                if (string.IsNullOrEmpty(target))
                {
                    Application.Current?.MainPage?.DisplayAlert("入力エラー", "遷移先／操作内容を入力してください。", "OK");
                    return;
                }

                // タブごとの自動付加ルール
                if (_selected == TabType.Transition)
                {
                    // 末尾に全角「へ」がなければ追加
                    if (!target.EndsWith("へ", StringComparison.Ordinal))
                        target += "へ";
                }
                else if (_selected == TabType.Add)
                {
                    // 前に「表示部に」が無ければ追加
                    if (!target.StartsWith("表示部に", StringComparison.Ordinal))
                        target = "表示部に" + target;
                    // 後ろに「を追加」が無ければ追加
                    if (!target.EndsWith("を追加", StringComparison.Ordinal))
                        target = target + "を追加";
                }
                // Other タブはそのまま
            }

            // 条件が不要（非分岐）であれば condition を null にして返す（呼び出し側で無視可）
            if (!RequireCondition) condition = null;

            Close((condition, target));
        }
    }
}
