using System.ComponentModel;

namespace _2vdm_spec_generator.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        string text;

        public string Text
        {
            get => text;
            set
            {
                text = value;
                OnPropertyChanged(nameof(Text));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
