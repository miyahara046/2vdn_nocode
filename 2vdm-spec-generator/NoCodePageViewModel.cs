using System.Windows.Input;

public class NoCodePageViewModel
{
    public ICommand GoToStartPageCommand { get; }

    public NoCodePageViewModel()
    {
        GoToStartPageCommand = new Command(async () =>
        {
            await Shell.Current.GoToAsync("//StartPage");
        });
    }
}
