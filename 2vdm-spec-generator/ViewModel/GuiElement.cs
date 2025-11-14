using System;
using System.ComponentModel;

namespace _2vdm_spec_generator.ViewModel
{
    public class GuiElement : INotifyPropertyChanged
    {
        public GuiElementType Type { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public string Description { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        // ==== サイズ（Drawable と合わせる） ====
        public float Width { get; set; } = 160;
        public float Height { get; set; } = 50;

        // ==== 選択状態 ====
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        // ==== 固定ノード（Timeout 固定） ====
        public bool IsFixed { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public enum GuiElementType
    {
        Screen,
        Button,
        Event,
        Timeout,
        Operation
    }
}
