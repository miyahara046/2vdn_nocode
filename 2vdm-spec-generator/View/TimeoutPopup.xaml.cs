using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;

namespace _2vdm_spec_generator.View
{
    public partial class TimeoutPopup : Popup
    {
        // Windows ネイティブハンドラを二重登録しないように追跡
        private readonly HashSet<Entry> _nativeHandlerAttached = new();

        public TimeoutPopup()
        {
            InitializeComponent();
            // フォーカスしておく（Windows での動作を想定）
            SecondsEntry?.Focus();

            // Enter キーで確定／フォーカス移動: Completed を接続
            try
            {
                SecondsEntry.Completed += SecondsEntry_Completed;
                TargetEntry.Completed += TargetEntry_Completed;
            }
            catch
            {
                // 無視
            }

            // Windows では Esc を捕まえてキャンセルするためネイティブキーイベントを接続
            SecondsEntry.HandlerChanged += (s, e) => AttachWinEscapeHandlerIfNeeded(SecondsEntry);
            TargetEntry.HandlerChanged += (s, e) => AttachWinEscapeHandlerIfNeeded(TargetEntry);
        }

        private void SecondsEntry_Completed(object sender, EventArgs e)
        {
            // Enter でターゲット入力へフォーカス
            TargetEntry?.Focus();
        }

        private void TargetEntry_Completed(object sender, EventArgs e)
        {
            // 最終入力欄なので確定処理を呼ぶ
            OnConfirmClicked(this, EventArgs.Empty);
        }

#if WINDOWS
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
                // 無視
            }
        }

        private void TextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Close(null);
                e.Handled = true;
            }
        }
#endif

        // 背景クリック時の処理（現在は何もしない）
        private void OnBackgroundTapped(object sender, EventArgs e)
        {
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        // OK / Enter 押下時の共通処理
        private void OnConfirmClicked(object sender, EventArgs e)
        {
            if (!int.TryParse(SecondsEntry.Text?.Trim(), out int seconds) || seconds <= 0)
            {
                Application.Current?.MainPage?.DisplayAlert("入力エラー", "正の整数で秒数を入力してください。", "OK");
                return;
            }

            var target = TargetEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                Application.Current?.MainPage?.DisplayAlert("入力エラー", "遷移先を入力してください。", "OK");
                return;
            }

            Close((seconds, target));
        }
    }
}
