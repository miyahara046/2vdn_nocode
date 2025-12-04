using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;
using System.Collections.Generic;
using _2vdm_spec_generator.ViewModel;
using Microsoft.Maui.ApplicationModel; // MainThread

#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
#endif

// アプリ固有の View 名前空間を定義する
namespace _2vdm_spec_generator.View
{
    // GUI 図のレンダラーを表すクラス定義
    public class GuiDiagramRenderer : ContentView
    {
        // 描画用 GraphicsView の参照を保持するフィールド
        private readonly GraphicsView _graphicsView;
        // 実際の描画ロジックを持つ Drawable の参照
        private readonly GuiDiagramDrawable _drawable;
        // 描画をスクロールするための ScrollView
        private readonly ScrollView _scrollView;

        // 現在ドラッグ中のノード参照
        private GuiElement _draggedNode = null;
        // ドラッグ開始時のポインタとノード位置のオフセット
        private PointF _dragOffset;

        // 垂直スナップ幅（グリッド）を定義
        private const float SnapSize = 80f;
        // 左列の X 座標を定義
        private const float LeftColumnX = 40f;
        // 右列の X 座標を定義
        private const float RightColumnX = 350f;

        // ノード位置変更を通知するコールバック（外部で購読可能）
        public Action<IEnumerable<GuiElement>> PositionsChanged { get; set; }

        // ノードがクリックされたことを通知するコールバック
        public Action<GuiElement> NodeClicked { get; set; }

        // 追加：分岐がタップされたときに親イベントと分岐インデックスを渡すコールバック
        public Action<GuiElement, int?> BranchClicked { get; set; }

        // 外部から要素リストをセットし、描画を更新するメソッド
        public void SetElements(IEnumerable<GuiElement> elements)
        {
            // Drawable に要素のコピーを設定する
            _drawable.Elements = elements.ToList();
            // 自動配置を行う
            _drawable.ArrangeNodes();
            // GraphicsView を再描画するよう要求する
            _graphicsView?.Invalidate();
        }

        // コンストラクタ：Drawable / GraphicsView を初期化し、外側の ScrollView がある想定で GraphicsView を直接 Content に設定します
        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();

            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.White,
                InputTransparent = false
            };

            // 重要: 内部で ScrollView を作らない（親の XAML ScrollView に委ねる）
            Content = _graphicsView;

            // GraphicsView のハンドラー変更イベントを監視してプラットフォーム固有の入力をフックする
            _graphicsView.HandlerChanged += GraphicsView_HandlerChanged;

#if !WINDOWS
            var pan = new PanGestureRecognizer();
            pan.PanUpdated += NonWindows_PanUpdated;
            _graphicsView.GestureRecognizers.Add(pan);
#endif
        }

        // GraphicsView のハンドラーが変化したときに呼ばれる（プラットフォーム固有フック用）
        private void GraphicsView_HandlerChanged(object sender, EventArgs e)
        {
#if WINDOWS
            // 既存のハンドラーがある場合はイベントを解除する
            if (_graphicsView.Handler?.PlatformView is UIElement oldEl)
            {
                oldEl.PointerPressed -= Platform_PointerPressed;
                oldEl.PointerMoved -= Platform_PointerMoved;
                oldEl.PointerReleased -= Platform_PointerReleased;
                oldEl.PointerWheelChanged -= Platform_PointerWheelChanged; // 追加：解除
            }

            // 新しいハンドラーがある場合はプラットフォームのポインタイベントを登録する
            if (_graphicsView.Handler?.PlatformView is UIElement el)
            {
                el.PointerPressed += Platform_PointerPressed;
                el.PointerMoved += Platform_PointerMoved;
                el.PointerReleased += Platform_PointerReleased;
                el.PointerWheelChanged += Platform_PointerWheelChanged; // 追加：登録
            }
#endif
        }

#if WINDOWS
        // Windows 向け：ポインタ押下ハンドラ
        private void Platform_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // PlatformView を UIElement として取得できない場合は無視
            if (!(sender is UIElement ui)) return;
            // 押下位置を取得してドラッグ開始を試みる
            var pt = e.GetCurrentPoint(ui).Position;
            TryStartDrag((float)pt.X, (float)pt.Y);
        }

        // Windows 向け：ポインタ移動ハンドラ
        private void Platform_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // ドラッグ対象がない場合は処理しない
            if (_draggedNode == null) return;
            if (!(sender is UIElement ui)) return;
            var pt = e.GetCurrentPoint(ui).Position;
            ContinueDrag((float)pt.X, (float)pt.Y);
        }

        // Windows 向け：ポインタ解放ハンドラ
        private void Platform_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // ドラッグ中であれば終了処理を行う
            if (_draggedNode != null)
            {
                FinishDrag(_draggedNode);
            }
        }

        // Windows 向け：マウスホイール処理を親の ScrollView に渡す
        private void Platform_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (_scrollView == null) return;
                if (!(sender is UIElement ui)) return;

                var pt = e.GetCurrentPoint(ui);
                // MouseWheelDelta は通常 ±120 / notch
                int delta = pt.Properties.MouseWheelDelta;

                // 方向は環境によるが ScrollToAsync は Y 座標増加で下方向スクロールになるため符号を反転
                double scrollDelta = -delta / 3.0; // 感度調整: 必要なら調整して下さい

                // UI スレッドで実行
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        double newY = Math.Max(0.0, _scrollView.ScrollY + scrollDelta);
                        await _scrollView.ScrollToAsync(_scrollView.ScrollX, newY, false);
                    }
                    catch
                    {
                        // ignore
                    }
                });
            }
            catch
            {
                // 無視
            }
        }
#endif

#if !WINDOWS
        // 非 Windows 向け：パン開始時の元の位置を保持するためのフィールド
        private PointF? _panStartOriginalPos = null;
        // 非 Windows 向け：パン対象のノード参照
        private GuiElement _panTarget = null;

        // 非 Windows 向け：PanGesture の更新イベントハンドラ
        private void NonWindows_PanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // 開始時にドラッグ可能な近いノードを選ぶ（Y の絶対値で最も近いもの）
                    _panTarget = _drawable.Elements.Where(CanDrag).OrderBy(el => Math.Abs(el.Y)).FirstOrDefault();
                    if (_panTarget != null)
                    {
                        // 元の座標を保存して選択通知を出す
                        _panStartOriginalPos = new PointF(_panTarget.X, _panTarget.Y);
                        // 選択として通知（タップと同等の選択通知）
                        _panTarget.IsSelected = true;
                        NodeClicked?.Invoke(_panTarget);
                        _graphicsView?.Invalidate();
                    }
                    break;

                case GestureStatus.Running:
                    // 実際の移動中は Y を更新して再描画を要求する
                    if (_panTarget != null && _panStartOriginalPos.HasValue)
                    {
                        var newY = _panStartOriginalPos.Value.Y + (float)e.TotalY;
                        _panTarget.Y = newY;
                        ApplyFixedX(_panTarget);
                        _graphicsView?.Invalidate();
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    // 終了時は FinishDrag を呼んでスナップ・再配置などを行う
                    if (_panTarget != null)
                    {
                        FinishDrag(_panTarget);
                    }
                    _panTarget = null;
                    _panStartOriginalPos = null;
                    break;
            }
        }
#endif

        // ノードがドラッグ可能かを判定する（Timeout は不可）
        private bool CanDrag(GuiElement el)
        {
            if (el == null) return false;

            // タイムアウト要素自体は常にドラッグ不可
            if (el.Type == GuiElementType.Timeout) return false;

            // イベントで、ターゲットがタイムアウト要素に紐づいている場合はドラッグ不可にする
            if (el.Type == GuiElementType.Event && !string.IsNullOrWhiteSpace(el.Target))
            {
                // 完全一致をまず試す
                var timeoutExact = _drawable.Elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && string.Equals(e.Name, el.Target, StringComparison.Ordinal));
                if (timeoutExact != null) return false;

                // 部分一致（ターゲットにタイムアウト名が含まれる、またはその逆）も考慮
                var timeoutContains = _drawable.Elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && (el.Target.Contains(e.Name) || e.Name.Contains(el.Target)));
                if (timeoutContains != null) return false;
            }

            // 上記に該当しなければドラッグ可能
            return true;
        }

        // ドラッグ開始を試みる。ポインタ位置にあるノードを逆順（描画順）で検索する
        private void TryStartDrag(float pointerX, float pointerY)
        {
            // 描画リストを逆順で巡回して最前面のノードを見つける
            foreach (var el in _drawable.Elements.AsEnumerable().Reverse())
            {
                // ドラッグ可能でなければスキップ
                if (!CanDrag(el)) continue;

                // ノード矩形を生成してヒット判定を行う
                var rect = new RectF(el.X, el.Y, GuiDiagramDrawable.NodeWidth, GuiDiagramDrawable.NodeHeight);
                if (rect.Contains(pointerX, pointerY))
                {
                    // ドラッグ対象を設定してオフセットを保持する
                    _draggedNode = el;
                    _dragOffset = new PointF(pointerX - el.X, pointerY - el.Y);

                    // 選択状態をセットして再描画を要求する
                    el.IsSelected = true;
                    _graphicsView?.Invalidate();

                    // クリック／選択の通知（ViewModel 側へ）
                    NodeClicked?.Invoke(el);

                    return;
                }
            }

            // 既存ノードにヒットしなかった場合、分岐可視ノード（Condition / Target ダイヤ）をチェックして選択可能にする
            if (_drawable.BranchVisuals != null && _drawable.BranchVisuals.Count > 0)
            {
                // Draw と同じ計算式で矩形を作る（GuiDiagramDrawable と整合）
                float condW = GuiDiagramDrawable.NodeWidth * 0.9f;
                float condH = GuiDiagramDrawable.NodeHeight * 0.7f;
                float diamondW = GuiDiagramDrawable.NodeWidth * 0.8f;
                float diamondH = GuiDiagramDrawable.NodeHeight * 0.8f;
                float midGap = 24f;
                float condRightShift = 40f;

                foreach (var bv in _drawable.BranchVisuals)
                {
                    // condCenter / targetCenter を再計算
                    float condCenterX = bv.CenterX - (diamondW / 2f + midGap / 2f + condW / 2f) + condRightShift;
                    var condCenter = new PointF(condCenterX, bv.CenterY);
                    var condRect = new RectF(condCenter.X - condW / 2f, condCenter.Y - condH / 2f, condW, condH);

                    float targetCenterX = bv.CenterX + (diamondW / 2f + midGap / 2f);
                    var targetCenter = new PointF(targetCenterX, bv.CenterY);
                    // ダイヤは簡易的に矩形でヒット判定（十分な判定精度）
                    var diamondRect = new RectF(targetCenter.X - diamondW / 2f, targetCenter.Y - diamondH / 2f, diamondW, diamondH);

                    if (condRect.Contains(pointerX, pointerY) || diamondRect.Contains(pointerX, pointerY))
                    {
                        var parent = bv.ParentEvent;
                        if (parent != null)
                        {
                            // 選択として扱う（ドラッグは開始しない。親イベントを選択する）
                            parent.IsSelected = true;
                            _graphicsView?.Invalidate();

                            // ViewModel 側に通知（親イベントと分岐インデックスを渡す）
                            NodeClicked?.Invoke(parent);
                            BranchClicked?.Invoke(parent, bv.BranchIndex);
                            return;
                        }
                    }
                }
            }
        }

        // ドラッグ中の継続処理：Y を更新し X を固定方向に合わせる
        private void ContinueDrag(float pointerX, float pointerY)
        {
            // ドラッグ対象がなければ何もしない
            if (_draggedNode == null) return;

            // ポインタ位置からオフセットを差し引いて新しい Y を決定する
            float newY = pointerY - _dragOffset.Y;
            _draggedNode.Y = newY;
            // 種類に応じて X を固定するロジックを適用
            ApplyFixedX(_draggedNode);

            // 再描画を要求する
            _graphicsView?.Invalidate();
        }

        // ドラッグ終了処理：スナップ、再配置、通知を行う
        private void FinishDrag(GuiElement finishedNode)
        {
            // null チェック
            if (finishedNode == null) return;

            // Y をグリッドに合わせて丸める
            finishedNode.Y = Snap(finishedNode.Y, SnapSize);
            // X 固定ルールを適用
            ApplyFixedX(finishedNode);
            // 全体の縦順を再フローして整列させる
            ReflowVerticalOrdering();

            // 選択状態を解除する
            finishedNode.IsSelected = false;

            // 位置変更を購読者に通知する
            PositionsChanged?.Invoke(_drawable.Elements);

            // 再描画を要求する
            _graphicsView?.Invalidate();

            // 内部のドラッグ状態をクリアする
            _draggedNode = null;
        }

        // 種類に応じて X 座標や固定フラグを適用する
        private void ApplyFixedX(GuiElement el)
        {
            // null チェック
            if (el == null) return;

            // 要素種類ごとに X を決定する
            switch (el.Type)
            {
                case GuiElementType.Screen:
                case GuiElementType.Button:
                case GuiElementType.Operation:
                    // 左列に揃える
                    el.IsFixed = false;
                    el.X = LeftColumnX;
                    break;
                case GuiElementType.Event:
                    // イベント：タイムアウトに紐づく場合は移動不可として固定
                    if (!string.IsNullOrWhiteSpace(el.Target))
                    {
                        var timeoutEl = _drawable.Elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && (string.Equals(e.Name, el.Target, StringComparison.Ordinal) || el.Target.Contains(e.Name) || e.Name.Contains(el.Target)));
                        if (timeoutEl != null)
                        {
                            el.IsFixed = true;
                            // タイムアウトに紐づくイベントは左列（タイムアウト列）に寄せる等、見た目も固定する
                            el.X = RightColumnX;
                            break;
                        }
                    }
                    // 通常のイベントは右列に揃える
                    el.IsFixed = false;
                    el.X = RightColumnX;
                    break;
                case GuiElementType.Timeout:
                    // タイムアウトは移動不可として左列に配置する
                    el.IsFixed = true;
                    el.X = LeftColumnX;
                    break;
            }
        }

        // 全体の縦方向の順序を再計算してノードをスナップ整列する
        private void ReflowVerticalOrdering()
        {
            // タイムアウトは上部に固定してソートする
            var timeouts = _drawable.Elements.Where(e => e.Type == GuiElementType.Timeout).OrderBy(e => e.Y).ToList();
            // 移動可能なノードを Y 順で取得する
            var movable = _drawable.Elements.Where(e => e.Type != GuiElementType.Timeout).OrderBy(e => e.Y).ToList();

            // 基準 Y を決定する（タイムアウトの下から）
            float baseY = 8f;
            if (timeouts.Any())
            {
                var lastT = timeouts.Last();
                baseY = lastT.Y + GuiDiagramDrawable.NodeHeight + 8f;
            }

            // 移動可能ノードを等間隔で並べる
            float y = baseY;
            foreach (var el in movable)
            {
                el.Y = y;
                ApplyFixedX(el);
                y += SnapSize;
            }

            // レンダラーの再描画を要求する
            _graphicsView.Invalidate();
        }

        // 指定したグリッドに沿って値を丸めるユーティリティ
        private static float Snap(float value, float grid) => (float)(Math.Round(value / grid) * grid);

        // 外部からレンダリングを要求するメソッド（要素リストを設定して描画領域を更新）
        public void Render(IEnumerable<GuiElement> elements)
        {
            // null 安全にリスト化して内部 Drawable に設定する
            var list = elements?.ToList() ?? new();
            _drawable.Elements = list;
            _drawable.ArrangeNodes();

            // 必要な高さを計算して GraphicsView と ContentView に反映する
            float maxY = 0f;
            if (_drawable.Elements.Any())
                maxY = _drawable.Elements.Max(e => e.Y + GuiDiagramDrawable.NodeHeight);

            var height = Math.Max(maxY + 120f, 300f);
            var width = 1000f;

            // GraphicsView の要求サイズを設定する
            _graphicsView.HeightRequest = height;
            _graphicsView.WidthRequest = width;
            // ContentView 自身の高さは最大 800 に制限して設定する
            this.HeightRequest = Math.Min(height, 800);

            // レイアウト更新と再描画を要求する
            InvalidateMeasure();
            _graphicsView.Invalidate();
        }

        // 現在のドラッグを外部から強制終了するヘルパー
        public void FinishCurrentDrag() => FinishDrag(_draggedNode);

#if WINDOWS
        // ファイル内に次のメソッドを追加してください：
        private void ScrollView_HandlerChanged(object sender, EventArgs e)
        {
            // プラットフォームの古いハンドラがあれば解除
            if (_scrollView.Handler?.PlatformView is UIElement oldSv)
            {
                oldSv.PointerWheelChanged -= ScrollViewer_PointerWheelChanged;
            }

            // 新しいプラットフォーム View が来たら登録
            if (_scrollView.Handler?.PlatformView is UIElement newSv)
            {
                newSv.PointerWheelChanged += ScrollViewer_PointerWheelChanged;
            }
        }

        // ScrollView 側のホイールイベント。既定のスクロールを妨げないよう e.Handled を操作しないか false に設定します。
        // ここでは何もしない（必要ならログを追加）。
        private void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // 何もしない -> ネイティブのスクロール処理が動くはず
            // デバッグ確認用:
            // System.Diagnostics.Debug.WriteLine("ScrollViewer PointerWheel: " + e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta);
            // 明示的に伝搬させたいなら e.Handled = false;
        }
#endif
    }
}
