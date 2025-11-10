using CommunityToolkit.Maui.Views;

namespace _2vdm_spec_generator.View
{
    public partial class TimeoutPopup : Popup
    {
        public TimeoutPopup() => InitializeComponent();

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        private void OnConfirmClicked(object sender, EventArgs e)
        {
            if (int.TryParse(SecondsEntry.Text?.Trim(), out int seconds) &&
                !string.IsNullOrWhiteSpace(TargetEntry.Text))
            {
                Close((seconds, TargetEntry.Text.Trim()));
            }
            else
            {
                Application.Current?.MainPage?.DisplayAlert(
                    "入力エラー", "タイムアウト秒数と遷移先を正しく入力してください。", "OK");
            }
        }
    }
}
