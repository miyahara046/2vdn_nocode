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

namespace _2vdm_spec_generator.View
{
    public class GuiDiagramRenderer : ContentView
    {
        private readonly GraphicsView _graphicsView;
        private readonly GuiDiagramDrawable _drawable;

        private GuiElement _draggedNode = null;
        private PointF _dragOffset;

        private const float SnapSize = 50f;
        private const float LeftColumnX = 40f;
        private const float RightColumnX = 350f;

        public Action<IEnumerable<GuiElement>> PositionsChanged { get; set; }
        public Action<GuiElement> NodeClicked { get; set; }
        public Action<GuiElement, int?> BranchClicked { get; set; }

        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();

            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.White,
                InputTransparent = false
            };

            // 内部で ScrollView を持たず、親（XAML）の ScrollView に任せる
            Content = _graphicsView;

            // ハンドラー変更イベントは GraphicsView のみに登録（プラットフォーム入力はここでフック）
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
            TryStartDrag((float)pt.X, (float)pt.Y);
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

        public void Render(IEnumerable<GuiElement> elements)
        {
            var list = elements?.ToList() ?? new();
            _drawable.Elements = list;
            _drawable.ArrangeNodes();

            float maxY = 0f;
            if (_drawable.Elements.Any())
                maxY = _drawable.Elements.Max(e => e.Y + GuiDiagramDrawable.NodeHeight);

            var height = Math.Max(maxY + 120f, 300f);
            var width = 1000f;

            _graphicsView.HeightRequest = height;
            _graphicsView.WidthRequest = width;

            InvalidateMeasure();
            _graphicsView.Invalidate();
        }

        public void FinishCurrentDrag() => FinishDrag(_draggedNode);
    }
}
