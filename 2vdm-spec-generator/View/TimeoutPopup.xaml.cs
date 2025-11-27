using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System;

namespace _2vdm_spec_generator.View
{
    public partial class TimeoutPopup : Popup
    {
        public TimeoutPopup()
        {
            InitializeComponent();
            SecondsEntry?.Focus();
        }

        // オーバーレイのタップを受けても何もしない（タップを消費してポップアップが閉じるのを防ぐ）
        private void OnBackgroundTapped(object sender, EventArgs e)
        {
            // 意図的に何もしない ? これで背景タップを消費する
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        private void OnConfirmClicked(object sender, EventArgs e)
        {
            if (!int.TryParse(SecondsEntry.Text?.Trim(), out int seconds) || seconds <= 0)
            {
                Application.Current?.MainPage?.DisplayAlert("入力エラー", "有効な秒数を入力してください（正の整数）。", "OK");
                return;
            }

            var target = TargetEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                Application.Current?.MainPage?.DisplayAlert("入力エラー", "遷移先（Target）を入力してください。", "OK");
                return;
            }

            Close((seconds, target));
        }
    }
}
