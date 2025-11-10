using CommunityToolkit.Maui.Views;

namespace _2vdm_spec_generator.View
{
    public partial class ConditionInputPopup : Popup
    {
        public ConditionInputPopup()
        {
            InitializeComponent();
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        private void OnOkClicked(object sender, EventArgs e)
        {
            var condition = ConditionEntry.Text?.Trim();
            var target = TargetEntry.Text?.Trim();

            if (string.IsNullOrEmpty(condition) || string.IsNullOrEmpty(target))
            {
                Application.Current?.MainPage?.DisplayAlert("“ü—ÍƒGƒ‰[", "—¼•û‚Ì—“‚ğ“ü—Í‚µ‚Ä‚­‚¾‚³‚¢B", "OK");
                return;
            }

            Close((condition, target));
        }
    }
}
