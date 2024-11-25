using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator
{
    public partial class MainPage : ContentPage
    {

        public MainPage(MainViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

    }

}
