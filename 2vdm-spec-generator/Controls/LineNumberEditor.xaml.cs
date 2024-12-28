using Microsoft.Maui.Controls;

namespace _2vdm_spec_generator.Controls
{
    public partial class LineNumberEditor : ContentView
    {
        public static readonly BindableProperty EditorTextProperty =
            BindableProperty.Create(nameof(EditorText), typeof(string),
                typeof(LineNumberEditor), string.Empty,
                propertyChanged: OnEditorTextChanged,
                defaultBindingMode: BindingMode.TwoWay);

        public string EditorText
        {
            get => (string)GetValue(EditorTextProperty);
            set => SetValue(EditorTextProperty, value);
        }

        public LineNumberEditor()
        {
            InitializeComponent();
            UpdateLineNumbers();
        }

        private static void OnEditorTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is LineNumberEditor editor)
            {
                editor.MainEditor.Text = (string)newValue;
                editor.UpdateLineNumbers();
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            EditorText = MainEditor.Text;
            UpdateLineNumbers();
        }

        private void OnEditorScrolled(object sender, ScrolledEventArgs e)
        {
            LineNumberScroll.ScrollToAsync(0, e.ScrollY, false);
        }

        private void UpdateLineNumbers()
        {
            var text = MainEditor.Text ?? string.Empty;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lineCount = lines.Length;
            var numbers = string.Join("\n", Enumerable.Range(1, lineCount));
            LineNumbers.Text = numbers;
        }
    }
} 