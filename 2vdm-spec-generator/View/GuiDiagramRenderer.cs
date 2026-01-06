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
using Windows.UI.Input; // for PointerPoint etc (API surfaces)
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
#endif

namespace _2vdm_spec_generator.View
{
    public class GuiDiagramRenderer : ContentView
    {
        private readonly GraphicsView _graphicsView;
        private readonly GuiDiagramDrawable _drawable;

        private GuiElement _draggedNode = null;
        private PointF _dragOffset;

        // 追加: ダブルクリック検出用フィールド
        private DateTime _lastLeftClickTime = DateTime.MinValue;
        private GuiElement _lastLeftClickElement = null;
        private const int DoubleClickThresholdMs = 400;

        private const float SnapSize = 50f;
        private const float LeftColumnX = 20f;
        private const float RightColumnX = 350f;

        public Action<IEnumerable<GuiElement>> PositionsChanged { get; set; }
        public Action<GuiElement> NodeClicked { get; set; }
        public Action<GuiElement, int?> BranchClicked { get; set; }

        // 既存: 右クリックの古い用途は置き換えます（画面はダブルクリックに）
        public Action<GuiElement> NodeRightClicked { get; set; }

        // 追加: ポップアップ（コンテキストメニュー）要求を View に通知する
        // actionKey には "edit"/"delete"/"properties"/"deleteBranch:{index}" 等を入れます
        public Action<GuiElement, string> NodeContextRequested { get; set; }

        // 追加: ノードのダブルクリック（特に Screen 開く用）
        public Action<GuiElement> NodeDoubleClicked { get; set; }

        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();

            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.White,
                InputTransparent = false,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start
            };

            Content = _graphicsView;

            this.HorizontalOptions = LayoutOptions.Start;
            this.VerticalOptions = LayoutOptions.Start;

            _graphicsView.HandlerChanged += GraphicsView_HandlerChanged;

#if !WINDOWS
            var pan = new PanGestureRecognizer();
            pan.PanUpdated += NonWindows_PanUpdated;
            _graphicsView.GestureRecognizers.Add(pan);
#endif
        }

        private void GraphicsView_HandlerChanged(object sender, EventArgs e)
        {
#if WINDOWS
            if (_graphicsView.Handler?.PlatformView is UIElement oldEl)
            {
                oldEl.PointerPressed -= Platform_PointerPressed;
                oldEl.PointerMoved -= Platform_PointerMoved;
                oldEl.PointerReleased -= Platform_PointerReleased;
            }

            if (_graphicsView.Handler?.PlatformView is UIElement el)
            {
                el.PointerPressed += Platform_PointerPressed;
                el.PointerMoved += Platform_PointerMoved;
                el.PointerReleased += Platform_PointerReleased;
            }
#endif
        }

#if WINDOWS
        private void Platform_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!(sender is UIElement ui)) return;
            var pt = e.GetCurrentPoint(ui).Position;
            // 右クリック判定
            var props = e.GetCurrentPoint(ui).Properties;
            bool isRight = props.IsRightButtonPressed;

            float px = (float)pt.X;
            float py = (float)pt.Y;

            if (isRight)
            {
                // 右クリック時はドラッグ開始せずヒットテストのみ行い、該当ノードを報告する
                // ノード本体のヒットテスト（すべての要素タイプを対象）
                foreach (var el in _drawable.Elements.AsEnumerable().Reverse())
                {
                    var rect = new RectF(el.X, el.Y, GuiDiagramDrawable.NodeWidth, GuiDiagramDrawable.NodeHeight);
                    if (rect.Contains(px, py))
                    {
                        // 画面(Screen) 以外もコンテキスト要求を送る
                        NodeContextRequested?.Invoke(el, "context");
                        e.Handled = true;
                        return;
                    }
                }

                // ブランチ領域（条件領域やダイヤモンド）も判定して、分岐インデックス情報を渡す
                if (_drawable.BranchVisuals != null && _drawable.BranchVisuals.Count > 0)
                {
                    float condW = GuiDiagramDrawable.NodeWidth * 0.9f;
                    float condH = GuiDiagramDrawable.NodeHeight * 0.7f;
                    float diamondW = GuiDiagramDrawable.NodeWidth * 0.8f;
                    float diamondH = GuiDiagramDrawable.NodeHeight * 0.8f;
                    float midGap = 24f;
                    float condRightShift = 40f;

                    foreach (var bv in _drawable.BranchVisuals)
                    {
                        float condCenterX = bv.CenterX - (diamondW / 2f + midGap / 2f + condW / 2f) + condRightShift;
                        var condCenter = new PointF(condCenterX, bv.CenterY);
                        var condRect = new RectF(condCenter.X - condW / 2f, condCenter.Y - condH / 2f, condW, condH);

                        float targetCenterX = bv.CenterX + (diamondW / 2f + midGap / 2f);
                        var targetCenter = new PointF(targetCenterX, bv.CenterY);
                        var diamondRect = new RectF(targetCenter.X - diamondW / 2f, targetCenter.Y - diamondH / 2f, diamondW, diamondH);

                        if (condRect.Contains(px, py) || diamondRect.Contains(px, py))
                        {
                            var parent = bv.ParentEvent;
                            if (parent != null)
                            {
                                // 分岐コンテキストを要求する（ actionKey に分岐インデックス情報を含める）
                                NodeContextRequested?.Invoke(parent, $"branch:{bv.BranchIndex}");
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }

                // 対象がなければ空白クリックとして View に通知（ここが無いと空白右クリックで何も起きない）
                NodeContextRequested?.Invoke(null, null);
                e.Handled = true;
                return;
            }

            // 左クリック（または右クリック以外）のときはダブルクリック判定を行う
            // 先にヒットテストしてクリック対象の要素を特定（ブランチは親イベントを参照）
            GuiElement clickedElement = null;
            int? clickedBranchIndex = null;

            foreach (var el in _drawable.Elements.AsEnumerable().Reverse())
            {
                var rect = new RectF(el.X, el.Y, GuiDiagramDrawable.NodeWidth, GuiDiagramDrawable.NodeHeight);
                if (rect.Contains(px, py))
                {
                    clickedElement = el;
                    break;
                }
            }

            if (clickedElement == null && _drawable.BranchVisuals != null && _drawable.BranchVisuals.Count > 0)
            {
                float condW = GuiDiagramDrawable.NodeWidth * 0.9f;
                float condH = GuiDiagramDrawable.NodeHeight * 0.7f;
                float diamondW = GuiDiagramDrawable.NodeWidth * 0.8f;
                float diamondH = GuiDiagramDrawable.NodeHeight * 0.8f;
                float midGap = 24f;
                float condRightShift = 40f;

                foreach (var bv in _drawable.BranchVisuals)
                {
                    float condCenterX = bv.CenterX - (diamondW / 2f + midGap / 2f + condW / 2f) + condRightShift;
                    var condCenter = new PointF(condCenterX, bv.CenterY);
                    var condRect = new RectF(condCenter.X - condW / 2f, condCenter.Y - condH / 2f, condW, condH);

                    float targetCenterX = bv.CenterX + (diamondW / 2f + midGap / 2f);
                    var targetCenter = new PointF(targetCenterX, bv.CenterY);
                    var diamondRect = new RectF(targetCenter.X - diamondW / 2f, targetCenter.Y - diamondH / 2f, diamondW, diamondH);

                    if (condRect.Contains(px, py) || diamondRect.Contains(px, py))
                    {
                        if (bv.ParentEvent != null)
                        {
                            clickedElement = bv.ParentEvent;
                            clickedBranchIndex = bv.BranchIndex;
                            break;
                        }
                    }
                }
            }

            // ダブルクリック判定：対象が Screen（あるいは Screen に対応する分岐）で、短時間で同じ要素を2回
            if (clickedElement != null && clickedElement.Type == GuiElementType.Screen)
            {
                var now = DateTime.UtcNow;
                if (_lastLeftClickElement != null && ReferenceEquals(_lastLeftClickElement, clickedElement)
                    && (now - _lastLeftClickTime).TotalMilliseconds <= DoubleClickThresholdMs)
                {
                    // ダブルクリック確定
                    NodeDoubleClicked?.Invoke(clickedElement);
                    // reset
                    _lastLeftClickTime = DateTime.MinValue;
                    _lastLeftClickElement = null;
                    e.Handled = true;
                    return;
                }
                // 単発クリックは記録してドラッグ処理へ（ドラッグは TryStartDrag に任せる）
                _lastLeftClickElement = clickedElement;
                _lastLeftClickTime = now;
            }
            else
            {
                // 画面以外のクリックはダブルクリック対象外にする（クリック記録をクリア）
                _lastLeftClickElement = null;
                _lastLeftClickTime = DateTime.MinValue;
            }

            // 右クリックでない場合は既存の処理（ドラッグ開始）を行う
            var pt2 = e.GetCurrentPoint(ui).Position;
            TryStartDrag((float)pt2.X, (float)pt2.Y);
        }

        private void Platform_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedNode == null) return;
            if (!(sender is UIElement ui)) return;
            var pt = e.GetCurrentPoint(ui).Position;
            ContinueDrag((float)pt.X, (float)pt.Y);
        }

        private void Platform_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedNode != null)
            {
                FinishDrag(_draggedNode);
            }
        }
#endif

#if !WINDOWS
        private PointF? _panStartOriginalPos = null;
        private GuiElement _panTarget = null;

        private void NonWindows_PanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _panTarget = _drawable.Elements.Where(CanDrag).OrderBy(el => Math.Abs(el.Y)).FirstOrDefault();
                    if (_panTarget != null)
                    {
                        _panStartOriginalPos = new PointF(_panTarget.X, _panTarget.Y);
                        _panTarget.IsSelected = true;
                        NodeClicked?.Invoke(_panTarget);
                        _graphicsView.Invalidate();
                    }
                    break;

                case GestureStatus.Running:
                    if (_panTarget != null && _panStartOriginalPos.HasValue)
                    {
                        var newY = _panStartOriginalPos.Value.Y + (float)e.TotalY;
                        _panTarget.Y = newY;
                        ApplyFixedX(_panTarget);

                        // 追加: パン中も対応要素を追従させる
                        SyncPairedElementDuringDrag(_panTarget);

                        _graphicsView.Invalidate();
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
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

        private bool CanDrag(GuiElement el)
        {
            if (el == null) return false;
            if (el.Type == GuiElementType.Timeout) return false;

            if (el.Type == GuiElementType.Event && !string.IsNullOrWhiteSpace(el.Target))
            {
                var timeoutExact = _drawable.Elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && string.Equals(e.Name, el.Target, StringComparison.Ordinal));
                if (timeoutExact != null) return false;

                var timeoutContains = _drawable.Elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && (el.Target.Contains(e.Name) || e.Name.Contains(el.Target)));
                if (timeoutContains != null) return false;
            }
            return true;
        }

        private void TryStartDrag(float pointerX, float pointerY)
        {
            foreach (var el in _drawable.Elements.AsEnumerable().Reverse())
            {
                if (!CanDrag(el)) continue;

                var rect = new RectF(el.X, el.Y, GuiDiagramDrawable.NodeWidth, GuiDiagramDrawable.NodeHeight);
                if (rect.Contains(pointerX, pointerY))
                {
                    _draggedNode = el;
                    _dragOffset = new PointF(pointerX - el.X, pointerY - el.Y);

                    el.IsSelected = true;
                    _graphicsView.Invalidate();

                    NodeClicked?.Invoke(el);

                    return;
                }
            }

            if (_drawable.BranchVisuals != null && _drawable.BranchVisuals.Count > 0)
            {
                float condW = GuiDiagramDrawable.NodeWidth * 0.9f;
                float condH = GuiDiagramDrawable.NodeHeight * 0.7f;
                float diamondW = GuiDiagramDrawable.NodeWidth * 0.8f;
                float diamondH = GuiDiagramDrawable.NodeHeight * 0.8f;
                float midGap = 24f;
                float condRightShift = 40f;

                foreach (var bv in _drawable.BranchVisuals)
                {
                    float condCenterX = bv.CenterX - (diamondW / 2f + midGap / 2f + condW / 2f) + condRightShift;
                    var condCenter = new PointF(condCenterX, bv.CenterY);
                    var condRect = new RectF(condCenter.X - condW / 2f, condCenter.Y - condH / 2f, condW, condH);

                    float targetCenterX = bv.CenterX + (diamondW / 2f + midGap / 2f);
                    var targetCenter = new PointF(targetCenterX, bv.CenterY);
                    var diamondRect = new RectF(targetCenter.X - diamondW / 2f, targetCenter.Y - diamondH / 2f, diamondW, diamondH);

                    if (condRect.Contains(pointerX, pointerY) || diamondRect.Contains(pointerX, pointerY))
                    {
                        var parent = bv.ParentEvent;
                        if (parent != null)
                        {
                            parent.IsSelected = true;
                            _graphicsView.Invalidate();

                            NodeClicked?.Invoke(parent);
                            BranchClicked?.Invoke(parent, bv.BranchIndex);
                            return;
                        }
                    }
                }
            }
        }

        private void ContinueDrag(float pointerX, float pointerY)
        {
            if (_draggedNode == null) return;

            float newY = pointerY - _dragOffset.Y;
            _draggedNode.Y = newY;
            ApplyFixedX(_draggedNode);

            // 追加: ドラッグ中に対応要素をリアルタイムで追従させる
            SyncPairedElementDuringDrag(_draggedNode);

            _graphicsView.Invalidate();
        }

        // ドラッグ終了処理：スナップ、再配置、通知を行う
        private void FinishDrag(GuiElement finishedNode)
        {
            if (finishedNode == null) return;

            // Y をグリッドに合わせて丸める
            finishedNode.Y = Snap(finishedNode.Y, SnapSize);
            ApplyFixedX(finishedNode);

            // 最終同期（Finish時にも必ず同期）
            SyncPairedElementDuringDrag(finishedNode);

            // 全体の縦順を再フローして整列させる
            ReflowVerticalOrdering();

            finishedNode.IsSelected = false;

            // 位置変更を購読者に通知する
            PositionsChanged?.Invoke(_drawable.Elements);

            _graphicsView.Invalidate();

            _draggedNode = null;
        }

        // 対応要素をドラッグ中／終了時に同期するユーティリティ
        private void SyncPairedElementDuringDrag(GuiElement source)
        {
            if (source == null) return;

            if (source.Type == GuiElementType.Button)
            {
                var evt = FindCorrespondingEventForButton(source);
                if (evt != null && (evt.Branches == null || evt.Branches.Count == 0))
                {
                    // イベントは中間列（描画側で再調整されるが Y は追従）
                    evt.Y = source.Y;
                    evt.IsFixed = true; // 常にイベントをボタンに合わせる設計
                }
            }
            else if (source.Type == GuiElementType.Event)
            {
                // 条件分岐イベントは対象外
                if (source.Branches != null && source.Branches.Count > 0) return;

                var btn = FindCorrespondingButtonForEvent(source);
                if (btn != null)
                {
                    btn.Y = source.Y;
                    // Button は通常移動可能なので IsFixed は変更しない
                }
            }
        }

        // イベントから対応するボタンを探す（名前先頭一致 / ターゲット一致）
        private GuiElement FindCorrespondingButtonForEvent(GuiElement evt)
        {
            if (evt == null) return null;
            var buttons = _drawable.Elements.Where(e => e.Type == GuiElementType.Button).ToList();

            if (!string.IsNullOrWhiteSpace(evt.Name))
            {
                var evtNameNorm = evt.Name.Trim();
                var b = buttons.FirstOrDefault(btn => !string.IsNullOrWhiteSpace(btn.Name) && evtNameNorm.StartsWith(btn.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (b != null) return b;
            }

            if (!string.IsNullOrWhiteSpace(evt.Target))
            {
                var b = buttons.FirstOrDefault(btn => !string.IsNullOrWhiteSpace(btn.Target) && string.Equals(btn.Target.Trim(), evt.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                if (b != null) return b;
            }

            return null;
        }

        // ボタンから対応する（単一）イベントを探す（優先はイベント名にボタン名が含まれる／ターゲット一致）
        private GuiElement FindCorrespondingEventForButton(GuiElement button)
        {
            if (button == null) return null;
            var events = _drawable.Elements.Where(e => e.Type == GuiElementType.Event && (e.Branches == null || e.Branches.Count == 0)).ToList();

            if (!string.IsNullOrWhiteSpace(button.Name))
            {
                var btnName = button.Name.Trim();
                var ev = events.FirstOrDefault(evt => !string.IsNullOrWhiteSpace(evt.Name) && evt.Name.Trim().StartsWith(btnName, StringComparison.OrdinalIgnoreCase));
                if (ev != null) return ev;
            }

            if (!string.IsNullOrWhiteSpace(button.Target))
            {
                var ev = events.FirstOrDefault(evt => !string.IsNullOrWhiteSpace(evt.Target) && string.Equals(evt.Target.Trim(), button.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                if (ev != null) return ev;
            }

            return null;
        }

        private void ApplyFixedX(GuiElement el)
        {
            if (el == null) return;

            switch (el.Type)
            {
                case GuiElementType.Screen:
                case GuiElementType.Button:
                case GuiElementType.Operation:
                    el.IsFixed = false;
                    el.X = LeftColumnX;
                    break;
                case GuiElementType.Event:
                    if (!string.IsNullOrWhiteSpace(el.Target))
                    {
                        var timeoutEl = _drawable.Elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && (string.Equals(e.Name, el.Target, StringComparison.Ordinal) || el.Target.Contains(e.Name) || e.Name.Contains(el.Target)));
                        if (timeoutEl != null)
                        {
                            el.IsFixed = true;
                            el.X = RightColumnX;
                            break;
                        }
                    }
                    el.IsFixed = false;
                    el.X = RightColumnX;
                    break;
                case GuiElementType.Timeout:
                    el.IsFixed = true;
                    el.X = LeftColumnX;
                    break;
            }
        }

        private void ReflowVerticalOrdering()
        {
            var timeouts = _drawable.Elements.Where(e => e.Type == GuiElementType.Timeout).OrderBy(e => e.Y).ToList();
            var movable = _drawable.Elements.Where(e => e.Type != GuiElementType.Timeout).OrderBy(e => e.Y).ToList();

            float baseY = 8f;
            if (timeouts.Any())
            {
                var lastT = timeouts.Last();
                baseY = lastT.Y + GuiDiagramDrawable.NodeHeight + 8f;
            }

            float y = baseY;
            foreach (var el in movable)
            {
                el.Y = y;
                ApplyFixedX(el);
                y += SnapSize;
            }

            _graphicsView.Invalidate();
        }

        private static float Snap(float value, float grid) => (float)(Math.Round(value / grid) * grid);

        // Render に画面一覧（画面管理クラス由来の名前集合）を渡せるようにした
        public void Render(IEnumerable<GuiElement> elements, IEnumerable<string> screenNames = null)
        {
            var list = elements?.ToList() ?? new();
            _drawable.Elements = list;

            // 外部から画面名集合が渡されたら Drawable にセット（Drawable 内で正規化して利用する）
            _drawable.ScreenNameSet = screenNames ?? null;

            _drawable.ArrangeNodes();

            float maxY = 0f;
            if (_drawable.Elements.Any())
                maxY = _drawable.Elements.Max(e => e.Y + GuiDiagramDrawable.NodeHeight);

            var height = Math.Max(maxY + 120f, 300f);
            var width = Math.Max(1000f, GuiDiagramDrawable.NodeWidth * 4f + 200f);

            _graphicsView.HeightRequest = height;
            _graphicsView.WidthRequest = width;

            this.WidthRequest = width;
            this.HeightRequest = height;

            _graphicsView.HorizontalOptions = LayoutOptions.Start;
            _graphicsView.VerticalOptions = LayoutOptions.Start;

            InvalidateMeasure();
            _graphicsView.Invalidate();
        }

        public void FinishCurrentDrag() => FinishDrag(_draggedNode);
    }
}
