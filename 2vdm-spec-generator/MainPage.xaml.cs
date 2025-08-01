using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator
{
    public partial class MainPage : ContentPage
    {

        public MainPage(MainViewModel viewModel)
        {
            try
            {
                InitializeComponent();
                BindingContext = viewModel;
            }
            catch (Exception ex)
            {
                // エラーメッセージをコンソールに出力
                // なぜだか以下のコンソールログ出力機能を追加するとエラーが発生しなくなった（気のせいかもしれないが一応メモ）
                Console.WriteLine($"ページの初期化中にエラーが発生しました:");
                Console.WriteLine($"エラーメッセージ: {ex.Message}");
                Console.WriteLine($"スタックトレース: {ex.StackTrace}");
                // エラーメッセージを表示
                Content = new VerticalStackLayout
                {
                    Children =
                    {
                        new Label { Text = "ページの初期化中にエラーが発生しました:" },
                        new Label { Text = ex.Message },
                        new Label { Text = ex.StackTrace }
                    }
                };
            }
        }

        private async void OnGoToStartPageClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//StartPage");
        }


    }

}
