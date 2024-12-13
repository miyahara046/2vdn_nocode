namespace _2vdm_spec_generator
{
    public partial class App : Application
    {
        public App(IServiceProvider services)
        {
            InitializeComponent();

            MainPage = new AppShell(services);
        }
    }
}
