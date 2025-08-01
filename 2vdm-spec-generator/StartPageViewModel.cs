using System.Windows.Input;

namespace _2vdm_spec_generator
{
    public class StartPageViewModel
    {
        public ICommand GoToMainPageCommand { get; }
        public ICommand GoToNoCodePageCommand { get; }

        public StartPageViewModel()
        {
            GoToMainPageCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync("//MainPage");
            });

            GoToNoCodePageCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync("//NoCodePage");
            });
        }
    }
}
