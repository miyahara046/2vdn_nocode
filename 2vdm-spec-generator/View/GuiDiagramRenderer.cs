using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;
using System.Collections.Generic;
using _2vdm_spec_generator.ViewModel;

#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
#endif

namespace _2vdm_spec_generator.View
{
    /// <summary>
    /// GraphicsView を内包したカスタムレンダラー。
    /// </summary>
    public class GuiDiagramRenderer : ContentView
    {
        private readonly GraphicsView _graphicsView;
        private readonly GuiDiagramDrawable _drawable;
        private readonly ScrollView _scrollView;

        private GuiElement _draggedNode = null;
        private PointF _dragOffset;

        private const float SnapSize = 40f;
        private const float LeftColumnX = 40f;
        private const float RightColumnX = 350f;

        // 通知コールバック
        public Action<IEnumerable<GuiElement>> PositionsChanged { get; set; }

        // 追加: ノードがクリック（選択）されたことを通知するコールバック
        // ViewModel 側で選択ノードを受け取り、削除などの操作を行う想定
        public Action<GuiElement> NodeClicked { get; set; }

        public void SetElements(IEnumerable<GuiElement> elements)
        {
            _drawable.Elements = elements.ToList();
            _drawable.ArrangeNodes();
            _graphicsView.Invalidate();
        }

        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();

            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.White,
                InputTransparent = false
            };

            _scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Both,
                Content = _graphicsView,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
                VerticalScrollBarVisibility = ScrollBarVisibility.Always
            };

            Content = _scrollView;

            _graphicsView.HandlerChanged += GraphicsView_HandlerChanged;

#if !WINDOWS
            var pan = new PanGestureRecognizer();
            pan.PanUpdated += NonWindows_PanUpdated;
            _graphicsView.GestureRecognizers.Add(pan);

            // 非Windowsでもタップ選択を確実に行いたい場合は TapGestureRecognizer を追加して
            // NodeClicked をトリガーする拡張が可能だが、TapGesture は位置情報を渡さないため
            // ここでは PanGesture 開始時の選択で代用する。
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
                // ドラッグで始まった場合は FinishDrag を呼ぶ（FinishDrag 内で選択解除）
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
                        // 選択として通知（タップと同等の選択通知）
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

        private bool CanDrag(GuiElement el) => el != null && el.Type != GuiElementType.Timeout;

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

                    // 選択を通知
                    el.IsSelected = true;
                    _graphicsView.Invalidate();

                    // クリック／選択の通知（ViewModel 側へ）
                    NodeClicked?.Invoke(el);

                    break;
                }
            }
        }

        private void ContinueDrag(float pointerX, float pointerY)
        {
            if (_draggedNode == null) return;

            float newY = pointerY - _dragOffset.Y;
            _draggedNode.Y = newY;
            ApplyFixedX(_draggedNode);

            _graphicsView.Invalidate();
        }

        private void FinishDrag(GuiElement finishedNode)
        {
            if (finishedNode == null) return;

            finishedNode.Y = Snap(finishedNode.Y, SnapSize);
            ApplyFixedX(finishedNode);
            ReflowVerticalOrdering();

            finishedNode.IsSelected = false;

            PositionsChanged?.Invoke(_drawable.Elements);

            _graphicsView.Invalidate();

            _draggedNode = null;
        }

        private void ApplyFixedX(GuiElement el)
        {
            if (el == null) return;

            switch (el.Type)
            {
                case GuiElementType.Screen:
                case GuiElementType.Button:
                case GuiElementType.Operation:
                    el.X = LeftColumnX;
                    break;
                case GuiElementType.Event:
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
            this.HeightRequest = Math.Min(height, 800);

            InvalidateMeasure();
            _graphicsView.Invalidate();
        }

        public void FinishCurrentDrag() => FinishDrag(_draggedNode);
    }
}
