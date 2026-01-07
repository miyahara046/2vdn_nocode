using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace _2vdm_spec_generator.ViewModel
{
    // GUI 上に配置される要素（画面、ボタン、イベントなど）を表すクラス
    // MVVM パターンで ViewModel の一部として使われる
    public class GuiElement : INotifyPropertyChanged
    {
        // 要素の種類（画面、ボタン、イベントなど）
        public GuiElementType Type { get; set; }

        // 要素の名前（画面名・ボタン名・イベント名など）
        public string Name { get; set; }

        // 遷移先の画面名や、イベントのターゲット名など
        public string Target { get; set; }

        // UI 上では表示されないが、説明文や補足を保持しておきたいときに使用
        public string Description { get; set; }

        // 配置位置（左上の基準座標）
        // X：横方向の位置（ピクセル）
        public float X { get; set; }

        // Y：縦方向の位置（ピクセル）
        public float Y { get; set; }

        // ==== サイズ（Drawable と合わせる） ====
        // 描画部分（GraphicsView等）と数値を合わせるため、デフォルトの幅と高さを決めている
        public float Width { get; set; } = 160;
        public float Height { get; set; } = 50;

        // ==== 選択状態 ====
        // ユーザーが GUI 要素をクリックしたときに「選択されているか」を保持する
        private bool _isSelected;

        // IsSelected が変更されたときは PropertyChanged を発火して UI を更新する
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                // 値が変わった時だけ通知を送る（無駄な更新を防ぐ）
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));  // UI に「値が変わったよ」と知らせる
                }
            }
        }

        // ==== 固定ノード（Timeout 固定） ====
        // 例えば Timeout イベントのように、ユーザーが動かせない固定ノードかどうかのフラグ
        public bool IsFixed { get; set; } = false;

        // 分岐（条件分岐）を表す構造
        public class EventBranch
        {
            public string Condition { get; set; }
            public string Target { get; set; }
        }

        private List<EventBranch> _branches = new List<EventBranch>();

        /// <summary>
        /// 条件分岐を持つイベントの場合、各分岐を保持する。
        /// - 非条件イベントでは空リスト。
        /// </summary>
        public List<EventBranch> Branches
        {
            get => _branches;
            set
            {
                if (!ReferenceEquals(_branches, value))
                {
                    _branches = value ?? new List<EventBranch>();
                    OnPropertyChanged(nameof(Branches));
                    OnPropertyChanged(nameof(IsBranch));
                    OnPropertyChanged(nameof(IsConditional)); // 互換通知
                }
            }
        }

        /// <summary>
        /// Branches が1つ以上あるかどうか（条件分岐イベント判定用）
        /// </summary>
        public bool IsBranch => Branches != null && Branches.Any();

        /// <summary>
        /// 旧名互換（以前は IsConditional として参照していた）
        /// </summary>
        public bool IsConditional => IsBranch;

        // プロパティ値が変更されたことを UI に通知するためのイベント
        public event PropertyChangedEventHandler PropertyChanged;

        // 呼び出されると PropertyChanged イベントを実行し、UI のバインディングを更新する
        protected virtual void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // GUI 要素の種類を列挙した Enum
    // 画面、ボタン、イベント、タイムアウト、操作などを分類する目的で使用
    public enum GuiElementType
    {
        Screen,     // 画面（UI画面）
        Button,     // ボタン
        Event,      // イベント
        Timeout,    // タイムアウトイベント
        Operation   // 操作（内部処理）
    }
}
